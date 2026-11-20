// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Feed;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// An entry in a <see cref="FeedSkeleton"/>, the unhydrated reference to a post a feed generator returns.
/// </summary>
public sealed record SkeletonFeedPost
{
    /// <summary>
    /// Creates a new instance of <see cref="SkeletonFeedPost"/>.
    /// </summary>
    /// <param name="post">The <see cref="AtUri"/> of the post.</param>
    /// <param name="reason">An optional <see cref="SkeletonReason"/> explaining why the post appears in the feed.</param>
    /// <param name="feedContext">Optional context the feed generator supplies, which a client can send back with interactions.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="post"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public SkeletonFeedPost(AtUri post, SkeletonReason? reason = null, string? feedContext = null)
    {
        ArgumentNullException.ThrowIfNull(post);

        Post = post;
        Reason = reason;
        FeedContext = feedContext;
    }

    /// <summary>
    /// Gets the <see cref="AtUri"/> of the post.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public AtUri Post { get; init; }

    /// <summary>
    /// Gets the reason the post appears in the feed, typically a <see cref="SkeletonReasonRepost"/> or a <see cref="SkeletonReasonPin"/>, if the feed generator supplied one.
    /// </summary>
    [JsonInclude]
    public SkeletonReason? Reason { get; init; }

    /// <summary>
    /// Gets the context the feed generator supplied for this post, if any.
    /// </summary>
    /// <remarks>
    /// <para>The context is passed through to clients, which can return it to the feed generator in an <see cref="Interaction"/>.</para>
    /// </remarks>
    [JsonInclude]
    public string? FeedContext { get; init; }
}
