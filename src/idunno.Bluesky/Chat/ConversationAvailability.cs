// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat;

/// <summary>
/// Encapsulates the availability of a conversation.
/// </summary>
public sealed class ConversationAvailability
{
    /// <summary>
    /// Creates a new instance of <see cref="ConversationAvailability"/>.
    /// </summary>
    /// <param name="canChat">A flag indicating whether the user can chat with the other user.</param>
    /// <param name="conversation">The conversation between the user and the other user, if it exists.</param>
    public ConversationAvailability(bool? canChat, ConversationView? conversation)
    {
        CanChat = canChat;
        Conversation = conversation;
    }

    /// <summary>
    /// Gets a flag indicating whether the user can chat with the other user.
    /// </summary>
    public bool? CanChat { get; init; }

    /// <summary>
    /// Gets the conversation between the user and the other user, if it exists.
    /// </summary>
    [JsonPropertyName("convo")]
    public ConversationView? Conversation { get; init; }
}