// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.RichText
{
    /// <summary>
    /// Facet feature for a hashtag. The text usually includes a '#' prefix, but the facet reference should not (except in the case of 'double hash tags').
    /// </summary>
    public sealed record TagFacetFeature : FacetFeature
    {
        /// <summary>
        /// Creates a new instance of <see cref="TagFacetFeature"/>.
        /// </summary>
        /// <param name="tag">The hashtag referred to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tag"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="tag"/> is white space.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tag"/> is longer than 640 characters or 64 graphemes.</exception>
        public TagFacetFeature(string tag)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tag);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
            Tag = tag;
        }

        /// <summary>
        /// The hashtag referred to.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string Tag { get; init; }
    }
}
