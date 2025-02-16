// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat
{
    /// <summary>
    /// Encapsulates a reference to a conversation and its revision.
    /// </summary>
    public record ConversationReference
    {
        [JsonConstructor]
        internal ConversationReference(string conversationId, string revision)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
            ArgumentException.ThrowIfNullOrWhiteSpace(revision);

            ConversationId = conversationId;
            Revision = revision;
        }

        /// <summary>
        /// Gets the conversation identifier.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("convoId")]
        public string ConversationId { get; init; }

        /// <summary>
        /// Gets the conversation revision.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("rev")]
        public string Revision { get; init; }
    }
}
