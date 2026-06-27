// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

namespace idunno.Bluesky.Chat.Group;

/// <summary>
/// A group conversation preview that can be shown in feeds, including to unauthenticated viewers.
/// </summary>
public sealed record JoinLinkPreviewView : View
{
    internal JoinLinkPreviewView(
        string conversationId,
        string code,
        string name,
        ProfileViewBasic owner,
        int memberCount,
        int memberLimit,
        bool requireApproval,
        string joinRule,
        ConversationView? conversation,
        JoinLinkViewerState? viewer)
    {
        ConversationId = conversationId;
        Code = code;
        Name = name;
        Owner = owner;
        MemberCount = memberCount;
        MemberLimit = memberLimit;
        RequireApproval = requireApproval;
        JoinRule = joinRule;
        Conversation = conversation;
        Viewer = viewer;
    }

    /// <summary>
    /// Gets the conversation ID of the group conversation.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("convoId")]
    public string ConversationId { get; init; }

    /// <summary>
    /// Gets the join link code for the group conversation.
    /// </summary>
    [JsonRequired]
    public string Code { get; init; }

    /// <summary>
    /// Gets the name of the group conversation.
    /// </summary>
    [JsonRequired]
    public string Name { get; init; }

    /// <summary>
    /// Gets the owner of the group conversation.
    /// </summary>
    [JsonRequired]
    public ProfileViewBasic Owner { get; init; }

    /// <summary>
    /// Gets the current member count of the group conversation.
    /// </summary>
    [JsonRequired]
    public int MemberCount { get; init; }

    /// <summary>
    /// Gets the member limit of the group conversation.
    /// </summary>
    [JsonRequired]
    public int MemberLimit { get; init; }

    /// <summary>
    /// Gets a flag indicating whether approval is required to join the group conversation.
    /// </summary>
    [JsonRequired]
    public bool RequireApproval { get; init; }

    /// <summary>
    /// Gets the join rule for the group conversation. Know values are available in <see cref="Group.JoinRule"/>.
    /// </summary>
    [JsonRequired]
    public string JoinRule { get; init; }

    /// <summary>
    /// Gets the conversation view for the group conversation. Only present if the request is authenticated and the user is a member of the group.
    /// </summary>
    [JsonPropertyName("convo")]
    public ConversationView? Conversation { get; init; }

    /// <summary>
    /// Gets the viewer state for the group conversation. Only present if the request is authenticated.
    /// </summary>
    public JoinLinkViewerState? Viewer { get; init; }
}