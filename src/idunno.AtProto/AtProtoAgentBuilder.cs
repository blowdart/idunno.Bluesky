// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using idunno.AtProto.Authentication;

namespace idunno.AtProto
{
    /// <summary>
    /// A builder for <see cref="AtProtoAgent"/> instances.
    /// </summary>
    public class AtProtoAgentBuilder
    {
        private const string DefaultService = "https://public.api.bsky.app";
        private const string DefaultDirectoryService = "https://plc.directory";

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgentBuilder"/>.
        /// </summary>
        protected AtProtoAgentBuilder()
        {
        }

        /// <summary>
        /// Gets or sets the service the agent will initially connect to.
        /// </summary>
        public Uri Service { get; set; } = new Uri(DefaultService);

        /// <summary>
        /// Gets or sets the directory service the agent will use to resolve plc DIDs.
        /// </summary>
        public Uri DirectoryService { get; set; } = new Uri(DefaultDirectoryService);

        /// <summary>
        /// Gets or sets the <see cref="ILoggerFactory"/> to use when creating loggers.
        /// </summary>
        public ILoggerFactory LoggerFactory { get; set;} = NullLoggerFactory.Instance;

        /// <summary>
        /// Gets or sets the <see cref="IHttpClientFactory"/> to use when creating <see cref="HttpClient"/>s.
        /// </summary>
        /// <remarks>
        ///<para>If an <see cref="IHttpClientFactory"/> is set then <see cref="HttpClientOptions"/> will be ignored.</para>
        /// </remarks>
        public IHttpClientFactory? HttpClientFactory { get; set; } = null!;

        /// <summary>
        /// Gets or sets the <see cref="HttpClientOptions"/> for the agent.
        /// </summary>
        public HttpClientOptions? HttpClientOptions { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="OAuthOptions"/> for the client.
        /// </summary>
        public OAuthOptions? OAuthOptions { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="JsonOptions"/> for the client."/>
        /// </summary>
        public JsonOptions? JsonOptions { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether background authorization token refresh is enabled.
        /// </summary>
        public bool BackgroundTokenRefreshEnabled { get; set; } = true;

        /// <summary>
        /// Creates a new <see cref="AtProtoAgentBuilder"/>.
        /// </summary>
        /// <returns>A new <see cref="AtProtoAgentBuilder"/></returns>
        public static AtProtoAgentBuilder Create() => new ();

        /// <summary>
        /// Sets the service the agent will initially connect to.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to initially connect to.</param>
        /// <returns>The same instance of <see cref="AtProtoAgentBuilder"/> for chaining.</returns>
        public AtProtoAgentBuilder ForService(Uri service)
        {
            ArgumentNullException.ThrowIfNull(service);

            Service = service;
            return this;
        }

        /// <summary>
        /// Sets the service the agent will initially connect to.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to initially connect to.</param>
        /// <returns>The same instance of <see cref="AtProtoAgentBuilder"/> for chaining.</returns>
        public AtProtoAgentBuilder ForService(string service)
        {
            ArgumentNullException.ThrowIfNull(service);

            return ForService(new Uri(service));
        }

        /// <summary>
        /// Sets the directory service the agent will use to resolve <see cref="Did"/>s.
        /// </summary>
        /// <param name="directoryService">The <see cref="Uri"/> of the directory service to use for resolving.</param>
        /// <returns>The same instance of <see cref="AtProtoAgentBuilder"/> for chaining.</returns>
        public AtProtoAgentBuilder WithDirectoryService(Uri directoryService)
        {
            ArgumentNullException.ThrowIfNull(directoryService);

            DirectoryService = directoryService;
            return this;
        }

        /// <summary>
        /// Sets the directory service the agent will use to resolve <see cref="Did"/>s.
        /// </summary>
        /// <param name="directoryService">The <see cref="Uri"/> of the directory service to use for resolving.</param>
        /// <returns>The same instance of <see cref="AtProtoAgentBuilder"/> for chaining.</returns>
        public AtProtoAgentBuilder WithDirectoryService(string directoryService)
        {
            ArgumentNullException.ThrowIfNull(directoryService);

            return WithDirectoryService(new Uri(directoryService));
        }

        /// <summary>
        /// Sets the <see cref="ILoggerFactory"/> to use when creating loggers.
        /// </summary>
        /// <param name="logger">The <see cref="ILoggerFactory"/> to use when creating loggers.</param>
        /// <returns>The same instance of <see cref="AtProtoAgentBuilder"/> for chaining.</returns>
        public AtProtoAgentBuilder WithLoggerFactory(ILoggerFactory logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            LoggerFactory = logger;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="OAuthOptions"/> to use using OAuth for authentication.
        /// </summary>
        /// <param name="configure">The <see cref="OAuthOptions"/> to use using OAuth for authentication.</param>
        /// <returns>The same instance of <see cref="AtProtoAgentBuilder"/> for chaining.</returns>
        public AtProtoAgentBuilder ConfigureOAuthOptions(Action<OAuthOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            OAuthOptions configuredOptions = new ();
            configure(configuredOptions);
            configuredOptions.Validate();

            OAuthOptions = configuredOptions;

            return this;
        }

        /// <summary>
        /// Sets the <see cref="JsonOptions"/> to use serializing or deserializing API calls.
        /// </summary>
        /// <param name="configure">The <see cref="JsonOptions"/> to use serializing or deserializing API calls.</param>
        /// <returns>The same instance of <see cref="AtProtoAgentBuilder"/> for chaining.</returns>
        public AtProtoAgentBuilder ConfigureHttpJsonOptions(Action<JsonOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            JsonOptions configuredOptions = new();

            configure(configuredOptions);

            JsonOptions = configuredOptions;

            return this;
        }

        /// <summary>
        /// Sets the <see cref="HttpClientOptions"/> the agent will use when making HTTP requests.
        /// </summary>
        /// <param name="configure">The <see cref="HttpClientOptions"/> the agent will use when making HTTP requests.</param>
        /// <returns>The same instance of <see cref="AtProtoAgentBuilder"/> for chaining.</returns>
        /// <para>
        /// Setting <see cref="HttpClientOptions.CheckCertificateRevocationList"/> to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
        /// false if you are using a debugging proxy which does not support CRLs.
        /// </para>
        public AtProtoAgentBuilder ConfigureHttpClientOptions(Action<HttpClientOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            HttpClientOptions configuredOptions = new();
            configure(configuredOptions);

            HttpClientOptions = configuredOptions;

            return this;
        }

        /// <summary>
        /// Turns off background authorization token refreshing.
        /// </summary>
        /// <returns>The same instance of <see cref="AtProtoAgentBuilder"/> for chaining.</returns>
        public AtProtoAgentBuilder DisableBackgroundTokenRefresh()
        {
            BackgroundTokenRefreshEnabled = false;
            return this;
        }

        /// <summary>
        /// Turns on background authorization token refreshing.
        /// </summary>
        /// <returns>The same instance of <see cref="AtProtoAgentBuilder"/> for chaining.</returns>
        public AtProtoAgentBuilder EnableBackgroundTokenRefresh()
        {
            BackgroundTokenRefreshEnabled = true;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="IHttpClientFactory"/> to use when creating <see cref="HttpClient"/>s.
        /// </summary>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use</param>
        /// <returns>The same instance of <see cref="AtProtoAgentBuilder"/> for chaining.</returns>
        /// <remarks>
        ///<para>If an <see cref="IHttpClientFactory"/> is set then <see cref="HttpClientOptions"/> will be ignored.</para>
        /// </remarks>
        public AtProtoAgentBuilder WithHttpClientFactory(IHttpClientFactory httpClientFactory)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);

            HttpClientFactory = httpClientFactory;
            return this;
        }

        /// <summary>
        /// Builds the <see cref="AtProtoAgent"/>.
        /// </summary>
        /// <returns>A configured <see cref="AtProtoAgent"/>.</returns>
        public virtual AtProtoAgent Build()
        {
            ArgumentNullException.ThrowIfNull(Service);
            ArgumentNullException.ThrowIfNull(LoggerFactory);

            if (HttpClientFactory == null)
            {
                return new AtProtoAgent(Service, new AtProtoAgentOptions
                {
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
                return new AtProtoAgent(Service, HttpClientFactory, new AtProtoAgentOptions
                {
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
