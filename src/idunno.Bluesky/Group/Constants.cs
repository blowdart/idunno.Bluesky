// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Group;

/// <summary>
/// Values indicating the status of a group conversation link.
/// </summary>
public static class LinkEnabledStatus
{
    /// <summary>
    /// Indicates the group conversation link is enabled.
    /// </summary>
    public const string Enabled = "enabled";

    /// <summary>
    /// Indicates the group conversation link is disabled.
    /// </summary>
    public const string Disabled = "disabled";
}

/// <summary>
/// Values indicating the join rules for a group conversation.
/// </summary>
public static class JoinRule
{
    /// <summary>
    /// Indicates that anyone can join the group conversation.
    /// </summary>
    public const string Anyone = "anyone";

    /// <summary>
    /// Indicates that only users followed by the owner can join the group conversation.
    /// </summary>
    public const string FollowedByOwner = "followedByOwner";
}