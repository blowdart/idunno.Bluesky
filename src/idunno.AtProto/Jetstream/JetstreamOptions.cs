// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace idunno.AtProto.Jetstream
{
    /// <summary>
    /// Configures options for an instance of <see cref="Jetstream"/>.
    /// </summary>
    public record JetstreamOptions
    {
        private int _readBufferSize = 8096;

        /// <summary>
        /// Gets or sets the <see cref="ILoggerFactory"/>, if any, to use when creating loggers.
        /// </summary>
        public ILoggerFactory? LoggerFactory { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IMeterFactory"/>, if any, to use when creating meters.
        /// </summary>
        public IMeterFactory? MeterFactory { get; set; }

        /// <summary>
        /// Gets a flag indicating whether the underlying WebSocket should use compression. Defaults to true.
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
        /// Gets the maximum size of messages to receive, in bytes.
        /// </summary>
        public int MaximumMessageSize
        {
            get
            {
                return _readBufferSize;
            }

            init
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

                _readBufferSize = value;
            }
        }

    }
}
