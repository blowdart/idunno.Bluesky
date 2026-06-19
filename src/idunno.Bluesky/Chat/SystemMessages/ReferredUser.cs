// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Chat.SystemMessages;

/// <summary>
/// A pointer to the user a system message refers to.
/// </summary>
public sealed record ReferredUser
{
    /// <summary>
    /// Creates a new instance of <see cref="ReferredUser"/>.
    /// </summary>
    /// <param name="did">The <see cref="AtProto.Did"/> of the user.</param>
    [JsonConstructor]
    internal ReferredUser(Did did)
    {
        ArgumentNullException.ThrowIfNull(did);
        Did = did;
    }

    /// <summary>
    /// Gets the <see cref="AtProto.Did"/> of the user.
    /// </summary>
    [JsonRequired]
    public Did Did { get; init; }
}