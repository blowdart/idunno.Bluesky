// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Graph.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in GetStarterPacks")]
    internal sealed record GetStarterPackResponse
    {
        [JsonConstructor]
        public GetStarterPackResponse(StarterPackView starterPack)
        {
            StarterPack = starterPack;
        }

        [JsonInclude]
        [JsonRequired]
        public StarterPackView StarterPack { get; init; }
    }
}
