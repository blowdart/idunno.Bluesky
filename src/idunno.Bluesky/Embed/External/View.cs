// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Embed.External;

/// <summary>
/// The view properties on a view over an <see cref="EmbeddedExternal"/> record.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(View), typeDiscriminator: "app.bsky.embed.external#viewExternal")]
public record View
{
    /// <summary>
    /// Creates a new instance of <see cref="View"/>.
    /// </summary>
    /// <param name="uri">The external <paramref name="uri"/>.</param>
    /// <param name="title">The title for the external link.</param>
    /// <param name="description">The description of the external link.</param>
    /// <param name="thumbnailUri">The <see cref="Uri"/> to a thumbnail image for the external link.</param>
    /// <param name="createdAt">The optional date and time the external link was created.</param>
    /// <param name="updatedAt">The optional date and time the external link was last updated.</param>
    /// <param name="readingTime">The optional estimated reading time in minutes for the external link, if applicable.</param>
    /// <param name="labels">The optional labels for the external link.</param>
    /// <param name="source">The source view for the external link, if available</param>
    /// <param name="associatedRefs">The optional associated references for the external link.</param>
    /// <param name="associatedProfiles"> Profiles of the owners of the Atmosphere records that backed this view, if available.</param>
    [JsonConstructor]
    internal View(
        Uri uri,
        string title,
        string description,
        Uri? thumbnailUri,
        DateTimeOffset? createdAt,
        DateTimeOffset? updatedAt,
        int? readingTime,
        IReadOnlyCollection<Label>? labels,
        Source.View source,
        IReadOnlyCollection<StrongReference>? associatedRefs,
        IReadOnlyCollection<ProfileViewBasic>? associatedProfiles) : base()
    {
        Uri = uri;
        Title = title;
        Description = description;
        ThumbnailUri = thumbnailUri;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ReadingTime = readingTime;
        Labels = labels;
        Source = source;
        AssociatedRefs = associatedRefs;
        AssociatedProfiles = associatedProfiles;
    }

    /// <summary>
    /// Gets the external <see cref="Uri"/>.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public Uri Uri { get; init; }

    /// <summary>
    /// The title for the external link.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public string Title { get; init; }

    /// <summary>
    /// The description of the external link.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public string Description { get; init; }

    /// <summary>
    /// The <see cref="Uri"/> to a thumbnail image for the external link.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("thumb")]
    public Uri? ThumbnailUri { get; init; }

    /// <summary>
    /// Gets when the external content was created, if available. Example: a publication date, for an article.
    /// </summary>
    [JsonInclude]
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Gets when the external content was last updated, if available.
    /// </summary>
    [JsonInclude]
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Gets the estimated reading time in minutes, if applicable and available.
    /// </summary>
    [JsonInclude]
    public int? ReadingTime { get; init; }

    /// <summary>
    /// Gets the labels for the source, if any.
    /// </summary>
    [JsonInclude]
    public IReadOnlyCollection<Label>? Labels { get; init; }

    /// <summary>
    /// Gets the view over the source of the external embed, if any.
    /// </summary>
    [JsonInclude]
    public Source.View Source { get; init; }

    /// <summary>
    /// Gets the <see cref="AtUri"/> of the Atmosphere records that backed this view, if any.
    /// </summary>
    [JsonInclude]
    public IReadOnlyCollection<StrongReference>? AssociatedRefs { get; init; }

    /// <summary>
    /// Gets the <see cref="ProfileViewBasic"/> of the owners of the Atmosphere records that backed this view, if any.
    /// </summary>
    [JsonInclude]
    public IReadOnlyCollection<ProfileViewBasic>? AssociatedProfiles { get; init; }

    /// <summary>
    /// A list of keys and element data that do not map to any strongly typed properties.
    /// </summary>
    [NotNull]
    [ExcludeFromCodeCoverage]
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
}
