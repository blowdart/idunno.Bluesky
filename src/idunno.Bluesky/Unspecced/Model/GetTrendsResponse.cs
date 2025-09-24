// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Unspecced.Model
{
    internal sealed record GetTrendsResponse(ICollection<TrendView> Trends)
    {
    }
}
