// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Represents embedded images in a post for.
/// </summary>
public record EmbeddedImages : EmbeddedMediaBase
{
    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedImages"/>.
    /// </summary>
    /// <param name="images">The images to embed in a post.</param>
    /// <exception cref="ArgumentNullException">Thrown when images is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the images contains more than the maximum number of images for a post, or does not contain any images.</exception>
    public EmbeddedImages(ICollection<EmbeddedImage> images)
    {
        ArgumentNullException.ThrowIfNull(images);

        ArgumentOutOfRangeException.ThrowIfLessThan(images.Count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(images.Count, Maximum.ImagesInPost);

        Images = images;
    }

    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedImages"/> when deserializing.
    /// </summary>
    /// <remarks>
    /// <para><see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/embed/images.json">app.bsky.embed.images</see>
    /// places no lower bound on the number of images, so an empty collection is a valid payload and must not be rejected when reading.
    /// The stricter validation in the public constructor applies only to images being embedded in a post by this library.</para>
    /// </remarks>
    [JsonConstructor]
    internal EmbeddedImages()
    {
        Images = [];
    }

    /// <summary>
    /// Gets the collection of images to embed.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public ICollection<EmbeddedImage> Images { get; init; }
}