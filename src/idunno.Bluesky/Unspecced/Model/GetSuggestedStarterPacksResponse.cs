// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Graph;

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Unspecced.Model;

internal sealed record GetSuggestedStarterPacksResponse([property: JsonRequired] ICollection<StarterPackView> StarterPacks)
{
}