// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

internal sealed record ListMutualGroupsResponse
{
    [JsonConstructor]
    public ListMutualGroupsResponse(ICollection<ConversationView> conversations, string? cursor)
    {
        Conversations = conversations;
        Cursor = cursor;
    }

    [JsonRequired]
    [JsonPropertyName("convos")]
    public ICollection<ConversationView> Conversations { get; init; }

    public string? Cursor { get; init; }
}
