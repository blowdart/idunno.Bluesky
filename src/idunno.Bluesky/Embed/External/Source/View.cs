// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed.External.Source;

/// <summary>
/// Gets a view over the external source for an embedded external object.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(View), typeDiscriminator: "app.bsky.embed.external#viewExternalSource")]
public record View :Bluesky.View
{
    [JsonConstructor]
    internal View(Uri uri, Uri? icon, string title, string? description, Theme? theme) : base()
    {
        Uri = uri;
        Icon = icon;
        Title = title;
        Description = description;
        Theme = theme;
    }

    /// <summary>
    /// Gets the URI of the source. For example, the https:// URL of a site.standard.publication record.
    /// </summary>
    [JsonRequired]
    public Uri Uri { get; init; }

    /// <summary>
    /// Gets the fully-qualified URL where an icon representing the source can be fetched. For example, CDN location provided by the App View.
    /// </summary>
    public Uri? Icon { get; init; }

    /// <summary>
    /// Gets the title of the external source record. For example, the title of a site.standard.publication record.
    /// </summary>
    [JsonRequired]
    public string Title { get; init; }

    /// <summary>
    /// Gets the description of the external source record, if available.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the theme of the external source record, if any.
    /// </summary>
    public Theme? Theme { get; init; }
}
