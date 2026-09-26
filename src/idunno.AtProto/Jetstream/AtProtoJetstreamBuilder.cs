// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// A builder to configure and create an instance of <see cref="AtProtoJetstream"/>.
/// </summary>
public sealed class AtProtoJetstreamBuilder
{
    private Uri? _service;

    /// <summary>
    /// Creates a new instance of <see cref="AtProtoJetstreamBuilder"/>.
    /// </summary>
    internal AtProtoJetstreamBuilder()
    {
    }

    /// <summary>
    /// Gets or sets the service the agent will initially connect to.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>If no service has been set this is a Bluesky jetstream server which speaks the <see cref="ProtocolVersion"/> selected.</para>
    /// </remarks>
    public Uri Service
    {
        get => _service ?? (ProtocolVersion == JetstreamProtocolVersion.V1 ? AtProtoJetstream.s_defaultV1Uri : AtProtoJetstream.s_defaultV2Uri);

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _service = value;
        }
    }

    /// <summary>
    /// Gets or sets the version of the jetstream protocol to connect with. Defaults to <see cref="JetstreamProtocolVersion.V2"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value being set is not a defined <see cref="JetstreamProtocolVersion"/>.</exception>
    public JetstreamProtocolVersion ProtocolVersion
    {
        get;

        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            field = value;
        }
    } = JetstreamProtocolVersion.V2;

    /// <summary>
    /// Gets or sets the <see cref="ILoggerFactory"/> to use when creating loggers.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    public ILoggerFactory LoggerFactory
    {
        get;

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    } = NullLoggerFactory.Instance;

    /// <summary>
    /// Gets or sets the <see cref="IMeterFactory"/> to use when creating meters.
    /// </summary>
    public IMeterFactory? MeterFactory { get; set; }

    /// <summary>
    /// Gets or sets a flag indicating whether compression should be used with the stream. Defaults to <see langword="true"/>.
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Gets or sets the compression dictionary used by zst decompression when <see cref="EnableCompression"/> is <see langword="true"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Copied on the way in and on the way out, so a caller which holds on to the array it supplied cannot change
    /// what the decompressor reads after the jetstream has been built.</para>
    /// </remarks>
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "zst expects a dictionary.")]
    [SuppressMessage("Major Code Smell", "S2365:Properties should not make collection or array copies", Justification = "The copies are what stop a caller changing the dictionary whilst native code is reading it.")]
    public byte[] CompressionDictionary
    {
        get => (byte[])field.Clone();

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            field = (byte[])value.Clone();
        }
    } = Resource.zstDictionary;

    /// <summary>
    /// Gets or sets the size, in bytes, of each block read from the web socket.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value being set is equal to, or less than, zero.</exception>
    /// <remarks>
    /// <para>This is the size of the buffer a single read fills, not a limit on anything. Use <see cref="MaximumTotalMessageSize"/>
    /// to limit how large a message may be.</para>
    /// </remarks>
    public int ReadBufferSize
    {
        get;

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

            field = value;
        }
    } = 8096;

    /// <summary>
    /// Gets or sets the maximum total size of a message the jetstream will accept. Messages exceeding this limit are rejected.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value being set is equal to, or less than, zero.</exception>
    /// <remarks>
    /// <para>This is also the size sent to the server, which will not send a message larger than it.</para>
    /// </remarks>
    public int MaximumTotalMessageSize
    {
        get;

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

            field = value;
        }
    } = WebSocketExtensions.DefaultMaxMessageSize;

    /// <summary>
    /// Gets or sets the maximum number of messages the jetstream will parse at once.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value being set is equal to, or less than, zero.</exception>
    /// <remarks>
    /// <para>Once this many messages are being parsed the jetstream stops reading from the web socket until one of them
    /// finishes, which is what applies back pressure to a server sending faster than the parsing keeps up with.</para>
    /// </remarks>
    public int MaximumConcurrentMessageParsers
    {
        get;

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

            field = value;
        }
    } = 64;

    /// <summary>
    /// Gets or sets how long to wait for a server to answer a close handshake before the connection is aborted instead.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value being set is equal to, or less than, zero.</exception>
    public TimeSpan CloseTimeout
    {
        get;

        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);

            field = value;
        }
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long a single write to the web socket may take before it is abandoned.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value being set is equal to, or less than, zero.</exception>
    public TimeSpan SendTimeout
    {
        get;

        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);

            field = value;
        }
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets any <see cref="AtProto.WebSocketOptions"/> to set on the underlying client WebSocket.
    /// </summary>
    public WebSocketOptions? WebSocketOptions { get; set; }

    /// <summary>
    /// Gets or sets a custom <see cref="TaskFactory"/> to use for background message parsing.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    public TaskFactory TaskFactory
    {
        get;

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    } = new TaskFactory(TaskScheduler.Default);

    /// <summary>
    /// Gets or sets any <see cref="Did"/>s to limit commit events to.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This is meant to be settable.")]
    public ICollection<Did>? DidsToFilterOn { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Nsid"/>s of any collections to limit commit events to.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This is meant to be settable.")]
    public ICollection<Nsid>? CollectionsToFilterOn { get; set; }

    /// <summary>
    /// Gets or sets the kinds of event to limit events to.
    /// </summary>
    /// <remarks>
    /// <para>Filtering on event kinds is only supported by <see cref="JetstreamProtocolVersion.V2"/>.</para>
    /// </remarks>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This is meant to be settable.")]
    public ICollection<JetStreamEventKind>? KindsToFilterOn { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="IHttpClientFactory"/> to use when creating <see cref="HttpClient"/>s.
    /// </summary>
    /// <remarks>
    /// <para>If an <see cref="IHttpClientFactory"/> is set then <see cref="HttpClientOptions"/> will be ignored.</para>
    /// </remarks>
    public IHttpClientFactory? HttpClientFactory { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="AtProto.HttpClientOptions"/> for the jetstream.
    /// </summary>
    public HttpClientOptions? HttpClientOptions { get; set; }

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
    /// Sets the version of the jetstream protocol the <see cref="AtProtoJetstream"/> instance will connect with.
    /// </summary>
    /// <param name="protocolVersion">The version of the jetstream protocol to connect with.</param>
    /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="protocolVersion"/> is not a defined <see cref="JetstreamProtocolVersion"/>.</exception>
    /// <remarks>
    /// <para>If <see cref="ConnectTo(Uri)"/> has not been called the service connected to is a Bluesky jetstream server which speaks <paramref name="protocolVersion"/>.</para>
    /// </remarks>
    public AtProtoJetstreamBuilder UseProtocolVersion(JetstreamProtocolVersion protocolVersion)
    {
        if (!Enum.IsDefined(protocolVersion))
        {
            throw new ArgumentOutOfRangeException(nameof(protocolVersion));
        }

        ProtocolVersion = protocolVersion;
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
    /// Sets the compression dictionary to use when <see cref="EnableCompression"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="compressionDictionary">A byte[] containing a custom zst dictionary.</param>
    /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="compressionDictionary"/> is <see langword="null"/>.</exception>
    public AtProtoJetstreamBuilder WithCompressionDictionary(byte[] compressionDictionary)
    {
        ArgumentNullException.ThrowIfNull(compressionDictionary);

        CompressionDictionary = compressionDictionary;

        return this;
    }

    /// <summary>
    /// Configures a custom <see cref="TaskFactory"/> to use for background message parsing.
    /// </summary>
    /// <param name="taskFactory">A custom <see cref="TaskFactory"/> to use for background message parsing.</param>
    /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="taskFactory"/> is <see langword="null"/>.</exception>
    public AtProtoJetstreamBuilder WithTaskFactory(TaskFactory taskFactory)
    {
        ArgumentNullException.ThrowIfNull(taskFactory);

        TaskFactory = taskFactory;

        return this;
    }

    /// <summary>
    /// Configures the size, in bytes, of each block the <see cref="AtProtoJetstream"/> reads from the web socket.
    /// </summary>
    /// <param name="readBufferSize">The size, in bytes, of each block read from the web socket.</param>
    /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="readBufferSize"/> is equal to, or less than, zero.</exception>
    /// <remarks>
    /// <para>
    ///   This is the size of the buffer a single read fills, not a limit on anything. A message larger than this is read in several
    ///   blocks and reassembled, so setting it does not bound how much a message can allocate. Use <see cref="SetMaximumTotalMessageSize(int)"/>
    ///   for that.
    /// </para>
    /// </remarks>
    public AtProtoJetstreamBuilder SetReadBufferSize(int readBufferSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(readBufferSize);

        ReadBufferSize = readBufferSize;

        return this;
    }

    /// <summary>
    /// Configures the maximum total size of a message the <see cref="AtProtoJetstream"/> will accept.
    /// </summary>
    /// <param name="maximumTotalMessageSize">The maximum total size, in bytes, of a message the jetstream will accept.</param>
    /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumTotalMessageSize"/> is equal to, or less than, zero.</exception>
    /// <remarks>
    /// <para>
    ///   Messages larger than this are rejected rather than reassembled, so this is the ceiling on how much memory a single message can consume,
    ///   whereas <see cref="SetReadBufferSize(int)"/> configures the size of each chunk read from the socket.
    /// </para>
    /// </remarks>
    public AtProtoJetstreamBuilder SetMaximumTotalMessageSize(int maximumTotalMessageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTotalMessageSize);

        MaximumTotalMessageSize = maximumTotalMessageSize;

        return this;
    }

    /// <summary>
    /// Configures the maximum number of messages the <see cref="AtProtoJetstream"/> will parse at once.
    /// </summary>
    /// <param name="maximumConcurrentMessageParsers">The maximum number of messages to parse at once.</param>
    /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumConcurrentMessageParsers"/> is equal to, or less than, zero.</exception>
    /// <remarks>
    /// <para>
    ///   Messages are parsed away from the loop which reads them, so this bounds how many of them may be in flight at once, whereas
    ///   <see cref="SetMaximumTotalMessageSize(int)"/> bounds how large any one of them may be. Once the limit is reached the jetstream
    ///   stops reading from the web socket until a parse finishes.
    /// </para>
    /// </remarks>
    public AtProtoJetstreamBuilder SetMaximumConcurrentMessageParsers(int maximumConcurrentMessageParsers)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumConcurrentMessageParsers);

        MaximumConcurrentMessageParsers = maximumConcurrentMessageParsers;

        return this;
    }

    /// <summary>
    /// Configures how long the <see cref="AtProtoJetstream"/> waits for a server to answer a close handshake.
    /// </summary>
    /// <param name="closeTimeout">How long to wait for a server to answer a close handshake before aborting the connection instead.</param>
    /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="closeTimeout"/> is equal to, or less than, zero.</exception>
    public AtProtoJetstreamBuilder SetCloseTimeout(TimeSpan closeTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(closeTimeout, TimeSpan.Zero);

        CloseTimeout = closeTimeout;

        return this;
    }

    /// <summary>
    /// Configures how long a single write to the web socket may take before the <see cref="AtProtoJetstream"/> abandons it.
    /// </summary>
    /// <param name="sendTimeout">How long a single write to the web socket may take before it is abandoned.</param>
    /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sendTimeout"/> is equal to, or less than, zero.</exception>
    public AtProtoJetstreamBuilder SetSendTimeout(TimeSpan sendTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sendTimeout, TimeSpan.Zero);

        SendTimeout = sendTimeout;

        return this;
    }

    /// <summary>
    /// Sets the <see cref="AtProto.WebSocketOptions"/> to apply to the underlying client WebSocket.
    /// </summary>
    /// <param name="webSocketOptions">The <see cref="AtProto.WebSocketOptions"/> to apply to the underlying client WebSocket.</param>
    /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="webSocketOptions"/> is <see langword="null"/>.</exception>
    public AtProtoJetstreamBuilder WithWebSocketOptions(WebSocketOptions webSocketOptions)
    {
        ArgumentNullException.ThrowIfNull(webSocketOptions);

        WebSocketOptions = webSocketOptions;

        return this;
    }

    /// <summary>
    /// Configures a filter for commit events to only raise events for the specified <paramref name="dids"/>.
    /// </summary>
    /// <param name="dids">The <see cref="Did"/>s to filter on.</param>
    /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dids"/> is <see langword="null"/>.</exception>
    /// <remarks><para>Can be combined with <see cref="FilterTo(Nsid[])"/>.</para></remarks>
    public AtProtoJetstreamBuilder FilterTo(Did[] dids)
    {
        ArgumentNullException.ThrowIfNull(dids);

        DidsToFilterOn = dids;

        return this;
    }

    /// <summary>
    /// Configures a filter for commit events to only raise events for the specified <paramref name="collections"/>.
    /// </summary>
    /// <param name="collections">The <see cref="Nsid"/> of any collections to filter on.</param>
    /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="collections"/> is <see langword="null"/>.</exception>
    /// <remarks><para>Can be combined with <see cref="FilterTo(Did[])"/>.</para></remarks>
    public AtProtoJetstreamBuilder FilterTo(Nsid[] collections)
    {
        ArgumentNullException.ThrowIfNull(collections);

        CollectionsToFilterOn = collections;

        return this;
    }

    /// <summary>
    /// Configures a filter to only raise events of the specified <paramref name="kinds"/>.
    /// </summary>
    /// <param name="kinds">The <see cref="JetStreamEventKind"/>s to filter on.</param>
    /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="kinds"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Filtering on event kinds is only supported by <see cref="JetstreamProtocolVersion.V2"/>.</para>
    /// <para>A collection filter only applies to commit events, so combining one with kinds which do not include
    /// <see cref="JetStreamEventKind.Commit"/> is rejected when connecting.</para>
    /// </remarks>
    public AtProtoJetstreamBuilder FilterTo(JetStreamEventKind[] kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);

        KindsToFilterOn = kinds;

        return this;
    }

    /// <summary>
    /// Sets the <see cref="IHttpClientFactory"/> to use when creating <see cref="HttpClient"/>s.
    /// </summary>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use.</param>
    /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClientFactory"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>If an <see cref="IHttpClientFactory"/> is set then <see cref="HttpClientOptions"/> will be ignored.</para>
    /// <para>
    ///   The factory has to have been configured with
    ///   <see cref="ServiceCollectionExtensions.AddAtProtoHttpClient(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>. Any other
    ///   registration produces a client without the SSRF protections a jetstream would otherwise apply for itself.
    /// </para>
    /// </remarks>
    public AtProtoJetstreamBuilder WithHttpClientFactory(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        HttpClientFactory = httpClientFactory;

        return this;
    }

    /// <summary>
    /// Sets the <see cref="AtProto.HttpClientOptions"/> the jetstream will use when making HTTP requests.
    /// </summary>
    /// <param name="configure">An action to configure the <see cref="AtProto.HttpClientOptions"/> the jetstream will use when making HTTP requests.</param>
    /// <returns>The same instance of <see cref="AtProtoJetstreamBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Setting <see cref="HttpClientOptions.CheckCertificateRevocationList"/> to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
    /// <see langword="false"/> if you are using a debugging proxy which does not support CRLs.
    /// </para>
    /// </remarks>
    public AtProtoJetstreamBuilder ConfigureHttpClientOptions(Action<HttpClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        HttpClientOptions configuredOptions = new();
        configure(configuredOptions);

        HttpClientOptions = configuredOptions;

        return this;
    }

    /// <summary>
    /// Builds a new instance of <see cref="AtProtoJetstream"/>.
    /// </summary>
    /// <returns>A configured <see cref="AtProtoJetstream"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the filters are larger than <see cref="ProtocolVersion"/> allows.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="KindsToFilterOn"/> contains a kind which cannot be filtered on.</exception>
    /// <exception cref="NotSupportedException">Thrown when <see cref="KindsToFilterOn"/> is not empty and <see cref="ProtocolVersion"/> is <see cref="JetstreamProtocolVersion.V1"/>.</exception>
    public AtProtoJetstream Build()
    {
        // Checked before the jetstream is created, so an invalid filter does not leave a jetstream behind to dispose.
        if (KindsToFilterOn is not null && KindsToFilterOn.Count > 0)
        {
            if (ProtocolVersion == JetstreamProtocolVersion.V1)
            {
                throw new NotSupportedException("Filtering by event kind needs a version 2 jetstream.");
            }

            if (KindsToFilterOn.Any(kind => kind == JetStreamEventKind.Unknown || !Enum.IsDefined(kind)))
            {
                throw new InvalidOperationException($"{nameof(KindsToFilterOn)} can only contain known event kinds.");
            }
        }

        JetstreamOptions options = new()
        {
            ProtocolVersion = ProtocolVersion,
            LoggerFactory = LoggerFactory,
            MeterFactory = MeterFactory,
            UseCompression = EnableCompression,
            Dictionary = CompressionDictionary,
            BufferSize = ReadBufferSize,
            MaxMessageSize = MaximumTotalMessageSize,
            TaskFactory = TaskFactory,
            CloseTimeout = CloseTimeout,
            SendTimeout = SendTimeout,
            MaximumConcurrentMessageParsers = MaximumConcurrentMessageParsers,
        };

        AtProtoJetstream jetstream;

        if (HttpClientFactory is null)
        {
            jetstream = new AtProtoJetstream(
                uri: Service,
                options: options,
                webSocketOptions: WebSocketOptions,
                httpClientOptions: HttpClientOptions,
                collections: CollectionsToFilterOn,
                dids: DidsToFilterOn);
        }
        else
        {
            jetstream = new AtProtoJetstream(
                httpClientFactory: HttpClientFactory,
                uri: Service,
                options: options,
                webSocketOptions: WebSocketOptions,
                collections: CollectionsToFilterOn,
                dids: DidsToFilterOn);
        }

        if (KindsToFilterOn is not null && KindsToFilterOn.Count > 0)
        {
            jetstream.KindFilter = [.. KindsToFilterOn];
        }

        return jetstream;
    }
}