// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.WebSockets;

namespace idunno.AtProto.Jetstream.Events
{
    /// <summary>
    /// Holds information for a connection state changed event.
    /// </summary>
    /// <param name="state">The <see cref="WebSocketState"/> of the underlying WebSocket.</param>
    public sealed class ConnectionStateChangedEventArgs(WebSocketState state) : EventArgs
    {
        /// <summary>
        /// Gets the new state of the underlying WebSocket.
        /// </summary>
        public WebSocketState State { get; } = state;
    }
}
