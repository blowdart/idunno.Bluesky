// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Indicates a feed presence caused by a repost.
    /// </summary>
    public sealed record ReasonRepost : ReasonBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="ReasonRepost"/>
        /// </summary>
        /// <param name="by">The <see cref="ProfileViewBasic"/> of the actor who reposted the post.</param>
        /// <param name="indexedAt">The <see cref="DateTimeOffset"/> when the repost was indexed.</param>
        [JsonConstructor]
        internal ReasonRepost(ProfileViewBasic by, DateTimeOffset indexedAt)
        {
            By = by;
            IndexedAt = indexedAt;
        }

        /// <summary>
        /// Gets the <see cref="ProfileViewBasic"/> of the actor who reposted the post.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public ProfileViewBasic By { get; init; }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> when the repost was indexed.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset IndexedAt { get; init; }
    }
}
