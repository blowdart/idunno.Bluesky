// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky
{
    /// <summary>
    /// Base record for common Bluesky records values
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true,
                     UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
    [JsonDerivedType(typeof(PostRecord), RecordType.Post)]
    [JsonDerivedType(typeof(FollowRecord), RecordType.Follow)]
    [JsonDerivedType(typeof(RepostRecord), RecordType.Repost)]
    [JsonDerivedType(typeof(LikeRecord), RecordType.Like)]
    //TODO: StarterPack-Joined
    public record BlueskyRecord : AtProtoRecordValue
    {
        /// <summary>
        /// Creates a new instance of <see cref="BlueskyRecord"/>
        /// </summary>
        /// <param name="createdAt">The date and time the record was created at.</param>
        [JsonConstructor]
        public BlueskyRecord(DateTimeOffset createdAt) : base()
        {
            CreatedAt = createdAt;
        }

        /// <summary>
        /// Gets the date and time the record was created at.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset CreatedAt { get; init; }
    }
}
