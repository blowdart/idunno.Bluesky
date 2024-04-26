// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky
{
    // https://docs.bsky.app/docs/advanced-guides/posts
    // https://docs.bsky.app/docs/advanced-guides/post-richtext

    /// <summary>
    /// Represents a facet for a <see cref="NewBlueskyPost"/> or <see cref="Feed.Post"/>.
    /// A facet is a piece of text that has been annotated with additional information.
    /// </summary>
    public class Facet
    {
        private readonly Encoding _utf8 = new UTF8Encoding();

        private Facet(string text, long startPosition)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (startPosition < 0)
            {
                throw new ArgumentException("startPosition cannot be < 0.", nameof(startPosition));
            }

            Index = new ByteRange(startPosition, startPosition + _utf8.GetByteCount(text));

            Text = text;
        }

        [JsonConstructor]
        protected Facet(ByteRange index, IList<FacetFeatureBase> features)
        {
            Index = index;
            Features = features;
            Text = string.Empty;
        }

        /// <summary>
        /// Creates a new instance of a <see cref="Facet"/> for a link.
        /// </summary>
        /// <param name="text">The text the facet applies to.</param>
        /// <param name="startPosition">The start position in a <see cref="NewBlueskyPost"/> the facet will begin at.</param>
        /// <param name="uri">The URI the link refers to.</param>
        public Facet(string text, long startPosition, Uri uri) : this(text, startPosition)
        {
            Features.Add(new LinkFacetFeature(uri));
        }

        /// <summary>
        /// Creates a new instance of a <see cref="Facet"/> for a mention.
        /// </summary>
        /// <param name="text">The text the facet applies to.</param>
        /// <param name="startPosition">The start position in a <see cref="NewBlueskyPost"/> the facet will begin at.</param>
        /// <param name="did">The <see cref="Did"/> of the actor being mentioned.</param>

        public Facet(string text, long startPosition, Did did) : this(text, startPosition)
        {
            Features.Add(new MentionFacetFeature(did));
        }

        /// <summary>
        /// Creates a new instance of a <see cref="Facet"/> for a hashtag.
        /// </summary>
        /// <param name="text">The text the facet applies to.</param>
        /// <param name="startPosition">The start position in a <see cref="NewBlueskyPost"/> the facet will begin at.</param>
        /// <param name="hashtag">The hashtag the text refers to.</param>

        public Facet(string text, long startPosition, string hashtag) : this(text, startPosition)
        {
            if (string.IsNullOrEmpty(hashtag))
            {
                throw new ArgumentNullException(nameof(hashtag));
            }

            if (hashtag.StartsWith('#'))
            {
                hashtag = hashtag[1..];
            }

            Features.Add(new HashtagFacetFeature(hashtag));
        }

        [JsonIgnore]
        internal string Text { get; }

        /// <summary>
        /// The range in the <see cref="NewBlueskyPost"/> the facet will apply to.
        /// </summary>
        /// <remarks>
        /// A byte range is applied over the UTF8 encoding of the <see cref="NewBlueskyPost"/> text.
        /// </remarks>
        [JsonInclude]
        [JsonRequired]
        public ByteRange Index { get; set; }

        /// <summary>
        /// A list of features belonging to this facet.
        /// </summary>
        /// <returns>
        /// A list of features belonging to this facet.
        /// </returns>
        [JsonInclude]
        [JsonRequired]
        public IList<FacetFeatureBase> Features { get; internal set; } = new List<FacetFeatureBase>();
    }
}
