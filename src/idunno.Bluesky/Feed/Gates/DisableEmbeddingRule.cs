// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Feed.Gates
{
    /// <summary>
    /// Post gate rule disabling embedding of the post it is applied to.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty", Justification = "Needed to discriminate on json type discriminator")]
    public sealed record DisableEmbeddingRule : PostGateRule
    {
    }
}
