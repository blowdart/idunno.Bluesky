// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model
{
    internal sealed record SendMessageBatchRequest
    {
        public SendMessageBatchRequest(ICollection<BatchedMessage> batchedMessages)
        {
            ArgumentNullException.ThrowIfNull(batchedMessages);

            Items = batchedMessages;
        }

        [JsonInclude]
        [JsonRequired]
        public ICollection<BatchedMessage> Items { get; init; }
    }
}
