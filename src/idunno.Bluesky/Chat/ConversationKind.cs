// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat;

/// <summary>
/// Represents the kind of a conversation.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(DirectConversation), typeDiscriminator: "chat.bsky.convo.defs#directConvo")]
[JsonDerivedType(typeof(GroupConversation), typeDiscriminator: "chat.bsky.convo.defs#groupConvo")]
public abstract record ConversationKind
{
}