// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Record
{
    /// <summary>
    /// Encapsulates the information needed to create a repost record.
    /// </summary>
    public sealed record Repost : BlueskyTimestampedRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="Repost"/> with<see cref = "BlueskyTimestampedRecord.CreatedAt" /> set to the current date and time.
        /// </summary>
        /// <param name="subject">The <see cref="StrongReference"/> to the post to be reposted.</param>
        /// <param name="via">An optional <see cref="StrongReference"/> to a repost record, if the repost is of a repost.</param>
        public Repost(StrongReference subject, StrongReference? via = null) : this(subject, DateTimeOffset.UtcNow, via)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="Repost"/>.
        /// </summary>
        /// <param name="subject">The <see cref="StrongReference"/> to the post to be reposted.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> for the repost creation date.</param>
        /// <param name="via">An optional <see cref="StrongReference"/> to a repost record, if the repost is of a repost.</param>
        [JsonConstructor]
        public Repost(StrongReference subject, DateTimeOffset createdAt, StrongReference? via =null ) : base(createdAt)
        {
            Subject = subject;
            Via = via;
        }

        /// <summary>
        /// Gets the <see cref="StrongReference"/> to the post to be reposted.
        /// </summary>
        [JsonInclude]
        public StrongReference Subject { get; init; }

        /// <summary>
        /// Gets the <see cref="StrongReference"/> to the repost record, if the repost is of a repost.
        /// </summary>
        [JsonInclude]
        public StrongReference? Via { get; init; }
    }
}
