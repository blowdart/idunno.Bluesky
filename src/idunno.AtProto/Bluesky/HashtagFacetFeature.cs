// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky
{
    /// <summary>
    /// A record containing the information necessary for a hashtag facet feature.
    /// </summary>
    public sealed record HashtagFacetFeature : FacetFeatureBase
    {
        /// <summary>
        /// Creates a new instance of a <see cref="HashtagFacetFeature"/> for the given tag.
        /// </summary>
        /// <param name="tag">The hashtag the feature will be for.</param>
        public HashtagFacetFeature(string tag) => Tag = tag;

        /// <summary>
        /// Gets the tag for this hashtag facet feature.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string Tag { get; internal set; }
    }
}
