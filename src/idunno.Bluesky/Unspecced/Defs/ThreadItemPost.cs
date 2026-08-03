// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Feed;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Unspecced;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates a post in a thread, and properties associated with the post in the context of the thread.
/// </summary>
public sealed record ThreadItemPost : ThreadItemValue
{
    [JsonConstructor]
    internal ThreadItemPost(
        PostView post,
        bool moreParents,
        int moreReplies,
        bool opThread,
        int? opThreadIndex,
        int? opThreadCount,
        bool hiddenByThreadGate,
        bool mutedByViewer)
    {
        Post = post;
        MoreParents = moreParents;
        MoreReplies = moreReplies;
        OpThread = opThread;
        OpThreadIndex = opThreadIndex;
        OpThreadCount = opThreadCount;
        HiddenByThreadGate = hiddenByThreadGate;
        MutedByViewer = mutedByViewer;
    }

    /// <summary>
    /// Gets the post associated with this thread item.
    /// </summary>
    [JsonRequired]
    public PostView Post { get; init; }

    /// <summary>
    /// Flag indicating whether this post has more parents that were not present in the response.
    /// </summary>
    [JsonRequired]
    public bool MoreParents { get; init; }

    /// <summary>
    /// Gets the numer of more replies the post has that were not present in the response. This is best-effort and might not be accurate.
    /// </summary>
    [JsonRequired]
    public int MoreReplies { get; init; }

    /// <summary>
    /// Flag indicating whether this post is part of a contiguous thread by the OP from the thread root.
    /// Sub-threads by OP deeper in the tree are not considered an OP thread.
    /// </summary>
    [JsonRequired]
    public bool OpThread { get; init; }

    /// <summary>
    /// Gets the 1-indexed position of this post within the contiguous OP thread. Only present when this post is part of the OP thread (see <see cref="OpThread"/>).
    /// </summary>
    public int? OpThreadIndex { get; init; }

    /// <summary>
    /// Gets the total number of posts in the contiguous OP thread that this post belongs to. Only present when this post is part of the OP thread (see <see cref="OpThread"/>).
    /// </summary>
    public int? OpThreadCount { get; init; }

    /// <summary>
    /// Flag indicating whether the threadgate created by the author indicates this post as a reply to be hidden for everyone consuming the thread.
    /// </summary>
    [JsonRequired]
    [JsonPropertyName("hiddenByThreadgate")]
    public bool HiddenByThreadGate { get; init; }

    /// <summary>
    /// Flag indicating whether the post is by an account muted by the viewer requesting it.
    /// </summary>
    [JsonRequired]
    public bool MutedByViewer { get; init; }
}
