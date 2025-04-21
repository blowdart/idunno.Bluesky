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
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = false,
                     UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
    [JsonDerivedType(typeof(Post), RecordType.Post)]
    [JsonDerivedType(typeof(FollowRecordValue), RecordType.Follow)]
    [JsonDerivedType(typeof(RepostRecordValue), RecordType.Repost)]
    [JsonDerivedType(typeof(LikeRecordValue), RecordType.Like)]
    [JsonDerivedType(typeof(BlockRecordValue), RecordType.Block)]
    [JsonDerivedType(typeof(ProfileRecordValue), RecordType.Profile)]
    [JsonDerivedType(typeof(StarterPackRecordValue), RecordType.StarterPack)]
    [JsonDerivedType(typeof(LabelerDeclarationRecordValue), RecordType.LabelerDeclaration)]
    [JsonDerivedType(typeof(VerificationRecordValue), RecordType.Verification)]
    public record BlueskyRecordValue : AtProtoRecordValue
    {
        /// <summary>
        /// Creates a new instance of <see cref="BlueskyRecordValue"/>.
        /// </summary>
        public BlueskyRecordValue()
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyRecordValue"/> from the specified <paramref name="record"/>.
        /// </summary>
        /// <param name="record">The <see cref="BlueskyRecordValue"/> to create the new instance from.</param>
        public BlueskyRecordValue(BlueskyRecordValue record) : base(record)
        {
        }
    }
}
