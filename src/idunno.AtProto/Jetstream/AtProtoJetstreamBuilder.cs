// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.AtProto.Jetstream
{
    /// <summary>
    /// A builder to configure and create an instance of <see cref="AtProtoJetstream"/>.
    /// </summary>
    public sealed class AtProtoJetstreamBuilder
    {
        private static readonly Uri s_defaultUri = new ("wss://jetstream1.us-west.bsky.network");

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoJetstreamBuilder"/>.
        /// </summary>
        internal AtProtoJetstreamBuilder()
        {
        }

        /// <summary>
        /// Gets or sets the service the agent will initially connect to.
        /// </summary>
        public Uri Service { get; set; } = s_defaultUri;

        /// <summary>
        /// Gets or sets the <see cref="ILoggerFactory"/> to use when creating loggers.
        /// </summary>
        public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;

        /// <summary>
        /// Gets or sets the <see cref="IMeterFactory"/> to use when creating meters.
        /// </summary>
        public IMeterFactory? MeterFactory { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether compression should be used with the stream.
        /// </summary>
        public bool EnableCompression { get; set; }

        /// <summary>
        /// Gets or sets the compression dictionary used by zst decompression when <see cref="EnableCompression"/> is <see langword="true"/>.
        /// </summary>
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "zst expects a dictionary and we're not concerned about mutability.")]
        public byte[] CompressionDictionary { get; set; } = Resource.zstDictionary;

        /// <summary>
        /// Gets or sets the maximum message size the Jetstream should send.
        /// </summary>
        public int MaximumMessageSize { get; set; } = 8096;

        /// <summary>
        /// Gets or sets a custom <see cref="TaskFactory"/> to use for background message parsing.
        /// </summary>
        public TaskFactory TaskFactory { get; set; } = new TaskFactory(TaskScheduler.Default);

        /// <summary>
        /// Gets or sets any <see cref="Did"/>s to limit commit events to.
        /// </summary>
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This is meant to be settable.")]
        public ICollection<Did>? DidsToFilterOn { get; set; }

        /// <summary>
        /// Gets or sets any <see cref="Did"/>s to limit commit events to.
        /// </summary>
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This is meant to be settable.")]
        public ICollection<Nsid>? CollectionsToFilterOn { get; set; }

        /// <summary>
        /// Creates a new <see cref="AtProtoJetstreamBuilder"/>.
        /// </summary>
        /// <returns>A new <see cref="AtProtoJetstreamBuilder"/></returns>
        public static AtProtoJetstreamBuilder Create() => new();

        /// <summary>
        /// Sets the jetstream URI the <see cref="AtProtoJetstream"/> instance will connect to.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to initially connect to.</param>
        /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is <see langword="null"/></exception>
        public AtProtoJetstreamBuilder ConnectTo(Uri service)
        {
            ArgumentNullException.ThrowIfNull(service);

            Service = service;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="ILoggerFactory"/> to use when creating loggers.
        /// </summary>
        /// <param name="logger">The <see cref="ILoggerFactory"/> to use when creating loggers.</param>
        /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is <see langword="null"/></exception>
        public AtProtoJetstreamBuilder WithLoggerFactory(ILoggerFactory logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            LoggerFactory = logger;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="IMeterFactory"/> to use when creating meters.
        /// </summary>
        /// <param name="meterFactory">The <see cref="IMeterFactory"/> to use when creating loggers.</param>
        /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="meterFactory"/> is <see langword="null"/></exception>
        public AtProtoJetstreamBuilder WithMeterFactory(IMeterFactory meterFactory)
        {
            ArgumentNullException.ThrowIfNull(meterFactory);

            MeterFactory = meterFactory;
            return this;
        }

        /// <summary>
        /// Configures the <see cref="AtProtoJetstream"/> to use compression
        /// </summary>
        /// <param name="useCompression">A flag indication whether compression should be enabled or not.</param>
        /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
        public AtProtoJetstreamBuilder UseCompression(bool useCompression)
        {
            EnableCompression = useCompression;

            return this;
        }

        /// <summary>
        /// Configures the compression dictionary to use when <see cref="EnableCompression"/> is <see langword="true"/>.
        /// </summary>
        /// <param name="compressionDictionary">A byte[] containing a custom zst dictionary.</param>
        /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
        public AtProtoJetstreamBuilder WithCompressionDictionary(byte [] compressionDictionary)
        {
            CompressionDictionary = compressionDictionary;

            return this;
        }

        /// <summary>
        /// Configures a custom <see cref="TaskFactory"/> to use for background message parsing.
        /// </summary>
        /// <param name="taskFactory">A custom <see cref="TaskFactory"/> to use for background message parsing.</param>
        /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
        public AtProtoJetstreamBuilder WithTaskFactory(TaskFactory taskFactory)
        {
            TaskFactory = taskFactory;

            return this;
        }

        /// <summary>
        /// Configures the maximum message size the <see cref="AtProtoJetstream"/> should send.
        /// </summary>
        /// <param name="maximumMessageSize">The maximum message size, in bytes, the jetstream should send.</param>
        /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumMessageSize"/> is equal to, or less than, zero.</exception>
        public AtProtoJetstreamBuilder SetMaximumMessageSize(int maximumMessageSize)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMessageSize);

            MaximumMessageSize = maximumMessageSize;

            return this;
        }

        /// <summary>
        /// Configures a filter for commit events to only raise events for the specified <paramref name="dids"/>.
        /// </summary>
        /// <param name="dids">The <see cref="Did"/>s to filter on.</param>
        /// <returns>A collection of <see cref="Did"/>s to limit commit events for.</returns>
        /// <remarks><para>Can be combined with <see cref="FilterTo(Nsid[])"/>.</para></remarks>
        public AtProtoJetstreamBuilder FilterTo(Did[] dids)
        {
            DidsToFilterOn = dids;

            return this;
        }

        /// <summary>
        /// Configures a filter for commit events to only raise events for the specified <paramref name="collections"/>.
        /// </summary>
        /// <param name="collections">The <see cref="Nsid"/> of any collections to filter on.</param>
        /// <returns>A collection of <see cref="Nsid"/>s to limit commit events for.</returns>
        /// <remarks><para>Can be combined with <see cref="FilterTo(Did[])"/>.</para></remarks>
        public AtProtoJetstreamBuilder FilterTo(Nsid[] collections)
        {
            CollectionsToFilterOn = collections;

            return this;
        }

        /// <summary>
        /// Builds a new instance of <see cref="AtProtoJetstream"/>.
        /// </summary>
        /// <returns>A configured <see cref="AtProtoJetstream"/>.</returns>
        public AtProtoJetstream Build()
        {
            return new AtProtoJetstream(
                uri: Service,
                options: new JetstreamOptions()
                {
                    LoggerFactory = LoggerFactory,
                    UseCompression = EnableCompression,
                    Dictionary = CompressionDictionary,
                    BufferSize = MaximumMessageSize,
                    TaskFactory = TaskFactory,
                },
                collections: CollectionsToFilterOn,
                dids: DidsToFilterOn);
        }
    }
}
