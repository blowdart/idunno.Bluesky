// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

/// <summary>
/// The age-assurance configuration containing each <paramref name="Regions"/> entry.
/// </summary>
/// <param name="Regions">The per-region age-assurance configurations.</param>
public sealed record Config([property: JsonRequired] IReadOnlyCollection<ConfigRegion> Regions);
