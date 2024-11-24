// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// Encapsulates information about an embedded image.
    /// </summary>
    public record EmbeddedImage
    {
        /// <summary>
        /// Creates a new instance of <see cref="EmbeddedImage"/>
        /// </summary>
        /// <param name="image">A <see cref="Blob"/> containing details of the uploaded image.</param>
        /// <param name="altText">The AltText (for accessibility) for the image.</param>
        /// <param name="aspectRatio">The image's aspect ratio.</param>
        [JsonConstructor]
        public EmbeddedImage(Blob image, string altText, AspectRatio? aspectRatio = null)
        {
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
        /// The image's aspect ratio, if any.
        /// </summary>
        [JsonInclude]
        public AspectRatio? AspectRatio { get; init; }
    }
}
