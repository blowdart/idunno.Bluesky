// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group;

/// <summary>
/// Base class for join link preview views.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(JoinLinkPreviewView), typeDiscriminator: "chat.bsky.group.defs#joinLinkPreviewView")]
[JsonDerivedType(typeof(DisabledJoinLinkPreviewView), typeDiscriminator: "chat.bsky.group.defs#disabledJoinLinkPreviewView")]
[JsonDerivedType(typeof(InvalidJoinLinkPreviewView), typeDiscriminator: "chat.bsky.group.defs#invalidJoinLinkPreviewView")]
public record JoinLinkPreviewViewBase : View
{
}
