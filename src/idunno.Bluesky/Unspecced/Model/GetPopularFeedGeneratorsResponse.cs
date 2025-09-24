// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Feed;

namespace idunno.Bluesky.Unspecced.Model
{
    internal sealed record GetPopularFeedGeneratorsResponse(ICollection<GeneratorView> Feeds, string? Cursor)
    {
    }
}
