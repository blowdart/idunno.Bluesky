// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Bookmarks.Model
{
    internal sealed record GetBookmarksResponse(ICollection<BookmarkView> Bookmarks, string? Cursor)
    {
    }
}
