// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Standard.Site;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// An RGB color value.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ThemeColorRgb), typeDiscriminator: "site.standard.theme.color#rgb")]
public record ThemeColorRgb : ThemeColor
{
    /// <summary>
    /// Create a new <see cref="ThemeColorRgb"/> instance.
    /// </summary>
    /// <param name="red">The red value for the color.</param>
    /// <param name="green">The green value for the color.</param>
    /// <param name="blue">The blue value for the color.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any of the color values are outside the range 0-255.</exception>
    public ThemeColorRgb(int red, int green, int blue)
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside the range 0-255.</exception>
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside the range 0-255.</exception>
    [JsonPropertyName("g")]
    [JsonRequired]
    public int Green
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
    /// The blue value of the color.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside the range 0-255.</exception>
    [JsonPropertyName("b")]
    [JsonRequired]
    public int Blue
    {
        get;

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 255);

            field = value;
        }
    }

}