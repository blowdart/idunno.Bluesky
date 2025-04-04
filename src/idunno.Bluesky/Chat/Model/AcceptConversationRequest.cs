// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model
{
    internal record AcceptConversationRequest
    {
        public AcceptConversationRequest(string conversationId)
        {
            ConversationId = conversationId;
        }

        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("convoId")]
        public string ConversationId { get; init; }
    }
}
