// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model
{
    internal record AddReactionResponse
    {
        public AddReactionResponse(MessageView message)
        {
            Message = message;
        }

        [JsonInclude]
        [JsonRequired]
        public MessageView Message { get; init; }
    }
}
