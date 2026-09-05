// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Contains user preferences for live events.
/// </summary>
/// <param name="HiddenFeedIds">A list of feed IDs that the user has hidden from live events</param>
/// <param name="HideAllFeeds">A flag indicating whether the user wants to hide all feeds from live events</param>
public record LiveEventPreferences(ICollection<string>? HiddenFeedIds, bool? HideAllFeeds) : Preference
{
}
