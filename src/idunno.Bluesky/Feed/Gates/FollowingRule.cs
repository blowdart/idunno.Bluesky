// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Feed.Gates
{
    /// <summary>
    /// Thread gate rule specifying that replies are allowed from actors the post creator follows.
    /// </summary>
    public sealed record FollowingRule : ThreadGateRule
    {
    }
}
