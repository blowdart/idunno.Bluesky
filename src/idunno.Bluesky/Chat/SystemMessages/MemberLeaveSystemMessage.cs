// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// System message indicating a user voluntarily left the group conversation.
/// </summary>
public sealed record MemberLeaveSystemMessage : SystemMessage
{
    [JsonConstructor]
    internal MemberLeaveSystemMessage(ReferredUser member)
    {
        Member = member;
    }

    /// <summary>
    /// Gets a current view of the member who left the group.
    /// </summary>
    [JsonRequired]
    public ReferredUser Member { get; set; }
}