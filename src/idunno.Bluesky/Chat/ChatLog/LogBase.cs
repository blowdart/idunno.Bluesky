// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// Base record for log entries.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(AcceptConversation), typeDiscriminator: "chat.bsky.convo.defs#logAcceptConvo")]
[JsonDerivedType(typeof(AddMember), typeDiscriminator: "chat.bsky.convo.defs#logAddMember")]
[JsonDerivedType(typeof(AddReaction), typeDiscriminator: "chat.bsky.convo.defs#logAddReaction")]
[JsonDerivedType(typeof(ApproveJoinRequest), typeDiscriminator: "chat.bsky.convo.defs#logApproveJoinRequest")]
[JsonDerivedType(typeof(BeginConversation), typeDiscriminator: "chat.bsky.convo.defs#logBeginConvo")]
[JsonDerivedType(typeof(CreateJoinLink), typeDiscriminator: "chat.bsky.convo.defs#logCreateJoinLink")]
[JsonDerivedType(typeof(CreateMessage), typeDiscriminator: "chat.bsky.convo.defs#logCreateMessage")]
[JsonDerivedType(typeof(DeleteMessage), typeDiscriminator: "chat.bsky.convo.defs#logDeleteMessage")]
[JsonDerivedType(typeof(DisableJoinLink), typeDiscriminator: "chat.bsky.convo.defs#logDisableJoinLink")]
[JsonDerivedType(typeof(EditGroup), typeDiscriminator: "chat.bsky.convo.defs#logEditGroup")]
[JsonDerivedType(typeof(EditJoinLink), typeDiscriminator: "chat.bsky.convo.defs#logEditJoinLink")]
[JsonDerivedType(typeof(EnableJoinLink), typeDiscriminator: "chat.bsky.convo.defs#logEnableJoinLink")]
[JsonDerivedType(typeof(IncomingJoinRequest), typeDiscriminator: "chat.bsky.convo.defs#logIncomingJoinRequest")]
[JsonDerivedType(typeof(LeaveConversation), typeDiscriminator: "chat.bsky.convo.defs#logLeaveConvo")]
[JsonDerivedType(typeof(LockConversation), typeDiscriminator: "chat.bsky.convo.defs#logLockConvo")]
[JsonDerivedType(typeof(LockConversationPermanently), typeDiscriminator: "chat.bsky.convo.defs#logLockConvoPermanently")]
[JsonDerivedType(typeof(MemberJoin), typeDiscriminator: "chat.bsky.convo.defs#logMemberJoin")]
[JsonDerivedType(typeof(MemberLeave), typeDiscriminator: "chat.bsky.convo.defs#logMemberLeave")]
[JsonDerivedType(typeof(MuteConversation), typeDiscriminator: "chat.bsky.convo.defs#logMuteConvo")]
[JsonDerivedType(typeof(OutgoingJoinRequest), typeDiscriminator: "chat.bsky.convo.defs#logOutgoingJoinRequest")]
[JsonDerivedType(typeof(ReadConversation), typeDiscriminator: "chat.bsky.convo.defs#logReadConvo")]
[JsonDerivedType(typeof(ReadJoinRequests), typeDiscriminator: "chat.bsky.convo.defs#logReadJoinRequests")]
#pragma warning disable CS0618 // Type or member is obsolete - kept for compatibility with older log entries.
[JsonDerivedType(typeof(ReadMessage), typeDiscriminator: "chat.bsky.convo.defs#logReadMessage")]
#pragma warning restore CS0618 // Type or member is obsolete
[JsonDerivedType(typeof(RemoveReaction), typeDiscriminator: "chat.bsky.convo.defs#logRemoveReaction")]
[JsonDerivedType(typeof(RemoveMember), typeDiscriminator: "chat.bsky.convo.defs#logRemoveMember")]
[JsonDerivedType(typeof(RejectJoinRequest), typeDiscriminator: "chat.bsky.convo.defs#logRejectJoinRequest")]
[JsonDerivedType(typeof(UnlockConversation), typeDiscriminator: "chat.bsky.convo.defs#logUnlockConvo")]
[JsonDerivedType(typeof(UnmuteConversation), typeDiscriminator: "chat.bsky.convo.defs#logUnmuteConvo")]
[JsonDerivedType(typeof(WithdrawIncomingJoinRequest), typeDiscriminator: "chat.bsky.convo.defs#logWithdrawIncomingJoinRequest")]
[JsonDerivedType(typeof(WithdrawOutgoingJoinRequest), typeDiscriminator: "chat.bsky.convo.defs#logWithdrawOutgoingJoinRequest")]
public record LogBase : AtProtoObject
{
    /// <summary>
    /// Creates a new instance of <see cref="LogBase"/>.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="revision">The conversation revision.</param>
    protected LogBase(string conversationId, string revision)
    {
        ConversationId = conversationId;
        Revision = revision;
    }

    /// <summary>
    /// Gets the conversation identifier.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("convoId")]
    public string ConversationId { get; set; }

    /// <summary>
    /// Gets the conversation revision.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("rev")]
    public string Revision { get; set; }
}