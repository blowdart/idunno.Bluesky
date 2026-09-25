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
    /// The maximum number of consecutive empty, non-final fragments to tolerate before abandoning a message.
    /// </summary>
    /// <remarks>
    /// <para>An empty fragment adds nothing to the message being assembled, so the maximum message size can never
    /// stop a peer which sends them endlessly. Without a limit of its own the read loop would run for as long as
    /// the peer cared to keep sending.</para>
    /// </remarks>
    private const int MaximumConsecutiveEmptyFragments = 16;

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
    /// <exception cref="WebSocketException">Thrown when the <paramref name="webSocket"/> is not open, or closed before the end of the message was reached.</exception>
    /// <exception cref="WebSocketMessageAbandonedException">Thrown when the message exceeds <paramref name="maxMessageSize"/>, or is made up of too many consecutive empty fragments.</exception>
    /// <remarks>
    /// <para>A message which is abandoned part way through leaves the fragments which make up the rest of it queued on the
    /// socket, so a <see cref="WebSocketMessageAbandonedException"/> means the connection can no longer be read from.</para>
    /// </remarks>
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
            int consecutiveEmptyFragments = 0;

            do
            {
                receiveResult = await webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (receiveResult.Count == 0 && !receiveResult.EndOfMessage)
                {
                    consecutiveEmptyFragments++;

                    if (consecutiveEmptyFragments > MaximumConsecutiveEmptyFragments)
                    {
                        if (logger is not null)
                        {
                            Logger.ReceivedTooManyEmptyFragments(logger, MaximumConsecutiveEmptyFragments);
                        }

                        throw new WebSocketMessageAbandonedException(
                            $"Message contained more than {MaximumConsecutiveEmptyFragments} consecutive empty fragments.",
                            WebSocketCloseStatus.ProtocolError);
                    }
                }
                else
                {
                    consecutiveEmptyFragments = 0;
                }

                if (ms.Length + receiveResult.Count > maxMessageSize)
                {
                    if (logger is not null)
                    {
                        Logger.ReceivedMessageTooLarge(logger, (int)ms.Length + receiveResult.Count, maxMessageSize);
                    }

                    throw new WebSocketMessageAbandonedException(
                        $"Message exceeds the maximum allowed size of {maxMessageSize} bytes.",
                        WebSocketCloseStatus.MessageTooBig);
                }

                await ms.WriteAsync(
                    buffer.AsMemory(buffer.Offset, receiveResult.Count),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            } while (!receiveResult.EndOfMessage && webSocket.State == WebSocketState.Open);

            if (!receiveResult.EndOfMessage)
            {
                // The socket closed part way through a message. What has been read is a fragment of a message rather
                // than a message, so returning it would hand the caller something it cannot parse and no way to tell
                // that apart from a message which really did end there.
                throw new WebSocketException(WebSocketError.InvalidState, "The web socket closed before the end of the message was reached.");
            }

            await ms.FlushAsync(cancellationToken).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            message = ms.ToArray();
            ms.Close();

            return (receiveResult, message);
        }
    }
}