// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Embed.Gallery;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Base record for view over embedded records. Used for json polymorphism.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(EmbeddedImagesView), typeDiscriminator: "app.bsky.embed.images#view")]
[JsonDerivedType(typeof(EmbeddedExternalView), typeDiscriminator: "app.bsky.embed.external#view")]
[JsonDerivedType(typeof(EmbeddedVideoView), typeDiscriminator: "app.bsky.embed.video#view")]
[JsonDerivedType(typeof(EmbeddedRecordView), typeDiscriminator: "app.bsky.embed.record#view")]
[JsonDerivedType(typeof(EmbeddedRecordWithMediaView), typeDiscriminator: "app.bsky.embed.recordWithMedia#view")]
[JsonDerivedType(typeof(Gallery.View), typeDiscriminator: "app.bsky.embed.gallery#view")]
public record EmbeddedView : View
{
}