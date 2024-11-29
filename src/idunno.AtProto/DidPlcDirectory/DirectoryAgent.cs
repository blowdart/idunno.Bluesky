// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use when making requests.</param>
        /// <param name="options"><see cref="DirectoryAgentOptions"/> for the use in the creation of this instance of <see cref="DirectoryAgent"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use when creating loggers.</param>
        public DirectoryAgent(HttpClient? httpClient = null, DirectoryAgentOptions? options = null, ILoggerFactory? loggerFactory = default) : base(httpClient)
        {
            if (options is not null)
            {
                PlcDirectory = options.PlcDirectoryUri;
            }

            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger<DirectoryAgent>();
        }

        /// <summary>
        /// Gets the default directory server used to issue commands against.
        /// </summary>
        /// <value>
        /// The default directory server used to issue commands against.
        /// </value>
        /// <remarks>
        /// <para>this directory is ignored if the DID is a web DID.</para>
        /// </remarks>
        private Uri PlcDirectory { get; } = s_defaultDirectoryServer;

        /// <summary>
        /// Gets the DID document for a DID.
        /// </summary>
        /// <param name="did">The DID to retrieve the DID Document for.</param>
        /// <param name="directory">The directory server used to retrieve the DID document from. This is ignored if the DID is a web DID.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<DidDocument>> ResolveDidDocument(Did did, Uri? directory = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            directory ??= PlcDirectory;

            Logger.ResolveDidDocumentCalled(_logger, did, directory);

            AtProtoHttpResult<DidDocument> result = await DirectoryServer.ResolveDidDocument(did, directory, HttpClient, _loggerFactory, cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                Logger.ResolveDidDocumentFailed(_logger, did, directory, result.StatusCode);
            }

            return result;
        }
    }
}
