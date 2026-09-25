// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Embed;

/// <summary>
/// An indicator that the view for the specified AT URI is detached.
/// </summary>
public record ViewDetached : View
{
    /// <summary>
    /// Creates a new instance of <see cref="ViewDetached"/>
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the view that is detached.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null" />.</exception>
    [JsonConstructor]
    internal ViewDetached(AtUri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        Uri = uri;
    }

    /// <summary>
    /// The <see cref="AtUri"/> of the view that is detached.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public AtUri Uri { get; init; }

    /// <summary>
    /// Flag indicating the view is detached.
    /// </summary>
    [JsonIgnore]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance property so it is reachable from a record instance and from pattern matching.")]
    public bool Detached => true;
}