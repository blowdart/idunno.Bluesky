// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Standard.Site;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// An RGBA color value.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ThemeColorRgba), typeDiscriminator: "site.standard.theme.color#rgba")]
public record ThemeColorRgba : ThemeColorRgb
{
    /// <summary>
    /// Create a new <see cref="ThemeColorRgb"/> instance.
    /// </summary>
    /// <param name="red">The red value for the color.</param>
    /// <param name="green">The green value for the color.</param>
    /// <param name="blue">The blue value for the color.</param>
    /// <param name="alpha">The alpha value for the color.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="red"/>, <paramref name="green"/>, <paramref name="blue"/>, or <paramref name="alpha"/>
    /// is outside their valid ranges.</exception>
    public ThemeColorRgba(int red, int green, int blue, int alpha) : base(red, green, blue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(red);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(red, 255);
        ArgumentOutOfRangeException.ThrowIfNegative(green);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(green, 255);
        ArgumentOutOfRangeException.ThrowIfNegative(blue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(blue, 255);

        ArgumentOutOfRangeException.ThrowIfNegative(alpha);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(alpha, 100);
        Alpha = alpha;
    }

    /// <summary>
    /// The alpha value of the color.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside the range 0-255.</exception>
    [JsonPropertyName("a")]
    [JsonRequired]
    public int Alpha
    {
        get;

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
            field = value;
        }
    }
}
