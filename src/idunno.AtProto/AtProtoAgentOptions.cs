// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

using idunno.AtProto.Authentication;

namespace idunno.AtProto
{
    /// <summary>
    /// Configurable properties to use when creating a new instance of <see cref="AtProtoAgent"/>.
    /// </summary>
    public class AtProtoAgentOptions
    {
        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgentOptions"/>"/>
        /// </summary>
        public AtProtoAgentOptions()
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgentOptions"/>"/>
        /// </summary>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>, if any, to use when creating loggers.</param>
        /// <param name="enableBackgroundTokenRefresh">Flag indicating whether credential refreshes should happen in the background.</param>
        /// <param name="plcDirectoryServer">The server to use when resolving plc DIDs.</param>
        /// <param name="oAuthOptions">Any <see cref="OAuthOptions"/> for the agent.</param>
        /// <param name="httpClientOptions">The HttpClient options for the agent.</param>
        /// <param name="httpJsonOptions">Any <see cref="JsonOptions"/> to use during serialization and deserialization.</param>
        public AtProtoAgentOptions(
            ILoggerFactory? loggerFactory,
            bool enableBackgroundTokenRefresh = true,
            Uri? plcDirectoryServer = null,
            OAuthOptions? oAuthOptions = null,
            HttpClientOptions? httpClientOptions = null,
            JsonOptions? httpJsonOptions = null) : this()
        {
            LoggerFactory = loggerFactory;
            EnableBackgroundTokenRefresh = enableBackgroundTokenRefresh;

            if (plcDirectoryServer is not null)
            {
                PlcDirectoryServer = plcDirectoryServer;
            }

            OAuthOptions = oAuthOptions;
            HttpClientOptions = httpClientOptions;
            HttpJsonOptions = httpJsonOptions;
        }

        /// <summary>
        /// Gets or sets the <see cref="ILoggerFactory"/>, if any, to use when creating loggers.
        /// </summary>
        public ILoggerFactory? LoggerFactory { get; set; }

        /// <summary>
        /// Specifies whether to enable background access token refresh.
        /// </summary>
        public bool EnableBackgroundTokenRefresh { get; set; } = true;

        /// <summary>
        /// Specifies the server to use when resolving plc DIDs.
        /// </summary>
        public Uri PlcDirectoryServer { get; set; } = new("https://plc.directory");

        /// <summary>
        /// Gets or sets any <see cref="JsonOptions"/> to use during serialization and deserialization."/>
        /// </summary>
        public JsonOptions? HttpJsonOptions { get; set; }

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
