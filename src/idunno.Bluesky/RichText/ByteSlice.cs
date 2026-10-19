// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.RichText;

/// <summary>
/// The start and end value for a slice to apply a facet to.
/// </summary>
public sealed record ByteSlice : IValidatableObject, IRecordValidatable
{
    /// <summary>
    /// Constructs a new instance of <see cref="ByteSlice"/>.
    /// </summary>
    /// <param name="byteStart">The byte index at which the facet starts.</param>
    /// <param name="byteEnd">The byte index at which the facet ends.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="byteStart"/> or <paramref name="byteEnd"/> is negative,
    /// or when <paramref name="byteEnd"/> is before <paramref name="byteStart"/>.</exception>
    /// <remarks>
    /// <para><paramref name="byteStart"/> is zero-indexed and inclusive.</para>
    /// <para><paramref name="byteEnd"/> is zero-indexed and exclusive.</para>
    /// </remarks>
    [JsonConstructor]
    public ByteSlice(long byteStart, long byteEnd)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteStart);
        ArgumentOutOfRangeException.ThrowIfNegative(byteEnd);
        ArgumentOutOfRangeException.ThrowIfLessThan(byteEnd, byteStart);

        ByteStart = byteStart;
        ByteEnd = byteEnd;
    }

    /// <summary>
    /// Gets the byte index at which the facet starts.
    /// </summary>
    /// <remarks>
    /// <para>This is is zero-indexed and inclusive.</para>
    /// </remarks>
    [JsonInclude]
    [JsonRequired]
    public long ByteStart { get; init; }

    /// <summary>
    /// Gets the byte index at which the facet ends.
    /// </summary>
    /// <remarks>
    /// <para>This is is zero-indexed and exclusive.</para>
    /// </remarks>
    [JsonInclude]
    [JsonRequired]
    public long ByteEnd { get; init; }

    internal static ByteSlice CreateUnvalidated(long byteStart, long byteEnd)
    {
        return new ByteSlice(byteStart, byteEnd, true);
    }

    private ByteSlice(long byteStart, long byteEnd, bool _)
    {
        ByteStart = byteStart;
        ByteEnd = byteEnd;
    }

    /// <summary>
    /// Validates the byte range.
    /// </summary>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A collection of validation errors, or an empty collection when the byte range is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateByteSlice();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord()
    {
        return ValidateByteSlice();
    }

    private IEnumerable<ValidationResult> ValidateByteSlice()
    {
        if (ByteStart < 0)
        {
            yield return new ValidationResult("The byte start cannot be negative.", [nameof(ByteStart)]);
        }

        if (ByteEnd < 0)
        {
            yield return new ValidationResult("The byte end cannot be negative.", [nameof(ByteEnd)]);
        }
        else if (ByteEnd < ByteStart)
        {
            yield return new ValidationResult("The byte end must be greater than or equal to the byte start.", [nameof(ByteEnd)]);
        }
    }
}