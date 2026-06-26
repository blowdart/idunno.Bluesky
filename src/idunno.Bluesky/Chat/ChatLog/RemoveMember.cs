// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// Event indicating a member was removed from a group conversation.
/// The member who was removed gets a <see cref="LeaveConversation"/> (to leave the conversation) but not a <see cref="RemoveMember"/> (because they already left, so can't see the system message).
/// </summary>
public sealed record RemoveMember : MessageRelatedProfilesLogBase
{
    [JsonConstructor]
    internal RemoveMember(string conversationId, string revision, MessageViewBase message, ICollection<ProfileViewBasic> relatedProfiles)
        : base(conversationId, revision, message, relatedProfiles)
    {
    }
}
