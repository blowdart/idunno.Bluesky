// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

/// <summary>
/// A rule that determines the access level granted in a region.
/// </summary>
[JsonConverter(typeof(ConfigRegionRuleConverter))]
public record ConfigRegionRule
{
    /// <summary>
    /// Gets fields from an unrecognized age-assurance rule.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for JSON extension data deserialization.")]
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
}
