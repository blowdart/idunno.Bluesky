// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// A record indicating when a follow occurred and the <see cref="Did"/> of the follower.
    /// </summary>
    public record FollowRecord : BlueskyRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="FollowRecord"/>.
        /// </summary>
        /// <param name="subject">The <see cref="Did"/> of the actor who caused the follow record to be created.</param>
        /// <param name="createdAt">The date and time the follow record was created at.</param>
        public FollowRecord(Did subject, DateTimeOffset createdAt) : base(createdAt)
        {
            Subject = subject;
        }

        /// <summary>
        /// The <see cref="Did"/> of the actor who caused the follow record to be created.
        /// </summary>
        public Did Subject { get; init; }
    }
}
