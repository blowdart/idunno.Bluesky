// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.WebSockets;

namespace idunno.AtProto.Jetstream.Events
{
    /// <summary>
    /// Encapsulates information given when the state of the underlying WebSocket in a <see cref="Jetstream"/> instance changes.
    /// </summary>
    /// <remarks>
    /// Creates a new instance of <see cref="ConnectionStateChangedEventArgs"/> with the specified <paramref name="state"/>.
    /// </remarks>
    /// <param name="state">The new state of the underlying WebSocket.</param>
    public sealed class ConnectionStateChangedEventArgs(WebSocketState state) : EventArgs
    {

        /// <summary>
        /// Gets the new state of the underlying WebSocket.
        /// </summary>
        public WebSocketState State { get; } = state;
    }
}
