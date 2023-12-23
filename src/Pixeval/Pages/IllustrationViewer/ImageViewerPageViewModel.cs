#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2023 Pixeval/ImageViewerPageViewModel.cs
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Pixeval.Attributes;
using Pixeval.Controls;
using Pixeval.Controls.IllustrationView;
using Pixeval.CoreApi.Net;
using Pixeval.Database;
using Pixeval.Database.Managers;
using Pixeval.Options;
using Pixeval.Util;
using Pixeval.Util.IO;
using Pixeval.Utilities;
using Pixeval.Utilities.Threading;

namespace Pixeval.Pages.IllustrationViewer;

public partial class ImageViewerPageViewModel : ObservableObject, IDisposable
{
    public enum LoadingPhase
    {
        [LocalizedResource(typeof(ImageViewerPageResources), nameof(ImageViewerPageResources.CheckingCache))]
        CheckingCache,

        [LocalizedResource(typeof(ImageViewerPageResources), nameof(ImageViewerPageResources.LoadingFromCache))]
        LoadingFromCache,

        [LocalizedResource(typeof(ImageViewerPageResources), nameof(ImageViewerPageResources.DownloadingUgoiraZipFormatted), DownloadingUgoiraZip)]
        DownloadingUgoiraZip,

        [LocalizedResource(typeof(ImageViewerPageResources), nameof(ImageViewerPageResources.MergingUgoiraFrames))]
        MergingUgoiraFrames,

        [LocalizedResource(typeof(ImageViewerPageResources), nameof(ImageViewerPageResources.DownloadingImageFormatted), DownloadingImage)]
        DownloadingImage,

        [LocalizedResource(typeof(ImageViewerPageResources), nameof(ImageViewerPageResources.LoadingImage))]
        LoadingImage
    }

    private bool _disposed;

    [ObservableProperty]
    private bool _isMirrored;

    [ObservableProperty]
    private bool _isPlaying = true;

    private TaskNotifier? _loadingOriginalSourceTask;

    [ObservableProperty]
    private double _loadingProgress;

    [ObservableProperty]
    private string? _loadingText;

    [ObservableProperty]
    private List<int>? _msIntervals;

    [ObservableProperty]
    private IEnumerable<IRandomAccessStream>? _originalImageSources;

    [ObservableProperty]
    private int _rotationDegree;

    [ObservableProperty]
    private float _scale = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFit))]
    private ZoomableImageMode _showMode;

    public ImageViewerPageViewModel(IllustrationViewerPageViewModel illustrationViewerPageViewModel, IllustrationItemViewModel illustrationViewModel)
    {
        IllustrationViewerPageViewModel = illustrationViewerPageViewModel;
        IllustrationViewModel = illustrationViewModel;
        _ = LoadImage();
    }

    /// <summary>
    /// <see cref="ShowMode"/> is <see cref="ZoomableImageMode.Fit"/> or not
    /// </summary>
    public bool IsFit => ShowMode is ZoomableImageMode.Fit;

    public IRandomAccessStream? OriginalImageStream => OriginalImageSources?.FirstOrDefault();

    public Task? LoadingOriginalSourceTask
    {
        get => _loadingOriginalSourceTask!;
        set => SetPropertyAndNotifyOnCompletion(ref _loadingOriginalSourceTask!, value);
    }

    public bool LoadingCompletedSuccessfully => LoadingOriginalSourceTask?.IsCompletedSuccessfully ?? false;

    public CancellationHandle ImageLoadingCancellationHandle { get; } = new CancellationHandle();

    public IllustrationItemViewModel IllustrationViewModel { get; }

    /// <summary>
    ///     The view model of the <see cref="IllustrationViewerPage" /> that hosts the owner <see cref="ImageViewerPage" />
    ///     of this <see cref="ImageViewerPageViewModel" />
    /// </summary>
    public IllustrationViewerPageViewModel IllustrationViewerPageViewModel { get; }

    public void Dispose()
    {
        _disposed = true;
        DisposeInternal();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// see <see cref="ZoomableImage.Zoom"/>
    /// </summary>
    /// <param name="delta"></param>
    public void Zoom(float delta)
    {
        Scale = MathF.Exp(MathF.Log(Scale) + delta / 5000f);
    }

    private void AdvancePhase(LoadingPhase phase)
    {
        LoadingText = phase.GetLocalizedResource() switch
        {
            { FormatKey: LoadingPhase } attr => attr.GetLocalizedResourceContent()?.Format((int)LoadingProgress),
            var attr => attr?.GetLocalizedResourceContent()
        };
    }

    private void AddHistory()
    {
        using var scope = App.AppViewModel.AppServicesScope;
        var manager = scope.ServiceProvider.GetRequiredService<BrowseHistoryPersistentManager>();
        _ = manager.Delete(x => x.Id == IllustrationViewerPageViewModel.IllustrationId);
        manager.Insert(new BrowseHistoryEntry { Id = IllustrationViewModel.Id });
    }

    private async Task LoadImage()
    {
        if (!LoadingCompletedSuccessfully || _disposed)
        {
            _disposed = false;
            const ThumbnailUrlOption option = ThumbnailUrlOption.Medium;
            _ = IllustrationViewModel.TryLoadThumbnail(this, option).ContinueWith(
                _ => OriginalImageSources ??= [IllustrationViewModel.ThumbnailStreams[option]],
                TaskScheduler.FromCurrentSynchronizationContext());
            AddHistory();
            await LoadOriginalImage();
            IllustrationViewModel.UnloadThumbnail(this, option);
        }

        return;

        async Task LoadOriginalImage()
        {
            var imageClient = App.AppViewModel.MakoClient.GetMakoHttpClient(MakoApiKind.ImageApi);
            var cacheKey = IllustrationViewModel.Illustrate.GetIllustrationOriginalImageCacheKey();
            AdvancePhase(LoadingPhase.CheckingCache);
            if (App.AppViewModel.AppSetting.UseFileCache && await App.AppViewModel.Cache.ExistsAsync(cacheKey))
            {
                AdvancePhase(LoadingPhase.LoadingFromCache);
                OriginalImageSources = IllustrationViewModel.IsUgoira
                    ? [await App.AppViewModel.Cache.GetAsync<IRandomAccessStream>(cacheKey)]
                    : await App.AppViewModel.Cache.GetAsync<IEnumerable<IRandomAccessStream>>(cacheKey);
                LoadingOriginalSourceTask = Task.CompletedTask;
            }
            else if (IllustrationViewModel.IsUgoira)
            {
                var ugoiraMetadata = await App.AppViewModel.MakoClient.GetUgoiraMetadataAsync(IllustrationViewModel.Id);
                if (ugoiraMetadata.UgoiraMetadataInfo?.ZipUrls?.Large is { } url)
                {
                    AdvancePhase(LoadingPhase.DownloadingUgoiraZip);
                    var downloadRes = await imageClient.DownloadAsStreamAsync(url, new Progress<int>(d =>
                    {
                        LoadingProgress = d;
                        AdvancePhase(LoadingPhase.DownloadingUgoiraZip);
                    }), ImageLoadingCancellationHandle);
                    switch (downloadRes)
                    {
                        case Result<Stream>.Success(var zipStream):
                            AdvancePhase(LoadingPhase.MergingUgoiraFrames);
                            OriginalImageSources = await IoHelper.GetStreamsFromZipStreamAsync(zipStream);
                            MsIntervals = ugoiraMetadata.UgoiraMetadataInfo.Frames?.Select(x => (int)x.Delay)?.ToList();
                            break;
                        case Result<Stream>.Failure(OperationCanceledException):
                            return;
                    }

                    AdvancePhase(LoadingPhase.LoadingImage);
                    LoadingOriginalSourceTask = Task.CompletedTask;
                }
            }
            else if (IllustrationViewModel.OriginalSourceUrl is { } src)
            {
                AdvancePhase(LoadingPhase.DownloadingImage);
                var ras = await imageClient.DownloadAsIRandomAccessStreamAsync(src, new Progress<int>(d =>
                {
                    LoadingProgress = d;
                    AdvancePhase(LoadingPhase.DownloadingImage);
                }), ImageLoadingCancellationHandle);
                switch (ras)
                {
                    case Result<IRandomAccessStream>.Success(var s):
                        OriginalImageSources = [s];
                        break;
                    default:
                        return;
                }

                AdvancePhase(LoadingPhase.LoadingImage);
                LoadingOriginalSourceTask = Task.CompletedTask;
            }

            if (OriginalImageStream is not null && !_disposed)
            {
                IllustrationViewerPageViewModel.UpdateCommandCanExecute();
                if (App.AppViewModel.AppSetting.UseFileCache)
                {
                    _ = await App.AppViewModel.Cache.TryAddAsync(cacheKey, OriginalImageStream!, TimeSpan.FromDays(1));
                }

                return;
            }

            throw new IllustrationSourceNotFoundException(ImageViewerPageResources.CannotFindImageSourceContent);
        }
    }

    public Visibility GetLoadingMaskVisibility(Task? loadingTask)
    {
        return !(loadingTask?.IsCompletedSuccessfully ?? false) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DisposeInternal()
    {
        OriginalImageStream?.Dispose();
        IllustrationViewModel.UnloadThumbnail(this, ThumbnailUrlOption.Medium);
        // if the loading task is null or hasn't been completed yet, the 
        // OriginalImageSources would be the thumbnail source, its disposal may 
        // causing the IllustrationGrid shows weird result such as an empty content
        if (LoadingCompletedSuccessfully)
        {
            //(OriginalImageSources as SoftwareBitmapSource)?.Dispose();
        }

        OriginalImageSources = null;
        LoadingOriginalSourceTask?.Dispose();
    }
}
