// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Chat.Group.Model;

internal class AddMembersRequest
{
    [JsonConstructor]
    public AddMembersRequest(string conversationId, IEnumerable<Did> members)
    {
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentOutOfRangeException.ThrowIfZero(members.Count());
        ConversationId = conversationId;
        Members = members;
    }

    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("id")]
    public string ConversationId { get; init; }

    [JsonInclude]
    [JsonRequired]
    public IEnumerable<Did> Members { get; init; }
}