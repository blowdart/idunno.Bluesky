// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.RichText
{
    /// <summary>
    /// Provides an object representation of a hash tag in a Bluesky post.
    /// </summary>
    public record HashTag : PostBuilderFacetFeature
    {
        /// <summary>
        /// Creates a new instance of <see cref="HashTag"/>.
        /// </summary>
        /// <param name="tag">The hash tag to add to a post. Do not include the '#' prefix except in the case of 'double hash tags'.</param>
        /// <exception cref="ArgumentNullException">if <paramref name="tag"/> is null or white space.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If <paramref name="tag"/> is longer than the allowed maximum.</exception>
        public HashTag(string tag)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(tag);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters, nameof(tag));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetLengthInGraphemes(), Maximum.TagLengthInGraphemes, nameof(tag));
            Tag = tag;

            string tagAsHashTagText = $"#{tag}";
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tagAsHashTagText.Length, Maximum.TagLengthInCharacters, nameof(tag));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tagAsHashTagText.GetLengthInGraphemes(), Maximum.TagLengthInGraphemes, nameof(tag));
            Text = tagAsHashTagText;
        }

        /// <summary>
        /// Creates a new instance of <see cref="HashTag"/>.
        /// </summary>
        /// <param name="tag">The hash tag to add to a post. Do not include the '#' prefix except in the case of 'double hash tags'.</param>
        /// <param name="text">The text to wrap the hashtag around, if any. The text usually includes a '#' prefix.</param>
        /// <exception cref="ArgumentNullException">if <paramref name="tag"/> is null or white space.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="tag"/> or <paramref name="text"/> are longer than the allowed maxiums.</exception>
        /// <remarks>
        /// <para>If <paramref name="text"/> is not specified the <paramref name="tag"/> will be used as the facet feature text.</para>
        /// </remarks>
        public HashTag(string tag, string text) : base(text)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(tag);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(text);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters, nameof(tag));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetLengthInGraphemes(), Maximum.TagLengthInGraphemes, nameof(tag));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.TagLengthInCharacters, nameof(text));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.GetLengthInGraphemes(), 64, nameof(text));
            Tag = tag;
            Text = text;
        }

        /// <summary>
        /// Gets the hash tag for this facet feature.
        /// </summary>
        public string Tag { get; init; }
    }
}
