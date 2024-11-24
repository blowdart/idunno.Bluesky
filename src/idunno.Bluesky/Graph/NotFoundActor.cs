// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Graph
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public record NotFoundActor : RelationshipType
    {
        [JsonConstructor]
        public NotFoundActor(AtIdentifier actor, bool notFound)
        {
            Actor = actor;
            NotFound = notFound;
        }

        [JsonInclude]
        [JsonRequired]
        public AtIdentifier Actor { get; init; }

        [JsonInclude]
        public bool NotFound { get; init; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"/{Actor.Value} not found./";
            }
        }
    }
}
