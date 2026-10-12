// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat;

/// <summary>
/// Encapsulates a batched <see cref="Message"/>.
/// </summary>
public record BatchedMessage
{
    /// <summary>
    /// Creates a new instance of <see cref="BatchedMessage"/>
    /// </summary>
    /// <param name="conversationId">The conversation identifier to post the <paramref name="message"/> in.</param>
    /// <param name="message">The <see cref="Message"/> to post.</param>
    /// <exception cref="ArgumentNullException"><paramref name="conversationId"/> or <paramref name="message"/> is <see langword="null"/>.</exception>
    public BatchedMessage(string conversationId, MessageInput message)
    {
        ArgumentNullException.ThrowIfNull(conversationId);

        ArgumentNullException.ThrowIfNull(message);

        ConversationId = conversationId;
        Message = message;
    }

    /// <summary>
    /// Gets the conversation identifier to post the <see cref="Message"/> in.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("convoId")]
    public string ConversationId { get; init; }

    /// <summary>
    /// Gets the <see cref="Message"/> to post.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public MessageInput Message { get; init; }
}