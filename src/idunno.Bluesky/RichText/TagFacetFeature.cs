// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.RichText;

/// <summary>
/// Facet feature for a hashtag. The text usually includes a '#' prefix, but the facet reference should not (except in the case of 'double hash tags').
/// </summary>
public sealed record TagFacetFeature : FacetFeature, IValidatableObject, IRecordValidatable
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

    internal static TagFacetFeature CreateUnvalidated(string tag)
    {
        return new TagFacetFeature(tag, true);
    }

    private TagFacetFeature(string tag, bool _)
    {
        Tag = tag;
    }

    /// <summary>
    /// Validates the hashtag length.
    /// </summary>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A collection of validation errors, or an empty collection when the hashtag is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateTag();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord()
    {
        return ValidateTag();
    }

    private IEnumerable<ValidationResult> ValidateTag()
    {
        if (Tag is null)
        {
            yield return new ValidationResult("The tag cannot be null.", [nameof(Tag)]);
        }
        else if (Tag.GetUtf8Length() > Maximum.TagLengthInBytes || Tag.GetGraphemeLength() > Maximum.TagLengthInGraphemes)
        {
            yield return new ValidationResult(
                $"The tag must not exceed {Maximum.TagLengthInBytes} UTF-8 bytes or {Maximum.TagLengthInGraphemes} graphemes.",
                [nameof(Tag)]);
        }
    }
}