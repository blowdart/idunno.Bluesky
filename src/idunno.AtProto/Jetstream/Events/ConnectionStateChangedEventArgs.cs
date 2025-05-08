// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.WebSockets;

namespace idunno.AtProto.Jetstream.Events
{
    /// <summary>
    /// Encapsulates information given when the state of the underlying WebSocket in a <see cref="Jetstream"/> instance changes.
    /// </summary>
    public sealed class ConnectionStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of <see cref="ConnectionStateChangedEventArgs"/> with the specified <paramref name="state"/>.
        /// </summary>
        /// <param name="state">The new state of the underlying WebSocket.</param>
        public ConnectionStateChangedEventArgs(WebSocketState state) => State = state;

        /// <summary>
        /// Gets the new state of the underlying WebSocket.
        /// </summary>
        public WebSocketState State { get; }
    }
}
