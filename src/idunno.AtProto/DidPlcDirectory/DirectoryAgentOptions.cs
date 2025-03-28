﻿// Copyright (c) Barry Dorrans. All rights reserved.
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
        private Uri _plcdirectoryUri = DirectoryAgent.s_defaultDirectoryServer;

        /// <summary>
        /// Specifies the server to use when resolving plc DIDs.
        /// </summary>
        public Uri PlcDirectoryUri
        {
            get
            {
                return _plcdirectoryUri;
            }

            set
            {
                ArgumentNullException.ThrowIfNull(value);

                if (value.Scheme != Uri.UriSchemeHttps)
                {
                    throw new ArgumentException("The PLC directory server must be an HTTPS URI.", nameof(value));
                }

                _plcdirectoryUri = value;
            }
        }

        /// <summary>
        /// Gets or sets any HttpClient options for the agent.
        /// </summary>
        /// <para>
        /// Setting <see cref="HttpClientOptions.CheckCertificateRevocationList"/>to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
        /// false if you are using a debugging proxy which does not support CRLs.
        /// </para>
        public HttpClientOptions? HttpClientOptions { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ILoggerFactory"/>, if any, to use when creating loggers.
        /// </summary>
        public ILoggerFactory? LoggerFactory { get; set; }
    }
}
