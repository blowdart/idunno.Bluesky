// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;

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
        [Description("posts_with_replies")]
        PostsWithReplies,

        /// <summary>
        /// Include only posts.
        /// </summary>
        [Description("posts_no_replies")]
        PostsNoReplies,

        /// <summary>
        /// Include posts with their media.
        /// </summary>
        [Description("posts_with_media")]
        PostsWithMedia,

        /// <summary>
        /// Include posts and author threads.
        /// </summary>
        [Description("posts_and_author_threads")]
        PostsAndAuthorThreads
    }
}
