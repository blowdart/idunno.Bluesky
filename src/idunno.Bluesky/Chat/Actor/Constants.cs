// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Chat.Actor;

/// <summary>
/// Known values for the <see cref="Declaration.AllowIncoming"/> property.
/// </summary>
public static class AllowIncoming
{
    /// <summary>
    /// Allow chats from all actors.
    /// </summary>
    public const string All = "all";

    /// <summary>
    /// Allow no chats from any actors.
    /// </summary>
    public const string None = "none";

    /// <summary>
    /// Allow chats only from actors the authenticated user is following.
    /// </summary>
    public const string Following = "following";
}

/// <summary>
/// Known values for the <see cref="Declaration.AllowGroupInvites"/> property.
/// </summary>
public static class AllowGroupInvites
{
    /// <summary>
    /// Allow group invites from all actors.
    /// </summary>
    public const string All = "all";

    /// <summary>
    /// Allow no group invites from any actors.
    /// </summary>
    public const string None = "none";

    /// <summary>
    /// Allow group invites only from actors the authenticated user is following.
    /// </summary>
    public const string Following = "following";
}

/// <summary>
/// Known values for a member role.
/// </summary>
public static class MemberRole
{
    /// <summary>
    /// The user is the owner of the chat.
    /// </summary>
    public const string Owner = "owner";
    
    /// <summary>
    /// The user is a standard member of the chat.
    /// </summary>
    public const string Standard = "standard";
}
