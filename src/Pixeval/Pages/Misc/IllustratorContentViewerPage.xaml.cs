#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2023 Pixeval/IllustratorContentViewerPage.xaml.cs
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
using Microsoft.UI.Xaml.Navigation;
using Pixeval.Controls;
using Pixeval.Controls.IllustratorContentViewer;

namespace Pixeval.Pages.Misc;

public sealed partial class IllustratorContentViewerPage : IDisposable
{
    private IllustratorContentViewerViewModel? _illustratorContentViewerViewModel;

    public IllustratorContentViewerPage()
    {
        InitializeComponent();
    }

    public void Dispose()
    {
        IllustratorContentViewer.Dispose();
    }

    public override async void OnPageActivated(NavigationEventArgs e)
    {
        if (e.Parameter is IllustratorItemViewModel viewModel)
        {
            _illustratorContentViewerViewModel = new IllustratorContentViewerViewModel(viewModel.UserDetail ?? await App.AppViewModel.MakoClient.GetUserFromIdAsync(viewModel.UserId, App.AppViewModel.AppSetting.TargetFilter));
            IllustratorContentViewer.ViewModel = _illustratorContentViewerViewModel;
        }
    }
}
