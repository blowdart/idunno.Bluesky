// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed.External.Source;

/// <summary>
/// Enumerates the RGB color components of a theme color.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(ColorRgb), typeDiscriminator: "app.bsky.embed.external#colorRGB")]
public record ColorRgb
{
    /// <summary>
    /// Create a new <see cref="ColorRgb"/> instance.
    /// </summary>
    /// <param name="red">The red value for the color.</param>
    /// <param name="green">The green value for the color.</param>
    /// <param name="blue">The blue value for the color.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a color component is out of the valid range (0-255).</exception>
    public ColorRgb(int red, int green, int blue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(red);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(red, 255);
        ArgumentOutOfRangeException.ThrowIfNegative(green);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(green, 255);
        ArgumentOutOfRangeException.ThrowIfNegative(blue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(blue, 255);

        Red = red;
        Green = green;
        Blue = blue;
    }

    /// <summary>
    /// The red value of the color.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of the valid range (0-255).</exception>
    [JsonPropertyName("r")]
    [JsonRequired]
    public int Red
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 255);

            field = value;
        }
    }

    /// <summary>
    /// The green value of the color.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of the valid range (0-255).</exception>
    [JsonPropertyName("g")]
    [JsonRequired]
    public int Green
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 255);

            field = value;
        }
    }


    /// <summary>
    /// The blue value of the color.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is out of the valid range (0-255).</exception>
    [JsonPropertyName("b")]
    [JsonRequired]
    public int Blue
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 255);

            field = value;
        }
    }
}
