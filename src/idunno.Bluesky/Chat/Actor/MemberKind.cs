// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Actor;

/// <summary>Base class for different types of conversation members.</summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(DirectConversationMember), typeDiscriminator: "chat.bsky.actor.defs#directConvoMember")]
[JsonDerivedType(typeof(GroupConversationMember), typeDiscriminator: "chat.bsky.actor.defs#groupConvoMember")]
[JsonDerivedType(typeof(PastGroupConversationMember), typeDiscriminator: "chat.bsky.actor.defs#pastGroupConvoMember")]
public abstract record MemberKind
{
}
