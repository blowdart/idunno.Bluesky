// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

namespace idunno.AtProto.Jetstream
{
    /// <summary>
    /// WebSocket options for the underlying client.
    /// </summary>
    public sealed record WebSocketOptions
    {
        /// <summary>
        /// Gets the proxy for WebSocket requests.
        /// </summary>
        public IWebProxy? Proxy { get; init; }

        /// <summary>
        /// Gets the WebSocket protocol keep-alive interval.
        /// </summary>
        public TimeSpan? KeepAliveInterval { get; init; }
    }
}
