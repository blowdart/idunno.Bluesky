// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents the kind of a conversation.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(DirectConversation), typeDiscriminator: "chat.bsky.convo.defs#directConvo")]
[JsonDerivedType(typeof(GroupConversation), typeDiscriminator: "chat.bsky.convo.defs#groupConvo")]
public abstract record ConversationKind
{
}