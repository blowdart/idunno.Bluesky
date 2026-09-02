// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Record;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Drafts;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates a local embedded image in a draft post.
/// </summary>
/// <param name="Items">The collection of embedded images in the gallery.</param>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = false, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(DraftEmbedGallery), typeDiscriminator: "app.bsky.draft.defs#draftEmbedGallery")]
public record DraftEmbedGallery(IReadOnlyCollection<DraftEmbedImage>? Items)
{
}
