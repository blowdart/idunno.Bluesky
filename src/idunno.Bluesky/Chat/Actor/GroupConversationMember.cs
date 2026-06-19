// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Actor;

/// <summary>
/// Encapsulates properties of a member of a group conversation.
/// </summary>
public sealed record GroupConversationMember : MemberKind
{
    [JsonConstructor]
    internal GroupConversationMember(ProfileViewBasic? addedBy, string memberRole)
    {
        AddedBy = addedBy;
        MemberRole = memberRole;
    }

    /// <summary>
    /// Gets the <see cref="ProfileViewBasic"/> of the member who added this member to the conversation.
    /// </summary>
    public ProfileViewBasic? AddedBy { get; init; }

    /// <summary>
    /// Gets the role of the member in the group conversation. Known values are contained in <see cref="MemberRole"/>.
    /// </summary>
    public string MemberRole { get; init; }
}
