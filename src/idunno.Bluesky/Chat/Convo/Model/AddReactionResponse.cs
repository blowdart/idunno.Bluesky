// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Convo.Model;

internal record AddReactionResponse
{
    [JsonConstructor]
    public AddReactionResponse(MessageView message)
    {
        Message = message;
    }

    [JsonInclude]
    [JsonRequired]
    public MessageView Message { get; set; }
}