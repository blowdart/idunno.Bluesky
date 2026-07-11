// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

namespace idunno.Bluesky.Chat.Group;

/// <summary>
/// A join request from the perspective of the requester, including enough group context to render the request in a list (e.g. group name, owner, member count).
/// </summary>
public sealed record JoinRequestConversationView : ConversationViewBase
{
    [JsonConstructor]
    internal JoinRequestConversationView(string conversationId, string name, ProfileViewBasic owner, int memberCount, int memberLimit, JoinLinkViewerState viewer)
        : base()
    {
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(viewer);

        ConversationId = conversationId;
        Name = name;
        Owner = owner;
        MemberCount = memberCount;
        MemberLimit = memberLimit;
        Viewer = viewer;
    }

    /// <summary>
    /// Gets the conversation ID of the group.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("convoId")]
    public string ConversationId { get; set; }

    /// <summary>
    /// Gets the name of the group.
    /// </summary>
    [JsonRequired]
    public string Name { get; set; }

    /// <summary>
    /// Gets the <see cref="ProfileViewBasic"/> of owner of the group.
    /// </summary>
    [JsonRequired]
    public ProfileViewBasic Owner { get; set; }

    /// <summary>
    /// Gets the number of members in the group.
    /// </summary>
    [JsonRequired]
    public int MemberCount { get; set; }

    /// <summary>
    /// Gets the maximum number of members allowed in the group.
    /// </summary>
    [JsonRequired]
    public int MemberLimit { get; set; }

    /// <summary>
    /// Gets the state of the viewer with respect to the join link for the group.
    /// </summary>
    [JsonRequired]
    public JoinLinkViewerState Viewer { get; set; }
}