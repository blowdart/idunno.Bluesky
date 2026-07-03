// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Convo.Model;

internal sealed record DeleteMessageForSelfRequest
{
    public DeleteMessageForSelfRequest(string convoId, string messageId)
    {
        ConvoId = convoId;
        MessageId = messageId;
    }

    [JsonInclude]
    [JsonRequired]
    public string ConvoId { get; init; }

    [JsonInclude]
    [JsonRequired]
    public string MessageId { get; init; }
}