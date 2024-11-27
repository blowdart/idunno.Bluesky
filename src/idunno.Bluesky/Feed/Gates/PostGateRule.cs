// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed.Gates
{
    /// <summary>
    /// Base record for Post Gate rules. Used for json polymorphism.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = false, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
    [JsonDerivedType(typeof(DisableEmbeddingRule), typeDiscriminator: "app.bsky.feed.postgate#disableRule")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty", Justification = "Needed to discriminate on json type discriminator")]
    public record PostGateRule
    {
    }
}
