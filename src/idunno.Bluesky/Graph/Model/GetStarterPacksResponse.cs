// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Graph.Model
{
    internal sealed record GetStarterPacksResponse
    {
        [JsonConstructor]
        public GetStarterPacksResponse(IReadOnlyList<StarterPackViewBasic> starterPacks)
        {
            StarterPacks = starterPacks;
        }

        [JsonInclude]
        [JsonRequired]
        public IReadOnlyList<StarterPackViewBasic> StarterPacks { get; init; }
    }
}
