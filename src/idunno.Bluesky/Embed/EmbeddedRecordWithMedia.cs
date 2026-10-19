// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed;

/// <summary>
/// An embedded record with media.
/// </summary>
public record EmbeddedRecordWithMedia : EmbeddedMediaBase, IValidatableObject, IRecordValidatable
{
    /// <summary>
    /// Creates a new <see cref="EmbeddedRecordWithMedia"/>
    /// </summary>
    /// <param name="record">The embedded record.</param>
    /// <param name="media">The media in the record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/> or <paramref name="media"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public EmbeddedRecordWithMedia(EmbeddedRecord record, EmbeddedMediaBase media)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(media);

        Media = media;
        Record = record;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedRecordWithMedia"/> class containing an external card.
    /// </summary>
    /// <param name="record">The record to embed.</param>
    /// <param name="media">The external card or media to attach to the record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> or <paramref name="media"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="media"/> is neither an external card nor embedded media.</exception>
    /// <remarks>
    /// <para>The card is represented by <see cref="EmbeddedExternalMedia"/> in <see cref="Media"/>.</para>
    /// </remarks>
    public EmbeddedRecordWithMedia(EmbeddedRecord record, EmbeddedBase media)
        : this(record, media switch
        {
            EmbeddedExternal externalCard => new EmbeddedExternalMedia(externalCard.External),
            EmbeddedMediaBase embeddedMedia => embeddedMedia,
            null => throw new ArgumentNullException(nameof(media)),
            _ => throw new ArgumentException("The embed must be media or an external card.", nameof(media))
        })
    {
    }

    /// <summary>
    /// Gets the media in the record.
    /// </summary>
    [JsonInclude]
    public EmbeddedMediaBase Media { get; init; }

    /// <summary>
    /// Gets the record to embed.
    /// </summary>
    [JsonInclude]
    public EmbeddedRecord Record { get; init; }

    /// <summary>
    /// Validates the embedded record and its nested media.
    /// </summary>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A collection of validation errors, or an empty collection when the embed is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateMedia();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateMedia();

    private IEnumerable<ValidationResult> ValidateMedia()
    {
        foreach (ValidationResult result in RecordValidation.Nested(Record, nameof(Record)))
        {
            yield return result;
        }

        if (Media is null)
        {
            yield return new ValidationResult("Media is required.", [nameof(Media)]);
        }
        else if (Media is EmbeddedRecordWithMedia)
        {
            yield return new ValidationResult("Record-with-media cannot contain another record-with-media embed.", [nameof(Media)]);
        }
        else if (Media is IRecordValidatable validatable)
        {
            foreach (ValidationResult result in RecordValidation.Nested(validatable, nameof(Media)))
            {
                yield return result;
            }
        }
    }
}
