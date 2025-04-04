// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model
{
    internal record AddReactionRequest
    {
        public AddReactionRequest(string conversationId, string messageId, string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
            ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            ConversationId = conversationId;
            MessageId = messageId;
            Value = value;
        }

        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("convoId")]
        public string ConversationId { get; init; }

        [JsonInclude]
        [JsonRequired]
        public string MessageId { get; init; }

        [JsonInclude]
        [JsonRequired]
        public string Value { get; init; }
    }
}
