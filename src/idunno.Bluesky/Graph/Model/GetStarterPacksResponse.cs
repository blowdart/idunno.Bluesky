// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Graph.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in GetStarterPacks.")]
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
