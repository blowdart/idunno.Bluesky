// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Embed.External;

/// <summary>
/// Represents a view of an article embedded from an external source.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(ArticleView), typeDiscriminator: "app.bsky.embed.external#viewArticle")]
public record ArticleView : Bluesky.View
{
    /// <summary>
    /// Gets the URI of the article, as a string.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The value is <see langword="null"/>.</exception>
    [JsonRequired]
    [JsonPropertyName("uri")]
    public string Identity
    {
        get;
        init => field = ExternalViewCollection.Require(value, nameof(Identity));
    } = null!;

    /// <summary>
    /// Gets the title of the article.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The value is <see langword="null"/>.</exception>
    [JsonRequired]
    public string Title
    {
        get;
        init => field = ExternalViewCollection.Require(value, nameof(Title));
    } = null!;

    /// <summary>
    /// Gets the description of the article.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The value is <see langword="null"/>.</exception>
    [JsonRequired]
    public string Description
    {
        get;
        init => field = ExternalViewCollection.Require(value, nameof(Description));
    } = null!;

    /// <summary>
    /// Gets the date and time when the article was created, if available.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Gets the date and time when the article was last updated, if available.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Gets the labels applied to the article, if any.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<Label>? Labels
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(Labels));
    }

    /// <summary>
    /// Gets the strong references associated with the article, if any.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<StrongReference>? AssociatedRefs
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(AssociatedRefs));
    }

    /// <summary>
    /// Gets the profiles associated with the article, if any.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<ProfileViewBasic>? AssociatedProfiles
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(AssociatedProfiles));
    }

    /// <summary>
    /// Gets the article image URI, if available.
    /// </summary>
    public Uri? Image { get; init; }

    /// <summary>
    /// Gets the publication that published the article, if available.
    /// </summary>
    public ArticlePublicationView? Publisher { get; init; }

    /// <summary>
    /// Gets the estimated reading time in minutes, if available.
    /// </summary>
    public int? ReadingTime { get; init; }

    /// <summary>
    /// Gets the number of likes, if available.
    /// </summary>
    public int? LikeCount { get; init; }

    /// <summary>
    /// Gets up to three available profile previews of likers, if any.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<ProfileViewBasic>? Likers
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(Likers));
    }
}
