// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Represents an embedded record pointing to a strongly referenced record, such as another post.
/// </summary>
public record EmbeddedRecord : EmbeddedBase, IValidatableObject, IRecordValidatable
{
    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedRecord"/>.
    /// </summary>
    /// <param name="record">A <see cref="StrongReference"/> to the record to embed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/> is <see langword="null" />.</exception>
    public EmbeddedRecord(StrongReference record)
    {
        ArgumentNullException.ThrowIfNull(record);

        Record = record;
    }

    /// <summary>
    /// Gets a <see cref="StrongReference"/> to the record to embed.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public StrongReference Record { get; init; }

    /// <summary>
    /// Validates the embedded record reference.
    /// </summary>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A collection of validation errors, or an empty collection when the reference is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateReference();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateReference();

    private IEnumerable<ValidationResult> ValidateReference()
    {
        if (Record is null)
        {
            yield return new ValidationResult("A record reference is required.", [nameof(Record)]);
        }
    }
}