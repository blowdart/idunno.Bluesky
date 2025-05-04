// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

using idunno.Bluesky.Actions;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Base record for common Bluesky record values
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true,
                     UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(Post), RecordType.Post)]
    [JsonDerivedType(typeof(Follow), RecordType.Follow)]
    [JsonDerivedType(typeof(Repost), RecordType.Repost)]
    [JsonDerivedType(typeof(Like), RecordType.Like)]
    [JsonDerivedType(typeof(Block), RecordType.Block)]
    [JsonDerivedType(typeof(Profile), RecordType.Profile)]
    [JsonDerivedType(typeof(StarterPack), RecordType.StarterPack)]
    [JsonDerivedType(typeof(LabelerDeclaration), RecordType.LabelerDeclaration)]
    [JsonDerivedType(typeof(Verification), RecordType.Verification)]
    [JsonDerivedType(typeof(BlueskyList), RecordType.List)]
    [JsonDerivedType(typeof(BlueskyListItem), RecordType.ListItem)]
    public record BlueskyRecord : AtProtoRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="BlueskyRecord"/>.
        /// </summary>
        public BlueskyRecord()
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyRecord"/> from the specified <paramref name="record"/>.
        /// </summary>
        /// <param name="record">The <see cref="BlueskyRecord"/> to create the new instance from.</param>
        public BlueskyRecord(BlueskyRecord record) : base(record)
        {
        }
    }
}
