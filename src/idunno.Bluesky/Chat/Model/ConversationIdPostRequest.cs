// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model
{
    internal sealed record ConversationIdPostRequest
    {
        public ConversationIdPostRequest(string convoId)
        {
            ConvoId = convoId;
        }

        [JsonInclude]
        [JsonRequired]
        public string ConvoId { get; init; }
    }
}
