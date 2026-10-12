// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Represents the complete response from <c>app.bsky.embed.getEmbedExternalView</c>.
/// </summary>
/// <param name="View">The legacy hydrated view, when one is available.</param>
/// <param name="Data">The preferred hydrated embed data, including its lexicon <c>$type</c> discriminator.</param>
/// <param name="AssociatedRefs">The strong references selected for persistence after URL validation succeeds.</param>
/// <param name="AssociatedRecords">The raw records selected for persistence after URL validation succeeds.</param>
/// <param name="UnverifiedAssociatedRefs">The references returned when URL validation fails; these must not be written to a post.</param>
/// <param name="UnverifiedAssociatedRecords">The raw records returned when URL validation fails; these must not be written to a post.</param>
public sealed record EmbeddedExternalViewResponse(
    EmbeddedExternalView? View,
    JsonElement? Data,
    IReadOnlyList<StrongReference>? AssociatedRefs,
    IReadOnlyList<JsonElement>? AssociatedRecords,
    IReadOnlyList<StrongReference>? UnverifiedAssociatedRefs,
    IReadOnlyList<JsonElement>? UnverifiedAssociatedRecords);
