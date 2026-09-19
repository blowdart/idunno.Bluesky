// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.RichText;

/// <summary>
/// Provides an object representation of a hash tag in a Bluesky post.
/// </summary>
public record HashTag : PostBuilderFacetFeature
{
    /// <summary>
    /// Creates a new instance of <see cref="HashTag"/>.
    /// </summary>
    /// <param name="tag">The hash tag to add to a post. Do not include the '#' prefix except in the case of 'double hash tags'.</param>
    /// <exception cref="ArgumentException">if <paramref name="tag"/> is <see langword="null"/> or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// If <paramref name="tag"/> is longer than the allowed maximum.</exception>
    /// <remarks>
    /// <para>The lexicon limits apply to <paramref name="tag"/> itself, not to the text it renders as, so a tag
    /// of the maximum allowed length is still valid once its '#' prefix is added.</para>
    /// </remarks>
    public HashTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetUtf8Length(), Maximum.TagLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);

        Tag = tag;
        Text = $"#{tag}";
    }

    /// <summary>
    /// Creates a new instance of <see cref="HashTag"/>.
    /// </summary>
    /// <param name="tag">The hash tag to add to a post. Do not include the '#' prefix except in the case of 'double hash tags'.</param>
    /// <param name="text">The text to wrap the hashtag around, if any. The text usually includes a '#' prefix.</param>
    /// <exception cref="ArgumentException">if <paramref name="tag"/> is <see langword="null"/> or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="tag"/> is longer than the allowed maximum.</exception>
    /// <remarks>
    /// <para>If <paramref name="text"/> is not specified the <paramref name="tag"/> will be used as the facet feature text.</para>
    /// <para>The lexicon limits apply to <paramref name="tag"/> only. <paramref name="text"/> is the text the tag
    /// renders as, and so counts towards the length of the post rather than the length of the tag.</para>
    /// </remarks>
    public HashTag(string tag, string text) : base(text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetUtf8Length(), Maximum.TagLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);

        Tag = tag;
        Text = text;
    }

    /// <summary>
    /// Gets the hash tag for this facet feature.
    /// </summary>
    public string Tag { get; init; }
}