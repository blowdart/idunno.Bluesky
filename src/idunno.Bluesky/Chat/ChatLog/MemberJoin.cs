// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// Event indicating a member joined a group conversation via a join link.
/// The member who was added gets a <see cref="BeginConversation"/> (to create the conversation) but also a <see cref="MemberJoin"/>
/// (to show the system message as the first message the user sees).
/// </summary>
public sealed record MemberJoin : MessageRelatedProfilesLogBase
{
    [JsonConstructor]
    internal MemberJoin(string conversationId, string revision, MessageViewBase message, ICollection<ProfileViewBasic> relatedProfiles)
        : base(conversationId, revision, message, relatedProfiles)
    {
    }
}
