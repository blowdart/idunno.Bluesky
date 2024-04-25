// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Directory;

namespace idunno.AtProto
{
    public class DirectoryAgent
    {
        /// <summary>
        /// Creates a new instance of <see cref="DirectoryAgent"/>.
        /// </summary>
        public DirectoryAgent() {}

        /// <summary>
        /// Creates a new instance of <see cref="DirectoryAgent"/> which will default to the specified <paramref name="service"/>.
        /// </summary>
        /// <param name="defaultServer">The service to connect to if no service is specified.</param>
        public DirectoryAgent(Uri defaultServer)
        {
            DefaultDirectoryServer = defaultServer;
        }

        /// <summary>
        /// Creates a new instance of <see cref="DirectoryAgent"/> which will default to the specified <paramref name="service"/>
        /// and will use the specified <paramref name="httpClientHandler"/> when HttpClients are created to make requests.
        /// </summary>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making requests.</param>
        public DirectoryAgent(HttpClientHandler httpClientHandler)
        {
            HttpClientHandler = httpClientHandler;
        }

        /// <summary>
        /// Creates a new instance of <see cref="DirectoryAgent"/> which will default to the specified <paramref name="service"/>
        /// and will use the specified <paramref name="httpClientHandler"/> when HttpClients are created to make requests.
        /// </summary>
        /// <param name="service">The service to connect to if no service is specified.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making requests.</param>
        public DirectoryAgent(Uri defaultServer, HttpClientHandler httpClientHandler)
        {
            DefaultDirectoryServer = defaultServer;
            HttpClientHandler = httpClientHandler;
        }

        /// <summary>
        /// Gets or sets the directory server used to issue commands against.
        /// </summary>
        /// <value>
        /// The directory server used to issue commands against.
        /// </value>
        public Uri DefaultDirectoryServer { get; set; } = DefaultServices.DefaultDirectoryServer;

        /// <summary>
        /// Gets or sets an HttpClientHandler to use when creating the HttpClient to make requests and receive responses.
        /// </summary>
        /// <value>An HttpClientHandler to use when creating the HttpClient to make requests and receive responses.</value>
        protected HttpClientHandler? HttpClientHandler { get; set; }

        /// <summary>
        /// Resolves a DID to a DID document
        ///
        /// </summary>
        /// <param name="did">The DID to resolve.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// An <see cref="HttpResult"/> wrapping a <see cref="DidDocument"/> containing the DID resolution results, or any error details returned by the service.
        public  async Task<HttpResult<DidDocument>> ResolveDIDDocument(Did did, CancellationToken cancellationToken = default)
        {
            return await ResolveDIDDocument(did, DefaultDirectoryServer, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves a DID to a DID document
        /// </summary>
        /// <param name="did">The DID to resolve.</param>
        /// <param name="directoryServer">The service used to resolve the <paramref name="handle"/>.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// An <see cref="HttpResult"/> wrapping a <see cref="DidDocument"/> containing the DID resolution results, or any error details returned by the service.
        public async Task<HttpResult<DidDocument>> ResolveDIDDocument(Did did, Uri directoryServer, CancellationToken cancellationToken = default)
        {
            return await DirectoryServer.ResolveDid(did, directoryServer, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }
    }
}
