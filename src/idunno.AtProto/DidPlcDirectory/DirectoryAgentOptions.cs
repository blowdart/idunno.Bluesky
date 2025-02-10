// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

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
        public Uri PlcDirectoryUri { get; set; } = DirectoryAgent.s_defaultDirectoryServer;

        /// <summary>
        /// Gets or sets any HttpClient options for the agent.
        /// </summary>
        /// <para>
        /// Setting <see cref="HttpClientOptions.CheckCertificateRevocationList"/>to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
        /// false if you are using a debugging proxy which does not support CRLs.
        /// </para>
        public HttpClientOptions? HttpClientOptions { get; set; }
    }
}
