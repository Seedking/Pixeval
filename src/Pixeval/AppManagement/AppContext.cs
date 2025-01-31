#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2023 Pixeval/AppContext.cs
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
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Media.Imaging;
using Pixeval.Controls.Windowing;
using Pixeval.CoreApi.Preference;
using Pixeval.Database.Managers;
using Pixeval.Download;
using Pixeval.Util.IO;
using Pixeval.Utilities;
using WinUI3Utilities;
using WinUI3Utilities.Attributes;

namespace Pixeval.AppManagement;

/// <summary>
///     Provide miscellaneous information about the app
/// </summary>
[AppContext<AppSetting>(ConfigKey = "Config", Type = ApplicationDataContainerType.Roaming, MethodName = "Config")]
[AppContext<Session>(ConfigKey = "Session", MethodName = "Session")]
public static partial class AppContext
{
    public const string AppIdentifier = nameof(Pixeval);

    public const string AppProtocol = "pixeval";

    public const string AppLogoNoCaptionUri = "ms-appx:///Assets/Images/logo-no-caption.png";

    public static readonly string AppIconFontFamilyName = AppHelper.IsWindows11 ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";

    public static readonly string DatabaseFilePath = AppKnownFolders.Local.Resolve("PixevalData.litedb");

    public static readonly string AppVersion = GitVersionInformation.AssemblySemVer;

    private static readonly WeakReference<SoftwareBitmapSource?> _imageNotAvailable = new(null);

    private static readonly WeakReference<IRandomAccessStream?> _imageNotAvailableStream = new(null);

    private static readonly WeakReference<SoftwareBitmapSource?> _pixivNoProfile = new(null);

    private static readonly WeakReference<IRandomAccessStream?> _pixivNoProfileStream = new(null);

    static AppContext()
    {
        // Keys in the RoamingSettings will be synced through the devices of the same user
        // For more detailed information see https://docs.microsoft.com/en-us/windows/apps/design/app-settings/store-and-retrieve-app-data
        InitializeConfig();
        InitializeSession();
    }

    public const string IconName = "logo44x44.ico";

    public static string IconAbsolutePath => Path.Combine(AppKnownFolders.Local.Self.Path, IconName);

    public static async Task<SoftwareBitmapSource> GetNotAvailableImageAsync()
    {
        if (!_imageNotAvailable.TryGetTarget(out var target))
            _imageNotAvailable.SetTarget(target = await (await GetNotAvailableImageStreamAsync()).GetSoftwareBitmapSourceAsync(false));
        return target;
    }

    public static async Task<IRandomAccessStream> GetNotAvailableImageStreamAsync()
    {
        if (!_imageNotAvailableStream.TryGetTarget(out var target))
            _imageNotAvailableStream.SetTarget(target = await GetAssetStreamAsync("Images/image-not-available.png"));
        return target;
    }

    public static async Task<SoftwareBitmapSource> GetPixivNoProfileImageAsync()
    {
        if (!_pixivNoProfile.TryGetTarget(out var target))
            _pixivNoProfile.SetTarget(target = await (await GetPixivNoProfileImageStreamAsync()).GetSoftwareBitmapSourceAsync(false));
        return target;
    }

    public static async Task<IRandomAccessStream> GetPixivNoProfileImageStreamAsync()
    {
        if (!_pixivNoProfileStream.TryGetTarget(out var target))
            _pixivNoProfileStream.SetTarget(target = await GetAssetStreamAsync("Images/pixiv_no_profile.png"));
        return target;
    }

    public static async Task WriteLogoIcoIfNotExist()
    {
        const string iconName = "logo44x44.ico";
        if (await AppKnownFolders.Local.TryGetFileRelativeToSelfAsync(iconName) is null)
        {
            var bytes = await GetAssetBytesAsync($"Images/{iconName}");
            await (await AppKnownFolders.Local.CreateFileAsync(iconName)).WriteBytesAsync(bytes);
        }
    }

    /// <summary>
    ///     Get the byte array of a file in the Assets folder
    /// </summary>
    /// <param name="relativeToAssetsFolder">A path with leading slash(or backslash) removed</param>
    /// <returns></returns>
    public static Task<byte[]> GetAssetBytesAsync(string relativeToAssetsFolder)
    {
        return GetResourceBytesAsync($"ms-appx:///Assets/{relativeToAssetsFolder}");
    }

    public static Task<IRandomAccessStreamWithContentType> GetAssetStreamAsync(string relativeToAssetsFolder)
    {
        return GetResourceStreamAsync($"ms-appx:///Assets/{relativeToAssetsFolder}");
    }

    public static async Task<byte[]> GetResourceBytesAsync(string path)
    {
        return (await (await StorageFile.GetFileFromApplicationUriAsync(new Uri(path))).ReadBytesAsync())!;
    }

    public static async Task<IRandomAccessStreamWithContentType> GetResourceStreamAsync(string path)
    {
        return await (await StorageFile.GetFileFromApplicationUriAsync(new Uri(path))).OpenReadAsync();
    }

    public static async Task<X509Certificate2> GetFakeCaRootCertificateAsync()
    {
        return new X509Certificate2(await GetAssetBytesAsync("Certs/pixeval_ca.cer"));
    }

    public static async Task<X509Certificate2> GetFakeServerCertificateAsync()
    {
        return new X509Certificate2(await GetAssetBytesAsync("Certs/pixeval_server_cert.pfx"), AppProtocol, X509KeyStorageFlags.UserKeySet);
    }

    public static void RestoreHistories()
    {
        using var scope = App.AppViewModel.AppServicesScope;
        var downloadHistoryManager = scope.ServiceProvider.GetRequiredService<DownloadHistoryPersistentManager>();
        // the HasFlag is not allow in expression tree
        _ = downloadHistoryManager.Delete(
            entry => entry.State == DownloadState.Running ||
                     entry.State == DownloadState.Queued ||
                     entry.State == DownloadState.Created ||
                     entry.State == DownloadState.Paused);

        foreach (var observableDownloadTask in downloadHistoryManager.Enumerate())
        {
            App.AppViewModel.DownloadManager.QueueTask(observableDownloadTask);
        }
    }

    /// <summary>
    ///     Erase all personal data, including session, configuration and image cache
    /// </summary>
    public static Task ClearDataAsync()
    {
        return Functions.IgnoreExceptionAsync(async () =>
        {
            ApplicationData.Current.RoamingSettings.DeleteContainer(ConfigContainerKey);
            ApplicationData.Current.LocalSettings.DeleteContainer(SessionContainerKey);
            await ApplicationData.Current.LocalFolder.ClearDirectoryAsync();
            await AppKnownFolders.Temporary.ClearAsync();
            await AppKnownFolders.SavedWallPaper.ClearAsync();
        });
    }

    public static void SaveContext()
    {
        // Save the current resolution
        App.AppViewModel.AppSetting.WindowWidth = WindowFactory.RootWindow.AppWindow.Size.Width;
        App.AppViewModel.AppSetting.WindowHeight = WindowFactory.RootWindow.AppWindow.Size.Height;
        if (!App.AppViewModel.SignOutExit)
        {
            if (App.AppViewModel.MakoClient != null!)
                SaveSession(App.AppViewModel.MakoClient.Session);
            SaveConfig(App.AppViewModel.AppSetting);
        }
    }
}
