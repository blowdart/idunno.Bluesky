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
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="uri"/> is longer than the maximum text allowed for the text in a link.</exception>
        public Link(string uri)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(uri);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(uri.Length, Maximum.LinkTextLengthInCharacters, nameof(uri));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(uri.GetLengthInGraphemes(), Maximum.LinkTextLengthInGraphemes, nameof(uri));

            if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri? createdUri))
            {
                throw new ArgumentException("cannot be parsed as a Uri.", nameof(uri));
            }
            if (createdUri is null)
            {
                throw new ArgumentException("passed validation but returned a null uri.", nameof(uri));
            }

            Uri = createdUri;
            Text = uri;
        }

        /// <summary>
        /// Creates a new instance of <see cref="Link"/>.
        /// </summary>
        /// <param name="uri">The <see cref="Uri"/> to add to a post.</param>
        /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">if <paramref name="uri"/> is longer than the maximum text allowed for the text in a link.</exception>
        public Link(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);

            string uriAsString = uri.ToString();
            ArgumentOutOfRangeException.ThrowIfGreaterThan(uriAsString.Length, Maximum.LinkTextLengthInCharacters, nameof(uri));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(uriAsString.GetLengthInGraphemes(), Maximum.LinkTextLengthInGraphemes, nameof(uri));

            Uri = uri;
            Text = uriAsString;
        }

        /// <summary>
        /// Creates a new instance of <see cref="Link"/>.
        /// </summary>
        /// <param name="uri">The <see cref="Uri"/> to add to a post.</param>
        /// <param name="text">The text to wrap the hashtag around, if any. If not specified the <paramref name="uri"/> will be used..</param>
        /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="text"/> is longer than the maximum allowed.</exception>
        public Link(Uri uri, string text) : base(text)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(text);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.LinkTextLengthInCharacters, nameof(text));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetLengthInGraphemes(), Maximum.LinkTextLengthInGraphemes, nameof(text));

            Uri = uri;
        }

        /// <summary>
        /// Creates a new instance of <see cref="Link"/>.
        /// </summary>
        /// <param name="uri">The <see cref="Uri"/> to add to a post.</param>
        /// <param name="text">The text to wrap the hashtag around, if any. If not specified the <paramref name="uri"/> will be used..</param>
        /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="text"/> is longer than the maximum allowed.</exception>
        /// <remarks>
        public Link(string uri, string text) : base(text)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(uri);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(text);

            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.LinkTextLengthInCharacters, nameof(text));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetLengthInGraphemes(), Maximum.LinkTextLengthInGraphemes, nameof(text));

            if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri? createdUri))
            {
                throw new ArgumentException("cannot be parsed as a Uri.", nameof(uri));
            }
            if (createdUri is null)
            {
                throw new ArgumentException("passed validation but returned a null uri.", nameof(uri));
            }

            Uri = createdUri;
        }

        /// <summary>
        /// Gets the <see cref="Uri"/> the facet feature links to.
        /// </summary>
        public Uri Uri { get; init; }
    }
}
