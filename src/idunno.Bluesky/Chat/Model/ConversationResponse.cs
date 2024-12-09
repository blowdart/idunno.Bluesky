// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in conversation APIs which return conversation views.")]
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
