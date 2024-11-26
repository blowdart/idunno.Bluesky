// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Graph.Model
{
    internal sealed record MuteActorRequest
    {
        public MuteActorRequest(AtIdentifier actor)
        {
            Actor = actor;
        }

        [JsonInclude]
        [JsonRequired]
        public AtIdentifier Actor { get; init; }
    }
}
