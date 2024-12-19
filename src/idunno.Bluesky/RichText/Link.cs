// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.RichText
{
    /// <summary>
    /// Provides an object representation of a hash tag in a Bluesky post.
    /// </summary>
    public record Link : PostBuilderFacetFeature
    {
        /// <summary>
        /// Creates a new instance of <see cref="Link"/>.
        /// </summary>
        /// <param name="uri">The <see cref="Uri"/> to add to a post.</param>
        /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is null.</exception>
        public Link(string uri)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(uri);

            if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri? createdUri))
            {
                throw new ArgumentException("cannot be parsed as a Uri.");
            }

            Uri = createdUri;
            Text = uri;
        }

        /// <summary>
        /// Creates a new instance of <see cref="Link"/>.
        /// </summary>
        /// <param name="uri">The <see cref="Uri"/> to add to a post.</param>
        /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is null.</exception>
        public Link(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);

            string uriAsString = uri.ToString();

            Uri = uri;
            Text = uriAsString;
        }

        /// <summary>
        /// Creates a new instance of <see cref="Link"/>.
        /// </summary>
        /// <param name="uri">The <see cref="Uri"/> to add to a post.</param>
        /// <param name="text">The text to wrap the hashtag around, if any. If not specified the <paramref name="uri"/> will be used..</param>
        public Link(Uri uri, string text) : base(text)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(text);

            Uri = uri;
        }

        /// <summary>
        /// Creates a new instance of <see cref="Link"/>.
        /// </summary>
        /// <param name="uri">The <see cref="Uri"/> to add to a post.</param>
        /// <param name="text">The text to wrap the hashtag around, if any. If not specified the <paramref name="uri"/> will be used..</param>
        /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is null.</exception>
        public Link(string uri, string text) : base(text)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(uri);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(text);

            if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri? createdUri))
            {
                throw new ArgumentException("cannot be parsed as a Uri.");
            }

            Uri = createdUri;
        }

        /// <summary>
        /// Gets the <see cref="Uri"/> the facet feature links to.
        /// </summary>
        public Uri Uri { get; init; }
    }
}
