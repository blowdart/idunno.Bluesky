// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// Represents a view over an <see cref="EmbeddedImages"/> record.
    /// </summary>
    public sealed record EmbeddedImagesView : EmbeddedView
    {
        /// <summary>
        /// Creates a new instance of <see cref="EmbeddedImagesView"/>.
        /// </summary>
        /// <param name="images">The collection of embedded images in a post.</param>
        [JsonConstructor]
        internal EmbeddedImagesView(IReadOnlyList<EmbeddedImageView> images)
        {
            Images = images;
        }

        /// <summary>
        /// Gets the collection of embedded images.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public IReadOnlyList<EmbeddedImageView> Images { get; init; }
    }
}
