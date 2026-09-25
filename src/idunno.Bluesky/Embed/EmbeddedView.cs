// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Base record for view over embedded records. Used for json polymorphism.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(EmbeddedImagesView), typeDiscriminator: EmbeddedViewTypeDiscriminators.ImagesView)]
[JsonDerivedType(typeof(EmbeddedExternalView), typeDiscriminator: EmbeddedViewTypeDiscriminators.ExternalView)]
[JsonDerivedType(typeof(EmbeddedVideoView), typeDiscriminator: EmbeddedViewTypeDiscriminators.VideoView)]
[JsonDerivedType(typeof(EmbeddedRecordView), typeDiscriminator: EmbeddedViewTypeDiscriminators.EmbedView)]
[JsonDerivedType(typeof(EmbeddedRecordWithMediaView), typeDiscriminator: EmbeddedViewTypeDiscriminators.RecordWithMediaView)]
[JsonDerivedType(typeof(Gallery.View), typeDiscriminator: EmbeddedViewTypeDiscriminators.GalleryView)]
public record EmbeddedView : View
{
}