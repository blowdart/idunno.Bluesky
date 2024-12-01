// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// Represents an embedded record in a post for externally linked content.
    /// </summary>
    public sealed record EmbeddedExternal : EmbeddedBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="EmbeddedExternal"/>
        /// </summary>
        /// <param name="external">The properties for the externally linked content.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="external"/> is null.</exception>
        [JsonConstructor]
        public EmbeddedExternal(ExternalProperties external)
        {
            ArgumentNullException.ThrowIfNull(external);
            External = external;
        }

        /// <summary>
        /// Creates a new instance of <see cref="EmbeddedExternal"/>.
        /// </summary>
        /// <param name="uri">The external <see cref="Uri"/> for the link.</param>
        /// <param name="title">The title for the external link.</param>
        /// <param name="description">The description of the external link, if any.</param>
        /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or <paramref name="title"/> is null.</exception>
        public EmbeddedExternal(Uri uri, string title, string? description = null, Blob? thumbnail = null) :
            this(new ExternalProperties(uri, title, description, thumbnail))
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(title);
        }

        /// <summary>
        /// Creates a new instance of <see cref="EmbeddedExternal"/>.
        /// </summary>
        /// <param name="uri">The external <see cref="Uri"/> for the link.</param>
        /// <param name="title">The title for the external link.</param>
        /// <param name="description">The description of the external link, if any.</param>
        /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or <paramref name="title"/> is null.</exception>
        public EmbeddedExternal(string uri, string title, string? description = null, Blob? thumbnail = null) :
            this(new ExternalProperties(new Uri(uri), title, description, thumbnail))
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(title);
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
    public sealed record ExternalProperties
    {
        /// <summary>
        /// Creates a new instance of <see cref="ExternalProperties"/>.
        /// </summary>
        /// <param name="uri">The external <see cref="Uri"/> for the link.</param>
        /// <param name="title">The title for the external link.</param>
        /// <param name="description">The description of the external link, if any.</param>
        /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or <paramref name="title"/> is null.</exception>
        [JsonConstructor]
        public ExternalProperties(Uri uri, string title, string? description = null, Blob? thumbnail = null) : base()
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(title);

            Uri = uri;
            Title = title;
            Description = description ?? string.Empty;
            Thumbnail = thumbnail;
        }

        /// <summary>
        /// Creates a new instance of <see cref="ExternalProperties"/>.
        /// </summary>
        /// <param name="uri">The external uri for the link.</param>
        /// <param name="title">The title for the external link.</param>
        /// <param name="description">The description of the external link.</param>
        /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or <paramref name="title"/> is null.</exception>
        public ExternalProperties(string uri, string title, string? description = null, Blob? thumbnail = null) :
            this(new Uri(uri), title, description, thumbnail)
        {
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
        public string? Description { get; init; }

        /// <summary>
        /// The <see cref="Uri"/> to a thumbnail image for the external link.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("thumb")]
        public Blob? Thumbnail { get; init; }
    }
}
