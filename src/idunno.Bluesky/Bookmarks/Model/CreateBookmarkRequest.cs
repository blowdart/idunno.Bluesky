// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Bookmarks.Model
{
    internal sealed record CreateBookmarkRequest(AtUri Uri, Cid Cid)
    {
    }
}
