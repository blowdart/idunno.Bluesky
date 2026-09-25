// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Bookmarks.Model;

internal sealed record GetBookmarksResponse([property: JsonRequired] ICollection<BookmarkView> Bookmarks, string? Cursor)
{
}