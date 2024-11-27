// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Represents the view over a post from a feed.
    /// </summary>
    public sealed record FeedViewPost : View
    {
        [JsonConstructor]
        internal FeedViewPost(PostView post, ReplyReference? reply, ReasonBase? reason, string? feedContext)
        {
            Post = post;
            Reply = reply;
            Reason = reason;
            FeedContext = feedContext;
        }

        /// <summary>
        /// A <see cref="PostView"/> of the post.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public PostView Post { get; init; }

        /// <summary>
        /// A <see cref="ReplyReference"/> to a post this post was in reply to, if any.
        /// </summary>
        [JsonInclude]
        public ReplyReference? Reply { get; init; }

        /// <summary>
        /// An optional reason indicating why the post is in a feed, typically either a <see cref="ReasonRepost"/> or <see cref="ReasonPin"/>.
        /// </summary>
        [JsonInclude]
        public ReasonBase? Reason { get; init; }

        /// <summary>
        /// Context provided by feed generator that may be passed back alongside interactions.
        /// </summary>
        [JsonInclude]
        public string? FeedContext { get; init; }
    }
}
