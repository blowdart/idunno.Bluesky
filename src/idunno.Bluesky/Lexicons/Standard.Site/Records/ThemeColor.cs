// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Standard.Site;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Color information for themes.
/// </summary>
/// <remarks>
///<para>Only <see cref="ThemeColorRgb"/> is supported at this moment. This class is used for future expansion.</para>
/// </remarks>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ThemeColorRgb), typeDiscriminator: "site.standard.theme.color#rgb")]
[JsonDerivedType(typeof(ThemeColorRgba), typeDiscriminator: "site.standard.theme.color#rgba")]
public abstract record ThemeColor
{
    /// <summary>
    /// Creates a new instance of the <see cref="ThemeColor"/> record.
    /// </summary>
    protected ThemeColor()
    {
    }
}
