// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using System.Text.Json;

namespace idunno.Bluesky.RichText
{
    /// <summary>
    /// The detailed features of a <see cref="Facet"/>.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(MentionFacetFeature), typeDiscriminator: "app.bsky.richtext.facet#mention")]
    [JsonDerivedType(typeof(LinkFacetFeature), typeDiscriminator: "app.bsky.richtext.facet#link")]
    [JsonDerivedType(typeof(TagFacetFeature), typeDiscriminator: "app.bsky.richtext.facet#tag")]
    public record FacetFeature
    {
        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [JsonExtensionData]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be writable for json deserialization")]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
    }
}
