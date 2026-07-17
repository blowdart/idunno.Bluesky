// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Namespace does not match folder structure
using idunno.Bluesky.Graph.Model;

namespace idunno.Bluesky.Graph;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Contains the results of a search for starter packs from the app.bsky.graph.searchStarterPacksV2 api.
/// </summary>
public sealed class SearchStarterPacksV2Result : PagedViewReadOnlyCollection<StarterPackView>
{
    internal SearchStarterPacksV2Result(SearchStarterPacksV2Response response)
        : base(response.StarterPacks, response.Cursor)
    {
        HitsTotal = response.HitsTotal;
    }

    internal SearchStarterPacksV2Result(PagedViewReadOnlyCollection<StarterPackView> data, int? hitsTotal, string? cursor)
        : base(data, cursor)
    {
        HitsTotal = hitsTotal;
    }

    /// <summary>
    /// Gets the estimated total number of matching hits. May be rounded or truncated.
    /// </summary>
    public int? HitsTotal { get; init; }
}
