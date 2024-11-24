// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// A user's <see cref="Preference"/>s controlling how posts in feeds are displayed.
    /// </summary>
    public sealed record class FeedViewPreference : Preference
    {
        /// <summary>
        /// Creates a new instance of <see cref="FeedViewPreference"/>.
        /// </summary>
        /// <param name="feed">The URI of the feed, or an identifier which describes the feed.</param>
        /// <param name="hideReplies">Flag indicating whether to hide replies in the feed.s</param>
        /// <param name="hideRepliesByUnfollowed">Flag indicating whether to replies in the feed if they are not by followed users.</param>
        /// <param name="hideRepliesByLikeCount">Minimum number of likes a reply must have in the feed before being shown.</param>
        /// <param name="hideRepostsInFeed">Flag indicating whether to hide reposts in the feed.</param>
        /// <param name="hideQuotePosts">Flag indicating whether to hide quote posts in the feed.</param>
        public FeedViewPreference(string feed, bool hideReplies, bool hideRepliesByUnfollowed, int? hideRepliesByLikeCount, bool hideRepostsInFeed, bool hideQuotePosts)
        {
            Feed = feed;
            HideReplies = hideReplies;
            HideRepliesByUnfollowed = hideRepliesByUnfollowed;
            HideRepliesByLikeCount = hideRepliesByLikeCount;
            HideRepostsInFeed = hideRepostsInFeed;
            HideQuotePosts = hideQuotePosts;
        }

        /// <summary>
        /// Gets the URI of the feed, or an identifier which describes the feed.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string Feed { get; init; }

        /// <summary>
        /// Flag indicating whether to hide replies in the feed.
        /// </summary>
        [JsonInclude]
        public bool HideReplies { get; init; }

        /// <summary>
        /// Flag indicating whether to replies in the feed if they are not by followed users.
        /// </summary>
        [JsonInclude]
        public bool HideRepliesByUnfollowed { get; init; }

        /// <summary>
        /// Gets the minimum number of likes a reply must have in the feed before being shown
        /// </summary>
        [JsonInclude]
        public int? HideRepliesByLikeCount { get; init; }

        /// <summary>
        /// Flag indicating whether to hide reposts in the feed.
        /// </summary>
        [JsonInclude]
        public bool HideRepostsInFeed { get; init; }

        /// <summary>
        /// Flag indicating whether to hide quote posts in the feed.
        /// </summary>
        [JsonInclude]
        public bool HideQuotePosts { get; init; }
    }
}
