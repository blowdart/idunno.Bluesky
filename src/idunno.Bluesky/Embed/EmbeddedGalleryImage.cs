// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Encapsulates information about an image embedded in a gallery.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = false, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(EmbeddedGalleryImage), typeDiscriminator: "app.bsky.embed.gallery#image")]
public record EmbeddedGalleryImage
{
    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedGalleryImage"/>
    /// </summary>
    /// <param name="image">A <see cref="Blob"/> containing details of the uploaded image.</param>
    /// <param name="altText">The AltText (for accessibility) for the image.</param>
    /// <param name="aspectRatio">The image's aspect ratio.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the parameters are <see langword="null" />.</exception>
    [JsonConstructor]
    public EmbeddedGalleryImage(Blob image, string altText, AspectRatio aspectRatio)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(altText);
        ArgumentNullException.ThrowIfNull(aspectRatio);

        Image = image;
        AltText = altText;
        AspectRatio = aspectRatio;
    }

    /// <summary>
    /// Gets a <see cref="Blob"/> containing details of the uploaded image.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public Blob Image { get; init; }

    /// <summary>
    /// Gets AltText (for accessibility) for the image.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("alt")]
    public string AltText { get; init; }

    /// <summary>
    /// The image's aspect ratio.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public AspectRatio AspectRatio { get; init; }
}
