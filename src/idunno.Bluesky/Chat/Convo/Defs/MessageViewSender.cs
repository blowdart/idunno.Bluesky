// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates a view over the sender of a message.
/// </summary>
public sealed record MessageViewSender
{
    /// <summary>
    /// Creates a new instance of <see cref="MessageViewSender"/>.
    /// </summary>
    /// <param name="did">The <see cref="AtProto.Did"/> of the message author.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public MessageViewSender(Did did)
    {
        ArgumentNullException.ThrowIfNull(did);
        Did = did;
    }

    /// <summary>
    /// Gets the <see cref="AtProto.Did"/> of the message author.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public Did Did { get; init; }
}