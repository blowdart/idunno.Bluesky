// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

/// <summary>
/// An assurance rule that applies when an account meets the <paramref name="Age"/> threshold.
/// </summary>
/// <param name="Age">The <paramref name="Age"/> threshold.</param>
/// <param name="Access">The access level granted when this rule applies.</param>
public sealed record ConfigRegionRuleIfAssuredOverAge(
    [property: JsonRequired] int Age,
    [property: JsonRequired] AgeAssuranceAccess Access) : ConfigRegionRule;
