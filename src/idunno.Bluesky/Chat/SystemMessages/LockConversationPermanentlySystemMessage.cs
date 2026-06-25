// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// System message indicating the group conversation was locked permanently.
/// </summary>
public sealed record LockConversationPermanentlySystemMessage : SystemMessage
{
    [JsonConstructor]
    internal LockConversationPermanentlySystemMessage(ReferredUser lockedBy)
    {
        LockedBy = lockedBy;
    }

    /// <summary>
    /// Gets a current view of the user who locked the group.
    /// </summary>
    [JsonRequired]
    public ReferredUser LockedBy { get; internal init; }
}