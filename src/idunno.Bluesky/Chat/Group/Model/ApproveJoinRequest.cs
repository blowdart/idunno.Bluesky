// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Chat.Group.Model;

internal record ApproveJoinRequest
{
    public ApproveJoinRequest(string conversationId, Did member)
    {
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(member);
        ConversationId = conversationId;
        Member = member;
    }

    /// <summary>
    /// Gets the ID of the conversation to add the specified member to.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("convoId")]
    public string ConversationId { get; init; }
    
    /// <summary>
    /// Gets the <see cref="Did"/> of the member to add to the conversation.
    /// </summary>
    [JsonRequired]
    public Did Member { get; init; }
}
