// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Graph
{
    /// <summary>
    /// Encapsulates a relationship between two actors, where one actor cannot be found.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public record NotFoundActor : RelationshipType
    {
        /// <summary>
        /// Creates a new instance of <see cref="NotFoundActor"/>.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the non-found actor.</param>
        /// <param name="notFound">A flag indicating that the actor was not found.</param>
        [JsonConstructor]
        public NotFoundActor(AtIdentifier actor, bool notFound)
        {
            Actor = actor;
            NotFound = notFound;
        }

        /// <summary>
        /// Gets the <see cref="AtIdentifier"/> of the non-found actor.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public AtIdentifier Actor { get; init; }

        /// <summary>
        /// Gets a flag indicating that the actor was not found.
        /// </summary>
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
