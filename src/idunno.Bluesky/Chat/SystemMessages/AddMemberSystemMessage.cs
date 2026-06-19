// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.SystemMessages;

/// <summary>
/// Represents a system message indicating a member was added to the chat.
/// </summary>
public sealed record AddMemberSystemMessage : SystemMessageBase
{
    internal AddMemberSystemMessage(ReferredUser member, string role, ReferredUser addedBy)
    {
        Member = member;
        Role = role;
        AddedBy = addedBy;
    }

    /// <summary>
    /// Gets a reference to the actor added to a group chat.
    /// </summary>
    [JsonRequired]
    public ReferredUser Member { get; init; }

    /// <summary>
    /// Gets the role the actor was added as. The role from <see cref="Member"/> will reflect the current data, not historical.
    /// </summary>
    [JsonRequired]
    public string Role { get; init; }

    /// <summary>
    /// Gets a reference to the actor added <see cref="Member"/>
    /// </summary>
    [JsonRequired]
    public ReferredUser AddedBy { get; init; }
}