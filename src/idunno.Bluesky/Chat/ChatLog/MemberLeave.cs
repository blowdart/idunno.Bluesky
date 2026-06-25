// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// Event indicating a member left a group conversation.
/// The member who left gets a <see cref="LeaveConversation"/> (to leave the conversation) but not a <see cref="MemberLeave"/> (because they already left, so can't see the system message).
/// </summary>
public sealed record MemberLeave : MessageRelatedProfilesLogBase
{
    [JsonConstructor]
    internal MemberLeave(string conversationId, string revision, MessageViewBase message, ProfileViewBasic relatedProfiles)
        : base(conversationId, revision, message, relatedProfiles)
    {
    }
}
