// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Graph
{
    /// <summary>
    /// Encapsulates the relationships between an actor and other actors, if any.
    /// </summary>
    public sealed class ActorRelationships
    {
        internal ActorRelationships(Did did, IDictionary<Did, Relationship> relationships)
        {
            Did = did;

            if (relationships is not null)
            {
                Relationships = relationships.AsReadOnly();
            }
            else
            {
                Relationships = new Dictionary<Did, Relationship>().AsReadOnly();
            }
        }

        /// <summary>
        /// The <see cref="Did"/> of the actor whose relationships are mapped out.
        /// </summary>
        public Did Did { get; init; }

        /// <summary>
        /// The relationship between the actor and other actors.
        /// </summary>
        public IReadOnlyDictionary<Did, Relationship> Relationships { get; init; }
    }
}
