// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Graph;

namespace idunno.Bluesky.Unspecced.Model
{
    internal sealed record GetSuggestedStarterPacksResponse(ICollection<StarterPackView> StarterPacks)
    {
    }
}
