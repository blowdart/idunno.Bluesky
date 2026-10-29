// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat;

/// <summary>
/// Encapsulates the availability of a conversation.
/// </summary>
/// <param name="canChat">A flag indicating whether the user can chat with the other user.</param>
/// <param name="conversation">The conversation between the user and the other user, if it exists.</param>
public sealed class ConversationAvailability(bool canChat, ConversationView? conversation)
{

    /// <summary>
    /// Gets a flag indicating whether the user can chat with the other user.
    /// </summary>
    [JsonRequired]
    public bool CanChat { get; init; } = canChat;

    /// <summary>
    /// Gets the conversation between the user and the other user, if it exists.
    /// </summary>
    [JsonPropertyName("convo")]
    public ConversationView? Conversation { get; init; } = conversation;
}
