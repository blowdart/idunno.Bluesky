// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat;

/// <summary>
/// Conatins the unread conversation counts for a user.
/// </summary>
public sealed record UnreadConversationCounts
{
    /// <summary>
    /// Creates a new instance of the <see cref="UnreadConversationCounts"/> record.
    /// </summary>
    /// <param name="unreadAcceptedConversations">The number of unread, unlocked accepted conversations.</param>
    /// <param name="unreadRequestedConversations">The number of unread, unlocked requested conversations.</param>
    public UnreadConversationCounts(int unreadAcceptedConversations, int unreadRequestedConversations)
    {
        UnreadAcceptedConversations = unreadAcceptedConversations;
        UnreadRequestedConversations = unreadRequestedConversations;
    }

    /// <summary>
    /// Gets the number of unread, unlocked accepted conversations. Counts conversations with unread messages and unread join requests.
    /// </summary>
    /// <remarks>
    /// <para>Capped at 31.</para>
    /// </remarks>
    [JsonRequired]
    [JsonPropertyName("unreadAcceptedConvos")]
    public int UnreadAcceptedConversations { get; init; }

    /// <summary>
    /// Gets the number of unread, unlocked request conversations.
    /// Includes conversations with unread messages, but not with unread join request,
    /// since only the owner of a group has join requests to read, and the group would necessarily be accepted.
    /// </summary>
    /// <remarks>
    /// <para>Capped at 11.</para>
    /// </remarks>
    [JsonRequired]
    [JsonPropertyName("unreadRequestConvos")]
    public int UnreadRequestedConversations { get; init; }
}