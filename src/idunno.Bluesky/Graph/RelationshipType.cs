// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.Json;

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Graph
{
    /// <summary>
    /// Base record for relationship types between two actors.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(Relationship), typeDiscriminator: "app.bsky.graph.defs#relationship")]
    [JsonDerivedType(typeof(NotFoundActor), typeDiscriminator: "app.bsky.graph.defs#notFoundActor")]
    public record RelationshipType : AtProtoObject
    {
        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [NotNull]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Must be writable for JSON deserialization.")]
        [JsonExtensionData]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
    }
}
