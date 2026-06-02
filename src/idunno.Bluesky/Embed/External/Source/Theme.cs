// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed.External.Source;

/// <summary>
/// The colour theme of the external source record.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(Theme), typeDiscriminator: "app.bsky.embed.external#viewExternalSourceTheme")]
public record Theme
{
    /// <summary>
    /// Gets the background <see cref="ColorRgb"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ColorRgb? BackgroundRGB { get; init; }

    /// <summary>
    /// Gets the foreground <see cref="ColorRgb"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ColorRgb? ForegroundRGB { get; init; }

    /// <summary>
    /// Gets the accent <see cref="ColorRgb"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ColorRgb? AccentRGB { get; init; }

    /// <summary>
    /// Gets the accent foreground <see cref="ColorRgb"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ColorRgb? AccentForegroundRGB { get; init; }
}
