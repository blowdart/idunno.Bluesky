// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model
{
    internal sealed record ConversationResponse
    {
        [JsonConstructor]
        public ConversationResponse(ConversationView conversation)
        {
            Conversation = conversation;
        }

        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("convo")]
        public ConversationView Conversation { get; init; }
    }
}
