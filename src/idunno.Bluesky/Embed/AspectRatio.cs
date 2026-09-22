// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed;

// https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/embed/defs.json

/// <summary>
/// Represents an aspect ratio. It may be approximate, and may not correspond to absolute dimensions in any given unit
/// </summary>
public sealed record AspectRatio
{
    /// <summary>
    /// Creates a new instance of <see cref="AspectRatio"/>.
    /// </summary>
    /// <param name="width">The ratio width.</param>
    /// <param name="height">The ratio height.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> or <paramref name="height"/> is less than 1.</exception>
    [JsonConstructor]
    public AspectRatio(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        Width = width;
        Height = height;
    }

    /// <summary>
    /// Gets the width of this instance 
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1.</exception>
    [JsonInclude]
    [JsonRequired]
    public int Width
    {
        get;

        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
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
        get;

        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
        }
    }
}