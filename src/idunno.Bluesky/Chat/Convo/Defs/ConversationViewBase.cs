// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>Base class for conversation views</summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ConversationView), "chat.bsky.convo.defs#convoView")]
[JsonDerivedType(typeof(Group.JoinRequestConversationView), "chat.bsky.group.defs#joinRequestConvoView")]
public record ConversationViewBase : View
{
}