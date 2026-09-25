// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Base class for embedded records.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(EmbeddedExternal), typeDiscriminator: EmbeddedRecordTypeDiscriminators.External)]
[JsonDerivedType(typeof(EmbeddedImages), typeDiscriminator: EmbeddedRecordTypeDiscriminators.Images)]
[JsonDerivedType(typeof(EmbeddedVideo), typeDiscriminator: EmbeddedRecordTypeDiscriminators.Video)]
[JsonDerivedType(typeof(EmbeddedRecord), typeDiscriminator: EmbeddedRecordTypeDiscriminators.Record)]
[JsonDerivedType(typeof(EmbeddedRecordWithMedia), typeDiscriminator: EmbeddedRecordTypeDiscriminators.RecordWithMedia)]
[JsonDerivedType(typeof(EmbeddedGallery), typeDiscriminator: EmbeddedRecordTypeDiscriminators.Gallery)]
public record EmbeddedBase : AtProtoRecord
{
}