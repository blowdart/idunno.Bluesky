// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

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
    /// Gets the number of unread, unlocked accepted conversations.
    /// Counts conversations with unread messages and unread join requests.
    /// </summary>
    /// <remarks>
    /// <para>Capped at 100.</para>
    /// </remarks>
    [JsonRequired]
    [JsonPropertyName("unreadAcceptedConvos")]
    public int UnreadAcceptedConversations { get; init; }

    /// <summary>
    ///Gets the number of unread, unlocked requested conversations.
    /// Includes conversations with unread messages, but not conversations with unread join requests,
    /// since only the owner of a group has join requests to read, and the group would necessarily be accepted.
    /// </summary>
    /// <remarks>
    /// <para>Capped at 100.</para>
    /// </remarks>
    [JsonRequired]
    [JsonPropertyName("unreadRequestConvos")]
    public int UnreadRequestedConversations { get; init; }
}