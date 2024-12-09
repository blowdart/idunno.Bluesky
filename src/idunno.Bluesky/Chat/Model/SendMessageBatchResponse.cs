// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model
{
    internal sealed record SendMessageBatchResponse
    {
        [JsonConstructor]
        public SendMessageBatchResponse(ICollection<MessageView> items)
        {
            Items = items;
        }

        [JsonInclude]
        [JsonRequired]
        public ICollection<MessageView> Items { get; set; }
    }
}
