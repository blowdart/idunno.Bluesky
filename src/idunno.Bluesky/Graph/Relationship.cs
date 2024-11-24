// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Graph
{
    // https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/graph/defs.json

    /// <summary>
    /// Lists the bi-directional graph relationships between one actor (not indicated in the object), and the target actors (the DID included in the object)
    /// </summary>
    public sealed record Relationship : RelationshipType
    {
        /// <summary>
        /// Creates a new instance of <see cref="Relationship"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> of the target actor.</param>
        /// <param name="following">If the actor follows this <see cref="Did"/>, this is the <see cref="AtUri"/> of the follow record, otherwise null</param>
        /// <param name="followedBy">If the actor is followed by this <see cref="Did"/>, contains the <see cref="AtUri"/> of the follow record.</param>
        [JsonConstructor]
        public Relationship(Did did, AtUri?following, AtUri? followedBy)
        {
            Did = did;
            Following = following;
            FollowedBy = followedBy;
        }

        /// <summary>
        /// The <see cref="AtProto.Did"/> of the target actor.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Did Did { get; init; }

        /// <summary>
        /// If the actor follows this <see cref="Did"/>, this is the <see cref="AtUri"/> of the follow record, otherwise null.
        /// </summary>
        [JsonInclude]
        public AtUri? Following { get; init; }

        /// <summary>
        /// If the actor is followed by this <see cref="Did"/>, contains the <see cref="AtUri"/> of the follow record.
        /// </summary>
        [JsonInclude]
        public AtUri? FollowedBy { get; init; }
    }
}
