// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Base record for system messages.
/// </summary>
[SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty", Justification = "Base class for system message json polymorphism")]
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(AddMemberSystemMessage), typeDiscriminator: "chat.bsky.convo#systemMessageDataAddMember")]
[JsonDerivedType(typeof(CreateJoinLinkSystemMessage), typeDiscriminator: "chat.bsky.convo#systemMessageDataCreateJoinLink")]
[JsonDerivedType(typeof(DisableJoinLinkSystemMessage), typeDiscriminator: "chat.bsky.convo#systemMessageDataDisableJoinLink")]
[JsonDerivedType(typeof(EditGroupSystemMessage), typeDiscriminator: "chat.bsky.convo#systemMessageDataEditGroup")]
[JsonDerivedType(typeof(EditJoinLinkSystemMessage), typeDiscriminator: "chat.bsky.convo#systemMessageDataEditJoinLink")]
[JsonDerivedType(typeof(EnableJoinLinkSystemMessage), typeDiscriminator: "chat.bsky.convo#systemMessageDataEnableJoinLink")]
[JsonDerivedType(typeof(LockConversationSystemMessage), typeDiscriminator: "chat.bsky.convo#systemMessageDataLockConvo")]
[JsonDerivedType(typeof(LockConversationPermanentlySystemMessage), typeDiscriminator: "chat.bsky.convo#systemMessageDataLockConvoPermanently")]
[JsonDerivedType(typeof(MemberJoinSystemMessage), typeDiscriminator: "chat.bsky.convo#systemMessageDataMemberJoin")]
[JsonDerivedType(typeof(MemberLeaveSystemMessage), typeDiscriminator: "chat.bsky.convo#systemMessageDataMemberLeave")]
[JsonDerivedType(typeof(RemoveMemberSystemMessage), typeDiscriminator: "chat.bsky.convo#systemMessageDataRemoveMember")]
[JsonDerivedType(typeof(UnlockConversationSystemMessage), typeDiscriminator: "chat.bsky.convo#systemMessageDataUnlockConvo")]
public record SystemMessage
{
    /// <summary>
    /// A list of keys and element data that do not map to any strongly typed properties.
    /// </summary>
    [NotNull]
    [ExcludeFromCodeCoverage]
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
}