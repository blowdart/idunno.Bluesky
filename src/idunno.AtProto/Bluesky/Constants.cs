// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Bluesky
{
    /// <summary>
    /// Namespaced Identifier (NSIDs) of record collection used in AT Protocol record creation, deletion and updating.
    /// </summary>
    /// <remarks>
    /// See https://atproto.com/specs/nsid for a description of NSIDs.
    /// </remarks>
    public static class CollectionType
    {
        /// <summary>
        /// The NSID for a Bluesky posts record collection.
        /// </summary>
        public const string Post = "app.bsky.feed.post";

        /// <summary>
        /// The NSID for a Bluesky likes record collection.
        /// </summary>
        public const string Like = "app.bsky.feed.like";

        /// <summary>
        /// The NSID for a Bluesky reposts record collection.
        /// </summary>
        public const string Repost = "app.bsky.feed.repost";

        /// <summary>
        /// The NSID for a Bluesky follows record collection.
        /// </summary>
        public const string Follow = "app.bsky.graph.follow";
    }

    /// <summary>
    /// The type discriminators used for various post records in a feed or thread.
    /// </summary>
    public static class RecordTypes
    {
        /// <summary>
        /// Indicates a thread view post.
        /// </summary>
        public const string ThreadViewPost = "app.bsky.feed.defs#threadViewPost";

        /// <summary>
        /// Indicates a not found post in a thread.
        /// </summary>
        public const string NotFoundPost = "app.bsky.feed.defs#notFoundPost";

        /// <summary>
        /// Indicates a post in a thread that is blocked to or by the current user.
        /// </summary>
        public const string BlockedPost = "app.bsky.feed.defs#blockedPost";

        /// <summary>
        /// Indicates a post record.
        /// </summary>
        public const string Post = "app.bsky.feed.post";
    }

    /// <summary>
    /// The type discriminators used for rich text facets.
    /// </summary>
    public static class FacetTypes
    {
        /// <summary>
        /// Indicates the facet feature for a hashtag.
        /// </summary>
        public const string HashtagFacet = "app.bsky.richtext.facet#tag";

        /// <summary>
        /// Indicates the facet feature for a link.
        /// </summary>
        public const string LinkFacet = "app.bsky.richtext.facet#link";

        /// <summary>
        /// Indicates the facet feature for a mention.
        /// </summary>
        public const string MentionFacet = "app.bsky.richtext.facet#mention";
    }

    public static class ReasonTypes
    {
        public const string Repost = "app.bsky.feed.defs#reasonRepost";
    }
}
