// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed.External;

/// <summary>
/// Represents the color theme of an external article publication.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(ArticlePublicationTheme), typeDiscriminator: "app.bsky.embed.external#viewArticlePublicationTheme")]
public record ArticlePublicationTheme : Bluesky.View
{
    /// <summary>
    /// Gets the background color, if available.
    /// </summary>
    public string? Background { get; init; }

    /// <summary>
    /// Gets the foreground color, if available.
    /// </summary>
    public string? Foreground { get; init; }

    /// <summary>
    /// Gets the accent color, if available.
    /// </summary>
    public string? Accent { get; init; }

    /// <summary>
    /// Gets the accent foreground color, if available.
    /// </summary>
    public string? AccentForeground { get; init; }
}
