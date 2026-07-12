// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Standard.Site;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Simplified standard.site publication theme for tools and apps to utilize when displaying content.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(BasicTheme), "site.standard.theme.basic")]
public record BasicTheme
{
    /// <summary>
    /// Constructs a new instance of <see cref="BasicTheme"/>.
    /// </summary>
    /// <param name="background">Color used for content background.</param>
    /// <param name="foreground">Color used for content foreground.</param>
    /// <param name="accent">Color used for links and button background.</param>
    /// <param name="accentForeground">Color used for button text.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when any of the parameters are <see langword="null"/>.</exception>
    [JsonConstructor]
    public BasicTheme(ThemeColor background, ThemeColor foreground, ThemeColor accent, ThemeColor accentForeground)
    {
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(foreground);
        ArgumentNullException.ThrowIfNull(accent);
        ArgumentNullException.ThrowIfNull(accentForeground);

        Background = background;
        Foreground = foreground;
        Accent = accent;
        AccentForeground = accentForeground;
    }

    /// <summary>
    /// Color used for content background.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    [JsonRequired]
    public ThemeColor Background
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// Color used for content foreground.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    [JsonRequired]
    public ThemeColor Foreground
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// Color used for links and button background.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    [JsonRequired]
    public ThemeColor Accent
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// Color used for button text.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    [JsonRequired]
    public ThemeColor AccentForeground
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }
}