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
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/>, if any, to use when creating loggers.</param>
        /// <param name="proxyUri">The proxy URI to use, if any.</param>
        /// <param name="checkCertificateRevocationList">Flag indicating whether certificate revocation lists should be checked. Defaults to <see langword="true" />.</param>
        /// <param name="httpUserAgent">The user agent string to use, if any.</param>
        /// <param name="timeout">The default HTTP timeout to use, if any.</param>
        /// <param name="options">Any <see cref="DirectoryAgentOptions"/> to configure this instance with.</param>
        /// <remarks>
        /// <para>
        /// Settings <paramref name="checkCertificateRevocationList"/> to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
        /// false if you are using a debugging proxy which does not support CRLs.
        /// </para>
        /// </remarks>
        public DirectoryAgent(
            ILoggerFactory? loggerFactory = default,
            Uri ? proxyUri = null,
            bool checkCertificateRevocationList = true,
            string? httpUserAgent = null,
            TimeSpan? timeout = null,
            DirectoryAgentOptions? options = null) : base(
                proxyUri: proxyUri,
                checkCertificateRevocationList: checkCertificateRevocationList,
                httpUserAgent: httpUserAgent,
                timeout: timeout)
        {
            if (options is not null)
            {
                PlcDirectory = options.PlcDirectoryUri;
            }

            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger<DirectoryAgent>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="DirectoryAgent"/>.
        /// </summary>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use when creating <see cref="HttpClient"/>s.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/>, if any, to use when creating loggers.</param>
        /// <param name="options">Any <see cref="DirectoryAgentOptions"/> to configure this instance with.</param>
        public DirectoryAgent(
            IHttpClientFactory httpClientFactory,
            ILoggerFactory? loggerFactory = default,
            DirectoryAgentOptions? options = null) : base(httpClientFactory)
        {
            ArgumentNullException.ThrowIfNull(HttpClientFactory);

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
        /// <para>This directory is ignored if the DID is a web DID.</para>
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
