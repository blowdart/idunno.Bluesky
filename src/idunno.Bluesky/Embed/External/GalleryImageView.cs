// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed.External;

/// <summary>
/// Represents an image item in an external gallery.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(GalleryImageView), typeDiscriminator: "app.bsky.embed.external#viewGalleryImage")]
public record GalleryImageView : Bluesky.View
{
    /// <summary>
    /// Gets the URI where a thumbnail of the image can be fetched.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The value is <see langword="null"/>.</exception>
    [JsonRequired]
    public Uri Thumbnail
    {
        get;
        init => field = ExternalViewCollection.Require(value, nameof(Thumbnail));
    } = null!;

    /// <summary>
    /// Gets the URI where a large version of the image can be fetched.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The value is <see langword="null"/>.</exception>
    [JsonRequired]
    [JsonPropertyName("fullsize")]
    public Uri FullSize
    {
        get;
        init => field = ExternalViewCollection.Require(value, nameof(FullSize));
    } = null!;

    /// <summary>
    /// Gets the alt text description of the image, if available.
    /// </summary>
    [JsonPropertyName("alt")]
    public string? Alt { get; init; }

    /// <summary>
    /// Gets the aspect ratio of the image, if available.
    /// </summary>
    public AspectRatio? AspectRatio { get; init; }
}
