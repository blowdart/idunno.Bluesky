// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace idunno.AtProto.FireHose;

/// <summary>
/// Configures options for an instance of <see cref="AtProtoFireHose"/>.
/// </summary>
public record FireHoseOptions
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
    /// Gets the TaskFactory to use when creating new tasks.
    /// </summary>
    public TaskFactory TaskFactory { get; init; } = new TaskFactory(TaskScheduler.Default);

    /// <summary>
    /// Gets the maximum number of bytes to read from the web socket.
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
