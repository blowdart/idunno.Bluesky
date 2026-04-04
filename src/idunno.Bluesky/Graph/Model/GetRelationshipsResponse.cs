// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;

namespace idunno.Bluesky.Graph.Model;

[SuppressMessage("Performance", "CA1812", Justification = "Used in GetRelationships.")]
internal sealed class GetRelationshipsResponse(Did actor, ICollection<RelationshipType> relationships)
{
    public Did Actor { get; init; } = actor;

    public ICollection<RelationshipType> Relationships { get; init; } = relationships;
}
