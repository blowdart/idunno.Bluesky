// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


namespace idunno.AtProto
{
    public class AtProtoAgentOptions : AgentOptions
    {
        /// <summary>
        /// Specifies whether to enable background access token refresh.
        /// </summary>
        public bool EnableBackgroundTokenRefresh { get; set; } = true;

        /// <summary>
        /// Specifies the server to use when resolving plc DIDs.
        /// </summary>
        public Uri PlcDirectoryServer { get; set; } = new("https://plc.directory");
    }
}
