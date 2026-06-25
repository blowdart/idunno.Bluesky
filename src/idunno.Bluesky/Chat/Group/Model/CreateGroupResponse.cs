// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

/// <summary>
/// Response from a call to create a group conversation..
/// </summary>
public sealed record CreateGroupResponse
{
    [JsonConstructor]
    internal CreateGroupResponse(ConversationView conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        Conversation = conversation;
    }

    /// <summary>
    /// Gets the conversation view for the newly created group conversation.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("convo")]
    public ConversationView Conversation { get; internal init; }
}
