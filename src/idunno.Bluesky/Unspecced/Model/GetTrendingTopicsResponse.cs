// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Unspecced.Model;

internal sealed record GetTrendingTopicsResponse([property: JsonRequired] IReadOnlyCollection<TrendingTopic> Topics, [property: JsonRequired] IReadOnlyCollection<TrendingTopic> Suggested)
{
}