// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// A log entry indicating a reaction was added to a message.
/// </summary>
public sealed record AddReaction : MessageLogBase
{
    [JsonConstructor]
    internal AddReaction(string conversationId, string revision, MessageViewBase message, ReactionView reaction, ICollection<ProfileViewBasic>? relatedProfiles)
        : base(conversationId, revision, message)
    {
        Reaction = reaction;
        RelatedProfiles = relatedProfiles;
    }

    /// <summary>
    /// Gets the reaction that was added to the message.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public ReactionView Reaction { get; set; }

    /// <summary>
    /// Gets the profiles referred in the message and reaction views. This isn't required for compatibility, because it was added later, but should generally be present.
    /// </summary>
    [JsonInclude]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter needed for deserialization.")]
    public ICollection<ProfileViewBasic>? RelatedProfiles { get; set; }
}