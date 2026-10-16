// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Embed.External;

/// <summary>
/// Represents a view of a gallery embedded from an external source.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(GalleryView), typeDiscriminator: "app.bsky.embed.external#viewGallery")]
public record GalleryView : Bluesky.View
{
    /// <summary>
    /// Gets the URI of the gallery, as a string.
    /// </summary>
    /// <exception cref="JsonException">The value is <see langword="null"/>.</exception>
    [JsonRequired]
    [JsonPropertyName("uri")]
    public string Identity
    {
        get;
        init => field = ExternalViewCollection.Require(value, nameof(Identity));
    } = null!;

    /// <summary>
    /// Gets the title of the gallery.
    /// </summary>
    /// <exception cref="JsonException">The value is <see langword="null"/>.</exception>
    [JsonRequired]
    public string Title
    {
        get;
        init => field = ExternalViewCollection.Require(value, nameof(Title));
    } = null!;

    /// <summary>
    /// Gets the description of the gallery.
    /// </summary>
    /// <exception cref="JsonException">The value is <see langword="null"/>.</exception>
    [JsonRequired]
    public string Description
    {
        get;
        init => field = ExternalViewCollection.Require(value, nameof(Description));
    } = null!;

    /// <summary>
    /// Gets the date and time when the gallery was created, if available.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Gets the date and time when the gallery was last updated, if available.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Gets the labels applied to the gallery, if any.
    /// </summary>
    /// <exception cref="JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<Label>? Labels
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(Labels));
    }

    /// <summary>
    /// Gets the strong references associated with the gallery, if any.
    /// </summary>
    /// <exception cref="JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<StrongReference>? AssociatedRefs
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(AssociatedRefs));
    }

    /// <summary>
    /// Gets the profiles associated with the gallery, if any.
    /// </summary>
    /// <exception cref="JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<ProfileViewBasic>? AssociatedProfiles
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(AssociatedProfiles));
    }

    /// <summary>
    /// Gets the media items in the gallery as JSON elements so unrecognized union members retain their discriminator and fields.
    /// </summary>
    /// <exception cref="JsonException">The collection is <see langword="null"/> or contains a <see langword="null"/> or non-object item.</exception>
    [JsonRequired]
    public IReadOnlyCollection<JsonElement> Items
    {
        get;
        init => field = ExternalViewCollection.CopyUnion(value, nameof(Items));
    } = Array.Empty<JsonElement>();

    /// <summary>
    /// Gets the number of likes, if available.
    /// </summary>
    public int? LikeCount { get; init; }

    /// <summary>
    /// Gets up to three available profile previews of likers, if any.
    /// </summary>
    /// <exception cref="JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<ProfileViewBasic>? Likers
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(Likers));
    }
}
