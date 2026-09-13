// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.RichText;

/// <summary>
/// Facet feature for a URL. The text URL may have been simplified or truncated, but the facet reference should be a complete URL.
/// </summary>
public sealed record LinkFacetFeature : FacetFeature
{
    /// <summary>
    /// Constructs a new instance of <see cref="LinkFacetFeature"/>.
    /// </summary>
    /// <param name="uri">The <see cref="Uri"/> for the facet.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "The official Bluesky can create facets with illegal URIs, so this parameter is a string to accommodate those cases.")]
    public LinkFacetFeature(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        Uri = uri;
    }

    /// <summary>
    /// The <see cref="Uri"/> for the facet.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The official Bluesky can create facets with illegal URIs, so this property is a string to accommodate those cases.")]
    public string Uri { get; init; }
}