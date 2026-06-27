// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

namespace idunno.Bluesky.Chat.Group.Model;

/// <summary>
/// Encapsulates the response from adding a member to a group conversation.
/// </summary>

// See https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/addMembers.json
public sealed record AddMembersResponse
{
    [JsonConstructor]
    internal AddMembersResponse(ConversationView conversation, ICollection<ProfileViewBasic>? addedMembers)
    {
        Conversation = conversation;

        if (addedMembers is not null)
        {
            AddedMembers = addedMembers;
        }
    }

    /// <summary>
    /// Gets the <see cref="ConversationView"/> of the group conversation after adding members.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("convo")]
    public ConversationView Conversation { get; internal init; }

    /// <summary>
    /// Gets the collection of <see cref="ProfileViewBasic"/> of members that were added to the group conversation.
    /// </summary>
    [NotNull]
    public ICollection<ProfileViewBasic>? AddedMembers { get; internal init; } = [];
}