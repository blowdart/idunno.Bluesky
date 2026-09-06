// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Graph.Model;

internal sealed record SearchStarterPacksV2Response([field: JsonRequired] ICollection<StarterPackView> StarterPacks, int? HitsTotal, string? Cursor)
{
}