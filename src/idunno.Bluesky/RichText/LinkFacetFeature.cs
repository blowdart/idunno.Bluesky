// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.RichText
{
    /// <summary>
    /// Facet feature for a URL. The text URL may have been simplified or truncated, but the facet reference should be a complete URL.
    /// </summary>
    public sealed record LinkFacetFeature : FacetFeature
    {
        /// <summary>
        /// Constructs a new instance of <see cref="LinkFacetFeature"/>.
        /// </summary>
        /// <param name="uri">The <see cref="Uri"/> for the facet.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is null.</exception>
        [JsonConstructor]
        public LinkFacetFeature(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);

            Uri = uri;
        }

        /// <summary>
        /// The <see cref="Uri"/> for the facet.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Uri Uri { get; init; }
    }
}
