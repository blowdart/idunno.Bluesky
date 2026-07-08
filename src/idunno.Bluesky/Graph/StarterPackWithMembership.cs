// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Graph;

/// <summary>
/// A starter pack and an optional list item indicating membership of a target user to that starter pack.
/// </summary>
public sealed record StarterPackWithMembership : View
{
    /// <summary>
    /// Creates a new instance of <see cref="StarterPackWithMembership"/>.
    /// </summary>
    /// <param name="starterPack">The starter pack.</param>
    /// <param name="listItem">The list item indicating membership of a target user to the starter pack.</param>
    [JsonConstructor]
    internal StarterPackWithMembership(StarterPackView starterPack, ListItemView? listItem)
    {
        StarterPack = starterPack;
        ListItem = listItem;
    }

    /// <summary>
    /// Gets the starter pack.
    /// </summary>
    [JsonRequired]
    public StarterPackView StarterPack { get; set; }

    /// <summary>
    /// Gets the list item indicating membership of a target user to the starter pack.
    /// </summary>
    public ListItemView? ListItem { get; set; }
}