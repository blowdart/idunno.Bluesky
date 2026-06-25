// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Chat.Group.Model;

internal sealed record RemoveMembersRequest
{
    public RemoveMembersRequest(string conversationId, ICollection<Did> members)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(members);
        if (members.Count == 0)
        {
            throw new ArgumentException("Members collection cannot be empty.", nameof(members));
        }

        ConversationId = conversationId;
        Members = members;
    }

    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("convoId")]
    public string ConversationId { get; init; }

    [JsonInclude]
    [JsonRequired]
    public ICollection<Did> Members { get; init; }
}
