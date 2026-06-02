// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto;

/// <summary>
/// Represents a link to content identified by a <see cref="Cid">content identifier</see>.
/// </summary>
public sealed record CidLink : IEquatable<CidLink>
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

    /// <summary>
    /// Gets a hash code for the current object.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode() => Link.GetHashCode();

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns><see langword="true"/> if the current object is equal to the <paramref name="other"/>; otherwise, <see langword="false" />.</returns>
    public bool Equals(CidLink? other)
    {
        if (other is null)
        {
            return false;
        }

        // Optimization for a common success case.
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // If run-time types are not exactly the same, return false.
        if (GetType() != other.GetType())
        {
            return false;
        }

        // Return true if the fields match.
        return Equals(Link, other.Link);
    }
}
