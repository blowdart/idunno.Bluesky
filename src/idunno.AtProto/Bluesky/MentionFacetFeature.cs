// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky
{
    /// <summary>
    /// A record containing the information necessary for a mention facet feature.
    /// </summary>
    public sealed record MentionFacetFeature : FacetFeatureBase
    {
        /// <summary>
        /// Creates a new instance of a <see cref="MentionFacetFeature"/> for the given actor DID.
        /// </summary>
        /// <param name="did">The <see cref="Did"> of the actor the feature refers to.</param>
        public MentionFacetFeature(Did did) => Did = did;

        /// <summary>
        /// Gets the DID for this actor being mentioned by this facet.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Did Did { get; internal set; }
    }
}
