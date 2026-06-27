// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// Event indicating a group conversation was locked permanently.
/// </summary>
public sealed record LockConversationPermanently : MessageRelatedProfilesLogBase
{
    [JsonConstructor]
    internal LockConversationPermanently(string conversationId, string revision, MessageViewBase message, ICollection<ProfileViewBasic> relatedProfiles)
        : base(conversationId, revision, message, relatedProfiles)
    {
    }
}