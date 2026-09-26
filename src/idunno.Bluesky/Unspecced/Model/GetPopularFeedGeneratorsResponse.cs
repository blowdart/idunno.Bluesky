// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Feed;

namespace idunno.Bluesky.Unspecced.Model;

internal sealed record GetPopularFeedGeneratorsResponse([property: JsonRequired] ICollection<GeneratorView> Feeds, string? Cursor)
{
}