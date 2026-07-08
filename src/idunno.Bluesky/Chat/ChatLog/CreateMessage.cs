// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// A log entry indicating a message was created in a chat.
/// </summary>
public sealed record CreateMessage : MessageRelatedProfilesLogBase
{
    [JsonConstructor]
    internal CreateMessage(string conversationId, string revision, MessageViewBase message, IReadOnlyCollection<ProfileViewBasic> relatedProfiles)
        : base(conversationId, revision, message, relatedProfiles)
    {
    }
}