// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Unspecced;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// An unhydrated reference to an article recommended for Atmosphere Explore.
/// </summary>
public sealed record AtmosphereExploreArticle
{
    /// <summary>
    /// Creates a new instance of <see cref="AtmosphereExploreArticle"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the recommended article.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public AtmosphereExploreArticle(AtUri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        Uri = uri;
    }

    /// <summary>
    /// Gets the <see cref="AtUri"/> of the recommended article.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public AtUri Uri { get; init; }
}
