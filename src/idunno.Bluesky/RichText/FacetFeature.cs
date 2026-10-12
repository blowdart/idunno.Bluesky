// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.RichText;

/// <summary>
/// The detailed features of a <see cref="Facet"/>.
/// </summary>
[JsonConverter(typeof(Json.FacetFeatureConverter))]
public record FacetFeature
{
    /// <summary>
    /// A list of keys and element data that do not map to any strongly typed properties.
    /// </summary>
    [JsonExtensionData]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be writable for json deserialization")]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
}