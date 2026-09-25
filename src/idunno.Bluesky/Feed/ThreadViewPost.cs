// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed;

/// <summary>
/// Encapsulates a view over a post in a thread.
/// </summary>
public record ThreadViewPost : PostViewBase
{
    /// <summary>
    /// Creates a new instance of <see cref="ThreadViewPost"/>.
    /// </summary>
    /// <param name="post">The <see cref="PostView"/> of the post.</param>
    /// <param name="parent">The <see cref="PostViewBase"/> of the parent of the <paramref name="post"/>, if any.</param>
    /// <param name="replies">The collection of <see cref="PostViewBase"/> of replies to the <paramref name="post"/>, if any.</param>
    /// <param name="threadGate">The <see cref="ThreadGateView"/> over the thread gate applied to the post, if any.</param>
    /// <param name="threadContext">The <see cref="ThreadContext"/> for the post, if any.</param>
    /// <remarks>
    /// <para>A <see langword="null"/> entry in <paramref name="replies"/> is dropped. Neither
    /// <see cref="JsonRequiredAttribute"/> nor <see cref="System.Text.Json.JsonSerializerOptions.RespectNullableAnnotations"/>
    /// applies to a collection's element type, so a service can return one inside an otherwise well formed thread.</para>
    /// </remarks>
    [JsonConstructor]
    internal ThreadViewPost(PostView post, PostViewBase? parent, IReadOnlyList<PostViewBase>? replies, ThreadGateView? threadGate, ThreadContext? threadContext)
    {
        Post = post;
        Parent = parent;
        Replies = replies is null ? null : [.. replies.Where(reply => reply is not null)];
        ThreadGate = threadGate;
        ThreadContext = threadContext;
    }

    /// <summary>
    /// Gets the <see cref="PostView"/> of the post.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public PostView Post { get; init; }

    /// <summary>
    /// Gets the <see cref="PostViewBase"/> of the parent of the <see cref="Post"/>, if any.
    /// </summary>
    [JsonInclude]
    public PostViewBase? Parent { get; init; }

    /// <summary>
    /// Gets a collection of <see cref="PostViewBase"/> of replies to the <see cref="Post"/>, if any.
    /// </summary>
    [JsonInclude]
    public IReadOnlyList<PostViewBase>? Replies { get; init; }

    /// <summary>
    /// Gets a <see cref="ThreadGateView"/> over the thread gate applied to the post, if any.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("threadgate")]
    public ThreadGateView? ThreadGate { get; init; }

    /// <summary>
    /// Gets the <see cref="ThreadContext"/> for the post, if any.
    /// </summary>
    [JsonInclude]
    public ThreadContext? ThreadContext { get; init; }
}