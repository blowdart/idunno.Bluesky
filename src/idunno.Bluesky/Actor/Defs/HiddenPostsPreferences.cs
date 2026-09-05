// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// User <see cref="Preference"/> containing the <see cref="AtUri"/>s of posts the user has hidden.
/// </summary>
public sealed record HiddenPostsPreferences : Preference
{
    /// <summary>
    /// Creates a new instance of <see cref="HiddenPostsPreferences"/>.
    /// </summary>
    /// <param name="items">A list of URIs of posts the account owner has hidden.</param>
    [JsonConstructor]
    public HiddenPostsPreferences(IReadOnlyList<AtUri> items)
    {
        if (items is null)
        {
            Items = new List<AtUri>().AsReadOnly();
        }
        else
        {
            Items = new List<AtUri>(items).AsReadOnly();
        }
    }

    /// <summary>
    /// A list of URIs of posts the account owner has hidden.
    /// </summary>
    public IReadOnlyList<AtUri> Items { get; init; }
}