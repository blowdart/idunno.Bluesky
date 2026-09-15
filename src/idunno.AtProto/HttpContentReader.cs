// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Text;

namespace idunno.AtProto;

/// <summary>
/// Reads HTTP response bodies without allowing a remote server to dictate how much memory is allocated.
/// </summary>
/// <remarks>
/// <para>
///   Responses must be requested with <see cref="HttpCompletionOption.ResponseHeadersRead"/> for these methods to bound
///   anything. The default completion option buffers the entire body before the response message is returned, which
///   allocates the memory these methods exist to avoid.
/// </para>
/// </remarks>
internal static class HttpContentReader
{
    /// <summary>
    /// Reads <paramref name="content"/> as a string, rejecting it if it is longer than <paramref name="maximumLength"/> bytes.
    /// </summary>
    /// <param name="content">The <see cref="HttpContent"/> to read.</param>
    /// <param name="maximumLength">The maximum number of bytes to accept.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>
    /// The task object representing the asynchronous operation, whose result is the content as a string,
    /// or <see langword="null"/> if the content is longer than <paramref name="maximumLength"/>.
    /// </returns>
    public static async Task<string?> ReadAsString(HttpContent content, int maximumLength, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);

        if (content.Headers.ContentLength > maximumLength)
        {
            return null;
        }

        // Read one byte more than the maximum so an over-long response can be detected rather than silently truncated.
        (byte[] buffer, int bytesRead) = await ReadAtMost(content, maximumLength + 1, cancellationToken).ConfigureAwait(false);

        try
        {
            if (bytesRead > maximumLength)
            {
                return null;
            }

            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Reads at most <paramref name="maximumLength"/> bytes of <paramref name="content"/> as a string, truncating anything longer.
    /// </summary>
    /// <param name="content">The <see cref="HttpContent"/> to read.</param>
    /// <param name="maximumLength">The maximum number of bytes to read.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>
    /// The task object representing the asynchronous operation, whose result is as much of the content as was allowed to be read.
    /// </returns>
    /// <remarks>
    /// <para>
    ///   Truncation may leave the result unparsable. Use this only where a partial value is more useful than none, such as
    ///   when capturing a response body for diagnostics.
    /// </para>
    /// </remarks>
    public static async Task<string> ReadAsStringTruncating(HttpContent content, int maximumLength, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);

        (byte[] buffer, int bytesRead) = await ReadAtMost(content, maximumLength, cancellationToken).ConfigureAwait(false);

        try
        {
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<(byte[] Buffer, int BytesRead)> ReadAtMost(HttpContent content, int maximumLength, CancellationToken cancellationToken)
    {
        // Size the initial buffer from Content-Length when it is known and plausible, otherwise start small and grow.
        // Renting the maximum up front would allocate it on every response, and ArrayPool.Shared allocates outright
        // above its maximum retained array size rather than pooling.
        const int initialBufferSize = 8 * 1024;

        int initialLength = content.Headers.ContentLength is long contentLength && contentLength > 0 && contentLength <= maximumLength
            ? (int)Math.Min(contentLength, maximumLength)
            : Math.Min(initialBufferSize, maximumLength);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(initialLength);
        int bytesRead = 0;

        try
        {
            using (Stream contentStream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            {
                while (bytesRead < maximumLength)
                {
                    if (bytesRead == buffer.Length)
                    {
                        // Grow through a long, as doubling a buffer larger than half of int.MaxValue would otherwise
                        // overflow to a negative length and throw from Rent rather than stopping at maximumLength.
                        byte[] grown = ArrayPool<byte>.Shared.Rent((int)Math.Min((long)buffer.Length * 2, maximumLength));
                        Buffer.BlockCopy(buffer, 0, grown, 0, bytesRead);
                        ArrayPool<byte>.Shared.Return(buffer);
                        buffer = grown;
                    }

                    // The rented buffer may be larger than requested, so bound the read by the requested length rather than by its size.
                    int available = Math.Min(buffer.Length, maximumLength) - bytesRead;

                    int read = await contentStream.ReadAsync(buffer.AsMemory(bytesRead, available), cancellationToken).ConfigureAwait(false);

                    if (read == 0)
                    {
                        break;
                    }

                    bytesRead += read;
                }
            }
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
            throw;
        }

        return (buffer, bytesRead);
    }
}
