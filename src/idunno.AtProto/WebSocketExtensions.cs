// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

internal static class WebSocketExtensions
{
    /// <summary>
    /// The default maximum message size, in bytes. Defaults to 1 MB.
    /// </summary>
    internal const int DefaultMaxMessageSize = 1048576;

    /// <summary>
    /// Reads blocks from the specified <paramref name="webSocket"/>, until an end of message is encountered,
    /// or the web socket is no longer open, and returns the final <see cref="WebSocketReceiveResult"/>
    /// and a byte array containing the read message.
    /// </summary>
    /// <param name="webSocket">The <see cref="ClientWebSocket"/> to read the message from.</param>
    /// <param name="bufferSize">The maximum block size, in bytes, to read from <paramref name="webSocket"/>.</param>
    /// <param name="maxMessageSize">The maximum total message size, in bytes. Defaults to 1 MB.</param>
    /// <param name="logger">The <see cref="ILogger"/> to use for logging, if any.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="WebSocketException">Thrown when the <paramref name="webSocket"/> is not open, or the message exceeds <paramref name="maxMessageSize"/>.</exception>
    public static async Task<(WebSocketReceiveResult Result, byte[] Message)> ReceiveNextMessageAsync(
        this ClientWebSocket webSocket,
        int bufferSize,
        int maxMessageSize = DefaultMaxMessageSize,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArraySegment<byte> buffer = new(new byte[bufferSize]);
        WebSocketReceiveResult receiveResult;
        byte[] message;

        if (webSocket.State != WebSocketState.Open)
        {
            throw new WebSocketException(WebSocketError.InvalidState);
        }

        using (MemoryStream ms = new())
        {
            do
            {
                receiveResult = await webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (ms.Length + receiveResult.Count > maxMessageSize)
                {
                    if (logger is not null)
                    {
                        Logger.ReceivedMessageTooLarge(logger, (int)ms.Length + receiveResult.Count, maxMessageSize);
                    }

                    throw new WebSocketException(WebSocketError.InvalidMessageType, $"Message exceeds the maximum allowed size of {maxMessageSize} bytes.");
                }

                await ms.WriteAsync(
                    buffer.AsMemory(buffer.Offset, receiveResult.Count),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            } while (!receiveResult.EndOfMessage && webSocket.State == WebSocketState.Open);

            await ms.FlushAsync(cancellationToken).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            message = ms.ToArray();
            ms.Close();

            return (receiveResult, message);
        }
    }
}
