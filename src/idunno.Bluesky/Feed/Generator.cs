// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Record;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Feed;

/// <summary>
/// Declares the existence and metadata of a feed generator.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true,
                 UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(Generator), typeDiscriminator: RecordType.Generator)]
public record Generator : BlueskyRecord
{
    /// <summary>
    /// Gets the DID of the feed generator service.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public required Did Did { get; init; }

    /// <summary>
    /// Gets the display name of the feed generator.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the description of the feed generator, if any.
    /// </summary>
    [JsonInclude]
    public string? Description { get; init; }

    /// <summary>
    /// Gets any rich text facets applied to the description.
    /// </summary>
    [JsonInclude]
    public IReadOnlyList<Facet>? DescriptionFacets { get; init; }

    /// <summary>
    /// Gets the avatar of the feed generator, if any.
    /// </summary>
    [JsonInclude]
    public Blob? Avatar { get; init; }

    /// <summary>
    /// Gets a value indicating whether the feed generator accepts interaction feedback.
    /// </summary>
    [JsonInclude]
    public bool? AcceptsInteractions { get; init; }

    /// <summary>
    /// Gets any self-labels applied to the feed generator.
    /// </summary>
    [JsonInclude]
    public SelfLabels? Labels { get; init; }

    /// <summary>
    /// Gets the content mode for the feed generator, if any.
    /// </summary>
    [JsonInclude]
    public GeneratorContentMode? ContentMode { get; init; }

    /// <summary>
    /// Gets the date and time when the feed generator record was created.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public required DateTimeOffset CreatedAt { get; init; }
}
