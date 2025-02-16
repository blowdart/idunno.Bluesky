// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Chat
{
    /// <summary>
    /// Encapsulates a reference to an individual message in a conversation.
    /// </summary>
    public sealed record MessageReference
    {
        /// <summary>
        /// Creates a new instance of <see cref="MessageReference"/>.
        /// </summary>
        /// <param name="did">The <see cref="AtProto.Did"/> of the message author.</param>
        /// <param name="conversationId">The conversation identifier.</param>
        /// <param name="messageId">The message identifier.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when  <paramref name="conversationId"/> or <paramref name="messageId"/> are null or whitespace.</exception>
        [JsonConstructor]
        public MessageReference(Did did, string conversationId, string messageId)
        {
            ArgumentNullException.ThrowIfNull(did);

            ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
            ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

            Did = did;
            ConversationId = conversationId;
            MessageId = messageId;
        }

        /// <summary>
        /// Gets the <see cref="AtProto.Did"/> of the message author.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Did Did { get; init; }

        /// <summary>
        /// Gets the conversation identifier.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string ConversationId { get; init; }

        /// <summary>
        /// Gets the message identifier.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string MessageId { get; init; }
    }
}
