// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// A <see cref="PagedViewReadOnlyCollection{T}"/> of <see cref="FeedViewPost"/>s in the requesting account's home timeline
    /// </summary>
    public class Timeline : PagedViewReadOnlyCollection<FeedViewPost>
    {
        internal Timeline() : this(new List<FeedViewPost>(), null)
        {
        }

        internal Timeline(IList<FeedViewPost> list, string? cursor = null) : base(list, cursor)
        {
        }

        internal Timeline(ICollection<FeedViewPost> collection, string? cursor = null) : this(new List<FeedViewPost>(collection), cursor)
        {
        }
    }
}
