// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates an actor's feed preferences.
/// </summary>
public record SavedFeedPreferencesV2 : Preference
{
    /// <summary>
    /// Creates a new instance of <see cref="SavedFeedPreferencesV2"/>.
    /// </summary>
    /// <param name="items">A list of feed preferences.</param>
    [JsonConstructor]
    public SavedFeedPreferencesV2(IReadOnlyList<SavedFeed> items)
    {
        if (items is null)
        {
            Items = new List<SavedFeed>().AsReadOnly();
        }
        else
        {
            Items = new List<SavedFeed>(items).AsReadOnly();
        }
    }

    /// <summary>
    /// Gets a readonly list of an actors saved feed preferences.
    /// </summary>
    [JsonRequired]
    public IReadOnlyList<SavedFeed> Items { get; init; }
}

