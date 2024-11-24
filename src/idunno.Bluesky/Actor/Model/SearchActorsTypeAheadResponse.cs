// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor.Model
{
    internal record SearchActorsTypeAheadResponse
    {
        [JsonConstructor]
        public SearchActorsTypeAheadResponse(IReadOnlyCollection<ProfileViewBasic> actors)
        {
            Actors = actors;
        }

        [JsonInclude]
        public IReadOnlyCollection<ProfileViewBasic> Actors { get; init; }
    }
}
