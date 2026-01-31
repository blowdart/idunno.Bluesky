// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Graph.Model
{
    internal sealed record GetActorStarterPacksResponse
    {
        public GetActorStarterPacksResponse(IReadOnlyList<StarterPackViewBasic> starterPacks)
        {
            StarterPacks = starterPacks;
        }

        [JsonRequired]
        public IReadOnlyList<StarterPackViewBasic> StarterPacks { get; init; }

        public string? Cursor { get; init; }
    }
}
