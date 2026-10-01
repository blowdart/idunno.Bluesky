// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Text;

namespace idunno.AtProto;

/// <summary>
/// An HTTP response body held in a buffer rented from <see cref="ArrayPool{T}.Shared"/>.
/// </summary>
/// <remarks>
/// <para>
///   Dispose an instance to return its buffer to the pool. Nothing which outlives the instance may keep a reference to
///   the buffer, so parse it into objects which own their data, rather than, for example, a <see cref="System.Text.Json.JsonDocument"/>
///   over its memory.
/// </para>
/// </remarks>
internal sealed class PooledContent : IDisposable
{
    private byte[]? _buffer;
    private readonly int _length;

    /// <summary>
    /// Creates a new instance of <see cref="PooledContent"/>, which takes ownership of <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">A buffer rented from <see cref="ArrayPool{T}.Shared"/>.</param>
    /// <param name="length">The number of bytes of <paramref name="buffer"/> which hold content.</param>
    public PooledContent(byte[] buffer, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, buffer.Length);

        _buffer = buffer;
        _length = length;
    }

    /// <summary>
    /// Gets the content, without any leading UTF-8 byte order mark.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
    public ReadOnlySpan<byte> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(_buffer is null, this);

            ReadOnlySpan<byte> content = new(_buffer, 0, _length);

            return content.StartsWith(Encoding.UTF8.Preamble) ? content[Encoding.UTF8.Preamble.Length..] : content;
        }
    }

    /// <summary>
    /// Decodes the content as a UTF-8 string.
    /// </summary>
    /// <returns>The content as a string.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
    public override string ToString() => Encoding.UTF8.GetString(Span);

    /// <summary>
    /// Returns the buffer to the pool.
    /// </summary>
    public void Dispose()
    {
        byte[]? buffer = Interlocked.Exchange(ref _buffer, null);

        if (buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
