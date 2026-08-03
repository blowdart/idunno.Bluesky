// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

using idunno.Bluesky.Feed;
using idunno.Bluesky.Unspecced.Model;

namespace idunno.Bluesky.Unspecced;

/// <summary>
/// Encapsulates a thread of posts and properties of the thread.
/// </summary>
[Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
public class PostThreadV2
{
    internal PostThreadV2(ICollection<ThreadItem> items, ThreadGateView? threadGate, bool hasOtherReplies)
    {
        Thread = new ReadOnlyCollection<ThreadItem>([.. items]);
        ThreadGate = threadGate;
        HasOtherReplies = hasOtherReplies;
    }

    internal PostThreadV2(GetPostThreadV2Response response) : this(response.Thread, response.Threadgate, response.HasOtherReplies)
    {
    }

    /// <summary>
    /// Gets a flat list of thread items. The depth of each item is indicated by the depth property inside the item.
    /// </summary>
    public IReadOnlyCollection<ThreadItem> Thread { get; init; }

    /// <summary>
    /// Gets the thread gate for the thread, if any.
    /// </summary>
    public ThreadGateView? ThreadGate { get; init; }

    /// <summary>
    /// Flag indicating if there are other replies to the thread that are not included in this response.
    /// </summary>
    public bool HasOtherReplies { get; init; }
}
