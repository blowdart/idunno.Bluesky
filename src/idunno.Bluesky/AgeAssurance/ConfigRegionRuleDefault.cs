// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

/// <summary>
/// An age-assurance rule that applies by default.
/// </summary>
/// <param name="Access">The access level granted when this rule applies.</param>
public sealed record ConfigRegionRuleDefault([property: JsonRequired] AgeAssuranceAccess Access) : ConfigRegionRule;
