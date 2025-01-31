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

        private readonly string? _httpUserAgent;
        private readonly TimeSpan? _timeout;
        private readonly Uri? _proxyUri;
        private readonly bool _checkCertificateRevocationList = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent"/> class.
        /// </summary>
        /// <param name="proxyUri">The proxy URI to use, if any.</param>
        /// <param name="checkCertificateRevocationList">Flag indicating whether certificate revocation lists should be checked. Defaults to <see langword="true" />.</param>
        /// <param name="httpUserAgent">The user agent string to use, if any.</param>
        /// <param name="timeout">Overrides the default amount of time to wait before an HTTP request times out.</param>
        /// <remarks>
        /// <para>
        /// Settings <paramref name="checkCertificateRevocationList"/> to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
        /// false if you are using a debugging proxy which does not support CRLs.
        /// </para>
        /// </remarks>
        protected Agent(Uri? proxyUri = null, bool checkCertificateRevocationList = true, string? httpUserAgent = null, TimeSpan? timeout = null)
        {
            _proxyUri = proxyUri;
            _checkCertificateRevocationList = checkCertificateRevocationList;
            _httpUserAgent = httpUserAgent;
            _timeout = timeout;

            IServiceCollection services = new ServiceCollection();

            services
                .AddHttpClient(HttpClientName, client => InternalConfigureHttpClient(client, httpUserAgent, timeout))
                .ConfigurePrimaryHttpMessageHandler(() => BuildProxyClientHandler(proxyUri, checkCertificateRevocationList));

            _serviceProvider = services.BuildServiceProvider();
            HttpClientFactory = _serviceProvider.GetService<IHttpClientFactory>()!;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent"/> class.
        /// </summary>
        /// <param name="httpClientFactory">An <see cref="IHttpClientFactory"/> to use to create HTTP clients.</param>
        /// <remarks>
        /// <para>The agent will request a named HttpClient with a name of "idunno.AtProto".</para>
        /// </remarks>
        protected Agent(IHttpClientFactory httpClientFactory)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            HttpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Gets the <see cref="IHttpClientFactory"/> used when creating <see cref="HttpClient"/>s.
        /// </summary>
        protected IHttpClientFactory HttpClientFactory { get; init; }

        /// <summary>
        /// Gets a new HttpClientHandler configured with any proxy settings passed during the agent configuration.
        /// </summary>
        protected HttpClientHandler HttpClientHandler => BuildProxyClientHandler(_proxyUri, _checkCertificateRevocationList);

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
            InternalConfigureHttpClient(client, _httpUserAgent, _timeout);

            return client;
        }

        /// <summary>
        /// Creates a client handler to configure proxy setup with the initialization parameters specified when creating the agent.
        /// </summary>
        /// <returns>An <see cref="HttpClientHandler"/> configured to any proxy specified when the agent was created.</returns>
        protected HttpClientHandler CreateProxyHttpClientHandler()
        {
            return BuildProxyClientHandler(_proxyUri, _checkCertificateRevocationList);
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
