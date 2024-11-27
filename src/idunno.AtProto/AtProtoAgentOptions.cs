// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

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
    }
}
