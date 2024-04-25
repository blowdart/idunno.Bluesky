// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky
{
    /// <summary>
    /// A record containing the information necessary for a link facet feature.
    /// </summary>
    public sealed record LinkFacetFeature : FacetFeatureBase
    {
        /// <summary>
        /// Creates a new instance of a <see cref="LinkFacetFeature"/> for the given URI.
        /// </summary>
        /// <param name="uri">The URI the feature will be for.</param>
        public LinkFacetFeature(Uri uri) => Uri = uri;

        /// <summary>
        /// Gets the URI for this link facet feature.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Uri Uri { get; internal set; }
    }
}
