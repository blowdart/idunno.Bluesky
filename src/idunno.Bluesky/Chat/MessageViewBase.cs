// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat;

/// <summary>
/// Base class for message views. Used for json polymorphic deserialization.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(MessageView), typeDiscriminator: "chat.bsky.convo.defs#messageView")]
[JsonDerivedType(typeof(DeletedMessageView), typeDiscriminator: "chat.bsky.convo.defs#deletedMessageView")]
[JsonDerivedType(typeof(SystemMessageView), typeDiscriminator: "chat.bsky.convo.defs#systemMessageView")]
[JsonDerivedType(typeof(MessageBeforeUserJoinedGroupView), typeDiscriminator: "chat.bsky.convo.defs#messageBeforeUserJoinedGroupView")]
public record MessageViewBase : View
{
    /// <summary>
    /// Constructs a new instance of <see cref="MessageViewBase"/>.
    /// </summary>
    public MessageViewBase()
    {
    }
}