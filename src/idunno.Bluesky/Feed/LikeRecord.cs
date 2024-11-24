// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// A record indicating a like.
    /// </summary>
    public record LikeRecord : BlueskyRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="LikeRecord"/>.
        /// </summary>
        /// <param name="subject">The subject of the record.</param>
        /// <param name="createdAt">The date and time the record was created.</param>
        public LikeRecord(StrongReference subject, DateTimeOffset createdAt) : base(createdAt)
        {
            Subject = subject;
        }

        /// <summary>
        /// A <see cref="StrongReference"/> to the subject of the like.
        /// </summary>
        public StrongReference Subject { get; init; }

    }
}
