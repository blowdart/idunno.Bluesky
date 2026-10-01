// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
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
    /// <param name="webSocket">The <see cref="WebSocket"/> to read the message from.</param>
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
        this WebSocket webSocket,
        int bufferSize,
        int maxMessageSize = DefaultMaxMessageSize,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        if (webSocket.State != WebSocketState.Open)
        {
            throw new WebSocketException(WebSocketError.InvalidState);
        }

        // Both buffers are rented, and the message is copied out of them once it is complete. Most messages arrive in a
        // single fragment, which is copied straight from the receive buffer without being assembled at all.
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        byte[]? assembled = null;
        int assembledLength = 0;

        try
        {
            WebSocketReceiveResult receiveResult;
            int consecutiveEmptyFragments = 0;

            do
            {
                receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, 0, bufferSize), cancellationToken).ConfigureAwait(false);

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

                if ((long)assembledLength + receiveResult.Count > maxMessageSize)
                {
                    if (logger is not null)
                    {
                        Logger.ReceivedMessageTooLarge(logger, assembledLength + receiveResult.Count, maxMessageSize);
                    }

                    throw new WebSocketMessageAbandonedException(
                        $"Message exceeds the maximum allowed size of {maxMessageSize} bytes.",
                        WebSocketCloseStatus.MessageTooBig);
                }

                if (assembled is null && receiveResult.EndOfMessage)
                {
                    return (receiveResult, buffer.AsSpan(0, receiveResult.Count).ToArray());
                }

                assembled = EnsureCapacity(assembled, assembledLength, assembledLength + receiveResult.Count, bufferSize);
                buffer.AsSpan(0, receiveResult.Count).CopyTo(assembled.AsSpan(assembledLength));
                assembledLength += receiveResult.Count;
            } while (!receiveResult.EndOfMessage && webSocket.State == WebSocketState.Open);

            if (!receiveResult.EndOfMessage)
            {
                // The socket closed part way through a message. What has been read is a fragment of a message rather
                // than a message, so returning it would hand the caller something it cannot parse and no way to tell
                // that apart from a message which really did end there.
                throw new WebSocketException(WebSocketError.InvalidState, "The web socket closed before the end of the message was reached.");
            }

            return (receiveResult, assembled.AsSpan(0, assembledLength).ToArray());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);

            if (assembled is not null)
            {
                ArrayPool<byte>.Shared.Return(assembled);
            }
        }
    }

    /// <summary>
    /// Returns a pooled buffer of at least <paramref name="required"/> bytes, holding the first <paramref name="used"/> bytes of <paramref name="current"/>.
    /// </summary>
    /// <param name="current">The current pooled buffer, if any, which is returned to the pool if a larger one is needed.</param>
    /// <param name="used">The number of bytes of <paramref name="current"/> in use.</param>
    /// <param name="required">The number of bytes the buffer must hold.</param>
    /// <param name="minimumSize">The smallest buffer to rent.</param>
    /// <returns>A pooled buffer of at least <paramref name="required"/> bytes.</returns>
    private static byte[] EnsureCapacity(byte[]? current, int used, int required, int minimumSize)
    {
        if (current is not null && current.Length >= required)
        {
            return current;
        }

        // Doubling keeps the number of copies logarithmic in the message size. The size is grown through a long so a
        // large buffer cannot overflow, and the maximum message size has already bounded required.
        long grownSize = Math.Max(Math.Max(required, minimumSize), (long)(current?.Length ?? 0) * 2);
        byte[] grown = ArrayPool<byte>.Shared.Rent((int)Math.Min(grownSize, Array.MaxLength));

        if (current is not null)
        {
            current.AsSpan(0, used).CopyTo(grown);
            ArrayPool<byte>.Shared.Return(current);
        }

        return grown;
    }
}
