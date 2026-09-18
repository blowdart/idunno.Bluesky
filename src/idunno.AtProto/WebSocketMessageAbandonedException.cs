// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.WebSockets;

namespace idunno.AtProto;

/// <summary>
/// Thrown when a message is abandoned part way through being read from a web socket.
/// </summary>
/// <remarks>
/// <para>The fragments which make up the rest of the abandoned message are still queued on the socket, and a web socket
/// gives no way to skip them, so the next read returns the tail of the abandoned message as though it were a message in
/// its own right. The connection is no longer usable, and the <see cref="CloseStatus"/> says why, so the peer can be told.</para>
/// <para>It does not derive from <see cref="WebSocketException"/>, which is sealed.</para>
/// </remarks>
public sealed class WebSocketMessageAbandonedException : Exception
{
    /// <summary>
    /// Creates a new instance of <see cref="WebSocketMessageAbandonedException"/>.
    /// </summary>
    public WebSocketMessageAbandonedException()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="WebSocketMessageAbandonedException"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public WebSocketMessageAbandonedException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="WebSocketMessageAbandonedException"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public WebSocketMessageAbandonedException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="WebSocketMessageAbandonedException"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="closeStatus">The <see cref="WebSocketCloseStatus"/> to close the connection with.</param>
    public WebSocketMessageAbandonedException(string message, WebSocketCloseStatus closeStatus) : base(message)
    {
        CloseStatus = closeStatus;
    }

    /// <summary>
    /// Gets the <see cref="WebSocketCloseStatus"/> the connection should be closed with.
    /// </summary>
    public WebSocketCloseStatus CloseStatus { get; } = WebSocketCloseStatus.ProtocolError;
}
