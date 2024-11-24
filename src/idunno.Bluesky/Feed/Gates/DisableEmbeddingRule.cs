// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Feed.Gates
{
    /// <summary>
    /// Post gate rule disabling embedding of the post it is applied to.
    /// </summary>
    public sealed record DisableEmbeddingRule : PostGateRule
    {
    }
}
