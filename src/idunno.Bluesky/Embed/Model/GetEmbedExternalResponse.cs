// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed.Model;
/// <summary>
/// The response from app.bsky.embed.getEmbedExternalView.
/// </summary>
internal sealed record GetEmbedExternalResponse(
    EmbeddedExternalView? View,
    JsonElement? Data,
    IReadOnlyList<StrongReference>? AssociatedRefs,
    IReadOnlyList<JsonElement>? AssociatedRecords,
    IReadOnlyList<StrongReference>? UnverifiedAssociatedRefs,
    IReadOnlyList<JsonElement>? UnverifiedAssociatedRecords);