// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed.External;

/// <summary>
/// The properties for an embedded external media record.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true,
                 UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(Properties), typeDiscriminator: "app.bsky.embed.external#external")]
public record Properties
{
    /// <summary>
    /// Creates a new instance of <see cref="Properties"/>.
    /// </summary>
    /// <param name="uri">The external uri, as a string, for the link.</param>
    /// <param name="title">The title for the external link.</param>
    /// <param name="description">The description of the external link, if any.</param>
    /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
    /// <param name="associatedRefs">The collection of <see cref="StrongReference"/> representing the Atmosphere records for this external content, if any.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> or <paramref name="description"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "The Bluesky web app can create facets with illegal URIs, so a string is used to accommodate them.")]
    public Properties(string uri, string title, string description, Blob? thumbnail = null, IReadOnlyCollection<StrongReference>? associatedRefs = null) : base()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Uri = uri;
        Title = title;
        Description = description;
        Thumbnail = thumbnail;
        AssociatedRefs = associatedRefs;
    }

    /// <summary>
    /// Gets or sets the external uri, as a string.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is <see langword="null"/> or whitespace.</exception>
    /// <remarks><para>This property is a string to accommodate illegal URIs that the Bluesky web app can create. Validate the URI before using it.</para></remarks>
    [JsonInclude]
    [JsonRequired]
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The official Bluesky can create facets with illegal URIs, so this property is a string to accommodate those cases.")]
    public string Uri {
        get;

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the title for the external link.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    public string Title
    {
        get;

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the description of the external link.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    public string Description
    {
        get;

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the <see cref="Blob"/> to a thumbnail image for the external link.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("thumb")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Blob? Thumbnail { get; set; }

    /// <summary>
    /// Gets or sets a collection of <see cref="StrongReference"/>s to the Atmosphere records representing this external content, if they exist.
    /// </summary>
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyCollection<StrongReference>? AssociatedRefs { get; set; }
}