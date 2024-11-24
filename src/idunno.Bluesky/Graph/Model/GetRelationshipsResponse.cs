// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Graph.Model
{
    internal sealed class GetRelationshipsResponse
    {
        public GetRelationshipsResponse(Did actor, ICollection<RelationshipType> relationships)
        {
            Actor = actor;
            Relationships = relationships;
        }

        public Did Actor { get; init; }

        public ICollection<RelationshipType> Relationships { get; init; }
    }
}
