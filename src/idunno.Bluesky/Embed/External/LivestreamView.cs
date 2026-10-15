// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Embed.External;

/// <summary>
/// Represents a view of an external livestream.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(LivestreamView), typeDiscriminator: "app.bsky.embed.external#viewLivestream")]
public record LivestreamView : Bluesky.View
{
    /// <summary>
    /// Gets the URI of the livestream, as a string.
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
    /// Gets the title of the livestream.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The value is <see langword="null"/>.</exception>
    [JsonRequired]
    public string Title
    {
        get;
        init => field = ExternalViewCollection.Require(value, nameof(Title));
    } = null!;

    /// <summary>
    /// Gets the description of the livestream.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The value is <see langword="null"/>.</exception>
    [JsonRequired]
    public string Description
    {
        get;
        init => field = ExternalViewCollection.Require(value, nameof(Description));
    } = null!;

    /// <summary>
    /// Gets the date and time when the livestream was created, if available.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Gets the date and time when the livestream was last updated, if available.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Gets the labels applied to the livestream, if any.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<Label>? Labels
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(Labels));
    }

    /// <summary>
    /// Gets the strong references associated with the livestream, if any.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<StrongReference>? AssociatedRefs
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(AssociatedRefs));
    }

    /// <summary>
    /// Gets the profiles associated with the livestream, if any.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The collection contains a <see langword="null"/> entry.</exception>
    public IReadOnlyCollection<ProfileViewBasic>? AssociatedProfiles
    {
        get;
        init => field = ExternalViewCollection.Copy(value, nameof(AssociatedProfiles));
    }

    /// <summary>
    /// Gets the livestream image URI, if available.
    /// </summary>
    public Uri? Image { get; init; }

    /// <summary>
    /// Gets a value indicating whether the livestream is active.
    /// </summary>
    [JsonRequired]
    public bool Active { get; init; }

    /// <summary>
    /// Gets the date and time when the livestream started, if available.
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// Gets the date and time when the livestream ended, if available.
    /// </summary>
    public DateTimeOffset? EndedAt { get; init; }
}
