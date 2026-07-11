// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed.Gallery;

/// <summary>
/// Represents a view over an embedded gallery in a post.
/// </summary>
public record View : EmbeddedView
{
    /// <summary>
    /// Creates a new instance of <see cref="View"/>.
    /// </summary>
    /// <param name="items">The collection of <see cref="ViewImage"/> items to include in the gallery view.</param>
    /// <exception cref="ArgumentNullException">Thrown when the items collection is <see langword="null"/>.</exception>
    public View(ICollection<ViewImage> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        Items = items;
    }

    /// <summary>
    /// Gets the collection of <see cref="ViewImage"/> items included in the gallery view.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public ICollection<ViewImage> Items { get; init; }
}