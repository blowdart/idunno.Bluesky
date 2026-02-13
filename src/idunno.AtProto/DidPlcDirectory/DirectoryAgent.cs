// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using idunno.AtProto;

namespace idunno.DidPlcDirectory
{
    /// <summary>
    /// An agent proving operations for DID directory services.
    /// </summary>
    public sealed class DirectoryAgent : Agent
    {
        internal static readonly Uri s_defaultDirectoryServer = new("https://plc.directory");

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DirectoryAgent> _logger;

        /// <summary>
        /// Creates a new instance of <see cref="DirectoryAgent"/>.
        /// </summary>
        /// <param name="options">Any <see cref="DirectoryAgentOptions"/> to configure this instance with.</param>
        public DirectoryAgent(
            DirectoryAgentOptions? options = null) : base(options?.HttpClientOptions, null)
        {
            if (options is not null)
            {
                PlcDirectory = options.PlcDirectoryUri;
                _loggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            }
            else
            {
                _loggerFactory = NullLoggerFactory.Instance;
            }

            _logger = _loggerFactory.CreateLogger<DirectoryAgent>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="DirectoryAgent"/>.
        /// </summary>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use when creating <see cref="HttpClient"/>s.</param>
        /// <param name="options">Any <see cref="DirectoryAgentOptions"/> to configure this instance with.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClientFactory"/> is <see langword="null"/>.</exception>
        public DirectoryAgent(
            IHttpClientFactory httpClientFactory,
            DirectoryAgentOptions? options = null) : base(httpClientFactory, null)
        {
            ArgumentNullException.ThrowIfNull(HttpClientFactory);

            if (options is not null)
            {
                PlcDirectory = options.PlcDirectoryUri;
                _loggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            }
            else
            {
                _loggerFactory = NullLoggerFactory.Instance;
            }

            _logger = _loggerFactory.CreateLogger<DirectoryAgent>();
        }

        /// <summary>
        /// Gets the default directory server used to issue commands against.
        /// </summary>
        /// <value>
        /// The default directory server used to issue commands against.
        /// </value>
        /// <remarks>
        /// <para>This directory is ignored if the DID is a web DID.</para>
        /// </remarks>
        internal Uri PlcDirectory { get; } = s_defaultDirectoryServer;

        /// <summary>
        /// Gets the <see cref="DidDocument" /> for a <paramref name="did"/>.
        /// </summary>
        /// <param name="did">The DID to retrieve the DID Document for.</param>
        /// <param name="directory">The directory server used to retrieve the <see cref="DidDocument" /> from. This is ignored if the <paramref name="did"/> is a web <see cref="Did"/>.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
        public async Task<AtProtoHttpResult<DidDocument>> ResolveDidDocument(Did did, Uri? directory = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            directory ??= PlcDirectory;

            Logger.ResolveDidDocumentCalled(_logger, did, directory);

            AtProtoHttpResult<DidDocument> result = await DirectoryServer.ResolveDidDocument(
                did: did,
                directory: directory,
                httpClient: HttpClient,
                loggerFactory: _loggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                Logger.ResolveDidDocumentFailed(_logger, did, directory, result.StatusCode);
            }

            return result;
        }
    }
}
