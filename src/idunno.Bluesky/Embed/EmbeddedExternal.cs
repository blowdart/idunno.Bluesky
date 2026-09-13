// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
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
    /// <param name="uri">The external uri for the link, as a string.</param>
    /// <param name="title">The title for the external link.</param>
    /// <param name="description">The description of the external link, if any.</param>
    /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
    /// <param name="associatedRefs">An array of strong references associated with the embed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="title"/>, or <paramref name="description"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "The Bluesky web app can create facets with illegal URIs, so a string is used to accommodate them.")]
    public EmbeddedExternal(string uri, string title, string description, Blob? thumbnail = null, StrongReference[]? associatedRefs = null) :
        this(new External.Properties(uri, title, description, thumbnail, associatedRefs))
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