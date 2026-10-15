// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

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
    IReadOnlyList<JsonElement>? UnverifiedAssociatedRecords)
{
    /// <summary>
    /// Deserializes the preferred embed data using caller-provided JSON type metadata.
    /// </summary>
    /// <typeparam name="T">The type to which the embed data is projected.</typeparam>
    /// <param name="jsonTypeInfo">The source-generated JSON type metadata for <typeparamref name="T"/>.</param>
    /// <returns>The typed embed data, or <see langword="default"/> when the response has no data.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="jsonTypeInfo"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The embed data cannot be deserialized as <typeparamref name="T"/>.</exception>
    /// <remarks>
    /// <para>The original <see cref="Data"/> remains available unchanged, so callers can retain data with an unknown
    /// discriminator or project it more than once. Passing generated metadata keeps the projection compatible with
    /// applications that disable reflection-based JSON serialization.</para>
    /// </remarks>
    public T? DeserializeData<T>(JsonTypeInfo<T> jsonTypeInfo)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

        return Data is JsonElement data ? data.Deserialize(jsonTypeInfo) : default;
    }
}
