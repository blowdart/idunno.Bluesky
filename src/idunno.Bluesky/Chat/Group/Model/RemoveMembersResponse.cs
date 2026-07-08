// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Group.Model;

/// <summary>
/// Represents the response from the removeMembers API.
/// </summary>
public sealed record RemoveMembersResponse
{
    [JsonConstructor]
    internal RemoveMembersResponse(ConversationView conversation)
    {
        Conversation = conversation;
    }

    /// <summary>
    /// Gets the conversation view after members have been removed.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("convo")]
    public ConversationView Conversation { get; init; }
}