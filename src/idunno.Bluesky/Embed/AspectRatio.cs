// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed;

// https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/embed/defs.json

/// <summary>
/// Represents an aspect ratio. It may be approximate, and may not correspond to absolute dimensions in any given unit
/// </summary>
public sealed record AspectRatio : IValidatableObject, IRecordValidatable
{
    private readonly int _width;
    private readonly int _height;

    internal AspectRatio(int width, int height, bool validate)
    {
        if (validate)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        }

        _width = width;
        _height = height;
    }

    /// <summary>
    /// Creates a new instance of <see cref="AspectRatio"/>.
    /// </summary>
    /// <param name="width">The ratio width.</param>
    /// <param name="height">The ratio height.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> or <paramref name="height"/> is less than 1.</exception>
    [JsonConstructor]
    public AspectRatio(int width, int height) : this(width, height, validate: true)
    {
    }

    /// <summary>
    /// Gets the width of this instance 
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1.</exception>
    [JsonInclude]
    [JsonRequired]
    public int Width
    {
        get => _width;

        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _width = value;
        }
    }

    /// <summary>
    /// Gets the height of this instance 
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1.</exception>
    [JsonInclude]
    [JsonRequired]
    public int Height
    {
        get => _height;

        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _height = value;
        }
    }

    /// <summary>
    /// Validates the aspect ratio dimensions.
    /// </summary>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A collection of validation errors, or an empty collection when the dimensions are valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateDimensions();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateDimensions();

    private IEnumerable<ValidationResult> ValidateDimensions()
    {
        if (Width < 1)
        {
            yield return new ValidationResult("Width must be at least 1.", [nameof(Width)]);
        }

        if (Height < 1)
        {
            yield return new ValidationResult("Height must be at least 1.", [nameof(Height)]);
        }
    }
}