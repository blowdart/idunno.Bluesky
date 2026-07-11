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
    /// <param name="uri">The external <see cref="Uri"/> for the link.</param>
    /// <param name="title">The title for the external link.</param>
    /// <param name="description">The description of the external link, if any.</param>
    /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
    /// <param name="associatedRefs">The collection of <see cref="StrongReference"/> representing the Atmosphere records for this external content, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="title"/>, or <paramref name="description"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    [SuppressMessage(
        "ApiDesign",
        "RS0027:API with optional parameter(s) should have the most parameters amongst its public overloads",
        Justification = "Alternate constructions take URI as a string, so having the exact same parameters ensures consistency")]
    public Properties(Uri uri, string title, string description, Blob? thumbnail = null, IReadOnlyCollection<StrongReference>? associatedRefs = null) : base()
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);

        Uri = uri;
        Title = title;
        Description = description ?? string.Empty;
        Thumbnail = thumbnail;
        AssociatedRefs = associatedRefs;
    }

    /// <summary>
    /// Creates a new instance of <see cref="Properties"/>.
    /// </summary>
    /// <param name="uri">The external uri for the link.</param>
    /// <param name="title">The title for the external link.</param>
    /// <param name="description">The description of the external link.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="title"/>, or <paramref name="description"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Alternative constructor for convenience")]
    public Properties(string uri, string title, string description) : this(uri: new Uri(uri), title: title, description: description)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="Properties"/>.
    /// </summary>
    /// <param name="uri">The external uri for the link.</param>
    /// <param name="title">The title for the external link.</param>
    /// <param name="description">The description of the external link.</param>
    /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="title"/>, or <paramref name="description"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Alternative constructor for convenience")]
    public Properties(string uri, string title, string description, Blob? thumbnail) :
        this(uri: new Uri(uri), title: title, description: description, thumbnail: thumbnail)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="Properties"/>.
    /// </summary>
    /// <param name="uri">The external uri for the link.</param>
    /// <param name="title">The title for the external link.</param>
    /// <param name="description">The description of the external link.</param>
    /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
    /// <param name="associatedRefs">The collection of <see cref="StrongReference"/> representing the Atmosphere records for this external content, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="title"/>, or <paramref name="description"/> is <see langword="null"/>.</exception>
    public Properties(string uri, string title, string description, Blob? thumbnail, IReadOnlyCollection<StrongReference>? associatedRefs) :
        this(new Uri(uri), title, description, thumbnail, associatedRefs)
    {
    }

    /// <summary>
    /// Gets or sets the external <see cref="Uri"/>.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public Uri Uri { get; set; }

    /// <summary>
    /// Gets or sets the title for the external link.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the description of the external link.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public string Description { get; set; }

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