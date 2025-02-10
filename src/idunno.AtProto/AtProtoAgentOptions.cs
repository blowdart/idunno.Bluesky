// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

namespace idunno.AtProto
{
    /// <summary>
    /// Configurable properties to use when creating a new instance of <see cref="AtProtoAgent"/>.
    /// </summary>
    public class AtProtoAgentOptions
    {
        /// <summary>
        /// Specifies whether to enable background access token refresh.
        /// </summary>
        public bool EnableBackgroundTokenRefresh { get; set; } = true;

        /// <summary>
        /// Specifies the server to use when resolving plc DIDs.
        /// </summary>
        public Uri PlcDirectoryServer { get; set; } = new("https://plc.directory");

        /// <summary>
        /// Gets or sets any OAuth options for the agent.
        /// </summary>
        public OAuthOptions? OAuthOptions { get; set; }

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
