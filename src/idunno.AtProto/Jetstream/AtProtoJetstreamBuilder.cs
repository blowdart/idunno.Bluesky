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
    private static readonly Uri s_defaultUri = new("wss://jetstream1.us-west.bsky.network");

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
    /// Gets or sets a flag indicating whether compression should be used with the stream. Defaults to <see langword="true"/>.
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Gets or sets the compression dictionary used by zst decompression when <see cref="EnableCompression"/> is <see langword="true"/>.
    /// </summary>
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "zst expects a dictionary and we're not concerned about mutability.")]
    public byte[] CompressionDictionary { get; set; } = Resource.zstDictionary;

    /// <summary>
    /// Gets or sets the size, in bytes, of each block read from the web socket.
    /// </summary>
    /// <remarks>
    /// <para>This is the size of the buffer a single read fills, not a limit on anything. Use <see cref="MaximumTotalMessageSize"/>
    /// to limit how large a message may be.</para>
    /// </remarks>
    public int ReadBufferSize { get; set; } = 8096;

    /// <summary>
    /// Gets or sets the maximum total size of a message the jetstream will accept. Messages exceeding this limit are rejected.
    /// </summary>
    /// <remarks>
    /// <para>This is also the size sent to the server, which will not send a message larger than it.</para>
    /// </remarks>
    public int MaximumTotalMessageSize { get; set; } = WebSocketExtensions.DefaultMaxMessageSize;

    /// <summary>
    /// Gets or sets how long to wait for a server to answer a close handshake before the connection is aborted instead.
    /// </summary>
    public TimeSpan CloseTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets any <see cref="AtProto.WebSocketOptions"/> to set on the underlying client WebSocket.
    /// </summary>
    public WebSocketOptions? WebSocketOptions { get; set; }

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
    /// <returns>A collection of <see cref="Did"/>s to limit commit events for.</returns>
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
    /// <returns>A collection of <see cref="Nsid"/>s to limit commit events for.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="collections"/> is <see langword="null"/>.</exception>
    /// <remarks><para>Can be combined with <see cref="FilterTo(Did[])"/>.</para></remarks>
    public AtProtoJetstreamBuilder FilterTo(Nsid[] collections)
    {
        ArgumentNullException.ThrowIfNull(collections);

        CollectionsToFilterOn = collections;

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
    public AtProtoJetstream Build()
    {
        JetstreamOptions options = new()
        {
            LoggerFactory = LoggerFactory,
            MeterFactory = MeterFactory,
            UseCompression = EnableCompression,
            Dictionary = CompressionDictionary,
            BufferSize = ReadBufferSize,
            MaxMessageSize = MaximumTotalMessageSize,
            TaskFactory = TaskFactory,
            CloseTimeout = CloseTimeout,
        };

        if (HttpClientFactory is null)
        {
            return new AtProtoJetstream(
                uri: Service,
                options: options,
                webSocketOptions: WebSocketOptions,
                httpClientOptions: HttpClientOptions,
                collections: CollectionsToFilterOn,
                dids: DidsToFilterOn);
        }
        else
        {
            return new AtProtoJetstream(
                httpClientFactory: HttpClientFactory,
                uri: Service,
                options: options,
                webSocketOptions: WebSocketOptions,
                collections: CollectionsToFilterOn,
                dids: DidsToFilterOn);
        }
    }
}