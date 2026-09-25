// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Embed;

/// <summary>
/// An indicator that the view for the specified AT URI was not found.
/// </summary>
public sealed record ViewNotFound : View
{
    /// <summary>
    /// Creates a new instance of <see cref="ViewNotFound"/>
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the view that was not found.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null" />.</exception>
    [JsonConstructor]
    internal ViewNotFound(AtUri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        Uri = uri;
    }

    /// <summary>
    /// The <see cref="AtUri"/> of the view that was not found.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public AtUri Uri { get; init; }

    /// <summary>
    /// Flag indicating the view was not found.
    /// </summary>
    [JsonIgnore]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance property so it is reachable from a record instance and from pattern matching.")]
    public bool NotFound => true;
}