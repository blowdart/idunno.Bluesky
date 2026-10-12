// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

/// <summary>
/// A rule that determines the access level granted in a region.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(ConfigRegionRuleDefault), "app.bsky.ageassurance.defs#configRegionRuleDefault")]
[JsonDerivedType(typeof(ConfigRegionRuleIfDeclaredOverAge), "app.bsky.ageassurance.defs#configRegionRuleIfDeclaredOverAge")]
[JsonDerivedType(typeof(ConfigRegionRuleIfDeclaredUnderAge), "app.bsky.ageassurance.defs#configRegionRuleIfDeclaredUnderAge")]
[JsonDerivedType(typeof(ConfigRegionRuleIfAssuredOverAge), "app.bsky.ageassurance.defs#configRegionRuleIfAssuredOverAge")]
[JsonDerivedType(typeof(ConfigRegionRuleIfAssuredUnderAge), "app.bsky.ageassurance.defs#configRegionRuleIfAssuredUnderAge")]
[JsonDerivedType(typeof(ConfigRegionRuleIfAccountNewerThan), "app.bsky.ageassurance.defs#configRegionRuleIfAccountNewerThan")]
[JsonDerivedType(typeof(ConfigRegionRuleIfAccountOlderThan), "app.bsky.ageassurance.defs#configRegionRuleIfAccountOlderThan")]
public record ConfigRegionRule
{
    /// <summary>
    /// Gets fields from an unrecognized age-assurance rule.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for JSON extension data deserialization.")]
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
}
