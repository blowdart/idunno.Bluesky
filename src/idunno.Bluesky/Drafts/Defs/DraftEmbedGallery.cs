// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Drafts;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates a gallery of local embedded images in a draft post.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = false, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(DraftEmbedGallery), typeDiscriminator: "app.bsky.draft.defs#draftEmbedGallery")]
public record DraftEmbedGallery
{
    /// <summary>
    /// Creates a new instance of <see cref="DraftEmbedGallery"/> with the specified items.
    /// </summary>
    /// <param name="items">The collection of embedded images in the gallery.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public DraftEmbedGallery(IReadOnlyList<DraftEmbedImage> items)
    {
        Items = items;
    }

    /// <summary>
    /// Gets the collection of embedded images in the gallery.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when setting to <see langword="null"/>.</exception>
    [JsonRequired]
    public IReadOnlyList<DraftEmbedImage> Items
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = new List<DraftEmbedImage>(value).AsReadOnly();
        }
    }
}
