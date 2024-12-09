// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model
{
    internal sealed record SendMessageRequest
    {
        public SendMessageRequest(string convoId, MessageInput message)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(convoId);
            ArgumentNullException.ThrowIfNull(message);

            ConvoId = convoId;
            Message = message;
        }

        [JsonInclude]
        [JsonRequired]
        public string ConvoId { get; init; }

        [JsonInclude]
        [JsonRequired]
        public MessageInput Message { get; init; }
    }
}
