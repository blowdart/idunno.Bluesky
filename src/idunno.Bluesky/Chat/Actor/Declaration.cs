// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Record;

namespace idunno.Bluesky.Chat.Actor;

/// <summary>
/// A declaration of a Bluesky chat account.
/// </summary>
/// <param name="AllowIncoming">Indicates which actors are allowed to send chat messages to the account. Known values are defined in <see cref="Actor.AllowIncoming"/>.</param>
/// <param name="AllowGroupInvites">Indicates which actors are allowed to send group invites to the account. Known values are defined in <see cref="Actor.AllowGroupInvites"/>.</param>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(Declaration), "chat.bsky.actor.declaration")]
public record Declaration(string AllowIncoming, string AllowGroupInvites) : BlueskyRecord
{
}
