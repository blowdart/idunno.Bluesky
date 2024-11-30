// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// Base record for various embedded media records in a Bluesky post.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(EmbeddedExternal), typeDiscriminator: EmbeddedRecordTypeDiscriminators.External)]
    [JsonDerivedType(typeof(EmbeddedImages), typeDiscriminator: EmbeddedRecordTypeDiscriminators.Images)]
    [JsonDerivedType(typeof(EmbeddedVideo), typeDiscriminator: EmbeddedRecordTypeDiscriminators.Video)]
    [JsonDerivedType(typeof(EmbeddedRecordWithMedia), typeDiscriminator: EmbeddedRecordTypeDiscriminators.RecordWithMedia)]
    public record EmbeddedMediaBase : EmbeddedBase
    {
    }
}
