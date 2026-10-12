// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

/// <summary>
/// An assurance rule that applies to accounts created after the <paramref name="Date"/> threshold.
/// </summary>
/// <param name="Date">The <paramref name="Date"/> threshold.</param>
/// <param name="Access">The access level granted when this rule applies.</param>
public sealed record ConfigRegionRuleIfAccountNewerThan(
    [property: JsonRequired] DateTimeOffset Date,
    [property: JsonRequired] AgeAssuranceAccess Access) : ConfigRegionRule;
