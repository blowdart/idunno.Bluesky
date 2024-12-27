// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides a base class for agent classes that will use HttpClients to connect with a service.
    /// </summary>
    public abstract class Agent : IDisposable
    {
        private static readonly string s_defaultAgent = "idunno.AtProto/" + typeof(Agent).Assembly.GetName().Version;

        private static readonly TimeSpan s_defaultHttpTimeout = new(0, 5, 0);
        private static readonly HttpClientHandler s_httpClientHandler = new() { AutomaticDecompression = DecompressionMethods.All, };
        private static readonly HttpClient s_sharedClient= new(s_httpClientHandler, disposeHandler: true) { Timeout = s_defaultHttpTimeout };

        private volatile bool _disposed;

        /// <summary>
        /// Initializes static members of the <see cref="Agent"/> class.
        /// </summary>
        static Agent()
        {
            // Configure the shared client with opinionated defaults.
            s_sharedClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            s_sharedClient.DefaultRequestVersion = HttpVersion.Version20;
            s_sharedClient.DefaultRequestHeaders.UserAgent.ParseAdd("idunno.AtProto/" + typeof(Agent).Assembly.GetName().Version);

            s_sharedClient.DefaultRequestHeaders.Accept.Clear();
            s_sharedClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent"/> class.
        /// </summary>
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use when making HTTP requests.</param>
        protected Agent(HttpClient? httpClient = null)
        {
            HttpClient = httpClient ?? s_sharedClient;
        }

        /// <summary>
        /// Gets the HttpClient to use when making requests.
        /// </summary>
        protected HttpClient HttpClient { get; }

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

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Handler lifetime is delegated to the HttpClient")]
        [SuppressMessage("Reliability", "CA5399:Enable HttpClient certificate revocation list check", Justification = "Fiddler and other dev time proxies don't support CRLs in their generated certificates, so this should be off by default.")]
        private static HttpClient CreateHttpClient(Uri? proxyUri=null, string? httpUserAgent=null, TimeSpan? timeout = null)
        {
            timeout ??= s_defaultHttpTimeout;

            HttpClientHandler? httpClientHandler;
            if (proxyUri is not null)
            {
                httpClientHandler = new HttpClientHandler
                {
                    Proxy = new WebProxy
                    {
                        Address = proxyUri,
                        BypassProxyOnLocal = true,
                        UseDefaultCredentials = true
                    },
                    UseProxy = true,
                    CheckCertificateRevocationList = false,

                    AutomaticDecompression = DecompressionMethods.All,
                };
            }
            else
            {
                httpClientHandler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.All,
                };
            }

            HttpClient httpClient = new(handler: httpClientHandler, disposeHandler: true)
            {
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
                DefaultRequestVersion = HttpVersion.Version20,
                Timeout = (TimeSpan)timeout
            };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(httpUserAgent ?? s_defaultAgent);

            return httpClient;
        }

        /// <summary>
        /// Creates an HttpClient with an opinionated configuration, using the specified <paramref name="httpUserAgent"/>.
        /// </summary>
        /// <param name="httpUserAgent">The HTTP User Agent to use in all requests.</param>
        /// <param name="timeout">An optional timeout to use when waiting for requests.</param>
        /// <returns>An HttpClient configured with the <paramref name="httpUserAgent"/>.</returns>
        /// <exception cref="ArgumentNullException">Throw <paramref name="httpUserAgent"/> is null.</exception>
        /// <remarks>
        ///</remarks>
        public static HttpClient CreateConfiguredHttpClient(string httpUserAgent, TimeSpan? timeout = null)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(httpUserAgent);

            return CreateHttpClient(httpUserAgent: httpUserAgent, timeout: timeout);
        }

        /// <summary>
        /// Creates an HttpClient with an opinionated configuration, using the <paramref name="proxyUri"/>.
        /// </summary>
        /// <param name="proxyUri">The <paramref name="proxyUri"/> of the proxy client to use in all requests.</param>
        /// <param name="timeout">An optional timeout to use when waiting for requests.</param>
        /// <returns>An HttpClient configured with the <paramref name="proxyUri"/>.</returns>
        /// <exception cref="ArgumentNullException">Throw if <paramref name="proxyUri"/> is null.</exception>
        /// <remarks>
        ///<para>
        /// The created <see cref="HttpClient"/> will be configured with supported compression algorithms enabled and to use HTTP2.0.
        /// The client will be configured to use <paramref name="proxyUri"/> if specified. If a proxy URI is specified the client will also be configured
        /// to disable CRL checks.
        ///</para>
        ///</remarks>
        public static HttpClient CreateConfiguredHttpClient(Uri proxyUri, TimeSpan? timeout = null)
        {
            ArgumentNullException.ThrowIfNull(proxyUri);

            return CreateHttpClient(proxyUri: proxyUri, timeout: timeout);
        }

        /// <summary>
        /// Creates an HttpClient with an opinionated configuration, using the <paramref name="proxyUri"/> and <paramref name="httpUserAgent"/>.
        /// </summary>
        /// <param name="proxyUri">The <paramref name="proxyUri"/> of the proxy client to use in all requests.</param>
        /// <param name="httpUserAgent">The HTTP User Agent to use in all requests.</param>
        /// <param name="timeout">An optional timeout to use when waiting for requests.</param>
        /// <returns>An HttpClient configured with the <paramref name="proxyUri"/> and <paramref name="httpUserAgent"/>.</returns>
        /// <exception cref="ArgumentNullException">Throw if <paramref name="proxyUri"/> or <paramref name="httpUserAgent"/> is null.</exception>
        /// <remarks>
        ///<para>
        /// The created <see cref="HttpClient"/> will be configured with supported compression algorithms enabled and to use HTTP2.0.
        /// The client will be configured to use <paramref name="proxyUri"/> if specified. If a proxy URI is specified the client will also be configured
        /// to disable CRL checks.
        /// If a <paramref name="httpUserAgent"/> is specified the client will be configured to use it with all requests.
        ///</para>
        ///</remarks>
        public static HttpClient CreateConfiguredHttpClient(Uri proxyUri, string httpUserAgent, TimeSpan timeout)
        {
            ArgumentNullException.ThrowIfNull(proxyUri);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(httpUserAgent);

            return CreateHttpClient(proxyUri: proxyUri, httpUserAgent: httpUserAgent, timeout: timeout);
        }
    }
}
