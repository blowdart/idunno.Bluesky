// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

namespace idunno.Bluesky.Chat.Group;

/// <summary>
/// A join request from the perspective of the group owner.
/// </summary>
public sealed record JoinRequestView : View
{
    /// <summary>
    /// Creates a new instance of <see cref="JoinRequestView"/>.
    /// </summary>
    /// <param name="conversationId">The conversation ID of the group the request is for.</param>
    /// <param name="requestedBy">The profile of the user who requested to join the group.</param>
    /// <param name="requestedAt">The date and time when the join request was made.</param>
    public JoinRequestView(string conversationId, ProfileViewBasic requestedBy, DateTimeOffset requestedAt)
    {
        ConversationId = conversationId;
        RequestedBy = requestedBy;
        RequestedAt = requestedAt;
    }

    /// <summary>
    /// Gets the conversation ID of the group the request is for.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("convoId")]
    public string ConversationId { get; set; }

    /// <summary>
    /// Gets the profile of the user who requested to join the group.
    /// </summary>
    [JsonRequired]
    public ProfileViewBasic RequestedBy { get; set; }

    /// <summary>
    /// Gets the date and time when the join request was made.
    /// </summary>
    [JsonRequired]
    public DateTimeOffset RequestedAt { get; set; }
}
