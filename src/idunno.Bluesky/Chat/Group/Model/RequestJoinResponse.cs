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
    /// Gets the status of the join request. Known values are in <see cref="JoinStatus"/>.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public string Status { get; init; }

    /// <summary>
    /// Gets the group conversation joined. This is only present in the case of <see cref="Status"/> is "joined".
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("convo")]
    public ConversationView? Conversation { get; init; }
}