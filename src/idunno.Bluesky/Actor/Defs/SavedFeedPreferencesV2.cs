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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public SavedFeedPreferencesV2(IReadOnlyList<SavedFeed> items) => Items = items;

    /// <summary>
    /// Gets a readonly list of an actors saved feed preferences.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value set is <see langword="null"/>.</exception>
    [JsonRequired]
    public IReadOnlyList<SavedFeed> Items
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = new List<SavedFeed>(value).AsReadOnly();
        }
    }
}

