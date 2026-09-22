// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky.Embed;

/// <summary>
/// An indicator that the view for the specified AT URI is blocked.
/// </summary>
public sealed record ViewBlocked : View
{
    /// <summary>
    /// Creates a new instance of <see cref="ViewBlocked"/>
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the view that is blocked.</param>
    /// <param name="blockedAuthor">Information on the blocked author.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or <paramref name="blockedAuthor"/> is <see langword="null" />.</exception>
    [JsonConstructor]
    internal ViewBlocked(AtUri uri, BlockedAuthor blockedAuthor)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(blockedAuthor);

        Uri = uri;
        BlockedAuthor = blockedAuthor;
    }

    /// <summary>
    /// The <see cref="AtUri"/> of the view that is blocked.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public AtUri Uri { get; init; }

    /// <summary>
    /// Flag indicating the view is blocked.
    /// </summary>
    [JsonIgnore]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance property so it is reachable from a record instance and from pattern matching.")]
    public bool Blocked => true;

    /// <summary>
    /// Information on the <see cref="BlockedAuthor"/>
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("author")]
    public BlockedAuthor BlockedAuthor { get; init; }
}