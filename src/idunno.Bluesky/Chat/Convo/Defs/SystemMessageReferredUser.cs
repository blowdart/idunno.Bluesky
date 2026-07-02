// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A pointer to the user a system message refers to.
/// </summary>
public sealed record SystemMessageReferredUser
{
    /// <summary>
    /// Creates a new instance of <see cref="SystemMessageReferredUser"/>.
    /// </summary>
    /// <param name="did">The <see cref="AtProto.Did"/> of the user.</param>
    [JsonConstructor]
    internal SystemMessageReferredUser(Did did)
    {
        ArgumentNullException.ThrowIfNull(did);
        Did = did;
    }

    /// <summary>
    /// Gets the <see cref="AtProto.Did"/> of the user.
    /// </summary>
    [JsonRequired]
    public Did Did { get; set; }
}