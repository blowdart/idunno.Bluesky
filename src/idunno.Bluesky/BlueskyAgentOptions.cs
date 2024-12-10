// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky
{
    /// <summary>
    /// Options for configuring a Bluesky agent.
    /// </summary>
    public class BlueskyAgentOptions : AtProtoAgentOptions
    {
        /// <summary>
        /// Gets or sets the service uri used when making calls to APIs that do not require authentication,
        /// as documented in the <see href="https://docs.bsky.app/docs/category/http-reference">Bluesky API documentation</see>.
        /// </summary>
        public Uri PublicAppViewUri { get; set; } = DefaultServiceUris.PublicAppViewUri;

        /// <summary>
        /// Gets or sets the facet extractor to use when extracting facets from post or message texts.
        /// </summary>
        public IFacetExtractor? FacetExtractor {get; set; }
    }
}
