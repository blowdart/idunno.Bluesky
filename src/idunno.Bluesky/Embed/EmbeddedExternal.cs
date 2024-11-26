// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// Represents an embedded record in a post for external media.
    /// </summary>
    public record EmbeddedExternal : EmbeddedMediaBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="EmbeddedMediaBase"/>
        /// </summary>
        /// <param name="external">The properties for the embedded media.</param>
        [JsonConstructor]
        public EmbeddedExternal(ExternalProperties external)
        {
            External = external;
        }

        /// <summary>
        /// Gets the properties for the embedded media.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public ExternalProperties External { get; init; }
    }

    /// <summary>
    /// The properties for an embedded external media record.
    /// </summary>
    public record ExternalProperties
    {
        /// <summary>
        /// Creates a new instance of <see cref="ExternalProperties"/>.
        /// </summary>
        /// <param name="uri">The external <paramref name="uri"/>.</param>
        /// <param name="title">The title for the external link.</param>
        /// <param name="description">The description of the external link.</param>
        /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
        [JsonConstructor]
        internal ExternalProperties(Uri uri, string title, string description, Blob? thumbnail) : base()
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(title);
            ArgumentNullException.ThrowIfNull(description);

            Uri = uri;
            Title = title;
            Description = description;
            Thumbnail = thumbnail;
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
        public Blob? Thumbnail { get; init; }
    }
}
