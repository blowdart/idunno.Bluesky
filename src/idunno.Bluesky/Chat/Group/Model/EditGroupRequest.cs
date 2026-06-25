// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

internal sealed record EditGroupRequest
{
    public EditGroupRequest(string conversationId, string name)
    {
        ConversationId = conversationId ?? throw new ArgumentNullException(nameof(conversationId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("convoId")]
    public string ConversationId { get; set; }

    [JsonInclude]
    [JsonRequired]
    public string Name { get; set; }
}
