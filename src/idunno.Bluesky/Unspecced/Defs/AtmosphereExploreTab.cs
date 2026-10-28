// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.Bluesky.Embed.External;

#pragma warning disable IDE0130 // Preserve the established namespace independently of folder layout.
namespace idunno.Bluesky.Unspecced;
#pragma warning restore IDE0130

/// <summary>
/// Represents curated Atmosphere Explore content.
/// </summary>
public sealed record AtmosphereExploreTab
{
    /// <summary>
    /// Gets the optional announcement banner.
    /// </summary>
    public AtmosphereAnnouncementBanner? AnnouncementBanner { get; init; }

    /// <summary>
    /// Gets the curated articles.
    /// </summary>
    [JsonRequired]
    public IReadOnlyList<AtmosphereArticleItem> Articles
    {
        get;
        init => field = RequiredItems(value);
    } = [];

    /// <summary>
    /// Gets the curated publications.
    /// </summary>
    [JsonRequired]
    public IReadOnlyList<AtmospherePublicationItem> Publications
    {
        get;
        init => field = RequiredItems(value);
    } = [];

    /// <summary>
    /// Gets the curated photo galleries.
    /// </summary>
    [JsonRequired]
    public IReadOnlyList<AtmosphereGalleryItem> Photos
    {
        get;
        init => field = RequiredItems(value);
    } = [];

    /// <summary>
    /// Gets the curated livestreams.
    /// </summary>
    [JsonRequired]
    public IReadOnlyList<AtmosphereLivestreamItem> Livestreams
    {
        get;
        init => field = RequiredItems(value);
    } = [];

    /// <summary>
    /// Gets the curated applications.
    /// </summary>
    [JsonRequired]
    public IReadOnlyList<AtmosphereAppCard> Apps
    {
        get;
        init => field = RequiredItems(value);
    } = [];

    private static System.Collections.ObjectModel.ReadOnlyCollection<T> RequiredItems<T>(IReadOnlyList<T> items)
    {
        if (items is null || items.Any(item => item is null
            or AtmosphereArticleItem { View: null }
            or AtmospherePublicationItem { View: null }
            or AtmosphereGalleryItem { View: null }
            or AtmosphereLivestreamItem { View: null }))
        {
            throw new JsonException("Explore collections and their items' hydrated views must be non-null.");
        }

        return Array.AsReadOnly(items.ToArray());
    }
}

/// <summary>
/// Represents a curated article.
/// </summary>
/// <param name="Featured">A flag indicating whether the article is featured.</param>
/// <param name="View">The hydrated article.</param>
public sealed record AtmosphereArticleItem([property: JsonRequired] bool Featured, [property: JsonRequired] ArticleView View);

/// <summary>
/// Represents a curated publication.
/// </summary>
/// <param name="Featured">A flag indicating whether the publication is featured.</param>
/// <param name="View">The hydrated publication.</param>
public sealed record AtmospherePublicationItem([property: JsonRequired] bool Featured, [property: JsonRequired] ArticlePublicationView View);

/// <summary>
/// Represents a curated photo gallery.
/// </summary>
/// <param name="Featured">A flag indicating whether the gallery is featured.</param>
/// <param name="View">The hydrated gallery.</param>
public sealed record AtmosphereGalleryItem([property: JsonRequired] bool Featured, [property: JsonRequired] GalleryView View);

/// <summary>
/// Represents a curated livestream.
/// </summary>
/// <param name="Featured">A flag indicating whether the livestream is featured.</param>
/// <param name="View">The hydrated livestream.</param>
public sealed record AtmosphereLivestreamItem([property: JsonRequired] bool Featured, [property: JsonRequired] LivestreamView View);

/// <summary>
/// Represents an Atmosphere announcement banner.
/// </summary>
public sealed record AtmosphereAnnouncementBanner
{
    /// <summary>
    /// Gets the banner identifier.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Gets the banner title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the banner description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the destination URI.
    /// </summary>
    public Uri? Url { get; init; }

    /// <summary>
    /// Gets the banner image URI.
    /// </summary>
    public Uri? Image { get; init; }

    /// <summary>
    /// Gets the overlay color.
    /// </summary>
    public string? OverlayColor { get; init; }

    /// <summary>
    /// Gets the text color.
    /// </summary>
    public string? TextColor { get; init; }
}

/// <summary>
/// Represents a curated Atmosphere application.
/// </summary>
public sealed record AtmosphereAppCard
{
    /// <summary>
    /// Gets the application identifier.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Gets the application title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the application description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the application logo URI.
    /// </summary>
    public Uri? Logo { get; init; }

    /// <summary>
    /// Gets the background image URI.
    /// </summary>
    public Uri? BackgroundImage { get; init; }

    /// <summary>
    /// Gets the overlay color.
    /// </summary>
    public string? OverlayColor { get; init; }

    /// <summary>
    /// Gets the text color.
    /// </summary>
    public string? TextColor { get; init; }

    /// <summary>
    /// Gets the application URI.
    /// </summary>
    public Uri? Url { get; init; }

    /// <summary>
    /// Gets the application category.
    /// </summary>
    public string? Category { get; init; }
}
