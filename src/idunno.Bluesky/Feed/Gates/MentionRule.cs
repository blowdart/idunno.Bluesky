// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Feed.Gates
{
    /// <summary>
    /// Thread gate rule specifying that replies are allowed from actors mentioned in the post.
    /// </summary>
    public sealed record MentionRule : ThreadGateRule
    {
    }
}
