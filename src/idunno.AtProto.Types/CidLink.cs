// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto;

/// <summary>
/// Represents a link to content identified by a <see cref="Cid">content identifier</see>.
/// </summary>
public record CidLink
{
    /// <summary>
    /// Creates a new instance of <see cref="CidLink"/>.
    /// </summary>
    /// <param name="link">The <see cref="Cid">content identifier</see> of the content.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="link"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public CidLink(Cid link)
    {
        ArgumentNullException.ThrowIfNull(link);
        Link = link;
    }

    /// <summary>
    /// Gets the <see cref="Cid">content identifier</see> of the content.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("$link")]
    [JsonRequired]
    public Cid Link { get; init; }
}
