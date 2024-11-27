// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.DidPlcDirectory
{
    /// <summary>
    /// A class for configuring the DirectoryAgent.
    /// </summary>
    public class DirectoryAgentOptions
    {
        /// <summary>
        /// Specifies the server to use when resolving plc DIDs.
        /// </summary>
        public Uri PlcDirectoryUri { get; set; } = DirectoryAgent.s_defaultDirectoryServer;
    }
}
