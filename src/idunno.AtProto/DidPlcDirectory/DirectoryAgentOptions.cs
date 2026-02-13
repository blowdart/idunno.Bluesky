// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

using idunno.AtProto;

namespace idunno.DidPlcDirectory
{
    /// <summary>
    /// A class for configuring the DirectoryAgent.
    /// </summary>
    public sealed class DirectoryAgentOptions
    {
        /// <summary>
        /// Specifies the server to use when resolving plc DIDs.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is not an HTTPS URI.</exception>>
        public Uri PlcDirectoryUri
        {
            get;

            set
            {
                ArgumentNullException.ThrowIfNull(value);

                if (value.Scheme != Uri.UriSchemeHttps)
                {
                    throw new ArgumentException("The PLC directory server must be an HTTPS URI.", nameof(value));
                }

                field = value;
            }
        } = DirectoryAgent.s_defaultDirectoryServer;

        /// <summary>
        /// Gets or sets any HttpClient options for the agent.
        /// </summary>
        /// <para>
        /// Setting <see cref="HttpClientOptions.CheckCertificateRevocationList"/>to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
        /// <see langword="false"/> if you are using a debugging proxy which does not support CRLs.
        /// </para>
        public HttpClientOptions? HttpClientOptions { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ILoggerFactory"/>, if any, to use when creating loggers.
        /// </summary>
        public ILoggerFactory? LoggerFactory { get; set; }
    }
}
