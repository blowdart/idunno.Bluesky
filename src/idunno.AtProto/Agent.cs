// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

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

        // Keep this enabled as after the great join wave of Oct 16th some PDSs now always return
        // gziped content even if the client doesn't ask for it in with the Content-Encoding header.
        private static readonly HttpClientHandler s_httpClientHandler = new() { AutomaticDecompression = DecompressionMethods.All };

        private static readonly HttpClient s_sharedClient = new(s_httpClientHandler);

        private volatile bool _disposed;

        /// <summary>
        /// Initializes static members of the <see cref="Agent"/> class.
        /// </summary>
        static Agent()
        {
            // Configure the shared client with opinionated defaults.
            s_sharedClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
            s_sharedClient.DefaultRequestVersion = HttpVersion.Version20;
            s_sharedClient.DefaultRequestHeaders.UserAgent.ParseAdd(s_defaultAgent);

            s_sharedClient.DefaultRequestHeaders.Accept.Clear();
            s_sharedClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent"/> class.
        /// </summary>
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use when making HTTP requests.</param>
        protected Agent(HttpClient? httpClient = null)
        {
            // If an HttpClient is specified which doesn't have a UserAgent then configure it to use the default
            // user agent indicating the library name and version.
            if (httpClient is not null && httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(s_defaultAgent);
            }

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
    }
}
