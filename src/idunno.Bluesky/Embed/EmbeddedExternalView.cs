// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// Represents a view over an <see cref="EmbeddedExternal"/> record.
    /// </summary>
    public sealed record EmbeddedExternalView : EmbeddedView
    {
        /// <summary>
        /// Creates a new instance of <see cref="EmbeddedExternalView"/>.
        /// </summary>
        /// <param name="external">The external view properties for the embedded media.</param>
        [JsonConstructor]
        internal EmbeddedExternalView(EmbeddedExternalViewProperties external) : base()
        {
            External = external;
        }

        /// <summary>
        /// Gets the properties for this instance.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public EmbeddedExternalViewProperties External { get; init; }
    }

    /// <summary>
    /// The view properties on a view over an <see cref="EmbeddedExternal"/> record.
    /// </summary>
    public record EmbeddedExternalViewProperties
    {
        /// <summary>
        /// Creates a new instance of <see cref="EmbeddedExternalViewProperties"/>.
        /// </summary>
        /// <param name="uri">The external <paramref name="uri"/>.</param>
        /// <param name="title">The title for the external link.</param>
        /// <param name="description">The description of the external link.</param>
        /// <param name="thumbnailUri">The <see cref="Uri"/> to a thumbnail image for the external link.</param>
        [JsonConstructor]
        internal EmbeddedExternalViewProperties(Uri uri, string title, string description, Uri? thumbnailUri) : base()
        {
            Uri = uri;
            Title = title;
            Description = description;
            ThumbnailUri = thumbnailUri;
        }

        /// <summary>
        /// Gets the external <see cref="Uri"/>.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Uri Uri { get; init; }

        /// <summary>
        /// The title for the external link.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string Title { get; init; }

        /// <summary>
        /// The description of the external link.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string Description { get; init; }

        /// <summary>
        /// The <see cref="Uri"/> to a thumbnail image for the external link.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("thumb")]
        public Uri? ThumbnailUri { get; init; }
    }
}
