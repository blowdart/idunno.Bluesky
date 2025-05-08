// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Actions
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
        public Repost(StrongReference subject) : this(subject, DateTimeOffset.UtcNow)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="Repost"/>.
        /// </summary>
        /// <param name="subject">The <see cref="StrongReference"/> to the post to be reposted.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> for the repost creation date.</param>
        [JsonConstructor]
        public Repost(StrongReference subject, DateTimeOffset createdAt) : base(createdAt)
        {
            Subject = subject;
        }

        /// <summary>
        /// Gets the <see cref="StrongReference"/> to the post to be reposted.
        /// </summary>
        [JsonInclude]
        public StrongReference Subject { get; init; }
    }
}
