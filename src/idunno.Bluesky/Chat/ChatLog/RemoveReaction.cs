// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// A log entry indicating a reaction was removed from a message.
/// </summary>
public sealed record RemoveReaction : MessageRelatedProfilesLogBase
{
    [JsonConstructor]
    internal RemoveReaction(string conversationId, string revision, MessageViewBase message, ReactionView reaction, ICollection<ProfileViewBasic> relatedProfiles)
        : base(conversationId, revision, message, relatedProfiles)
    {
        Reaction = reaction;
    }

    /// <summary>
    /// Gets the reaction that was added to the message.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public ReactionView Reaction { get; set; }
}