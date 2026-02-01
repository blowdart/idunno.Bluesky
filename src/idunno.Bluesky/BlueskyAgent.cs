// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using idunno.AtProto;
using idunno.Bluesky.RichText;
using idunno.AtProto.Authentication;

namespace idunno.Bluesky
{
    /// <summary>
    /// An <see cref="AtProtoAgent"/> implementation for sending requests to and receiving responses from a Bluesky service.
    /// </summary>
    public partial class BlueskyAgent : AtProtoAgent
    {
        private readonly ILogger<BlueskyAgent> _logger;

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyAgent"/>.
        /// </summary>
        /// <param name="options"><see cref="BlueskyAgentOptions"/> for the use in the creation of this instance of <see cref="BlueskyAgent"/>.</param>
        /// <remarks>
        ///   <para>
        ///     Setting <see cref="HttpClientOptions.CheckCertificateRevocationList"/> to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
        ///     false if you are using a debugging proxy which does not support CRLs.
        ///   </para>
        /// </remarks>
        public BlueskyAgent(BlueskyAgentOptions ? options = null) : base (
            service: DefaultServiceUris.BlueskyApiUri,
            options: options)
        {
            if (options is not null && options.PublicAppViewUri is not null)
            {
                ReadOnlyServiceUri = options.PublicAppViewUri;
            }

            if (options is not null && options.FacetExtractor is not null)
            {
                FacetExtractor = options.FacetExtractor;
            }
            else
            {
                FacetExtractor = new DefaultFacetExtractor(ResolveHandle);
            }

            if (options is not null)
            {
                LoggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            }
            else
            {
                LoggerFactory = NullLoggerFactory.Instance;
            }

            _logger = LoggerFactory.CreateLogger<BlueskyAgent>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyAgent"/> and sets the agent authentication to
        /// a <see cref="DPoPAccessCredentials"/> derived from the <paramref name="principal"/>.
        /// </summary>
        /// <param name="principal">The <see cref="ClaimsPrincipal"/> to extract authentication properties from.</param>
        /// <param name="options"><see cref="BlueskyAgentOptions"/> for the use in the creation of this instance of <see cref="BlueskyAgent"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="principal"/> is null.</exception>
        /// <remarks>
        ///   <para>
        ///     Setting <see cref="HttpClientOptions.CheckCertificateRevocationList"/> to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
        ///     false if you are using a debugging proxy which does not support CRLs.
        ///   </para>
        /// </remarks>
        public BlueskyAgent(ClaimsPrincipal principal, BlueskyAgentOptions? options = null) : base(
            principal: principal,
            options: options)
        {
            ArgumentNullException.ThrowIfNull(principal);

            if (options is not null && options.PublicAppViewUri is not null)
            {
                ReadOnlyServiceUri = options.PublicAppViewUri;
            }

            if (options is not null && options.FacetExtractor is not null)
            {
                FacetExtractor = options.FacetExtractor;
            }
            else
            {
                FacetExtractor = new DefaultFacetExtractor(ResolveHandle);
            }

            if (options is not null)
            {
                LoggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            }
            else
            {
                LoggerFactory = NullLoggerFactory.Instance;
            }

            _logger = LoggerFactory.CreateLogger<BlueskyAgent>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyAgent"/> and sets the agent authentication to
        /// a <see cref="DPoPAccessCredentials"/> derived from the <paramref name="identity"/>.
        /// </summary>
        /// <param name="identity">The <see cref="ClaimsPrincipal"/> to extract authentication properties from.</param>
        /// <param name="options"><see cref="BlueskyAgentOptions"/> for the use in the creation of this instance of <see cref="BlueskyAgent"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is null.</exception>
        /// <remarks>
        ///   <para>
        ///     Setting <see cref="HttpClientOptions.CheckCertificateRevocationList"/> to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
        ///     false if you are using a debugging proxy which does not support CRLs.
        ///   </para>
        /// </remarks>

        public BlueskyAgent(ClaimsIdentity identity, BlueskyAgentOptions? options = null) : base(
            identity: identity,
            options: options)
        {
            ArgumentNullException.ThrowIfNull(identity);

            if (options is not null && options.PublicAppViewUri is not null)
            {
                ReadOnlyServiceUri = options.PublicAppViewUri;
            }

            if (options is not null && options.FacetExtractor is not null)
            {
                FacetExtractor = options.FacetExtractor;
            }
            else
            {
                FacetExtractor = new DefaultFacetExtractor(ResolveHandle);
            }

            if (options is not null)
            {
                LoggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            }
            else
            {
                LoggerFactory = NullLoggerFactory.Instance;
            }

            _logger = LoggerFactory.CreateLogger<BlueskyAgent>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyAgent"/>
        /// </summary>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use when creating <see cref="HttpClient"/>s.</param>
        /// <param name="options">Any <see cref="AtProtoAgentOptions"/> to configure this instance with.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClientFactory"/> is null.</exception>
        public BlueskyAgent(IHttpClientFactory httpClientFactory, BlueskyAgentOptions? options = null) : base(
            service: DefaultServiceUris.BlueskyApiUri,
            httpClientFactory: httpClientFactory,
            options: options)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);

            if (options is not null && options.PublicAppViewUri is not null)
            {
                ReadOnlyServiceUri = options.PublicAppViewUri;
            }

            if (options is not null && options.FacetExtractor is not null)
            {
                FacetExtractor = options.FacetExtractor;
            }
            else
            {
                FacetExtractor = new DefaultFacetExtractor(ResolveHandle);
            }

            if (options is not null)
            {
                LoggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            }
            else
            {
                LoggerFactory = NullLoggerFactory.Instance;
            }

            _logger = LoggerFactory.CreateLogger<BlueskyAgent>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyAgent"/> and sets the agent authentication to
        /// a <see cref="DPoPAccessCredentials"/> derived from the <paramref name="principal"/>.
        /// </summary>
        /// <param name="principal">The <see cref="ClaimsPrincipal"/> to extract authentication properties from.</param>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use when creating <see cref="HttpClient"/>s.</param>
        /// <param name="options">Any <see cref="AtProtoAgentOptions"/> to configure this instance with.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="principal"/> or <paramref name="httpClientFactory"/>is <see langword="null"/>.</exception>
        public BlueskyAgent(ClaimsPrincipal principal, IHttpClientFactory httpClientFactory, BlueskyAgentOptions? options = null) : base(
                principal : principal,
                httpClientFactory: httpClientFactory,
                options: options)
        {
            ArgumentNullException.ThrowIfNull(principal);
            ArgumentNullException.ThrowIfNull(httpClientFactory);

            if (options is not null && options.PublicAppViewUri is not null)
            {
                ReadOnlyServiceUri = options.PublicAppViewUri;
            }

            if (options is not null && options.FacetExtractor is not null)
            {
                FacetExtractor = options.FacetExtractor;
            }
            else
            {
                FacetExtractor = new DefaultFacetExtractor(ResolveHandle);
            }

            if (options is not null)
            {
                LoggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            }
            else
            {
                LoggerFactory = NullLoggerFactory.Instance;
            }

            _logger = LoggerFactory.CreateLogger<BlueskyAgent>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyAgent"/> and sets the agent authentication to
        /// a <see cref="DPoPAccessCredentials"/> derived from the <paramref name="identity"/>.
        /// </summary>
        /// <param name="identity">The <see cref="ClaimsIdentity"/> to extract authentication properties from.</param>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use when creating <see cref="HttpClient"/>s.</param>
        /// <param name="options">Any <see cref="AtProtoAgentOptions"/> to configure this instance with.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> or <paramref name="httpClientFactory"/>is <see langword="null"/>.</exception>
        public BlueskyAgent(ClaimsIdentity identity, IHttpClientFactory httpClientFactory, BlueskyAgentOptions? options = null) : base(
                identity: identity,
                httpClientFactory: httpClientFactory,
                options: options)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentNullException.ThrowIfNull(httpClientFactory);

            if (options is not null && options.PublicAppViewUri is not null)
            {
                ReadOnlyServiceUri = options.PublicAppViewUri;
            }

            if (options is not null && options.FacetExtractor is not null)
            {
                FacetExtractor = options.FacetExtractor;
            }
            else
            {
                FacetExtractor = new DefaultFacetExtractor(ResolveHandle);
            }

            if (options is not null)
            {
                LoggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            }
            else
            {
                LoggerFactory = NullLoggerFactory.Instance;
            }

            _logger = LoggerFactory.CreateLogger<BlueskyAgent>();
        }

        /// <summary>
        /// Gets the service uri used to issue read only commands against.
        /// </summary>
        /// <value>
        /// The service used to issue read only commands against.
        /// </value>
        protected Uri ReadOnlyServiceUri { get; init; } = DefaultServiceUris.PublicAppViewUri;

        /// <summary>
        /// Gets the service uri to issue commands against, based on whether the current session is authenticated.
        /// </summary>
        /// <value>The service uri to issue commands against, based on whether the current session is authenticated.</value>
        protected Uri AuthenticatedOrUnauthenticatedServiceUri
        {
            get
            {
                if (!IsAuthenticated)
                {
                    return ReadOnlyServiceUri;
                }

                return Service;
            }
        }

        /// <summary>
        /// Gets the configured <see cref="IFacetExtractor"/> for this instance.
        /// </summary>
        public IFacetExtractor FacetExtractor
        {
            get;

            private set;
        }

        /// <summary>
        /// Creates a new <see cref="BlueskyAgentBuilder"/>.
        /// </summary>
        /// <returns>A new <see cref="BlueskyAgentBuilder"/></returns>
        public static new BlueskyAgentBuilder CreateBuilder() => BlueskyAgentBuilder.Create();

        /// <summary>
        /// Generates an AT URI from the specified Uri.
        /// This Uri is very dependent on the Bluesky web client and its format is subject to change.
        /// </summary>
        /// <param name="uri">A URI from the Bluesky web client.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An AT URI corresponding to the resource the Bluesky web client URI.</returns>
        /// <remarks>
        /// <para>This method makes outgoing web requests to resolve the handle in a Bluesky URI to a <see cref="AtProto.Did"/>.</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is in an unexpected format.</exception>
        /// <exception cref="HandleResolutionException">Thrown when the handle in <paramref name="uri"/> could not be resolved to a <see cref="AtProto.Did"/>.</exception>
        public async Task<AtUri> BuildAtUriFromBlueskyWebUri(Uri uri, CancellationToken cancellationToken = default)
        {
            // Bluesky web client URIs should be in the format
            // https://bsky.app/profile/<handle>/post/<rkey>/

            ArgumentNullException.ThrowIfNull(uri);

            if (uri.Scheme != "https")
            {
                throw new ArgumentException("Scheme is not https.");
            }

            if (uri.HostNameType != UriHostNameType.Dns || uri.Host != "bsky.app")
            {
                throw new ArgumentException($"{uri.Host} is not a known Bluesky web host.");
            }

            string[] pathComponents = uri.AbsolutePath.Split('/');

            if (pathComponents.Length != 5)
            {
                throw new ArgumentException($"{uri.AbsolutePath} does not have four components.");
            }

            if (pathComponents[1] != "profile")
            {
                throw new ArgumentException($"{uri.AbsolutePath} is not a profile path.");
            }

            if (pathComponents[3] != "post")
            {
                throw new ArgumentException($"{uri.AbsolutePath} is not a post path.");
            }

            if (!Handle.TryParse(pathComponents[2], out Handle? handle))
            {
                throw new ArgumentException($"{pathComponents[2]} is not a valid handle.");
            }

            Did? did = await ResolveHandle(handle.ToString(), cancellationToken).ConfigureAwait(false) ?? throw new HandleResolutionException($"Handle resolution did not succeed.");
            string rkey = pathComponents[4];

            string rebuiltAtUri = $"at://{did}/app.bsky.feed.post/{rkey}";

            if (!AtUri.TryParse(rebuiltAtUri, out AtUri? atUri))
            {
                throw new ArgumentException($"AtUri could not be created from {rebuiltAtUri}.", nameof(uri));
            }
            else
            {
                return atUri;
            }
        }

        /// <summary>
        /// Generates an Bluesky Web URI from the specified <paramref name="atUri" />, if the AT URI is in the app.bsky.feed.post collection.
        /// This Uri is very dependent on the Bluesky web client and its format is subject to change.
        /// </summary>
        /// <param name="atUri">The <see cref="AtUri"/> to generate a web URI for.</param>
        /// <returns>A URI for the Bluesky Web Client which will display the record specified by the <paramref name="atUri"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="atUri"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="atUri"/> is in an unexpected format or collection.</exception>
        public static Uri BuildBlueskyPostUriFromAtUri(AtUri atUri)
        {
            ArgumentNullException.ThrowIfNull(atUri);

            if (atUri.Collection is null)
            {
                throw new ArgumentException($"{atUri} has no collection.", nameof(atUri));
            }

            if (atUri.Collection != CollectionNsid.Post)
            {
                throw new ArgumentException($"{atUri} collection is not {CollectionNsid.Post}.", nameof(atUri));
            }

            if (atUri.Repo is null)
            {
                throw new ArgumentException($"{atUri} has no repo.", nameof(atUri));
            }

            if (atUri.RecordKey is null)
            {
                throw new ArgumentException($"{atUri} has no recordKey.", nameof(atUri));
            }

            return new Uri($"https://bsky.app/profile/{atUri.Repo}/post/{atUri.RecordKey}");
        }
    }
}
