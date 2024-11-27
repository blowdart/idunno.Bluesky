// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Feed.Gates
{
    /// <summary>
    /// Thread gate rule specifying that replies are allowed from actors mentioned in the post.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty", Justification = "Needed to discriminate on json type discriminator")]
    public sealed record MentionRule : ThreadGateRule
    {
    }
}
