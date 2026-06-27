// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

/// <summary>
/// Contains the response from a request to join a group.
/// </summary>
public sealed record RequestJoinResponse
{
    [JsonConstructor]
    internal RequestJoinResponse(string status, ConversationView? conversation)
    {
        Status = status;
        Conversation = conversation;
    }

    /// <summary>
    /// Gets the conversation view after the join request has been made. Known values are in <see cref="JoinStatus"/>.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public string Status { get; internal init; }

    /// <summary>
    /// Gets the group conversation joined. This is only present in the case of status=joined
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("convo")]
    public ConversationView? Conversation { get; internal init; }
}