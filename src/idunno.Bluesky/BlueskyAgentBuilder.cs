// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky
{
    /// <summary>
    /// A builder for <see cref="BlueskyAgent"/> instances.
    /// </summary>
    public sealed class BlueskyAgentBuilder : AtProtoAgentBuilder
    {
        private Uri _publicAppViewUri = DefaultServiceUris.PublicAppViewUri;
        private IFacetExtractor? _facetExtractor;

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgentBuilder"/>.
        /// </summary>
        private BlueskyAgentBuilder()
        {
        }

        /// <summary>
        /// Creates a new <see cref="BlueskyAgentBuilder"/>.
        /// </summary>
        /// <returns>A new <see cref="BlueskyAgentBuilder"/></returns>
        public new static BlueskyAgentBuilder Create() => new();

        /// <summary>
        /// Sets the app view URI the agent will use
        /// </summary>
        /// <param name="appViewUri">The public <see cref="Uri"/> of the app view the agent will use.</param>
        /// <returns>The same instance of <see cref="BlueskyAgentBuilder"/> for chaining.</returns>
        public BlueskyAgentBuilder WithPublicAppViewUri(Uri appViewUri)
        {
            ArgumentNullException.ThrowIfNull(appViewUri);
            _publicAppViewUri = appViewUri;
            return this;
        }

        /// <summary>
        /// Sets the facet extractor to use when parsing posts.
        /// </summary>
        /// <param name="facetExtractor">The factet extractor to use.</param>
        /// <returns>The same instance of <see cref="BlueskyAgentBuilder"/> for chaining.</returns>
        public BlueskyAgentBuilder SetFacetExtractor(IFacetExtractor facetExtractor)
        {
            ArgumentNullException.ThrowIfNull(facetExtractor);
            _facetExtractor = facetExtractor;
            return this;
        }

        /// <summary>
        /// Builds the <see cref="AtProtoAgent"/>.
        /// </summary>
        /// <returns>A configured <see cref="AtProtoAgent"/>.</returns>
        public new BlueskyAgent Build()
        {
            ArgumentNullException.ThrowIfNull(Service);
            ArgumentNullException.ThrowIfNull(LoggerFactory);

            if (HttpClientFactory == null)
            {
                return new BlueskyAgent(new BlueskyAgentOptions
                {
                    PublicAppViewUri = _publicAppViewUri,
                    FacetExtractor = _facetExtractor,
                    PlcDirectoryServer = DirectoryService,
                    LoggerFactory = LoggerFactory,
                    HttpClientOptions = HttpClientOptions,
                    OAuthOptions = OAuthOptions,
                    HttpJsonOptions = JsonOptions,
                    EnableBackgroundTokenRefresh = BackgroundTokenRefreshEnabled,
                });
            }
            else
            {
                return new BlueskyAgent(HttpClientFactory, new BlueskyAgentOptions
                {
                    PublicAppViewUri = _publicAppViewUri,
                    FacetExtractor = _facetExtractor,
                    PlcDirectoryServer = DirectoryService,
                    LoggerFactory = LoggerFactory,
                    OAuthOptions = OAuthOptions,
                    HttpJsonOptions = JsonOptions,
                    EnableBackgroundTokenRefresh = BackgroundTokenRefreshEnabled,
                });
            }
        }
    }
}
