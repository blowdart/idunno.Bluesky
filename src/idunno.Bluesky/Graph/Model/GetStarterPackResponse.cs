// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Graph.Model
{
    internal sealed class GetStarterPackResponse
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
