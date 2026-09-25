// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.Bluesky.Embed.Gallery;

namespace idunno.Bluesky.Embed;

/// <summary>
/// An assortment of media embedded in a Bluesky record (eg, a post).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "Matches Bluesky lexicon")]
public record EmbeddedGallery : EmbeddedMediaBase
{
    private List<GalleryImage> _items;

    /// <summary>
    /// Creates a new instance of the <see cref="EmbeddedGallery"/> class.
    /// </summary>
    /// <param name="items">The collection of <see cref="EmbeddedImage"/> items to include in the gallery.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the items collection contains <see langword="null" /> values or items with invalid properties.</exception>
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
            ValidateItem(item, nameof(items));
        }

        _items = [.. items];
    }

    /// <summary>
    /// Creates a new instance of the <see cref="EmbeddedGallery"/> class when deserializing.
    /// </summary>
    /// <remarks>
    /// <para><see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/embed/gallery.json">app.bsky.embed.gallery</see>
    /// allows up to twenty items, places no lower bound on the number of items, and places no maximum at all on the items in a gallery view.
    /// The limit of <see cref="Maximum.GalleryItems"/> is a client authoring limit, not a schema limit, so none of the validation the public
    /// constructors perform may be applied when reading, otherwise a valid gallery sent by the service would fail to deserialize.</para>
    /// </remarks>
    [JsonConstructor]
    internal EmbeddedGallery()
    {
        _items = [];
    }

    /// <summary>
    /// Creates a new instance of the <see cref="EmbeddedGallery"/> class from an existing instance.
    /// </summary>
    /// <param name="other">The instance to copy.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <see langword="null" />.</exception>
    /// <remarks>
    /// <para>The items are copied, so a gallery produced by a <see langword="with" /> expression does not share its items with the instance it was copied from.</para>
    /// </remarks>
    protected EmbeddedGallery(EmbeddedGallery other) : base(other)
    {
        ArgumentNullException.ThrowIfNull(other);

        _items = [.. other._items];
    }

    /// <summary>
    /// Creates a new instance of the <see cref="EmbeddedGallery"/> class.
    /// </summary>
    /// <param name="items">The collection of <see cref="EmbeddedImage"/> items to include in the gallery.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null" />.</exception>
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

        _items = galleryItems;
    }

    /// <summary>
    /// Gets the collection of <see cref="GalleryImage"/> items in the gallery.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null" />.</exception>
    /// <remarks>
    /// <para>The collection is read-only. Use <see cref="Add(GalleryImage)"/>, <see cref="Remove(GalleryImage)"/> and <see cref="Clear()"/> to change its contents,
    /// so that the gallery's authoring limits are applied.</para>
    /// </remarks>
    [JsonInclude]
    [JsonRequired]
    public IReadOnlyList<GalleryImage> Items
    {
        get => _items;

        internal set
        {
            ArgumentNullException.ThrowIfNull(value);

            _items = [.. value];
        }
    }

    /// <summary>
    /// Gets the number of elements contained in the gallery.
    /// </summary>
    [JsonIgnore]
    public int Count => _items.Count;

    /// <summary>
    /// Adds an <see cref="GalleryImage"/> item to the gallery.
    /// </summary>
    /// <param name="item">The <see cref="GalleryImage"/> item to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="item"/> has no image, or its image is not an image MIME type.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the gallery already contains the maximum number of items.</exception>
    public void Add(GalleryImage item)
    {
        ValidateItem(item, nameof(item));

        if (_items.Count >= Maximum.GalleryItems)
        {
            throw new InvalidOperationException($"Cannot add more than {Maximum.GalleryItems} items to the gallery.");
        }

        _items.Add(item);
    }

    /// <summary>
    /// Removes all items from the gallery.
    /// </summary>
    public void Clear() => _items.Clear();

    /// <summary>
    /// Determines whether the gallery contains a specific <see cref="EmbeddedImage"/> item.
    /// </summary>
    /// <param name="item">The <see cref="EmbeddedImage"/> item to locate in the gallery.</param>
    /// <returns><see langword="true"/> if the item is found; otherwise, <see langword="false"/>.</returns>
    public bool Contains(GalleryImage item) => _items.Contains(item);

    /// <summary>
    /// Copies the elements of the gallery to an array, starting at a particular array index.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in the array at which copying begins.</param>
    public void CopyTo(GalleryImage[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <summary>
    /// Removes a specific <see cref="GalleryImage"/> item from the gallery.
    /// </summary>
    /// <param name="item">The <see cref="GalleryImage"/> item to remove.</param>
    /// <returns><see langword="true"/> if the item was successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(GalleryImage item) => _items.Remove(item);

    private static void ValidateItem([NotNull] GalleryImage? item, string paramName)
    {
        if (item is null)
        {
            throw new ArgumentNullException(paramName, "Gallery items cannot be null.");
        }

        if (item.Image is null)
        {
            throw new ArgumentException("Gallery items cannot have a null Image property.", paramName);
        }

        if (item.Image.MimeType is null || !item.Image.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Gallery items cannot have a non-image MIME type.", paramName);
        }
    }

    /// <summary>
    /// Determines whether the specified <see cref="EmbeddedGallery"/> is equal to the current instance.
    /// </summary>
    /// <param name="other">The <see cref="EmbeddedGallery"/> to compare against the current instance.</param>
    /// <returns><see langword="true" /> if <paramref name="other"/> is equal to the current instance, otherwise <see langword="false" />.</returns>
    /// <remarks>
    /// <para><see cref="Items"/> is compared by its contents rather than by reference.</para>
    /// </remarks>
    public virtual bool Equals(EmbeddedGallery? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || EqualityContract != other.EqualityContract)
        {
            return false;
        }

        return base.Equals(other) && CollectionComparison.SequenceEquals(Items, other.Items);
    }

    /// <summary>
    /// Returns the hash code for the current instance.
    /// </summary>
    /// <returns>The hash code for the current instance.</returns>
    /// <remarks>
    /// <para>
    ///   The hash code is derived from the items the gallery currently contains, so it changes if the gallery is mutated.
    ///   Do not mutate an instance while it is being used as a key in a hashed collection.
    /// </para>
    /// </remarks>
    [SuppressMessage("Major Code Smell", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "Value equality over a mutable collection requires the hash code to be derived from the same state, which is documented on the member.")]
    public override int GetHashCode()
    {
        HashCode hashCode = new();

        hashCode.Add(base.GetHashCode());
        CollectionComparison.AddSequence(ref hashCode, Items);

        return hashCode.ToHashCode();
    }
}