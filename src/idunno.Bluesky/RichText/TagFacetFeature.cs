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
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="tag"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="tag"/> is longer than 640 characters or 64 graphemes.</exception>
        public TagFacetFeature(string tag)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(tag);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.Length, Maximum.TagLengthInCharacters);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetLengthInGraphemes(), Maximum.TagLengthInGraphemes);
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
