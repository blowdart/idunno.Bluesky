// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed;

/// <summary>
/// A representation of some externally linked content (eg, a URL and 'card'), embedded in a Bluesky record (eg, a post)
/// </summary>
public sealed record EmbeddedExternal : EmbeddedBase
{
    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedExternal"/>
    /// </summary>
    /// <param name="external">The properties for the externally linked content.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="external"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public EmbeddedExternal(External.Properties external)
    {
        ArgumentNullException.ThrowIfNull(external);
        External = external;
    }

    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedExternal"/>.
    /// </summary>
    /// <param name="uri">The external <see cref="Uri"/> for the link.</param>
    /// <param name="title">The title for the external link.</param>
    /// <param name="description">The description of the external link, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="title"/>, or <paramref name="description"/> is <see langword="null"/>.</exception>
    public EmbeddedExternal(Uri uri, string title, string description) :
        this(new External.Properties(uri, title, description, thumbnail: null))
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
    }

    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedExternal"/>.
    /// </summary>
    /// <param name="uri">The external <see cref="Uri"/> for the link.</param>
    /// <param name="title">The title for the external link.</param>
    /// <param name="description">The description of the external link, if any.</param>
    /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="title"/>, or <paramref name="description"/> is <see langword="null"/>.</exception>
    public EmbeddedExternal(Uri uri, string title, string description, Blob? thumbnail) :
        this(new External.Properties(uri, title, description, thumbnail))
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
    }

    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedExternal"/>.
    /// </summary>
    /// <param name="uri">The external <see cref="Uri"/> for the link.</param>
    /// <param name="title">The title for the external link.</param>
    /// <param name="description">The description of the external link, if any.</param>
    /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
    /// <param name="associatedRefs">An array of strong references associated with the embed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="title"/>, or <paramref name="description"/> is <see langword="null"/>.</exception>
    public EmbeddedExternal(Uri uri, string title, string description, Blob? thumbnail, StrongReference[]? associatedRefs) :
        this(new External.Properties(uri, title, description, thumbnail, associatedRefs))
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
    }

    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedExternal"/>.
    /// </summary>
    /// <param name="uri">The external <see cref="Uri"/> for the link.</param>
    /// <param name="title">The title for the external link.</param>
    /// <param name="description">The description of the external link, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="title"/>, or <paramref name="description"/> is <see langword="null"/>.</exception>
    public EmbeddedExternal(string uri, string title, string description) :
        this(new External.Properties(new Uri(uri), title, description, thumbnail: null))
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
    }

    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedExternal"/>.
    /// </summary>
    /// <param name="uri">The external <see cref="Uri"/> for the link.</param>
    /// <param name="title">The title for the external link.</param>
    /// <param name="description">The description of the external link, if any.</param>
    /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="title"/>, or <paramref name="description"/> is <see langword="null"/>.</exception>
    public EmbeddedExternal(string uri, string title, string description, Blob? thumbnail) :
        this(new External.Properties(new Uri(uri), title, description, thumbnail))
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
    }

    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedExternal"/>.
    /// </summary>
    /// <param name="uri">The external <see cref="Uri"/> for the link.</param>
    /// <param name="title">The title for the external link.</param>
    /// <param name="description">The description of the external link, if any.</param>
    /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
    /// <param name="associatedRefs">An array of strong references associated with the embed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="title"/>, or <paramref name="description"/> is <see langword="null"/>.</exception>
    public EmbeddedExternal(string uri, string title, string description, Blob? thumbnail, StrongReference[] associatedRefs) :
        this(new External.Properties(new Uri(uri), title, description, thumbnail, associatedRefs))
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
    }

    /// <summary>
    /// Gets the properties for the embedded media.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public External.Properties External { get; init; }
}