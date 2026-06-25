// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

/// <summary>
/// The response to an approve join request.
/// </summary>
public sealed record ApproveJoinResponse
{
    [JsonConstructor]
    internal ApproveJoinResponse(ConversationView conversation)
    {
        Conversation = conversation;
    }

    /// <summary>
    /// Gets a view over the conversation that the join request was approved for.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("convo")]
    public ConversationView Conversation { get; internal init; }
}
