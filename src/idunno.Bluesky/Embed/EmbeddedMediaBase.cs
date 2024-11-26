// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// Base record for various embedded media records in a Bluesky post.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(EmbeddedExternal), typeDiscriminator: "app.bsky.embed.external")]
    [JsonDerivedType(typeof(EmbeddedImages), typeDiscriminator: "app.bsky.embed.images")]
    [JsonDerivedType(typeof(EmbeddedVideo), typeDiscriminator: "app.bsky.embed.video")]
    public record EmbeddedMediaBase : EmbeddedBase
    {
    }
}
