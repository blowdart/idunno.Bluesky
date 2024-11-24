// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// A notification record triggered by a repost
    /// </summary>
    public record RepostRecord : BlueskyRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="LikeRecord"/>.
        /// </summary>
        /// <param name="subject">The subject of the record.</param>
        /// <param name="createdAt">The date and time the record was created.</param>
        public RepostRecord(
            StrongReference subject,
            DateTimeOffset createdAt) : base(createdAt)
        {
            Subject = subject;
        }

        /// <summary>
        /// A strong reference to the post being reposted.
        /// </summary>
        public StrongReference Subject { get; init; }
    }
}
