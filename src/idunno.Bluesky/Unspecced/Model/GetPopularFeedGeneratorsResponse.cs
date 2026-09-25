// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Feed;

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Unspecced.Model;

internal sealed record GetPopularFeedGeneratorsResponse([property: JsonRequired] ICollection<GeneratorView> Feeds, string? Cursor)
{
}