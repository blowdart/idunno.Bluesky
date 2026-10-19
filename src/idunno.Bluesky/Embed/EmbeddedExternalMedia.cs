// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Represents an external card used as media in an embedded record with media.
/// </summary>
public sealed record EmbeddedExternalMedia : EmbeddedMediaBase, IValidatableObject, IRecordValidatable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedExternalMedia"/> class.
    /// </summary>
    /// <param name="external">The properties of the external card.</param>
    /// <exception cref="ArgumentNullException"><paramref name="external"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public EmbeddedExternalMedia(External.Properties external)
    {
        ArgumentNullException.ThrowIfNull(external);

        External = external;
    }

    /// <summary>
    /// Gets the properties of the external card.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public External.Properties External { get; init; }

    /// <summary>
    /// Validates the external card used as media.
    /// </summary>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A collection of validation errors, or an empty collection when the card is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return RecordValidation.Nested(External, nameof(External));
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => RecordValidation.Nested(External, nameof(External));
}
