// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

/// <summary>
/// The response from a call to edit a group.
/// </summary>
public sealed record EditGroupResponse
{
    [JsonConstructor]
    internal EditGroupResponse(ConversationView conversation)
    {
        Conversation = conversation;
    }

    /// <summary>
    /// Gets a view of the edited conversation.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("convo")]
    public ConversationView Conversation { get; internal init; } = default!;
}
