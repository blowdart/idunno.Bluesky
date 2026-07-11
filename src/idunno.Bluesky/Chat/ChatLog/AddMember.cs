// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// Event indicating a member was added to a group conversation.
/// The member who was added gets a <see cref="BeginConversation"/> (to create the conversation) but also a <see cref="AddMember"/> (to show the system message as the first message the user sees).
/// </summary>
public sealed record AddMember : MessageRelatedProfilesLogBase
{
    [JsonConstructor]
    internal AddMember(string conversationId, string revision, MessageViewBase message, IReadOnlyCollection<ProfileViewBasic> relatedProfiles)
        : base(conversationId, revision, message, relatedProfiles)
    {
    }
}