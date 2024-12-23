// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


using System.Text.Json.Serialization;
using idunno.Bluesky.Actions;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Base record for common Bluesky record values which are always timestamped.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = false,
                     UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
    [JsonDerivedType(typeof(Post), RecordType.Post)]
    [JsonDerivedType(typeof(FollowRecordValue), RecordType.Follow)]
    [JsonDerivedType(typeof(RepostRecordValue), RecordType.Repost)]
    [JsonDerivedType(typeof(LikeRecordValue), RecordType.Like)]
    [JsonDerivedType(typeof(BlockRecordValue), RecordType.Block)]
    [JsonDerivedType(typeof(StarterPackRecordValue), RecordType.StarterPack)]
    public record BlueskyTimestampedRecordValue : BlueskyRecordValue
    {
        /// <summary>
        /// Creates a new instance of <see cref="BlueskyTimestampedRecordValue"/> with <see cref="CreatedAt"/> set to the current date and time.
        /// </summary>
        public BlueskyTimestampedRecordValue()
        {
            CreatedAt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyTimestampedRecordValue"/>
        /// </summary>
        /// <param name="createdAt">The date and time the record was created at.</param>
        [JsonConstructor]
        public BlueskyTimestampedRecordValue(DateTimeOffset createdAt) : base()
        {
            CreatedAt = createdAt;
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyTimestampedRecordValue"/> from the specified <paramref name="record"/>.
        /// </summary>
        /// <param name="record">The <see cref="BlueskyTimestampedRecordValue"/> to create the new instance from.</param>
        public BlueskyTimestampedRecordValue(BlueskyTimestampedRecordValue record) : base(record)
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
        public DateTimeOffset CreatedAt { get; init; }
    }
}
