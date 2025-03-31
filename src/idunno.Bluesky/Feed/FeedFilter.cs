// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Combinations of post/repost types to include in feed responses.
    /// </summary>
    public enum FeedFilter
    {
        /// <summary>
        /// Include posts and replies.
        /// </summary>
        PostsWithReplies,

        /// <summary>
        /// Include only posts.
        /// </summary>
        PostsNoReplies,

        /// <summary>
        /// Include posts with their media.
        /// </summary>
        PostsWithMedia,

        /// <summary>
        /// Include posts and author threads.
        /// </summary>
        PostsAndAuthorThreads
    }
}
