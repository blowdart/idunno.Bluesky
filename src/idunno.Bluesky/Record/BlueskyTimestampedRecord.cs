// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Actions;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Base record for common Bluesky record values which are always timestamped.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true,
                     UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(Post), RecordType.Post)]
    [JsonDerivedType(typeof(Follow), RecordType.Follow)]
    [JsonDerivedType(typeof(Repost), RecordType.Repost)]
    [JsonDerivedType(typeof(Like), RecordType.Like)]
    [JsonDerivedType(typeof(Block), RecordType.Block)]
    [JsonDerivedType(typeof(StarterPack), RecordType.StarterPack)]
    [JsonDerivedType(typeof(Verification), RecordType.Verification)]
    [JsonDerivedType(typeof(BlueskyList), RecordType.List)]
    [JsonDerivedType(typeof(BlueskyListItem), RecordType.ListItem)]
    public record BlueskyTimestampedRecord : BlueskyRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="BlueskyTimestampedRecord"/> with <see cref="CreatedAt"/> set to the current date and time.
        /// </summary>
        public BlueskyTimestampedRecord()
        {
            CreatedAt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyTimestampedRecord"/>
        /// </summary>
        /// <param name="createdAt">The date and time the record was created at.</param>
        [JsonConstructor]
        public BlueskyTimestampedRecord(DateTimeOffset createdAt) : base()
        {
            CreatedAt = createdAt.ToUniversalTime();
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyTimestampedRecord"/>
        /// </summary>
        /// <param name="createdAt">The date and time the record was created at.</param>
        public BlueskyTimestampedRecord(DateTimeOffset? createdAt) : base()
        {
            if (createdAt is null)
            {
                CreatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                CreatedAt = createdAt.Value.ToUniversalTime();
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyTimestampedRecord"/> from the specified <paramref name="record"/>.
        /// </summary>
        /// <param name="record">The <see cref="BlueskyTimestampedRecord"/> to create the new instance from.</param>
        public BlueskyTimestampedRecord(BlueskyTimestampedRecord record) : base(record)
        {
            if (record is not null)
            {
                CreatedAt = record.CreatedAt.ToUniversalTime();
            }
        }

        /// <summary>
        /// Gets the date and time the record was created at, if known.
        /// </summary>
        [JsonInclude]
        public DateTimeOffset CreatedAt { get; init; }
    }
}
