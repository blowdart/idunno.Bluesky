// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

internal sealed record CreateJoinLinkRequest
{
    internal CreateJoinLinkRequest(string conversationId, bool requireApproval, string joinRule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(joinRule);

        ConversationId = conversationId;
        RequireApproval = requireApproval;
        JoinRule = joinRule;
    }

    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("convoId")]
    public string ConversationId { get; set; }

    [JsonInclude]
    public bool RequireApproval { get; set; } = false;

    [JsonInclude]
    [JsonRequired]
    public string JoinRule { get; set; }
}
