// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Graph;

/// <summary>
/// A list and an optional list item indicating membership of a target user to that list.
/// </summary>
public record ListWithMembership : View
{
    /// <summary>
    /// Creates a new instance of <see cref="ListWithMembership"/>.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="listItem">The <see cref="ListItemView"/> indicating membership of a target user to the list.</param>
    [JsonConstructor]
    public ListWithMembership(ListView list, ListItemView? listItem = null)
    {
        List = list;
        ListItem = listItem;
    }

    /// <summary>
    /// Gets the list.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public ListView List { get; init; }

    /// <summary>
    /// Gets the list item indicating membership of a target user to the list.
    /// </summary>
    [JsonInclude]
    public ListItemView? ListItem { get; init; }
}