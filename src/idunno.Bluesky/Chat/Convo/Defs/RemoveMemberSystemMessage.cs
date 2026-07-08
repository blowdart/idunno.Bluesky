// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// System message indicating a user was removed from the group conversation.
/// </summary>
public sealed record RemoveMemberSystemMessage : SystemMessage
{
    [JsonConstructor]
    internal RemoveMemberSystemMessage(SystemMessageReferredUser member, SystemMessageReferredUser removedBy)
    {
        Member = member;
        RemovedBy = removedBy;
    }

    /// <summary>
    /// Gets a current view of the member who was removed.
    /// </summary>
    [JsonRequired]
    public SystemMessageReferredUser Member { get; init; }

    /// <summary>
    /// Gets a reference to the actor who removed <see cref="Member"/>
    /// </summary>
    [JsonRequired]
    public SystemMessageReferredUser RemovedBy { get; init; }
}