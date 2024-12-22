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
    public record BlueskyRecordValue : AtProtoRecordValue
    {
        /// <summary>
        /// Creates a new instance of <see cref="BlueskyRecordValue"/> with <see cref="CreatedAt"/> set to the current date and time.
        /// </summary>
        public BlueskyRecordValue()
        {
            CreatedAt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyRecordValue"/>
        /// </summary>
        /// <param name="createdAt">The date and time the record was created at.</param>
        [JsonConstructor]
        public BlueskyRecordValue(DateTimeOffset? createdAt) : base()
        {
            CreatedAt = createdAt;
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyRecordValue"/> from the specified <paramref name="record"/>.
        /// </summary>
        /// <param name="record">The <see cref="BlueskyRecordValue"/> to create the new instance from.</param>
        public BlueskyRecordValue(BlueskyRecordValue record) : base(record)
        {
            if (record is not null)
            {
                CreatedAt = record.CreatedAt;
            }
        }

        /// <summary>
        /// Gets the date and time the record was created at, if known.
        /// </summary>
        [JsonInclude]
        public DateTimeOffset? CreatedAt { get; init; }
    }
}
