// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Chat.Group.Model;

internal sealed record RejectJoinRequestRequest
{
    public RejectJoinRequestRequest(string conversationId, Did member)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);
        ArgumentNullException.ThrowIfNull(member);

        ConversationId = conversationId;
        Member = member;
    }

    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("convoId")]
    public string ConversationId { get; init; }

    [JsonInclude]
    [JsonRequired]
    public Did Member { get; init; }
}
