// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Configures options for an instance of <see cref="AtProtoJetstream"/>.
/// </summary>
public record JetstreamOptions
{
    /// <summary>
    /// Gets or sets the <see cref="ILoggerFactory"/>, if any, to use when creating loggers.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="IMeterFactory"/>, if any, to use when creating meters.
    /// </summary>
    public IMeterFactory? MeterFactory { get; set; }

    /// <summary>
    /// Gets a flag indicating whether the underlying WebSocket should use compression. Defaults to <see langword="true"/>.
    /// </summary>
    public bool UseCompression { get; init; } = true;

    /// <summary>
    /// Gets the dictionary to use for zst decompression.
    /// </summary>
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Used as an array by zst")]
    public byte[]? Dictionary { get; init; } = Resource.zstDictionary;

    /// <summary>
    /// Gets the TaskFactory to use when creating new tasks.
    /// </summary>
    public TaskFactory TaskFactory { get; init; } = new TaskFactory(TaskScheduler.Default);

    /// <summary>
    /// Gets the size, in bytes, of each block read from the web socket. Defaults to 8096 bytes (8 KB).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than or equal to zero.</exception>
    /// <remarks>
    /// <para>
    ///   This is the size of the buffer a single read fills, not a limit on anything. A message larger than this is read in
    ///   several blocks and reassembled. The limit on how large a message may be is <see cref="MaxMessageSize"/>.
    /// </para>
    /// </remarks>
    public int BufferSize
    {
        get;

        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

            field = value;
        }
    } = 8096;

    /// <summary>
    /// Gets the maximum total message size, in bytes. Messages exceeding this limit will be rejected. Defaults to 1 MB.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than or equal to zero.</exception>
    /// <remarks>
    /// <para>
    ///   The limit applies to the message itself, so when <see cref="UseCompression"/> is <see langword="true"/> it bounds the
    ///   decompressed message rather than the compressed frame it arrived in. A compressed frame can declare, and expand to, far
    ///   more than it occupies on the wire, so bounding only what was received would place no limit on what is allocated.
    /// </para>
    /// <para>
    ///   It is also sent to the server, which will not send a message larger than this.
    /// </para>
    /// </remarks>
    public int MaxMessageSize
    {
        get;

        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

            field = value;
        }
    } = WebSocketExtensions.DefaultMaxMessageSize;

    /// <summary>
    /// Gets how long to wait for a server to answer a close handshake before the connection is aborted instead. Defaults to 30 seconds.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than or equal to zero.</exception>
    /// <remarks>
    /// <para>
    ///   A graceful close is only complete once the server has replied to it, so without a deadline a server which never replies
    ///   holds the caller of <see cref="AtProtoJetstream.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus, string, CancellationToken)"/>
    ///   for as long as it cares to.
    /// </para>
    /// </remarks>
    public TimeSpan CloseTimeout
    {
        get;

        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);

            field = value;
        }
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the maximum number of messages which may be parsed at once. Defaults to 64.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than or equal to zero.</exception>
    /// <remarks>
    /// <para>
    ///   Messages are parsed away from the loop which reads them, so without a bound a server which sends faster than the parsing
    ///   keeps up with has every message it sends queued behind the ones still being parsed, and nothing stops that queue growing.
    ///   <see cref="MaxMessageSize"/> limits how large a single message may be, not how many of them may be in flight.
    /// </para>
    /// <para>
    ///   Once this many messages are being parsed the jetstream stops reading from the web socket until one of them finishes,
    ///   which is what lets the transport apply back pressure to the server.
    /// </para>
    /// </remarks>
    public int MaximumConcurrentMessageParsers
    {
        get;

        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

            field = value;
        }
    } = 64;
}