// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Embed.External;

/// <summary>
/// Represents a view of an external article publication.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(ArticlePublicationView), typeDiscriminator: "app.bsky.embed.external#viewArticlePublication")]
public record ArticlePublicationView : Bluesky.View
{
    /// <summary>
    /// Gets the URI of the publication, as a string.
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
    /// Gets the title of the publication.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The value is <see langword="null"/>.</exception>
    [JsonRequired]
    public string Title
    {
        get;
        init => field = ExternalViewCollection.Require(value, nameof(Title));
    } = null!;

    /// <summary>
    /// Gets the description of the publication.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The value is <see langword="null"/>.</exception>
    [JsonRequired]
    public string Description
    {
        get;
        init => field = ExternalViewCollection.Require(value, nameof(Description));
    } = null!;

    /// <summary>
    /// Gets the date and time when the publication was created, if available.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Gets the date and time when the publication was last updated, if available.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Gets the labels applied to the publication, if any.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<Label>? Labels
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(Labels));
    }

    /// <summary>
    /// Gets the strong references associated with the publication, if any.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<StrongReference>? AssociatedRefs
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(AssociatedRefs));
    }

    /// <summary>
    /// Gets the profiles associated with the publication, if any.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<ProfileViewBasic>? AssociatedProfiles
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(AssociatedProfiles));
    }

    /// <summary>
    /// Gets the publication logo URI, if available.
    /// </summary>
    public Uri? Logo { get; init; }

    /// <summary>
    /// Gets the publication image URI, if available.
    /// </summary>
    public Uri? Image { get; init; }

    /// <summary>
    /// Gets the publication theme, if available.
    /// </summary>
    public ArticlePublicationTheme? Theme { get; init; }

    /// <summary>
    /// Gets the number of subscribers, if available.
    /// </summary>
    public int? SubscriptionCount { get; init; }

    /// <summary>
    /// Gets up to three available profile previews of subscribers, if any.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<ProfileViewBasic>? Subscribers
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(Subscribers));
    }
}
