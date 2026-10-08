// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.RichText;

/// <summary>
/// Facet feature for a hashtag. The text usually includes a '#' prefix, but the facet reference should not (except in the case of 'double hash tags').
/// </summary>
public sealed record TagFacetFeature : FacetFeature
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TagFacetFeature"/> class.
    /// </summary>
    /// <param name="tag">The hashtag referred to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tag"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tag"/> is longer than 640 UTF-8 bytes or 64 graphemes.</exception>
    /// <remarks>
    /// <para>The lexicon permits empty and whitespace-only tags. The value is preserved unchanged.
    /// Use <see cref="HashTag"/> for authoring hashtags with nonempty, non-whitespace values.</para>
    /// </remarks>
    public TagFacetFeature(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetUtf8Length(), Maximum.TagLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
        Tag = tag;
    }

    /// <summary>
    /// Gets the hashtag referred to, which may be empty or whitespace-only.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public string Tag { get; init; }
}