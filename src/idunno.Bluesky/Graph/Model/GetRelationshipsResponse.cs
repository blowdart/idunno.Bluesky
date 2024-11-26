// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;

namespace idunno.Bluesky.Graph.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in GetRelationships.")]
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
