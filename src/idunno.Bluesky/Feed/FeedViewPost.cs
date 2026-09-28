// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed;

/// <summary>
/// Represents the view over a post from a feed.
/// </summary>
public sealed record FeedViewPost : View
{
    [JsonConstructor]
    internal FeedViewPost(
        PostView post,
        ReplyReference? reply,
        int? opThreadPostIndex,
        int? opThreadPostCount,
        ReasonBase? reason,
        string? feedContext)
    {
        Post = post;
        Reply = reply;
        OpThreadPostIndex = opThreadPostIndex;
        OpThreadPostCount = opThreadPostCount;
        Reason = reason;
        FeedContext = feedContext;
    }

    /// <summary>
    /// A <see cref="Feed.PostView"/> of the post.
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
    /// Gets the 1-indexed position of this post within the contiguous OP thread. Only present when this post is part of the OP thread.
    /// </summary>
    [JsonInclude]
    public int? OpThreadPostIndex { get; init; }

    /// <summary>
    /// Gets the total number of posts in the contiguous OP thread that this post belongs to. Only present when this post is part of the OP thread.
    /// </summary>
    [JsonInclude]
    public int? OpThreadPostCount { get; init; }

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