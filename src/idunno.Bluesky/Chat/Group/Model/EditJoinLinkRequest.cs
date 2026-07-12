// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

internal sealed record EditJoinLinkRequest
{
    internal EditJoinLinkRequest(string conversationId, bool? requireApproval, string? joinRule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        ConversationId = conversationId;
        RequireApproval = requireApproval;
        JoinRule = joinRule;
    }

    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("convoId")]
    public string ConversationId { get; set; }

    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RequireApproval { get; set; } = false;

    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? JoinRule { get; set; }
}