// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// Represents a view over an embedded image in a post.
    /// </summary>
    public sealed record EmbeddedImageView
    {
        /// <summary>
        /// Creates a new instance of <see cref="EmbeddedImageView"/>
        /// </summary>
        /// <param name="thumbnailUri">The fully-qualified URL where a thumbnail of the image can be fetched</param>
        /// <param name="fullSizeUri">The fully-qualified URL where a large version of the image can be fetched</param>
        /// <param name="altText">The alt text description of the image, for accessibility.</param>
        /// <param name="aspectRatio">An optional aspect ratio for the image.</param>
        [JsonConstructor]
        internal EmbeddedImageView(Uri thumbnailUri, Uri fullSizeUri, string altText, AspectRatio? aspectRatio)
        {
            ThumbnailUri = thumbnailUri;
            FullSizeUri = fullSizeUri;
            AltText = altText;
            AspectRatio = aspectRatio;
        }

        /// <summary>
        /// Gets the fully-qualified URL where a thumbnail of the image can be fetched. For example, a CDN location provided by the App View.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("thumb")]
        public Uri ThumbnailUri { get; init; }

        /// <summary>
        /// Gets the fully-qualified URL where a large version of the image can be fetched. May or may not be the exact original blob.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("fullsize")]
        public Uri FullSizeUri { get; init; }

        /// <summary>
        /// Gets the alt text description of the image, for accessibility.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("alt")]
        public string AltText { get; init; }

        /// <summary>
        /// Gets an optional aspect ratio for the image.
        /// </summary>
        [JsonInclude]
        public AspectRatio? AspectRatio { get; init; }
    }
}
