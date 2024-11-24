// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace idunno.Bluesky.Embed
{
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(EmbeddedExternal), typeDiscriminator: "app.bsky.embed.external")]
    [JsonDerivedType(typeof(EmbeddedImages), typeDiscriminator: "app.bsky.embed.images")]
    [JsonDerivedType(typeof(EmbeddedVideo), typeDiscriminator: "app.bsky.embed.video")]
    public record EmbeddedMediaBase : EmbeddedBase
    {
    }
}
