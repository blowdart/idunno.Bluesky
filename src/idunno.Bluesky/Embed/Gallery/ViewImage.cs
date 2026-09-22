// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed.Gallery;

/// <summary>
/// Encapsulates image properties returned in a gallery <see cref="View"/>.
/// </summary>
public record ViewImage
{
    /// <summary>
    /// Creates a new instance of <see cref="ViewImage"/>
    /// </summary>
    /// <param name="thumbnail">A fully-qualified URI where a thumbnail of the image can be fetched. For example, CDN location provided by the App View.</param>
    /// <param name="fullSize">A fully-qualified URI where a large version of the image can be fetched. May or may not be the exact original blob. For example, CDN location provided by the App View.</param>
    /// <param name="altText">Alt text description of the image, for accessibility.</param>
    /// <param name="aspectRatio">The aspect ratio of the image.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the parameters are <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>The lexicon declares <paramref name="thumbnail"/> and <paramref name="fullSize"/> as <c>uri</c> formatted strings,
    /// and does not require them to be absolute, so no absolute URI validation is performed.</para>
    /// </remarks>
    [JsonConstructor]
    public ViewImage(Uri thumbnail, Uri fullSize, string altText, AspectRatio aspectRatio)
    {
        ArgumentNullException.ThrowIfNull(thumbnail);
        ArgumentNullException.ThrowIfNull(fullSize);
        ArgumentNullException.ThrowIfNull(altText);
        ArgumentNullException.ThrowIfNull(aspectRatio);

        Thumbnail = thumbnail;
        FullSize = fullSize;
        AltText = altText;
        AspectRatio = aspectRatio;
    }

    /// <summary>
    /// Gets the fully-qualified URI where a thumbnail of the image can be fetched. For example, CDN location provided by the App View.
    /// </summary>
    [JsonRequired]
    public Uri Thumbnail { get; init; }

    /// <summary>
    /// Gets the fully-qualified URI where a large version of the image can be fetched.
    /// </summary>
    [JsonPropertyName("fullsize")]
    [JsonRequired]
    public Uri FullSize { get; init; }

    /// <summary>
    /// Gets the alt text description of the image, for accessibility.
    /// </summary>
    [JsonPropertyName("alt")]
    [JsonRequired]
    public string AltText { get; init; }

    /// <summary>
    /// Gets the aspect ratio of the image.
    /// </summary>
    [JsonRequired]
    public AspectRatio AspectRatio { get; init; }
}