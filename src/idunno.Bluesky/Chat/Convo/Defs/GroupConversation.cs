// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Group;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Holds information about a group conversation
/// </summary>
public sealed record GroupConversation : ConversationKind
{
    [JsonConstructor]
    internal GroupConversation(
        DateTimeOffset createdAt,
        JoinLinkView? joinLink,
        int? joinRequestCount,
        string lockStatus,
        bool lockStatusModerationOverride,
        int memberCount,
        int memberLimit,
        string name,
        int? unreadJoinRequestCount)
    {
        CreatedAt = createdAt;
        JoinLink = joinLink;
        JoinRequestCount = joinRequestCount;
        LockStatus = lockStatus;
        LockStatusModerationOverride = lockStatusModerationOverride;
        MemberCount = memberCount;
        MemberLimit = memberLimit;
        Name = name;
        UnreadJoinRequestCount = unreadJoinRequestCount;
    }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> the chat was created on.
    /// </summary>
    [JsonRequired]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the <see cref="JoinLinkView"/> for the conversation, if any.
    /// </summary>
    public JoinLinkView? JoinLink { get; init; }

    /// <summary>
    /// Gets the total number of pending join requests for the group conversation. Only present for the owner. Capped at 21.
    /// </summary>
    [NotNull]
    public int? JoinRequestCount { get; init; } = 0;

    /// <summary>
    /// Gets the lock status of the conversation. Known values are contained in <see cref="ConversationLockStatus"/>.
    /// </summary>
    [JsonRequired]
    public string LockStatus { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the lock status is being forced by a moderation override (account inactivation or convo takedown) rather than the owner's own setting.
    /// </summary>
    [JsonRequired]
    public bool LockStatusModerationOverride { get; init; }

    /// <summary>
    /// Gets the total number of members in the group conversation.
    /// </summary>
    [JsonRequired]
    public int MemberCount { get; init; }

    /// <summary>
    /// Gets the maximum number of members allowed in the group conversation.
    /// </summary>
    [JsonRequired]
    public int MemberLimit { get; init; }

    /// <summary>
    /// Gets the display name of the group conversation.
    /// </summary>
    [JsonRequired]
    public string Name { get; init; }

    /// <summary>
    /// Gets the number of unread join requests for the group conversation. Only present for the owner.
    /// </summary>
    public int? UnreadJoinRequestCount { get; init; }
}