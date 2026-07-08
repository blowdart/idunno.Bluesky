// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// System message indicating a user was added to the group conversation.
/// </summary>
public sealed record AddMemberSystemMessage : SystemMessage
{
    [JsonConstructor]
    internal AddMemberSystemMessage(SystemMessageReferredUser member, string role, SystemMessageReferredUser addedBy)
    {
        Member = member;
        Role = role;
        AddedBy = addedBy;
    }

    /// <summary>
    /// Gets a reference to the actor added to a group chat.
    /// </summary>
    [JsonRequired]
    public SystemMessageReferredUser Member { get; init; }

    /// <summary>
    /// Gets the role the actor was added as. The role from <see cref="Member"/> will reflect the current data, not historical.
    /// </summary>
    [JsonRequired]
    public string Role { get; init; }

    /// <summary>
    /// Gets a reference to the actor who added <see cref="Member"/>
    /// </summary>
    [JsonRequired]
    public SystemMessageReferredUser AddedBy { get; init; }
}