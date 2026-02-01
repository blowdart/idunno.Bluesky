// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Encapsulates the information needed to create a block record.
    /// </summary>
    public sealed record Block : BlueskyTimestampedRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="Block"/> with <see cref="BlueskyTimestampedRecord.CreatedAt"/> set to the current date and time.
        /// </summary>
        /// <param name="subject">The <see cref="Did"/> to the actor to be blocked.</param>
        public Block(Did subject) : this(subject, DateTimeOffset.UtcNow)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="Block"/>.
        /// </summary>
        /// <param name="subject">The <see cref="Did"/> to the actor to be blocked.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> for the repost creation date.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="subject"/> is <see langword="null"/>.</exception>
        [JsonConstructor]
        public Block(Did subject, DateTimeOffset createdAt) : base(createdAt)
        {
            ArgumentNullException.ThrowIfNull(subject);

            Subject = subject;
        }

        /// <summary>
        /// Gets the <see cref="Did"/> of the subject to be blocked.
        /// </summary>
        [JsonInclude]
        public Did Subject { get; init; }
    }
}
