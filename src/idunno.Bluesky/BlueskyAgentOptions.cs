// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.Bluesky.RichText;
using idunno.AtProto.Authentication;

namespace idunno.Bluesky
{
    /// <summary>
    /// Options for configuring a Bluesky agent.
    /// </summary>
    public class BlueskyAgentOptions : AtProtoAgentOptions
    {
        /// <summary>
        /// Creates a new instance of <see cref="BlueskyAgentOptions"/>
        /// </summary>
        public BlueskyAgentOptions() : base()
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyAgentOptions"/>"/>
        /// </summary>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>, if any, to use when creating loggers.</param>
        /// <param name="enableBackgroundTokenRefresh">Flag indicating whether credential refreshes should happen in the background.</param>
        /// <param name="publicAppViewUri">The service URI to use when making calls that to not require authentication</param>
        /// <param name="facetExtractor">The facet extractor to use when extracting facets from post or message texts.</param>
        /// <param name="oAuthOptions">Any <see cref="OAuthOptions"/> for the agent.</param>
        /// <param name="httpClientOptions">The HttpClient options for the agent.</param>
        /// <param name="httpJsonOptions">Any <see cref="JsonOptions"/> to use during serialization and deserialization.</param>
        public BlueskyAgentOptions(ILoggerFactory? loggerFactory,
            bool enableBackgroundTokenRefresh = true,
            Uri? publicAppViewUri = null,
            IFacetExtractor? facetExtractor = null,
            OAuthOptions? oAuthOptions = null,
            HttpClientOptions? httpClientOptions = null,
            JsonOptions? httpJsonOptions = null) : base(
                loggerFactory: loggerFactory,
                enableBackgroundTokenRefresh: enableBackgroundTokenRefresh,
                oAuthOptions: oAuthOptions,
                httpClientOptions: httpClientOptions)
        {
            LoggerFactory = loggerFactory;

            if (publicAppViewUri is not null)
            {
                PublicAppViewUri = publicAppViewUri;
            }

            if (facetExtractor is not null)
            {
                FacetExtractor = facetExtractor;
            }

            // Insert the Bluesky source generation context at the start of the type info resolver chain.
            if (httpJsonOptions is not null)
            {
                httpJsonOptions.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);
            }
            else
            {
                httpJsonOptions = new JsonOptions();
                httpJsonOptions.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);
            }
        }

        /// <summary>
        /// Gets or sets the service uri used when making calls to APIs that do not require authentication,
        /// as documented in the <see href="https://docs.bsky.app/docs/category/http-reference">Bluesky API documentation</see>.
        /// </summary>
        public Uri PublicAppViewUri { get; set; } = DefaultServiceUris.PublicAppViewUri;

        /// <summary>
        /// Gets or sets the facet extractor to use when extracting facets from post or message texts.
        /// </summary>
        public IFacetExtractor? FacetExtractor { get; set; }
    }
}
