#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2023 Pixeval/AttributeHelper.cs
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
using System.Reflection;
using Microsoft.UI.Xaml;
using Pixeval.Attributes;
using Pixeval.CoreApi.Global.Enum;
using WinUI3Utilities;

namespace Pixeval.Util;

public static class AttributeHelper
{
    public static TAttribute? GetCustomAttribute<TAttribute>(this Enum e) where TAttribute : Attribute
    {
        return e.GetType().GetField(e.ToString())?.GetCustomAttribute(typeof(TAttribute), false) as TAttribute;
    }
}

public static class LocalizedResourceAttributeHelper
{
    public static string? GetLocalizedResourceContent(this Enum e)
    {
        if (_predefinedResources.TryGetValue(e, out var v))
            return v;
        var attribute = e.GetCustomAttribute<LocalizedResource>();
        return attribute?.GetLocalizedResourceContent();
    }

    public static LocalizedResource? GetLocalizedResource(this Enum e)
    {
        return e.GetCustomAttribute<LocalizedResource>();
    }

    public static string? GetLocalizedResourceContent(this LocalizedResource attribute)
    {
        return GetLocalizedResourceContent(attribute.ResourceLoader, attribute.Key);
    }

    public static string? GetLocalizedResourceContent(Type resourceLoader, string key)
    {
        return resourceLoader.GetMember(key, BindingFlags.Static | BindingFlags.Public) switch
        {
            [FieldInfo fi] => fi?.GetValue(null),
            [PropertyInfo pi] => pi?.GetValue(null),
            _ => null
        } as string;
    }

    private static readonly Dictionary<Enum, string> _predefinedResources = new()
    {
        [TargetFilter.ForAndroid] = MiscResources.TargetFilterForAndroid,
        [TargetFilter.ForIos] = MiscResources.TargetFilterForIOS,

        [BackdropType.Acrylic] = MiscResources.AcrylicBackdrop,
        [BackdropType.Mica] = MiscResources.MicaBackdrop,
        [BackdropType.MicaAlt] = MiscResources.MicaAltBackdrop,
        [BackdropType.None] = MiscResources.NoneBackdrop,

        [SearchTagMatchOption.PartialMatchForTags] = MiscResources.SearchTagMatchOptionPartialMatchForTags,
        [SearchTagMatchOption.ExactMatchForTags] = MiscResources.SearchTagMatchOptionExactMatchForTags,
        [SearchTagMatchOption.TitleAndCaption] = MiscResources.SearchTagMatchOptionTitleAndCaption,

        [ElementTheme.Dark] = MiscResources.AppThemeDark,
        [ElementTheme.Light] = MiscResources.AppThemeLight,
        [ElementTheme.Default] = MiscResources.AppThemeSystemDefault,

        [IllustrationSortOption.PopularityDescending] = MiscResources.IllustrationSortOptionPopularityDescending,
        [IllustrationSortOption.PublishDateAscending] = MiscResources.IllustrationSortOptionPublishDateAscending,
        [IllustrationSortOption.PublishDateDescending] = MiscResources.IllustrationSortOptionPublishDateDescending,
        [IllustrationSortOption.DoNotSort] = MiscResources.IllustrationSortOptionDoNotSort,

        [SearchDuration.Undecided] = MiscResources.SearchDurationUndecided,
        [SearchDuration.WithinLastDay] = MiscResources.SearchDurationWithinLastDay,
        [SearchDuration.WithinLastWeek] = MiscResources.SearchDurationWithinLastWeek,
        [SearchDuration.WithinLastMonth] = MiscResources.SearchDurationWithinLastMonth
    };
}
