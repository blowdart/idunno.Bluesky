// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky.Bookmarks
{
    /// <summary>
    /// Encapsulates data about a bookmark.
    /// </summary>
    /// <param name="Subject">Gets the <see cref="StrongReference"/> to the bookmarked record.</param>
    /// <param name="CreatedAt">The <see cref="DateTimeOffset"/> the bookmark was created, if known.</param>
    /// <param name="Item">The item bookmarked. May be of type <see cref="PostView"/>, <see cref="BlockedPost"/> or <see cref="NotFoundPost"/>.</param>
    public sealed record BookmarkView(StrongReference Subject, DateTimeOffset? CreatedAt, PostViewBase Item) : View
    {
    }
}
