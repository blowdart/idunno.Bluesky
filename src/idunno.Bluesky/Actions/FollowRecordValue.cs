// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Actions
{
    /// <summary>
    /// Encapsulates the a follow record.
    /// </summary>
    public sealed record FollowRecordValue : BlueskyTimestampedRecordValue
    {
        /// <summary>
        /// Creates a new instance of <see cref="FollowRecordValue"/> with <see cref="BlueskyTimestampedRecordValue.CreatedAt"/> set to the current date and time.
        /// </summary>
        /// <param name="subject">The <see cref="Did"/> to the actor to be followed.</param>
        public FollowRecordValue(Did subject) : this(subject, DateTimeOffset.UtcNow)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="FollowRecordValue"/>.
        /// </summary>
        /// <param name="subject">The <see cref="Did"/> to the actor to be followed.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> for the repost creation date.</param>
        [JsonConstructor]
        public FollowRecordValue(Did subject, DateTimeOffset createdAt) : base(createdAt)
        {
            ArgumentNullException.ThrowIfNull(subject);
            Subject = subject;
        }

        /// <summary>
        /// Gets the <see cref="Did"/> of the actor being followed.
        /// </summary>
        [JsonInclude]
        public Did Subject { get; init; }
    }
}
