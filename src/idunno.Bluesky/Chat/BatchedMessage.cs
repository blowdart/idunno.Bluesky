// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat
{
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
        /// <exception cref="ArgumentException">Thrown when <paramref name="conversationId"/> is <see langword="null"/> or white space.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
        public BatchedMessage(string conversationId, MessageInput message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

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
}
