#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2023 Pixeval/IntrinsicIllustrationDownloadTask.cs
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
using System.Threading.Tasks;
using Pixeval.Controls;
using Pixeval.Database;
using Pixeval.Utilities;
using Pixeval.Utilities.Threading;

namespace Pixeval.Download.Models;

public class IntrinsicIllustrationDownloadTask : IllustrationDownloadTask
{
    /// <summary>
    /// The disposal of <paramref name="stream" /> is not handled
    /// </summary>
    public IntrinsicIllustrationDownloadTask(DownloadHistoryEntry entry, IllustrationItemViewModel illustrationViewModel, Stream stream) : base(entry, illustrationViewModel)
    {
        Report(100);
        Stream = stream;
    }

    public Stream Stream { get; }

    protected override async Task DownloadAsyncCore(Func<string, IProgress<double>?, CancellationHandle?, Task<Result<Stream>>> _, string url, string destination)
    {
        if (!App.AppViewModel.AppSetting.OverwriteDownloadedFile && File.Exists(destination))
            return;

        Stream.Position = 0;

        await ManageStream(Stream, destination);
    }
}
