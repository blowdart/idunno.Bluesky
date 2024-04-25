// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto
{
    /// <summary>
    /// Contains URIs for the services to connect to if a service is not specified.
    /// </summary>
    internal class DefaultServices
    {
        /// <summary>
        /// The default Bluesky service to make authenticated requests to.
        /// </summary>
        public static readonly Uri DefaultService = new("https://bsky.social");

        /// <summary>
        /// The default Bluesky service to make unauthenticated requests to.
        /// </summary>
        public static readonly Uri ReadOnlyService = new("https://api.bsky.app");

        /// <summary>
        /// The default Directory server to make requests to.
        /// </summary>
        public static readonly Uri DefaultDirectoryServer = new("https://plc.directory");
    }
}
