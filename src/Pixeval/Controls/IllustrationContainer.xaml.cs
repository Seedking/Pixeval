#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2023 Pixeval/IllustrationContainer.xaml.cs
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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using CommunityToolkit.WinUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pixeval.Controls.IllustrationView;
using Pixeval.Controls.TokenInput;
using Pixeval.Controls.Windowing;
using Pixeval.Download;
using Pixeval.Download.Models;
using Pixeval.Flyouts.IllustrationResultFilter;
using Pixeval.Options;
using Pixeval.Util;
using Pixeval.Util.UI;
using Pixeval.Utilities;
using WinUI3Utilities;
using WinUI3Utilities.Attributes;

namespace Pixeval.Controls;

[DependencyProperty<bool>("ShowCommandBar", "true")]
[DependencyProperty<object>("Header")]
public sealed partial class IllustrationContainer
{

    /// <summary>
    ///     The command elements that will appear at the left of the TopCommandBar
    /// </summary>
    public ObservableCollection<UIElement> CommandBarElements { get; } = [];

    public ObservableCollection<ICommandBarElement> PrimaryCommandsSupplements { get; } = [];

    public ObservableCollection<ICommandBarElement> SecondaryCommandsSupplements { get; } = [];

    public IllustrationContainer()
    {
        InitializeComponent();
        CommandBarElements.CollectionChanged += (_, args) =>
        {
            if (args is { Action: NotifyCollectionChangedAction.Add, NewItems: { } newItems })
                foreach (UIElement argsNewItem in newItems)
                    ExtraCommandsBar.Children.Insert(ExtraCommandsBar.Children.Count - 1, argsNewItem);
            else
                ThrowHelper.Argument(args);
        };
        PrimaryCommandsSupplements.CollectionChanged += (_, args) => AddCommandCallback(args, CommandBar.PrimaryCommands);
        SecondaryCommandsSupplements.CollectionChanged += (_, args) => AddCommandCallback(args, CommandBar.SecondaryCommands);
    }

    public ItemsViewLayoutType ItemsViewLayoutType => App.AppViewModel.AppSetting.ItemsViewLayoutType;

    public ThumbnailDirection ThumbnailDirection => App.AppViewModel.AppSetting.ThumbnailDirection;

    public IllustrationViewViewModel ViewModel => IllustrationView.ViewModel;

    private void IllustrationContainer_OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = IllustrationView.Focus(FocusState.Programmatic);
    }

    public void ScrollToTop()
    {
        _ = IllustrationView.ScrollView.ScrollTo(0, 0);
    }

    private FilterSettings _lastFilterSettings = FilterSettings.Default;

    private void AddCommandCallback(NotifyCollectionChangedEventArgs e, ICollection<ICommandBarElement> commands)
    {
        if (e is { Action: NotifyCollectionChangedAction.Add, NewItems: { } newItems })
            foreach (UIElement argsNewItem in newItems)
                commands.Add(argsNewItem.To<ICommandBarElement>());
        else
            ThrowHelper.Argument(e, "This collection does not support operations except the Add");
    }

    private void SelectAllToggleButton_OnTapped(object sender, TappedRoutedEventArgs e)
    {
        IllustrationView.IllustrationItemsView.SelectAll();
    }

    private async void AddAllToBookmarkButton_OnTapped(object sender, TappedRoutedEventArgs e)
    {
        // TODO custom bookmark tag
        var notBookmarked = ViewModel.SelectedIllustrations.Where(i => !i.IsBookmarked);
        var viewModelSelectedIllustrations = notBookmarked as IllustrationItemViewModel[] ?? notBookmarked.ToArray();
        if (viewModelSelectedIllustrations.Length > 5 &&
            await this.CreateOkCancelAsync(IllustrationViewCommandBarResources.SelectedTooManyItemsForBookmarkTitle,
                    IllustrationViewCommandBarResources.SelectedTooManyItemsForBookmarkContent) is not ContentDialogResult.Primary)
        {
            return;
        }

        foreach (var viewModelSelectedIllustration in viewModelSelectedIllustrations)
        {
            if (!viewModelSelectedIllustration.IsBookmarked)
                _ = viewModelSelectedIllustration.ToggleBookmarkStateAsync();
        }

        if (viewModelSelectedIllustrations.Length is var c and > 0)
        {
            _ = this.CreateAcknowledgementAsync(IllustrationViewCommandBarResources.AddAllToBookmarkTitle,
                IllustrationViewCommandBarResources.AddAllToBookmarkContentFormatted.Format(c));
        }
    }

    private async void SaveAllButton_OnTapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel.SelectedIllustrations.Length >= 20 && await this.CreateOkCancelAsync(IllustrationViewCommandBarResources.SelectedTooManyItemsTitle,
                IllustrationViewCommandBarResources.SelectedTooManyItemsForSaveContent) is not ContentDialogResult.Primary)
        {
            return;
        }

        using var scope = App.AppViewModel.AppServicesScope;
        var factory = scope.ServiceProvider.GetRequiredService<IDownloadTaskFactory<IllustrationItemViewModel, IllustrationDownloadTask>>();

        // This will run for quite a while
        _ = Task.Run(async () =>
        {
            var tasks = await WindowFactory.RootWindow.DispatcherQueue.EnqueueAsync(() => Task.WhenAll(
                    ViewModel.SelectedIllustrations
                        .Select(i => factory.CreateAsync(i, App.AppViewModel.AppSetting.DefaultDownloadPathMacro))));
            foreach (var viewModelSelectedIllustration in tasks)
            {
                App.AppViewModel.DownloadManager.QueueTask(viewModelSelectedIllustration);
            }
        });

        this.ShowTeachingTipAndHide(IllustrationViewCommandBarResources.DownloadItemsQueuedFormatted.Format(ViewModel.SelectedIllustrations.Length), milliseconds: 1500);
    }

    private async void OpenAllInBrowserButton_OnTapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel.SelectedIllustrations is { Length: var count } selected)
        {
            if (count > 15 && await this.CreateOkCancelAsync(IllustrationViewCommandBarResources.SelectedTooManyItemsTitle,
                        IllustrationViewCommandBarResources.SelectedTooManyItemsForOpenInBrowserContent) is not ContentDialogResult.Primary)
            {
                return;
            }

            foreach (var illustrationViewModel in selected)
            {
                _ = await Launcher.LaunchUriAsync(MakoHelper.GenerateIllustrationWebUri(illustrationViewModel.Id));
            }
        }
    }

    private void ShareButton_OnTapped(object sender, TappedRoutedEventArgs e)
    {
        // TODO share
        throw new NotImplementedException();
    }

    private void CancelSelectionButton_OnTapped(object sender, TappedRoutedEventArgs e)
    {
        IllustrationView.IllustrationItemsView.DeselectAll();
    }

    private void OpenConditionDialogButton_OnTapped(object sender, TappedRoutedEventArgs e)
    {
        FilterTeachingTip.IsOpen = true;
    }

    private void FilterTeachingTip_OnActionButtonClick(TeachingTip sender, object args)
    {
        FilterContent.Reset();
    }

    private void FilterTeachingTip_OnCloseButtonClick(TeachingTip sender, object args)
    {
        if (FilterContent.GetFilterSettings is not (
            var includeTags,
            var excludeTags,
            var leastBookmark,
            var maximumBookmark,
            _, // TODO user group name
            var illustratorName,
            var illustratorId,
            var illustrationName,
            var illustrationId,
            var publishDateStart,
            var publishDateEnd) filterSettings)
            return;

        if (filterSettings == _lastFilterSettings)
        {
            return;
        }

        _lastFilterSettings = filterSettings;

        ViewModel.DataProvider.View.Filter = o =>
        {
            var stringTags = o.Illustrate.Tags.Select(t => t.Name).ToArray();
            var result =
                ExamineExcludeTags(stringTags, excludeTags)
                && ExamineIncludeTags(stringTags, includeTags)
                && o.Bookmark >= leastBookmark
                && o.Bookmark <= maximumBookmark
                && illustrationName.Match(o.Illustrate.Title)
                && illustratorName.Match(o.Illustrate.User.Name)
                && (illustratorId is -1 || illustratorId == o.Illustrate.User.Id)
                && illustrationId is -1 || illustrationId == o.Id
                && o.PublishDate >= publishDateStart
                && o.PublishDate <= publishDateEnd;
            return result;
        };
        return;

        static bool ExamineExcludeTags(IEnumerable<string> tags, IEnumerable<Token> predicates)
            => predicates.Aggregate(true, (acc, token) => acc && tags.None(token.Match));

        static bool ExamineIncludeTags(ICollection<string> tags, IEnumerable<Token> predicates)
            => tags.Count is 0 || predicates.Aggregate(true, (acc, token) => acc && tags.Any(token.Match));
    }

    private void FastFilterAutoSuggestBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        PerformSearch(sender.Text);
    }

    public void PerformSearch(string text)
    {
        ViewModel.DataProvider.View.Filter = text.IsNullOrBlank()
            ? null
            : o => o.Id.ToString().Contains(text)
                   || o.Illustrate.Tags.Any(x => x.Name.Contains(text) || (x.TranslatedName?.Contains(text) ?? false))
                   || o.Illustrate.Title.Contains(text);
    }
}
