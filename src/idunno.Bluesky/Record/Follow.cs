// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Encapsulates the a follow record.
    /// </summary>
    public sealed record Follow : BlueskyTimestampedRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="Follow"/> with <see cref="BlueskyTimestampedRecord.CreatedAt"/> set to the current date and time.
        /// </summary>
        /// <param name="subject">The <see cref="Did"/> to the actor to be followed.</param>
        /// <param name="via">A <see cref="StrongReference"/> to the starter pack the follow originated from, if any.</param>
        public Follow(Did subject, StrongReference? via = null) : this(subject: subject, via: via, createdAt: DateTimeOffset.UtcNow)
        {
            ArgumentNullException.ThrowIfNull(subject);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Follow"/>.
        /// </summary>
        /// <param name="subject">The <see cref="Did"/> to the actor to be followed.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> for the repost creation date.</param>
        [JsonConstructor]
        public Follow(Did subject, DateTimeOffset createdAt) : this(subject: subject, via: null, createdAt: createdAt)
        {
            ArgumentNullException.ThrowIfNull(subject);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Follow"/>.
        /// </summary>
        /// <param name="subject">The <see cref="Did"/> to the actor to be followed.</param>
        /// <param name="via">A <see cref="StrongReference"/> to the starter pack the follow originated from, if any.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> for the repost creation date.</param>
        [JsonConstructor]
        public Follow(Did subject, StrongReference? via, DateTimeOffset createdAt) : base(createdAt)
        {
            ArgumentNullException.ThrowIfNull(subject);
            Subject = subject;
            Via = via;
        }

        /// <summary>
        /// Gets the <see cref="Did"/> of the actor being followed.
        /// </summary>
        [JsonInclude]
        public Did Subject { get; init; }

        /// <summary>
        /// Gets a <see cref="StrongReference"/> to the starter pack the follow originated from.
        /// </summary>
        public StrongReference? Via { get; init; }
    }
}
