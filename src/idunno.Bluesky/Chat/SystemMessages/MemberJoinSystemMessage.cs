// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// System message indicating a user joined the group conversation via a join link.
/// </summary>
public sealed record MemberJoinSystemMessage : SystemMessage
{
    [JsonConstructor]
    internal MemberJoinSystemMessage(ReferredUser member, string role, ReferredUser? approvedBy)
    {
        Member = member;
        Role = role;
        ApprovedBy = approvedBy;
    }

    /// <summary>
    /// Gets a current view of the member who joined.
    /// </summary>
    [JsonRequired]
    public ReferredUser Member { get; internal init; }

    /// <summary>
    /// Role the user was added to the group with. The role from 'member' will reflect the current data, not historical.
    /// </summary>
    [JsonRequired]
    public string Role { get; internal init; }

    /// <summary>
    /// Gets who approved the request, if the join link was configured to require approval. <see langword="null" /> if approval was not required
    /// </summary>
    public ReferredUser? ApprovedBy { get; internal init; }

}