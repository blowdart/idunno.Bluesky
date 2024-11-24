// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// Base class for embedded records.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(EmbeddedExternal), typeDiscriminator: "app.bsky.embed.external")]
    [JsonDerivedType(typeof(EmbeddedImages), typeDiscriminator: "app.bsky.embed.images")]
    [JsonDerivedType(typeof(EmbeddedVideo), typeDiscriminator: "app.bsky.embed.video")]
    [JsonDerivedType(typeof(EmbeddedRecord), typeDiscriminator: "app.bsky.embed.record")]
    [JsonDerivedType(typeof(EmbeddedRecordWithMedia), typeDiscriminator: "app.bsky.embed.recordWithMedia")]

    public record EmbeddedBase : AtProtoRecordValue
    {
    }
}
