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
    /// Gets the maximum size of messages to receive, in bytes. Defaults to 8096 bytes (8 KB).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than or equal to zero.</exception>
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
    public int MaxMessageSize
    {
        get;

        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

            field = value;
        }
    } = WebSocketExtensions.DefaultMaxMessageSize;
}
