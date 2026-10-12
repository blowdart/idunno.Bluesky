// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

internal sealed class WithdrawJoinRequestRequest
{
    public WithdrawJoinRequestRequest(string conversationId)
    {
        ArgumentNullException.ThrowIfNull(conversationId);

        ConversationId = conversationId;
    }

    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("convoId")]
    public string ConversationId { get; init; }
}