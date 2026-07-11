// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

internal sealed record DisableJoinLinkRequest
{
    public DisableJoinLinkRequest(string conversationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ConversationId = conversationId;
    }

    [JsonRequired]
    [JsonInclude]
    [JsonPropertyName("convoId")]
    public string ConversationId { get; init; }
}