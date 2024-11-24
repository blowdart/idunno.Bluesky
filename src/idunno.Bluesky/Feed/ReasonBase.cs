// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using System.Text.Json;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// The base record for the reason in a <see cref="FeedViewPost"/>.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(ReasonRepost), typeDiscriminator: "app.bsky.feed.defs#reasonRepost")]
    [JsonDerivedType(typeof(ReasonPin), typeDiscriminator: "app.bsky.feed.defs#reasonPin")]
    public abstract record ReasonBase
    {
        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [JsonExtensionData]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be writable for json deserialization.")]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
    }
}
