// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;

using Microsoft.Extensions.DependencyInjection;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides a base class for agent classes that will use HttpClients to connect with a service.
    /// </summary>
    public abstract class Agent : IDisposable
    {
        private const string HttpClientName = "idunno.atproto";

        private volatile bool _disposed;

        private readonly ServiceProvider? _serviceProvider;

        internal HttpClientOptions? _httpClientOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent"/> class.
        /// </summary>
        /// <param name="httpClientOptions">Any <see cref="HttpClientOptions"/> for the internal http client used to make HTTP requests.</param>
        /// <param name="jsonOptions">Any <see cref="JsonOptions"/> to use during serialization and deserialization.</param>
        /// <remarks>
        /// <para>
        /// Setting <see cref="HttpClientOptions.CheckCertificateRevocationList"/> to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
        /// false if you are using a debugging proxy which does not support CRLs.
        /// </para>
        /// </remarks>
        protected Agent(HttpClientOptions? httpClientOptions, JsonOptions? jsonOptions)
        {
            if (jsonOptions is not null)
            {
                JsonOptions = jsonOptions;
            }

            bool checkCrl;
            if (httpClientOptions is null)
            {
                checkCrl = true;
            }
            else
            {
                checkCrl = httpClientOptions.CheckCertificateRevocationList;
            }

            IServiceCollection services = new ServiceCollection();
            _httpClientOptions = httpClientOptions;

            services
                .AddHttpClient(HttpClientName, client => InternalConfigureHttpClient(client, _httpClientOptions?.HttpUserAgent, _httpClientOptions?.Timeout))
                .ConfigurePrimaryHttpMessageHandler(() => BuildProxyClientHandler(_httpClientOptions?.ProxyUri, checkCrl));

            _serviceProvider = services.BuildServiceProvider();
            HttpClientFactory = _serviceProvider.GetService<IHttpClientFactory>()!;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent"/> class.
        /// </summary>
        /// <param name="httpClientFactory">An <see cref="IHttpClientFactory"/> to use to create HTTP clients.</param>
        /// <param name="jsonOptions">Any <see cref="JsonOptions"/> to use during serialization and deserialization.</param>
        /// <remarks>
        /// <para>The agent will request a named HttpClient with a name of "idunno.AtProto".</para>
        /// </remarks>
        protected Agent(IHttpClientFactory httpClientFactory, JsonOptions? jsonOptions)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);

            if (jsonOptions is not null)
            {
                JsonOptions = jsonOptions;
            }

            HttpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Gets the <see cref="IHttpClientFactory"/> used when creating <see cref="HttpClient"/>s.
        /// </summary>
        protected IHttpClientFactory HttpClientFactory { get; init; }

        /// <summary>
        /// Gets a new HttpClientHandler configured with any proxy settings passed during the agent configuration.
        /// </summary>
        protected HttpClientHandler HttpClientHandler
        {
            get
            {
                bool checkCrl;

                if (_httpClientOptions is null)
                {
                    checkCrl = true;
                }
                else
                {
                    checkCrl = _httpClientOptions.CheckCertificateRevocationList;
                }


                return BuildProxyClientHandler(_httpClientOptions?.ProxyUri, checkCrl);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonOptions"/> for the agent.
        /// </summary>
        public JsonOptions JsonOptions { get; } = new JsonOptions();

        /// <summary>
        /// Gets an <see cref="HttpClient"/> to use when making requests.
        /// </summary>
        public HttpClient HttpClient => HttpClientFactory.CreateClient(HttpClientName);

        /// <summary>
        /// Gets a value indicating whether the agent has an active session.
        /// </summary>
        public virtual bool IsAuthenticated { get; }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="Agent"/> and optionally disposes of the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to releases only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _serviceProvider?.Dispose();

            _disposed = true;
        }

        /// <summary>
        /// Disposes of any managed and unmanaged resources used by the <see cref="Agent"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Configures an HttpClient with the initialization parameters specified when creating the agent.
        /// </summary>
        /// <param name="client">The <see cref="HttpClient"/> to configure.</param>
        /// <returns>The configured <see cref="HttpClient"/>.</returns>
        protected HttpClient ConfigureHttpClient(HttpClient client)
        {
            InternalConfigureHttpClient(client, _httpClientOptions?.HttpUserAgent, _httpClientOptions?.Timeout);

            return client;
        }

        /// <summary>
        /// Creates a client handler to configure proxy setup with the initialization parameters specified when creating the agent.
        /// </summary>
        /// <returns>An <see cref="HttpClientHandler"/> configured to any proxy specified when the agent was created.</returns>
        protected HttpClientHandler CreateProxyHttpClientHandler()
        {
            bool checkCrl;

            if (_httpClientOptions is null)
            {
                checkCrl = true;
            }
            else
            {
                checkCrl = _httpClientOptions.CheckCertificateRevocationList;
            }

            return BuildProxyClientHandler(_httpClientOptions?.ProxyUri, checkCrl);
        }

        private static void InternalConfigureHttpClient(HttpClient client, string? httpUserAgent = null, TimeSpan? timeout = null)
        {
            ArgumentNullException.ThrowIfNull(client);

            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            client.DefaultRequestVersion = HttpVersion.Version20;

            if (httpUserAgent is null)
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("idunno.AtProto/" + typeof(Agent).Assembly.GetName().Version);
            }
            else
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(httpUserAgent);
            }

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));

            if (timeout is null)
            {
                client.Timeout = new(0, 5, 0);
            }
            else
            {
                client.Timeout = (TimeSpan)timeout;
            }
        }

        private static HttpClientHandler BuildProxyClientHandler(Uri? proxyUri, bool checkCertificateRevocationList)
        { 
            if (proxyUri is not null)
            {
                return new HttpClientHandler
                {
                    Proxy = new WebProxy
                    {
                        Address = proxyUri,
                        BypassProxyOnLocal = true,
                        UseDefaultCredentials = true
                    },
                    UseProxy = true,
                    CheckCertificateRevocationList = checkCertificateRevocationList,

                    AutomaticDecompression = DecompressionMethods.All,
                };
            }
            else
            {
                return new HttpClientHandler
                {
                    CheckCertificateRevocationList = checkCertificateRevocationList,
                    AutomaticDecompression = DecompressionMethods.All
                };
            }
        }
    }
}
