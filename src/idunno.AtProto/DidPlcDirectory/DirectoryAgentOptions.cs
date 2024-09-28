// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.DidPlcDirectory
{
    /// <summary>
    /// A class for configuring the DirectoryAgent.
    /// </summary>
    public class DirectoryAgentOptions : AgentOptions
    {
        /// <summary>
        /// Specifies the server to use when resolving plc DIDs.
        /// </summary>
        public Uri PlcDirectoryUri { get; set; } = DirectoryAgent.s_defaultDirectoryServer;
    }
}
