// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Chat.Actor;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// Event indicating a prospective member withdrew their join request. Only the owner gets this.
/// </summary>
public sealed record WithdrawIncomingJoinRequest : LogBase
{
    /// <summary>
    /// Constructs a new instance of <see cref="WithdrawIncomingJoinRequest"/>.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="revision">The conversation revision.</param>
    /// <param name="message">A <see cref="MessageViewBase">view</see> over the message the log entry refers to.</param>
    [JsonConstructor]
    internal WithdrawIncomingJoinRequest(string conversationId, string revision, ProfileViewBasic member) : base(conversationId, revision)
    {
        Member = member;
    }

    /// <summary>
    /// Gets the orospective member who requested to join.
    /// </summary>
    [JsonRequired]
    public ProfileViewBasic Member { get; set; }
}