// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Graph;

/// <summary>
/// Represents a map of relationships between the actor <see cref="Did"/> and other actors in the Bluesky social network.
/// </summary>
public sealed record RelationshipMap
{
    /// <summary>
    /// Creates a new instance of <see cref="RelationshipMap"/>.
    /// </summary>
    /// <param name="actor">The <see cref="AtProto.Did"/> of the actor whose relationships are being described.</param>
    /// <param name="relationships">A collection of <see cref="RelationshipType"/> objects representing the relationships of the actor.</param>
    [JsonConstructor]
    public RelationshipMap(Did? actor, ICollection<RelationshipType> relationships)
    {
        Actor = actor;
        Relationships = relationships;
    }

    /// <summary>
    /// Gets the <see cref="AtProto.Did"/> of the actor whose relationships are being described.
    /// </summary>
    public Did? Actor { get; set; }

    /// <summary>
    /// Gets a collection of <see cref="RelationshipType"/> objects representing the relationships of the actor <see cref="Actor"/> and other actors.
    /// </summary>
    [JsonRequired]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
    public ICollection<RelationshipType> Relationships { get; set; }
}