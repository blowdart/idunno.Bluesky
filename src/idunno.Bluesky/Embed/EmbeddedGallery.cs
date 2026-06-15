// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Embed.Gallery;

namespace idunno.Bluesky.Embed;

/// <summary>
/// An assortment of media embedded in a Bluesky record (eg, a post).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "Matches Bluesky lexicon")]
public record EmbeddedGallery : EmbeddedMediaBase
{
    /// <summary>
    /// Creates a new instance of the <see cref="EmbeddedGallery"/> class.
    /// </summary>
    /// <param name="items">The collection of <see cref="EmbeddedImage"/> items to include in the gallery.</param>
    /// <exception cref="ArgumentException">Thrown when the items collection contains <see langword="null" /> values or items with invalid properties.</exception>
    [JsonConstructor]
    public EmbeddedGallery(ICollection<GalleryImage> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            throw new ArgumentException("Items collection cannot be empty.", nameof(items));
        }

        if (items.Count > Maximum.GalleryItems)
        {
            throw new ArgumentException($"Items collection cannot contain more than {Maximum.GalleryItems} items.", nameof(items));
        }

        foreach (GalleryImage? item in items)
        {
            if (item is null)
            {
                throw new ArgumentException("Items collection cannot contain null values.", nameof(items));
            }

            if (item.Image is null)
            {
                throw new ArgumentException("Items collection cannot contain items with null Image property.", nameof(items));
            }

            if (item.Image.MimeType is null || !item.Image.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Items collection cannot contain items with non-image MIME types.", nameof(items));
            }
        }

        Items = [.. items];
    }


    /// <summary>
    /// Creates a new instance of the <see cref="EmbeddedGallery"/> class.
    /// </summary>
    /// <param name="items">The collection of <see cref="EmbeddedImage"/> items to include in the gallery.</param>
    /// <exception cref="ArgumentException">Thrown when the items collection contains <see langword="null" /> values or items with invalid properties.</exception>
    public EmbeddedGallery(ICollection<EmbeddedImage> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            throw new ArgumentException("Items collection cannot be empty.", nameof(items));
        }

        if (items.Count > Maximum.GalleryItems)
        {
            throw new ArgumentException($"Items collection cannot contain more than {Maximum.GalleryItems} items.", nameof(items));
        }

        List<GalleryImage> galleryItems = new(items.Count);

        foreach (EmbeddedImage? item in items)
        {
            if (item is null)
            {
                throw new ArgumentException("Items collection cannot contain null values.", nameof(items));
            }

            if (item.Image is null)
            {
                throw new ArgumentException("Items collection cannot contain items with null Image property.", nameof(items));
            }

            if (item.Image.MimeType is null || !item.Image.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Items collection cannot contain items with non-image MIME types.", nameof(items));
            }

            if (item.AspectRatio is null)
            {
                throw new ArgumentException("Items collection cannot contain items with null AspectRatio property.", nameof(items));
            }

            galleryItems.Add(new GalleryImage(item.Image, item.AltText, item.AspectRatio));
        }

        Items = [.. galleryItems];
    }

    /// <summary>
    /// Gets a read-only collection of the <see cref="EmbeddedImage"/> items in the gallery.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public ICollection<GalleryImage> Items { get; internal set; }

    /// <summary>
    /// Gets the number of elements contained in the gallery.
    /// </summary>
    [JsonIgnore]
    public int Count => Items.Count;

    /// <summary>
    /// Adds an <see cref="GalleryImage"/> item to the gallery.
    /// </summary>
    /// <param name="item">The <see cref="GalleryImage"/> item to add.</param>
    /// <exception cref="InvalidOperationException">Thrown when the gallery already contains the maximum number of items.</exception>
    public void Add(GalleryImage item)
    {
        if (Items.Count >= Maximum.GalleryItems)
        {
            throw new InvalidOperationException($"Cannot add more than {Maximum.GalleryItems} items to the gallery.");
        }

        Items.Add(item);
    }

    /// <summary>
    /// Removes all items from the gallery.
    /// </summary>
    public void Clear() => Items.Clear();

    /// <summary>
    /// Determines whether the gallery contains a specific <see cref="EmbeddedImage"/> item.
    /// </summary>
    /// <param name="item">The <see cref="EmbeddedImage"/> item to locate in the gallery.</param>
    /// <returns><see langword="true"/> if the item is found; otherwise, <see langword="false"/>.</returns>
    public bool Contains(GalleryImage item) => Items.Contains(item);

    /// <summary>
    /// Copies the elements of the gallery to an array, starting at a particular array index.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in the array at which copying begins.</param>
    public void CopyTo(GalleryImage[] array, int arrayIndex) => Items.CopyTo(array, arrayIndex);

    /// <summary>
    /// Removes a specific <see cref="GalleryImage"/> item from the gallery.
    /// </summary>
    /// <param name="item">The <see cref="GalleryImage"/> item to remove.</param>
    /// <returns><see langword="true"/> if the item was successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(GalleryImage item) => Items.Remove(item);
}