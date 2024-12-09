// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in ListConversations.")]
    internal sealed record ListConvosResponse
    {
        [JsonConstructor]
        public ListConvosResponse(ICollection<ConversationView> conversations, string? cursor)
        {
            Conversations = conversations;
            Cursor = cursor;
        }

        [JsonInclude]
        [JsonRequired]
        [JsonPropertyName("convos")]
        public ICollection<ConversationView> Conversations { get; init; }

        [JsonInclude]
        public string? Cursor { get; init; }
    }
}
