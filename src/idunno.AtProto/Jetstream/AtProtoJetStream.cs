// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Net.Security;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Web;

using idunno.AtProto.Jetstream.Events;
using idunno.AtProto.Jetstream.Models;
using idunno.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using ZstdSharp;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// A class for consuming the AtProto jetstream.
/// </summary>
/// <remarks>
///<para>See https://github.com/bluesky-social/jetstream.</para>
/// </remarks>
[SuppressMessage("Usage", "S8949:Cancellation tokens should be forwarded", Justification = "The stored token belongs to the connection the jetstream reconnects on behalf of, not to the caller of an unrelated method.")]
public class AtProtoJetstream : IDisposable, IAsyncDisposable
{
#if NET9_0_OR_GREATER
    private readonly Lock _syncLock = new ();
#else
    private readonly object _syncLock = new();
#endif

    private volatile bool _disposed;

    /// <summary>
    /// Serialises connection attempts.
    /// </summary>
    /// <remarks>
    /// <para>Deliberately not disposed. Disposing it whilst a connection attempt holds it makes the release which ends
    /// that attempt throw, turning a disposal which races a connect into an exception out of <see cref="ConnectAsync(Uri?, long?, HttpClient?, CancellationToken)"/>.
    /// Nothing here uses <see cref="SemaphoreSlim.AvailableWaitHandle"/>, which is the only thing its disposal frees.</para>
    /// </remarks>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposing it whilst a connection attempt holds it makes the release which ends that attempt throw.")]
    private readonly SemaphoreSlim _connectSemaphore = new(1, 1);

    /// <summary>
    /// Serialises writes to the underlying web socket.
    /// </summary>
    /// <remarks>
    /// <para>A <see cref="ClientWebSocket"/> allows one outstanding send at a time, and throws if a second one starts
    /// whilst the first is in flight. Setting both filters together, or setting one whilst the connection is closing,
    /// otherwise puts two sends on the same socket.</para>
    /// <para>Deliberately not disposed, for the same reason as <see cref="_connectSemaphore"/>.</para>
    /// </remarks>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposing it whilst a send holds it makes the release which ends that send throw.")]
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    /// <summary>
    /// Bounds the number of messages being parsed at once.
    /// </summary>
    /// <remarks>
    /// <para>Parsing runs away from the receive loop, so without a bound a server which sends faster than the parsing
    /// keeps up with has every message it sends queued behind the ones still being parsed, and nothing stops that queue
    /// growing. <see cref="JetstreamOptions.MaxMessageSize"/> limits how large one message may be, not how many of them
    /// may be in flight.</para>
    /// <para>Deliberately not disposed, for the same reason as <see cref="_connectSemaphore"/>.</para>
    /// </remarks>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposing it whilst a parse holds it makes the release which ends that parse throw.")]
    private readonly SemaphoreSlim _parseSemaphore;

    private const string SubscribeEventsSubProtocol = "xrpc.v1.json";

    private const string SubscribeEventsTypePrefix = "network.bsky.jetstream.subscribeEvents#";

    private const string UnknownZstdDictionaryError = "UnknownZstdDictionary";

    /// <summary>
    /// The maximum number of collections a <see cref="JetstreamProtocolVersion.V2"/> server accepts in a filter.
    /// </summary>
    internal const int MaximumV2Collections = 100;

    /// <summary>
    /// The maximum number of dids a <see cref="JetstreamProtocolVersion.V2"/> server accepts in a filter.
    /// </summary>
    internal const int MaximumV2Dids = 10000;

    /// <summary>
    /// The smallest cursor a <see cref="JetstreamProtocolVersion.V2"/> server treats as a Unix microseconds timestamp
    /// rather than as a sequence number.
    /// </summary>
    internal const long TimestampCursorThreshold = 1_000_000_000_000_000;

    /// <summary>
    /// The largest zstd dictionary, in bytes, which will be downloaded from a server.
    /// </summary>
    private const int MaximumDictionarySize = 1024 * 1024;

    /// <summary>
    /// The largest error response, in bytes, which will be read from a server which refused a connection.
    /// </summary>
    private const int MaximumErrorResponseSize = 64 * 1024;

    /// <summary>
    /// The magic number which starts a structured zstd dictionary, RFC 8878 section 5.
    /// </summary>
    private const uint ZstdDictionaryMagicNumber = 0xEC30A437;

    /// <summary>
    /// The jetstream connected to when no uri is given and the protocol version is <see cref="JetstreamProtocolVersion.V1"/>.
    /// </summary>
    internal static readonly Uri s_defaultV1Uri = new("wss://jetstream1.us-west.bsky.network");

    /// <summary>
    /// The jetstream connected to when no uri is given and the protocol version is <see cref="JetstreamProtocolVersion.V2"/>.
    /// </summary>
    internal static readonly Uri s_defaultV2Uri = new("wss://jetstream.us-west.bsky.network");

    private readonly JetstreamMetrics _metrics;

    private readonly ILogger<AtProtoJetstream> _logger;

    private readonly Uri _uri;

    private readonly Decompressor? _decompressor;

    /// <summary>
    /// The ID of the dictionary loaded into <see cref="_decompressor"/> for a <see cref="JetstreamProtocolVersion.V2"/>
    /// connection, or zero if none has been loaded. Guarded by <see cref="_decompressorLock"/>.
    /// </summary>
    private uint _dictionaryId;

    /// <summary>
    /// Serialises use of <see cref="_decompressor"/>.
    /// </summary>
    /// <remarks>
    /// <para>A <see cref="Decompressor"/> holds the decompression context, so it cannot be used from two places at once.</para>
    /// </remarks>
#if NET9_0_OR_GREATER
    private readonly Lock _decompressorLock = new();
#else
    private readonly object _decompressorLock = new();
#endif

    private List<Nsid> _collections = [];

    private List<Did> _dids = [];

    private List<JetStreamEventKind> _kinds = [];

    // Incremented, under _syncLock, whenever a filter changes, and recorded against each connection, so a reconnection
    // made to apply changed filters can tell whether the connection it would replace already has them.
    private int _filterVersion;
    private int _connectedFilterVersion;

    // The largest sequence number seen, or long.MinValue before any has been. Read and written with Interlocked.
    private long _lastSequence = long.MinValue;

    // Events at or below this sequence number are dropped as already delivered. Set when the jetstream reconnects
    // itself from the last sequence it saw, as the cursor is inclusive and the event it names would otherwise be
    // delivered twice. Read and written with Interlocked.
    private long _skipAtOrBelowSequence = long.MinValue;

    // The socket a reconnection is replacing, so the receive loop reading it does not announce the close as though the
    // jetstream had been disconnected.
    private volatile ClientWebSocket? _replacedClient;

    // How the last caller connected, which a reconnection the jetstream makes by itself reuses. Held together, and
    // replaced rather than updated, so a reconnection never pairs the token of one connection with the client of another.
    private volatile ConnectionSettings? _connectionSettings;

    // Replaced on reconnection, under _syncLock, but read without it by the state properties and by anything holding
    // its own reference to a socket, so the write has to be published rather than left for a reader to notice.
    private volatile ClientWebSocket _client;

    private const string HttpClientName = Agent.HttpClientName;
    internal HttpClientOptions? _httpClientOptions;
    private readonly ServiceProvider? _serviceProvider;
    private readonly HttpClient _httpClient;

    // Written by whichever thread connects and read by the receive loop and the metrics it emits.
    private volatile Uri? _server;

    // DateTimeOffset is too wide to read or write atomically, so the timestamp is held as ticks, which are not.
    private long _messageLastReceivedTicks;

    private volatile bool _disconnectedGracefully;

    /// <summary>
    /// Creates a new instance of <see cref="Jetstream"/>.
    /// </summary>
    /// <param name="uri">The host uri to connection to. Defaults to wss://jetstream.us-west.bsky.network/ for <see cref="JetstreamProtocolVersion.V2"/>, or wss://jetstream1.us-west.bsky.network/ for <see cref="JetstreamProtocolVersion.V1"/>. Do not connect to untrusted jet stream servers.</param>
    /// <param name="options">Any options to configure this instance of <see cref="AtProtoJetstream"/>.</param>
    /// <param name="webSocketOptions">Any <see cref="AtProto.WebSocketOptions"/> to set on the underlying client WebSocket.</param>
    /// <param name="httpClientOptions">Any <see cref="HttpClientOptions"/> for the internal http client used to make HTTP requests.</param>
    /// <param name="collections">The <see cref="Nsid"/>s of any collection types to subscribe to. If <see langword="null"/> or empty all collection types will be subscribed to.</param>
    /// <param name="dids">Any <see cref="Did"/>s to subscribe to. If <see langword="null"/> or empty all dids will be subscribed to.</param>
    /// <exception cref="ArgumentException">Thrown when the protocol version is <see cref="JetstreamProtocolVersion.V2"/> and <paramref name="collections"/> or <paramref name="dids"/> has more entries than the server accepts.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overloaded to allow an application to supply its own IHttpClientFactory.")]
    public AtProtoJetstream(
        Uri? uri = null,
        JetstreamOptions? options = null,
        WebSocketOptions? webSocketOptions = null,
        HttpClientOptions? httpClientOptions = null,
        ICollection<Nsid>? collections = null,
        ICollection<Did>? dids = null) : this(
            httpClientFactory: null,
            httpClientOptions: httpClientOptions,
            uri: uri,
            options: options,
            webSocketOptions: webSocketOptions,
            collections: collections,
            dids: dids)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="Jetstream"/> which creates its <see cref="HttpClient"/>s from the
    /// specified <paramref name="httpClientFactory"/>.
    /// </summary>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use when creating <see cref="HttpClient"/>s.</param>
    /// <param name="uri">The host uri to connection to. Defaults to wss://jetstream.us-west.bsky.network/ for <see cref="JetstreamProtocolVersion.V2"/>, or wss://jetstream1.us-west.bsky.network/ for <see cref="JetstreamProtocolVersion.V1"/>. Do not connect to untrusted jet stream servers.</param>
    /// <param name="options">Any options to configure this instance of <see cref="AtProtoJetstream"/>.</param>
    /// <param name="webSocketOptions">Any <see cref="AtProto.WebSocketOptions"/> to set on the underlying client WebSocket.</param>
    /// <param name="collections">The <see cref="Nsid"/>s of any collection types to subscribe to. If <see langword="null"/> or empty all collection types will be subscribed to.</param>
    /// <param name="dids">Any <see cref="Did"/>s to subscribe to. If <see langword="null"/> or empty all dids will be subscribed to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClientFactory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the protocol version is <see cref="JetstreamProtocolVersion.V2"/> and <paramref name="collections"/> or <paramref name="dids"/> has more entries than the server accepts.</exception>
    /// <remarks>
    /// <para>
    ///   The <see cref="HttpClient"/> is taken from <paramref name="httpClientFactory"/> as it is, so the factory has to have been configured with
    ///   <see cref="ServiceCollectionExtensions.AddAtProtoHttpClient(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>. Any other
    ///   registration produces a client without the SSRF protections a jetstream would otherwise apply for itself.
    /// </para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overloaded to allow an application to supply its own IHttpClientFactory.")]
    public AtProtoJetstream(
        IHttpClientFactory httpClientFactory,
        Uri? uri = null,
        JetstreamOptions? options = null,
        WebSocketOptions? webSocketOptions = null,
        ICollection<Nsid>? collections = null,
        ICollection<Did>? dids = null) : this(
            httpClientFactory: httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory)),
            httpClientOptions: null,
            uri: uri,
            options: options,
            webSocketOptions: webSocketOptions,
            collections: collections,
            dids: dids)
    {
    }

    private AtProtoJetstream(
        IHttpClientFactory? httpClientFactory,
        HttpClientOptions? httpClientOptions,
        Uri? uri,
        JetstreamOptions? options,
        WebSocketOptions? webSocketOptions,
        ICollection<Nsid>? collections,
        ICollection<Did>? dids)
    {
        if (options is not null)
        {
            Options = options;
            LoggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
        }
        else
        {
            LoggerFactory = NullLoggerFactory.Instance;
        }

        _uri = uri ?? (Options.ProtocolVersion == JetstreamProtocolVersion.V2 ? s_defaultV2Uri : s_defaultV1Uri);

        if (collections is not null)
        {
            ValidateFilterSize(collections.Count, MaximumV2Collections, nameof(collections));
            _collections = [.. collections];
        }

        if (dids is not null)
        {
            ValidateFilterSize(dids.Count, MaximumV2Dids, nameof(dids));
            _dids = [.. dids];
        }

        _metrics = new JetstreamMetrics(Options.MeterFactory);

        _parseSemaphore = new SemaphoreSlim(Options.MaximumConcurrentMessageParsers, Options.MaximumConcurrentMessageParsers);

        if (Options.UseCompression)
        {
            _decompressor = new Decompressor();

            // A version 2 server names the dictionary it compresses with, so it is downloaded when connecting rather
            // than taken from the options.
            if (Options.ProtocolVersion == JetstreamProtocolVersion.V1 && Options.Dictionary is not null)
            {
                _decompressor.LoadDictionary(Options.Dictionary);
            }
        }

        if (webSocketOptions is not null)
        {
            WebSocketOptions = webSocketOptions;
        }

        _logger = LoggerFactory.CreateLogger<AtProtoJetstream>();

        _client = CreateWebSocketClient();

        _httpClientOptions = httpClientOptions;

        if (httpClientFactory is not null)
        {
            HttpClientFactory = httpClientFactory;
        }
        else
        {
            // Without a factory of its own an application has nowhere to configure this client, so the jetstream builds
            // the same SSRF protected client an agent builds for itself rather than a second definition of one.
            IServiceCollection services = new ServiceCollection();

            services
                .AddHttpClient(HttpClientName, client => Agent.InternalConfigureHttpClient(client, _httpClientOptions?.HttpUserAgent, _httpClientOptions?.Timeout))
                .ConfigurePrimaryHttpMessageHandler(() => Agent.CreateHttpMessageHandler(_httpClientOptions, LoggerFactory));

            _serviceProvider = services.BuildServiceProvider();
            HttpClientFactory = _serviceProvider.GetService<IHttpClientFactory>()!;
        }

        _httpClient = HttpClientFactory.CreateClient(HttpClientName);
    }

    /// <summary>
    /// Gets the <see cref="IHttpClientFactory"/> used when creating <see cref="HttpClient"/>s.
    /// </summary>
    protected IHttpClientFactory HttpClientFactory { get; init; }

    /// <summary>
    /// Gets a configured logger factory from which to create loggers.
    /// </summary>
    protected internal ILoggerFactory LoggerFactory { get; init; }

    /// <summary>
    /// Gets the configuration options for the jetstream.
    /// </summary>
    protected internal JetstreamOptions Options { get; init; } = new JetstreamOptions();

    internal WebSocketOptions WebSocketOptions { get; init; } = new WebSocketOptions();

    /// <summary>
    /// Gets or sets a list of <see cref="Did"/>s to filter events on.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the protocol version is <see cref="JetstreamProtocolVersion.V2"/> and the value being set has more dids than the server accepts.</exception>
    /// <remarks>
    /// <para>With <see cref="JetstreamProtocolVersion.V1"/> the filter only applies to commit events, and if the
    /// jetstream is connected the updated filters are sent to the server in the background, so they may not take
    /// effect until after the property has been set.</para>
    /// <para>With <see cref="JetstreamProtocolVersion.V2"/> the filter applies to every kind of event. Filters can only
    /// be set when connecting, so if the jetstream is connected it reconnects in the background, resuming from
    /// <see cref="LastSequence"/>.</para>
    /// </remarks>
    public IReadOnlyCollection<Did> DidFilter
    {
        get
        {
            lock (_syncLock)
            {
                return _dids.AsReadOnly();
            }
        }

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ValidateFilterSize(value.Count, MaximumV2Dids, nameof(value));

            lock (_syncLock)
            {
                _dids = [.. value];
                _filterVersion++;
            }

            ApplyUpdatedFilters();
        }
    }

    /// <summary>
    /// Gets or sets a list of <see cref="Nsid"/>s of collections to filter commit events on.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the protocol version is <see cref="JetstreamProtocolVersion.V2"/> and the value being set has more collections than the server accepts.</exception>
    /// <remarks>
    /// <para>If the jetstream is connected the updated filters are applied in the background, so they may not take
    /// effect until after the property has been set. See <see cref="DidFilter"/> for how each protocol version applies them.</para>
    /// <para>With <see cref="JetstreamProtocolVersion.V2"/> a collection can end in <c>.*</c> to match every collection
    /// under a prefix. The filter only constrains commit events, so combine it with a <see cref="KindFilter"/> of
    /// <see cref="JetStreamEventKind.Commit"/> to receive nothing but commits.</para>
    /// </remarks>
    public IReadOnlyCollection<Nsid> CollectionFilter
    {
        get
        {
            lock (_syncLock)
            {
                return _collections.AsReadOnly();
            }
        }

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ValidateFilterSize(value.Count, MaximumV2Collections, nameof(value));

            lock (_syncLock)
            {
                _collections = [.. value];
                _filterVersion++;
            }

            ApplyUpdatedFilters();
        }
    }

    /// <summary>
    /// Gets or sets the kinds of event to receive. If empty every kind of event is received.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the value being set contains <see cref="JetStreamEventKind.Unknown"/> or a value which is not a defined <see cref="JetStreamEventKind"/>.</exception>
    /// <exception cref="NotSupportedException">Thrown when the value being set is not empty and the protocol version is <see cref="JetstreamProtocolVersion.V1"/>, which cannot filter by kind.</exception>
    /// <remarks>
    /// <para>Only supported by <see cref="JetstreamProtocolVersion.V2"/>. If the jetstream is connected it reconnects
    /// in the background to apply the new filter, resuming from <see cref="LastSequence"/>.</para>
    /// <para>A <see cref="CollectionFilter"/> can only be used when this filter is empty or includes <see cref="JetStreamEventKind.Commit"/>.</para>
    /// </remarks>
    public IReadOnlyCollection<JetStreamEventKind> KindFilter
    {
        get
        {
            lock (_syncLock)
            {
                return _kinds.AsReadOnly();
            }
        }

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (value.Count > 0 && Options.ProtocolVersion == JetstreamProtocolVersion.V1)
            {
                throw new NotSupportedException("Filtering by event kind needs a version 2 jetstream.");
            }

            if (value.Any(kind => kind == JetStreamEventKind.Unknown || !Enum.IsDefined(kind)))
            {
                throw new ArgumentException("The kind filter can only contain known event kinds.", nameof(value));
            }

            lock (_syncLock)
            {
                _kinds = [.. value.Distinct()];
                _filterVersion++;
            }

            ApplyUpdatedFilters();
        }
    }

    /// <summary>
    /// Gets the largest sequence number received from the jetstream, if any.
    /// </summary>
    /// <remarks>
    /// <para>Only <see cref="JetstreamProtocolVersion.V2"/> servers send sequence numbers. Pass the value as the cursor
    /// to <see cref="ConnectAsync(Uri?, long?, HttpClient?, CancellationToken)"/> to resume from it after a disconnection.
    /// The cursor is inclusive, and events are delivered at least once, so a consumer which must not act on an event
    /// twice needs to check <see cref="AtJetstreamEvent.Sequence"/> for itself.</para>
    /// </remarks>
    public long? LastSequence
    {
        get
        {
            long sequence = Interlocked.Read(ref _lastSequence);

            return sequence == long.MinValue ? null : sequence;
        }
    }
    /// <summary>
    /// Gets a flag indicating whether the underlying WebSocket is connected to the jetstream.
    /// </summary>
    public bool IsConnected => _client.State == WebSocketState.Open;

    /// <summary>
    /// Gets the <see cref="WebSocketState"/> of the underlying web socket.
    /// </summary>
    public WebSocketState State => _client.State;

    /// <summary>
    /// Gets a flag indicating whether the underlying WebSocket was disconnected gracefully.
    /// </summary>
    public bool DisconnectedGracefully
    {
        get => _client.State == WebSocketState.Closed && _disconnectedGracefully;
        private set => _disconnectedGracefully = value;
    }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> indicating when last time a message from the JetsStream was received.
    /// </summary>
    public DateTimeOffset? MessageLastReceived
    {
        get
        {
            long ticks = Interlocked.Read(ref _messageLastReceivedTicks);

            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }

        private set => Interlocked.Exchange(ref _messageLastReceivedTicks, value?.UtcTicks ?? 0);
    }

    /// <summary>
    /// Raised when this instance of <see cref="AtProtoJetstream"/> receives a message.
    /// </summary>
    /// <remarks>
    /// <para>Raised on the thread which read the message, before it is parsed, so handlers run one at a time and in the
    /// order the messages arrived. A handler which blocks stops the jetstream reading anything else.</para>
    /// </remarks>
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Raised when this instance of <see cref="AtProtoJetstream"/> receives a message.
    /// </summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Raised when this instance of <see cref="AtProtoJetstream"/> parses a message and converts it to a record.
    /// </summary>
    /// <remarks>
    /// <para>Messages are parsed on <see cref="JetstreamOptions.TaskFactory"/>, so handlers can run concurrently with
    /// one another and are not raised in the order the messages arrived. A handler which touches shared state needs to
    /// do its own synchronisation, and one which cares about ordering needs to use
    /// <see cref="AtJetstreamEvent.TimeStamp"/> rather than the order it is called in.</para>
    /// </remarks>
    public event EventHandler<RecordReceivedEventArgs>? RecordReceived;

    /// <summary>
    /// Raised when this instance of <see cref="AtProtoJetstream"/> encounters a fault.
    /// </summary>
    public event EventHandler<FaultRaisedEventArgs>? FaultRaised;

    /// <summary>
    /// Raised when this instance of <see cref="AtProtoJetstream"/> receives an advisory notice from the server.
    /// </summary>
    /// <remarks>
    /// <para>Only <see cref="JetstreamProtocolVersion.V2"/> servers send notices. For example a server sends
    /// <c>OutdatedCursor</c> when a timestamp cursor was older than it keeps events for, and it resumed from the
    /// oldest event it has instead. A notice does not end the connection.</para>
    /// </remarks>
    public event EventHandler<InfoReceivedEventArgs>? InfoReceived;

    /// <summary>
    /// Creates a new <see cref="AtProtoJetstreamBuilder"/>.
    /// </summary>
    /// <returns>A new <see cref="AtProtoJetstreamBuilder"/></returns>
    public static AtProtoJetstreamBuilder CreateBuilder() => AtProtoJetstreamBuilder.Create();

    /// <summary>
    /// Gets the underlying <see cref="ClientWebSocket"/>.
    /// </summary>
    protected ClientWebSocket ClientWebSocket => _client;

    /// <summary>
    /// Called to raise any <see cref="MessageReceived"/> events, if any.
    /// </summary>
    /// <param name="e">The <see cref="MessageReceivedEventArgs"/> for the event.</param>
    protected virtual void OnMessageReceived(MessageReceivedEventArgs e)
    {
        EventHandler<MessageReceivedEventArgs>? messageReceived = MessageReceived;
        MessageLastReceived = DateTimeOffset.UtcNow;

        if (!_disposed)
        {
            messageReceived?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Called to raise any <see cref="RecordReceived"/> events, if any.
    /// </summary>
    /// <param name="e">The <see cref="RecordReceivedEventArgs"/> for the event.</param>
    protected virtual void OnRecordReceived(RecordReceivedEventArgs e)
    {
        EventHandler<RecordReceivedEventArgs>? messageParsed = RecordReceived;

        if (!_disposed)
        {
            messageParsed?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Called to raise any <see cref="ConnectionStateChanged"/> events, if any.
    /// </summary>
    /// <param name="e">The <see cref="ConnectionStateChangedEventArgs"/> for the event.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="e"/> is <see langword="null"/>.</exception>
    protected virtual void OnConnectionStateChanged(ConnectionStateChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        EventHandler<ConnectionStateChangedEventArgs>? connectionStatusChanged = ConnectionStateChanged;

        if (!_disposed)
        {
            connectionStatusChanged?.Invoke(this, e);

            JetStreamLogger.ClientStateChanged(_logger, e.State);
        }
    }

    /// <summary>
    /// Called to raise any <see cref="FaultRaised"/> events, if any.
    /// </summary>
    /// <param name="e">The <see cref="FaultRaisedEventArgs"/> for the event.</param>
    protected virtual void OnFaultRaised(FaultRaisedEventArgs e)
    {
        EventHandler<FaultRaisedEventArgs>? faultRaised = FaultRaised;
        _metrics.Faults.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));

        if (!_disposed)
        {
            faultRaised?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Called to raise any <see cref="InfoReceived"/> events, if any.
    /// </summary>
    /// <param name="e">The <see cref="InfoReceivedEventArgs"/> for the event.</param>
    protected virtual void OnInfoReceived(InfoReceivedEventArgs e)
    {
        EventHandler<InfoReceivedEventArgs>? infoReceived = InfoReceived;

        if (!_disposed)
        {
            infoReceived?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Connect to the JetStream instance via a WebSocket connection.
    /// </summary>
    public async Task ConnectAsync()
    {
        await ConnectAsync(
            uri: null,
            cursor: null,
            httpClient: null,
            cancellationToken: CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the JetStream instance via a WebSocket connection.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    public async Task ConnectAsync(
        CancellationToken cancellationToken)
    {
        await ConnectAsync(
            uri: null,
            cursor: null,
            httpClient: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the JetStream instance via a WebSocket connection.
    /// </summary>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for any HTTP requests. If <see langword="null"/> a default configured HttpClient from the internal <see cref="IHttpClientFactory"/> will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    public async Task ConnectAsync(
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        await ConnectAsync(
            uri: null,
            cursor: null,
            httpClient: httpClient,
            cancellationToken: default).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the JetStream instance via a WebSocket connection.
    /// </summary>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for any HTTP requests. If <see langword="null"/> a default configured HttpClient from the internal <see cref="IHttpClientFactory"/> will be used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    public async Task ConnectAsync(
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        await ConnectAsync(
            uri: null,
            cursor: null,
            httpClient: httpClient,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the JetStream instance via a WebSocket connection.
    /// </summary>
    /// <param name="startFrom">The time to begin playback from. A value of <see langword="null"/> results in live-tail operation.</param>
    public async Task ConnectAsync(
        DateTimeOffset startFrom)
    {
        await ConnectAsync(
            uri: null,
            cursor: ToCursor(startFrom),
            httpClient: null,
            cancellationToken: default).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the JetStream instance via a WebSocket connection.
    /// </summary>
    /// <param name="startFrom">The time to begin playback from. A value of <see langword="null"/> results in live-tail operation.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    public async Task ConnectAsync(
        DateTimeOffset? startFrom,
        CancellationToken cancellationToken)
    {
        long? cursor = startFrom.HasValue ? ToCursor(startFrom.Value) : null;

        await ConnectAsync(
            uri: null,
            cursor: cursor,
            httpClient: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the JetStream instance via a WebSocket connection.
    /// </summary>
    /// <param name="uri">The URI of the jetstream server to connection to. Defaults to the URI passed during construction</param>
    /// <param name="startFrom">The time to begin playback from. A value of <see langword="null"/> results in live-tail operation.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    public async Task ConnectAsync(
        Uri uri,
        DateTimeOffset? startFrom,
        CancellationToken cancellationToken)
    {
        long? cursor = startFrom.HasValue ? ToCursor(startFrom.Value) : null;

        await ConnectAsync(
            uri: uri,
            cursor: cursor,
            httpClient: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the JetStream instance via a WebSocket connection.
    /// </summary>
    /// <param name="uri">The URI of the jetstream server to connection to. Defaults to the URI passed during construction</param>
    /// <param name="cursor">
    ///   The cursor to begin playback from. A value of <see langword="null"/> results in live-tail operation.
    ///   With <see cref="JetstreamProtocolVersion.V1"/> the cursor is a Unix microseconds timestamp. With
    ///   <see cref="JetstreamProtocolVersion.V2"/> it is a sequence number, such as <see cref="LastSequence"/>, and the
    ///   event it names is delivered again. A value of 10^15 or more is treated as a Unix microseconds timestamp instead.
    /// </param>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for any HTTP requests. If <see langword="null"/> a default configured HttpClient from the internal <see cref="IHttpClientFactory"/> will be used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <exception cref="WebSocketException">Thrown when the underlying web socket could not connect.</exception>
    /// <exception cref="JetstreamConnectionException">Thrown when the server refused the connection with an HTTP error, for example because the cursor is older than the events it keeps.</exception>
    /// <exception cref="HttpRequestException">Thrown when the protocol version is <see cref="JetstreamProtocolVersion.V2"/>, compression is enabled, and the server's compression dictionary could not be downloaded.</exception>
    /// <exception cref="InvalidDataException">Thrown when the protocol version is <see cref="JetstreamProtocolVersion.V2"/>, compression is enabled, and the server's compression dictionary is not a zstd dictionary.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the protocol version is <see cref="JetstreamProtocolVersion.V2"/> and a <see cref="CollectionFilter"/> is set alongside a <see cref="KindFilter"/> which does not include <see cref="JetStreamEventKind.Commit"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the jetstream has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is relative, or does not use a web socket scheme.</exception>
    /// <remarks>
    /// <para>Connection attempts are serialised. A caller which arrives whilst another connection attempt is in
    /// progress waits for it to complete, and returns without doing anything if it left the jetstream connected.</para>
    /// <para>The jetstream does not reconnect by itself after a disconnection. A caller which wants to reconnect can
    /// call this method again, including from a <see cref="ConnectionStateChanged"/> handler. The one exception is a
    /// <see cref="JetstreamProtocolVersion.V2"/> jetstream whose filters change whilst it is connected, which
    /// reconnects to apply them.</para>
    /// <para>If a <see cref="JetstreamProtocolVersion.V2"/> server says it no longer has the compression dictionary
    /// the jetstream downloaded, the current one is downloaded and the connection is tried once more.</para>
    /// </remarks>
    public async Task ConnectAsync(
        Uri? uri,
        long? cursor,
        HttpClient? httpClient,
        CancellationToken cancellationToken = default)
    {
        await ConnectCoreAsync(uri, cursor, httpClient, reconnectForUpdatedFilters: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connects, or reconnects to apply updated filters.
    /// </summary>
    /// <param name="uri">The URI of the jetstream server to connection to, or <see langword="null"/> to use the one passed during construction.</param>
    /// <param name="cursor">The cursor to begin playback from.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for any HTTP requests, or <see langword="null"/> to use the internal one.</param>
    /// <param name="reconnectForUpdatedFilters">
    ///   <see langword="true"/> to close the current connection and reconnect to the same server from <see cref="LastSequence"/>,
    ///   if the jetstream is connected with filters older than the current ones. <paramref name="uri"/> and <paramref name="cursor"/> are ignored.
    /// </param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    private async Task ConnectCoreAsync(
        Uri? uri,
        long? cursor,
        HttpClient? httpClient,
        bool reconnectForUpdatedFilters,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // State changes are collected rather than raised as they happen, so they can be raised once neither the
        // connection semaphore nor the sync lock is held.
        List<WebSocketState> stateChanges = [];
        ClientWebSocket? connectedClient = null;

        try
        {
            // Connecting cannot be done under a lock because it awaits, so it is serialised with a semaphore instead.
            // Two callers which arrive together would otherwise both reach ClientWebSocket.ConnectAsync on the same
            // socket, where the second throws, and could each start a receive loop reading the same socket.
            await _connectSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                long skipAtOrBelowSequence = long.MinValue;

                if (reconnectForUpdatedFilters)
                {
                    ClientWebSocket current = _client;

                    // Checked now the semaphore is held, as a reconnection queued behind another one, or behind a
                    // caller's own connect or close, finds a connection which either has the filters already or is
                    // not one the jetstream should reopen.
                    if (current.State != WebSocketState.Open || Volatile.Read(ref _connectedFilterVersion) == Volatile.Read(ref _filterVersion))
                    {
                        return;
                    }

                    _replacedClient = current;

                    JetStreamLogger.ReconnectingForUpdatedFilters(_logger);

                    await CloseSocketAsync(
                        current,
                        WebSocketCloseStatus.NormalClosure,
                        "Filters updated",
                        recordAsGracefulDisconnection: true,
                        stateChanges,
                        cancellationToken).ConfigureAwait(false);

                    uri = _server;
                    httpClient = _connectionSettings?.HttpClient;

                    // Taken after the close, so it includes everything the old connection delivered. The cursor is
                    // inclusive, so the event it names is dropped rather than delivered a second time.
                    cursor = LastSequence;

                    if (cursor is not null)
                    {
                        skipAtOrBelowSequence = cursor.Value;
                    }
                }

                try
                {
                    connectedClient = await ConnectInternalAsync(uri, cursor, httpClient, skipAtOrBelowSequence, refreshDictionary: false, stateChanges, cancellationToken).ConfigureAwait(false);
                }
                catch (JetstreamConnectionException ex) when (
                    Options.ProtocolVersion == JetstreamProtocolVersion.V2 &&
                    Options.UseCompression &&
                    string.Equals(ex.ErrorDetail?.Error, UnknownZstdDictionaryError, StringComparison.Ordinal))
                {
                    // The server has replaced the dictionary it compresses with since it was downloaded, so the
                    // current one is downloaded and the connection tried once more.
                    connectedClient = await ConnectInternalAsync(uri, cursor, httpClient, skipAtOrBelowSequence, refreshDictionary: true, stateChanges, cancellationToken).ConfigureAwait(false);
                }

                if (connectedClient is not null && !reconnectForUpdatedFilters)
                {
                    _connectionSettings = new ConnectionSettings(httpClient, cancellationToken);
                }
            }
            finally
            {
                _connectSemaphore.Release();
            }
        }
        finally
        {
            // Raised with the semaphore released and no lock held, and on the failure path as well as the success one.
            // Raising them any earlier runs handler code inside both, where a handler which reconnects waits on a
            // semaphore its own caller is holding, and a handler which takes a lock of its own can deadlock against a
            // thread which holds that lock and is setting a filter.
            foreach (WebSocketState state in stateChanges)
            {
                OnConnectionStateChanged(new ConnectionStateChangedEventArgs(state));
            }
        }

        if (connectedClient is not null && !_disposed)
        {
            // The loop is given the socket this call connected rather than reading the field, so a later reconnection
            // which replaces the field does not hand this loop the new socket to read alongside the loop started for it.
            ReceiveLoop(connectedClient, cancellationToken).FireAndForget();
        }
    }

    /// <summary>
    /// Applies changed filters to an open connection.
    /// </summary>
    private void ApplyUpdatedFilters()
    {
        if (Options.ProtocolVersion == JetstreamProtocolVersion.V1)
        {
            SendOptionsUpdateMessage().FireAndForget();
        }
        else if (_client.State == WebSocketState.Open)
        {
            ReconnectForUpdatedFiltersAsync().FireAndForget();
        }
    }

    /// <summary>
    /// Reconnects a <see cref="JetstreamProtocolVersion.V2"/> jetstream so the server applies its current filters.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A reconnection runs in the background, so a failure is reported as a fault rather than thrown to nobody.")]
    private async Task ReconnectForUpdatedFiltersAsync()
    {
        ConnectionSettings? connectionSettings = _connectionSettings;

        if (_disposed || connectionSettings is null || connectionSettings.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        CancellationToken cancellationToken = connectionSettings.CancellationToken;

        try
        {
            await ConnectCoreAsync(uri: null, cursor: null, httpClient: null, reconnectForUpdatedFilters: true, cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Disposed whilst reconnecting, so there is nothing left to reconnect.
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller cancelled the connection the reconnection was made on behalf of.
        }
        catch (Exception ex)
        {
            JetStreamLogger.ReconnectForUpdatedFiltersFailed(_logger, ex);
            LogFault(ForLogging($"Reconnecting to apply updated filters failed: {ex.Message}"));
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Suppressing dispose exceptions on purpose.")]
    [SuppressMessage("Minor Code Smell", "S2486:Generic exceptions should not be ignored", Justification = "Suppressing dispose exceptions on purpose.")]
    private async Task<ClientWebSocket?> ConnectInternalAsync(
        Uri? uri,
        long? cursor,
        HttpClient? httpClient,
        long skipAtOrBelowSequence,
        bool refreshDictionary,
        List<WebSocketState> stateChanges,
        CancellationToken cancellationToken = default)
    {
        if (_client is not null && _client.State == WebSocketState.Open)
        {
            return null;
        }

        ClientWebSocket client;

        lock (_syncLock)
        {
            // Disposal is checked under the same lock it disposes the socket under, so a connect running alongside a
            // dispose either creates its socket first, and has it disposed there, or stops here.
            ObjectDisposedException.ThrowIf(_disposed, this);

            // The state is re-checked inside the lock. Checking it outside only narrows the race, it does not remove it,
            // and a caller which loses that race would otherwise have the socket it is about to connect disposed underneath it.
            //
            // Anything other than None has already been connected, and a ClientWebSocket can only be connected once, so
            // every one of those states needs a new socket. Listing the states which do rather than the one which does not
            // leaves out the half closed states, which a close that did not complete can leave behind, and a socket left in
            // one of those is neither replaced here nor connectable, so the jetstream can never reconnect.
            if (_client is null || _client.State != WebSocketState.None)
            {
                if (_client is not null)
                {
                    try
                    {
                        _client.Dispose();
                    }
                    catch (Exception ex)
                    {
                        JetStreamLogger.ErrorDisposingClientInConnectAsync(_logger, ex);
                    }
                }

                _client = CreateWebSocketClient();
                stateChanges.Add(_client.State);
            }

            // Everything below works against the socket this attempt settled on rather than the field, so a dispose or
            // a later reconnection which replaces the field cannot move this attempt onto a different socket part way through.
            client = _client;
        }

        uri ??= _uri;

        ValidateJetstreamUri(uri);

        _server = uri;

        List<Nsid> collections;
        List<Did> dids;
        List<JetStreamEventKind> kinds;
        int filterVersion;

        lock (_syncLock)
        {
            // The filters are copied under the lock, as a concurrent update to either of them would otherwise
            // be enumerated as the query string is built.
            collections = [.. _collections];
            dids = [.. _dids];
            kinds = [.. _kinds];
            filterVersion = _filterVersion;
        }

        httpClient ??= _httpClient;

        uint dictionaryId = 0;

        if (Options.ProtocolVersion == JetstreamProtocolVersion.V2 && Options.UseCompression)
        {
            dictionaryId = await GetDictionaryIdAsync(uri, httpClient, refreshDictionary, cancellationToken).ConfigureAwait(false);
        }

        Uri jetStreamUri = BuildSubscriptionUri(uri, cursor, collections, dids, kinds, dictionaryId);

        WebSocketState previousState = client.State;

        JetStreamLogger.ConnectingTo(_logger, jetStreamUri);

        // Reset before the attempt, as the flag describes how the connection this attempt is making ends, and carrying
        // the previous connection's answer into it would have a new connection reporting how an older one was disconnected.
        DisconnectedGracefully = false;

        // Reset for the same reason. The timestamp describes the connection being made, and a caller watching it to
        // decide whether a connection has gone quiet would otherwise be shown a time from a connection which has ended.
        MessageLastReceived = null;

        Interlocked.Exchange(ref _skipAtOrBelowSequence, skipAtOrBelowSequence);

        try
        {
            await client.ConnectAsync(
                uri: jetStreamUri,
                invoker: httpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Tagged with the server rather than with the subscription uri. The subscription uri carries every did and
            // collection the caller is following, and a cursor, so tagging with it would both publish who is being
            // watched to whatever collects the metrics and give the tag an unbounded set of values.
            _metrics.ConnectionsOpened.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
        }
        catch (Exception ex)
        {
            if (ex is WebSocketException webSocketException)
            {
                JetStreamLogger.WebSocketException(_logger, webSocketException);
            }

            // Counted once here, however the connection failed, rather than again below, and tagged with the server
            // rather than with the subscription uri for the reasons given above.
            _metrics.ConnectionFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));

            if (client.State != previousState)
            {
                stateChanges.Add(client.State);
            }

            // A server which refuses the upgrade with an HTTP error says why in the body of the response, which the web
            // socket client does not expose, so it is asked again over plain HTTP and the reason is thrown instead.
            HttpStatusCode statusCode = client.HttpStatusCode;

            if (ex is WebSocketException && (int)statusCode >= 400 && (int)statusCode < 600)
            {
                AtErrorDetail? errorDetail = await GetConnectionErrorDetailAsync(jetStreamUri, httpClient, cancellationToken).ConfigureAwait(false);

                JetStreamLogger.ConnectionRefused(_logger, (int)statusCode, errorDetail?.Error);

                throw new JetstreamConnectionException(statusCode, errorDetail, ex);
            }

            throw;
        }

        if (client.State != previousState)
        {
            stateChanges.Add(client.State);
        }

        if (client.State == WebSocketState.Open)
        {
            Volatile.Write(ref _connectedFilterVersion, filterVersion);

            return client;
        }

        JetStreamLogger.ConnectionFailed(_logger, client.State);
        _metrics.ConnectionFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));

        return null;
    }

    /// <summary>
    /// Builds the uri to subscribe to.
    /// </summary>
    /// <param name="server">The <see cref="Uri"/> of the jetstream server.</param>
    /// <param name="cursor">The cursor, if any, to begin playback from.</param>
    /// <param name="collections">The collections to filter on.</param>
    /// <param name="dids">The dids to filter on.</param>
    /// <param name="kinds">The kinds of event to filter on.</param>
    /// <param name="dictionaryId">The ID of the zstd dictionary to ask a <see cref="JetstreamProtocolVersion.V2"/> server to compress with, or zero for none.</param>
    /// <returns>The <see cref="Uri"/> to subscribe to.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the protocol version is <see cref="JetstreamProtocolVersion.V2"/> and <paramref name="collections"/> is not empty whilst <paramref name="kinds"/> excludes commits.</exception>
    internal Uri BuildSubscriptionUri(
        Uri server,
        long? cursor,
        IReadOnlyCollection<Nsid> collections,
        IReadOnlyCollection<Did> dids,
        IReadOnlyCollection<JetStreamEventKind> kinds,
        uint dictionaryId)
    {
        bool isV2 = Options.ProtocolVersion == JetstreamProtocolVersion.V2;

        if (isV2 && collections.Count > 0 && kinds.Count > 0 && !kinds.Contains(JetStreamEventKind.Commit))
        {
            // The server rejects this combination, as a collection filter only applies to commits.
            throw new InvalidOperationException("A collection filter can only be used when the kind filter is empty or includes commit events.");
        }

        Uri endpoint = new(server, isV2 ? "/xrpc/network.bsky.jetstream.subscribeEvents" : "/subscribe");

        string collectionParameter = isV2 ? "collections" : "wantedCollections";
        string didParameter = isV2 ? "dids" : "wantedDids";

        StringBuilder uriBuilder = new();

        uriBuilder.Append(endpoint);
        uriBuilder.Append('?');

        foreach (Nsid collection in collections)
        {
            uriBuilder.Append(
                CultureInfo.InvariantCulture,
                $"{collectionParameter}={HttpUtility.UrlEncode(collection.ToString())}&");
        }

        foreach (Did did in dids)
        {
            uriBuilder.Append(
                CultureInfo.InvariantCulture,
                $"{didParameter}={HttpUtility.UrlEncode(did.ToString())}&");
        }

        if (isV2)
        {
            foreach (JetStreamEventKind kind in kinds)
            {
                uriBuilder.Append(
                    CultureInfo.InvariantCulture,
                    $"kinds={ToQueryValue(kind)}&");
            }
        }

        if (cursor is not null)
        {
            uriBuilder.Append(
                CultureInfo.InvariantCulture,
                $"cursor={cursor}&");
        }

        if (isV2)
        {
            if (dictionaryId != 0)
            {
                uriBuilder.Append(
                    CultureInfo.InvariantCulture,
                    $"zstdDictionary={dictionaryId}&");
            }
        }
        else if (Options.UseCompression)
        {
            uriBuilder.Append(
                CultureInfo.InvariantCulture,
                $"compress=true&");
        }

        // The parameter is maxMessageSizeBytes, and it is the size of the message the server is willing to send, so it
        // takes the maximum message size rather than the size of the blocks the message is read in.
        uriBuilder.Append(
            CultureInfo.InvariantCulture,
            $"maxMessageSizeBytes={Options.MaxMessageSize}&");

        if (uriBuilder[^1] == '&')
        {
            uriBuilder.Length--;
        }

        if (uriBuilder[^1] == '?')
        {
            uriBuilder.Length--;
        }

        return new Uri(uriBuilder.ToString());
    }

    /// <summary>
    /// Gets the ID of the zstd dictionary a <see cref="JetstreamProtocolVersion.V2"/> server compresses with,
    /// downloading the dictionary and loading it into the decompressor if needed.
    /// </summary>
    /// <param name="server">The <see cref="Uri"/> of the jetstream server.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to download the dictionary with.</param>
    /// <param name="refresh"><see langword="true"/> to download the dictionary even if one has already been loaded.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The ID of the dictionary.</returns>
    /// <exception cref="HttpRequestException">Thrown when the dictionary could not be downloaded.</exception>
    /// <exception cref="InvalidDataException">Thrown when the download is not a structured zstd dictionary, or is larger than a dictionary is allowed to be.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the jetstream is disposed whilst the dictionary is downloaded.</exception>
    private async Task<uint> GetDictionaryIdAsync(Uri server, HttpClient httpClient, bool refresh, CancellationToken cancellationToken)
    {
        lock (_decompressorLock)
        {
            if (!refresh && _dictionaryId != 0)
            {
                return _dictionaryId;
            }
        }

        Uri dictionaryUri = new(ToHttpUri(server), "/xrpc/network.bsky.jetstream.getZstdDictionary");

        using HttpRequestMessage request = new(HttpMethod.Get, dictionaryUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Octet));

        using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        byte[] dictionary = await ReadBoundedAsync(response.Content, MaximumDictionarySize, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException($"The zstd dictionary is larger than {MaximumDictionarySize} bytes.");

        uint dictionaryId = ParseDictionaryId(dictionary);

        lock (_decompressorLock)
        {
            // Checked under the lock the decompressor is disposed under, as loading a dictionary into a disposed
            // decompressor hands native code a context which has been freed.
            ObjectDisposedException.ThrowIf(_disposed, this);

            _decompressor!.LoadDictionary(dictionary);
            _dictionaryId = dictionaryId;
        }

        JetStreamLogger.DictionaryLoaded(_logger, dictionaryId);

        return dictionaryId;
    }

    /// <summary>
    /// Reads the dictionary ID from the header of a structured zstd dictionary.
    /// </summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>The dictionary ID.</returns>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="dictionary"/> is not a structured zstd dictionary, or has no ID.</exception>
    internal static uint ParseDictionaryId(ReadOnlySpan<byte> dictionary)
    {
        if (dictionary.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(dictionary) != ZstdDictionaryMagicNumber)
        {
            throw new InvalidDataException("The zstd dictionary is not a structured dictionary.");
        }

        uint dictionaryId = BinaryPrimitives.ReadUInt32LittleEndian(dictionary[4..]);

        if (dictionaryId == 0)
        {
            throw new InvalidDataException("The zstd dictionary does not have an ID.");
        }

        return dictionaryId;
    }

    /// <summary>
    /// Asks a server which refused a connection why it did so.
    /// </summary>
    /// <param name="jetStreamUri">The <see cref="Uri"/> the connection was refused for.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to ask with.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The error the server gave, or <see langword="null"/> if it could not be read.</returns>
    /// <remarks>
    /// <para>A jetstream checks a request before it upgrades it to a web socket, so the same request made over plain
    /// HTTP is refused for the same reason, and that response can be read.</para>
    /// </remarks>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The error is only used to explain a failure which is thrown regardless.")]
    private async Task<AtErrorDetail?> GetConnectionErrorDetailAsync(Uri jetStreamUri, HttpClient httpClient, CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, ToHttpUri(jetStreamUri));
            using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            byte[]? body = await ReadBoundedAsync(response.Content, MaximumErrorResponseSize, cancellationToken).ConfigureAwait(false);

            if (body is null || body.Length == 0)
            {
                return null;
            }

            if (string.Equals(response.Content.Headers.ContentType?.MediaType, MediaTypeNames.Application.Json, StringComparison.OrdinalIgnoreCase))
            {
                AtErrorDetail? errorDetail = JsonSerializer.Deserialize(body, SourceGenerationContext.Default.AtErrorDetail);

                if (errorDetail is not null)
                {
                    // Remote input, which ends up in an exception message and so in whatever logs it.
                    errorDetail.Error = errorDetail.Error is null ? null : ForLogging(errorDetail.Error);
                    errorDetail.Message = errorDetail.Message is null ? null : ForLogging(errorDetail.Message);
                }

                return errorDetail;
            }

            return new AtErrorDetail { Message = ForLogging(Encoding.UTF8.GetString(body).Trim()) };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            JetStreamLogger.CouldNotReadConnectionError(_logger, ex);

            return null;
        }
    }

    /// <summary>
    /// Reads <paramref name="content"/>, up to <paramref name="maximumLength"/> bytes.
    /// </summary>
    /// <param name="content">The <see cref="HttpContent"/> to read.</param>
    /// <param name="maximumLength">The most bytes to read.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The content, or <see langword="null"/> if it is longer than <paramref name="maximumLength"/>.</returns>
    private static async Task<byte[]?> ReadBoundedAsync(HttpContent content, int maximumLength, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > maximumLength)
        {
            return null;
        }

        Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        await using (stream.ConfigureAwait(false))
        {
            using MemoryStream buffer = new();
            byte[] block = new byte[16 * 1024];
            int read;

            while ((read = await stream.ReadAsync(block, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > maximumLength)
                {
                    return null;
                }

                await buffer.WriteAsync(block.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            return buffer.ToArray();
        }
    }

    /// <summary>
    /// Converts a web socket <see cref="Uri"/> to the HTTP <see cref="Uri"/> for the same server and path.
    /// </summary>
    /// <param name="uri">The <see cref="Uri"/> to convert.</param>
    /// <returns>The HTTP <see cref="Uri"/>.</returns>
    internal static Uri ToHttpUri(Uri uri)
    {
        UriBuilder builder = new(uri);

        if (uri.Scheme.Equals(Uri.UriSchemeWss, StringComparison.OrdinalIgnoreCase))
        {
            builder.Scheme = Uri.UriSchemeHttps;
        }
        else if (uri.Scheme.Equals(Uri.UriSchemeWs, StringComparison.OrdinalIgnoreCase))
        {
            builder.Scheme = Uri.UriSchemeHttp;
        }

        return builder.Uri;
    }

    /// <summary>
    /// Converts <paramref name="startFrom"/> to a cursor.
    /// </summary>
    /// <param name="startFrom">The time to begin playback from.</param>
    /// <returns>The cursor for <paramref name="startFrom"/>.</returns>
    /// <remarks>
    /// <para>A <see cref="JetstreamProtocolVersion.V2"/> server treats a cursor below 10^15 as a sequence number, so a
    /// time before September 2001 is raised to the smallest timestamp. No server keeps events that old, so it resumes
    /// from the oldest event it has either way.</para>
    /// </remarks>
    internal long ToCursor(DateTimeOffset startFrom)
    {
        long cursor = startFrom.ToUnixTimeMilliseconds() * 1000;

        if (Options.ProtocolVersion == JetstreamProtocolVersion.V2 && cursor < TimestampCursorThreshold)
        {
            cursor = TimestampCursorThreshold;
        }

        return cursor;
    }

    /// <summary>
    /// Gets the value a <see cref="JetstreamProtocolVersion.V2"/> server expects for <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind">The <see cref="JetStreamEventKind"/> to convert.</param>
    /// <returns>The value for <paramref name="kind"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="kind"/> has no value the server accepts.</exception>
    private static string ToQueryValue(JetStreamEventKind kind) => kind switch
    {
        JetStreamEventKind.Account => "account",
        JetStreamEventKind.Commit => "commit",
        JetStreamEventKind.Identity => "identity",
        JetStreamEventKind.Sync => "sync",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The event kind cannot be filtered on.")
    };

    /// <summary>
    /// Checks a filter is no larger than a <see cref="JetstreamProtocolVersion.V2"/> server accepts.
    /// </summary>
    /// <param name="count">The number of entries in the filter.</param>
    /// <param name="maximum">The largest number of entries the server accepts.</param>
    /// <param name="parameterName">The name of the parameter the filter was passed in.</param>
    /// <exception cref="ArgumentException">Thrown when the protocol version is <see cref="JetstreamProtocolVersion.V2"/> and <paramref name="count"/> is larger than <paramref name="maximum"/>.</exception>
    private void ValidateFilterSize(int count, int maximum, string parameterName)
    {
        if (Options.ProtocolVersion == JetstreamProtocolVersion.V2 && count > maximum)
        {
            throw new ArgumentException($"A version 2 jetstream accepts at most {maximum} entries in this filter.", parameterName);
        }
    }
    /// <summary>
    /// Closes the existing WebSocket connection.
    /// </summary>
    /// <param name="status">Status for the shutdown. Defaults to <see cref="WebSocketCloseStatus.NormalClosure"/>.</param>
    /// <param name="statusDescription">Reason for the shutdown.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the underlying WebSocket has been disposed.</exception>
    /// <remarks>
    /// <para>A graceful close needs the server to answer it. A server which never does is waited on for no longer than
    /// <see cref="JetstreamOptions.CloseTimeout"/>, after which the connection is aborted instead.</para>
    /// </remarks>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catch all to avoid a close failure propagating.")]
    public async Task CloseAsync(
        WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure,
        string statusDescription = "Client disconnect",
        CancellationToken cancellationToken = default)
    {
        // Captured once, so a reconnection which replaces the field cannot leave this call inspecting one socket and
        // aborting another.
        await CloseSocketAsync(_client, status, statusDescription, recordAsGracefulDisconnection: true, stateChanges: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes the specified <paramref name="client"/>.
    /// </summary>
    /// <param name="client">The <see cref="ClientWebSocket"/> to close.</param>
    /// <param name="status">Status for the shutdown.</param>
    /// <param name="statusDescription">Reason for the shutdown.</param>
    /// <param name="recordAsGracefulDisconnection">Whether a completed close should be recorded as a graceful disconnection.</param>
    /// <param name="stateChanges">A list to add any state change to, so the caller can raise it once it holds nothing a handler could wait on, or <see langword="null"/> to raise it here.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <exception cref="ObjectDisposedException">Thrown if <paramref name="client"/> has been disposed.</exception>
    /// <remarks>
    /// <para>The socket to close is taken as a parameter rather than read from the field, so a caller which is working
    /// against a particular socket closes that one rather than whichever socket a reconnection has since installed.</para>
    /// </remarks>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catch all to avoid a close failure propagating.")]
    private async Task CloseSocketAsync(
        ClientWebSocket client,
        WebSocketCloseStatus status,
        string statusDescription,
        bool recordAsGracefulDisconnection,
        List<WebSocketState>? stateChanges,
        CancellationToken cancellationToken)
    {
        WebSocketState startingState = client.State;

        if (client.State == WebSocketState.Closed)
        {
            return;
        }
        else if (client.State == WebSocketState.Open)
        {
            // The close handshake completes when the server replies to it, so without a deadline of its own a server
            // which never replies holds the caller here for as long as it cares to.
            using CancellationTokenSource closeCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            closeCancellationTokenSource.CancelAfter(Options.CloseTimeout);

            try
            {
                // A close frame is a write, so it is serialised with the other writes to the socket. Waiting for the
                // other writes to finish is done on the close deadline rather than the caller's token, so a send which
                // is stuck against an unresponsive server cannot hold the close past the timeout which exists to stop
                // exactly that.
                await _sendSemaphore.WaitAsync(closeCancellationTokenSource.Token).ConfigureAwait(false);

                try
                {
                    await client.CloseAsync(status, statusDescription, closeCancellationTokenSource.Token).ConfigureAwait(false);
                }
                finally
                {
                    _sendSemaphore.Release();
                }

                DisconnectedGracefully = recordAsGracefulDisconnection;
                _metrics.ConnectionsClosed.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
            }
            catch (ObjectDisposedException)
            {
                throw;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The deadline expired rather than the caller cancelling, so the connection is dropped instead of
                // being left open waiting on a server which is not answering.
                JetStreamLogger.CloseTimedOut(_logger, Options.CloseTimeout);
                client.Abort();
                _metrics.ConnectionsClosed.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
            }
            catch (OperationCanceledException)
            {
                // The caller cancelled part way through the close, which can leave the socket half closed. A socket in
                // that state can neither be used nor connected again, so it is dropped rather than left behind for a
                // later reconnection to find.
                client.Abort();
            }
            catch (Exception ex)
            {
                JetStreamLogger.CloseError(_logger, ex);

                // The close frame may already have gone, which leaves the socket half closed, and a socket in that
                // state can neither be used nor connected again.
                client.Abort();
            }

        }
        else if (client.State == WebSocketState.Connecting)
        {
            try
            {
                client.Abort();
                _metrics.ConnectionsClosed.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
            }
            catch (ObjectDisposedException)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                // Swallow
            }
            catch (Exception ex)
            {
                JetStreamLogger.CloseError(_logger, ex);
            }
        }

        if (client.State != startingState)
        {
            if (stateChanges is not null)
            {
                stateChanges.Add(client.State);
            }
            else
            {
                OnConnectionStateChanged(new ConnectionStateChangedEventArgs(client.State));
            }
        }
    }

    /// <summary>
    /// Log a fault.
    /// </summary>
    /// <param name="fault">A description of the fault.</param>
    protected void LogFault(string fault = "Unspecified fault")
    {
        OnFaultRaised(new FaultRaisedEventArgs(fault));
    }

    /// <summary>
    /// Checks that <paramref name="uri"/> is one a jetstream can be subscribed to, and warns when it is not protected
    /// by TLS.
    /// </summary>
    /// <param name="uri">The <see cref="Uri"/> to check.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is relative, or does not use a web socket scheme.</exception>
    /// <remarks>
    /// <para>A subscription carries every <see cref="Did"/> and collection the caller is following, so an unencrypted
    /// connection publishes who is being watched to anything on the path. It is allowed, because a jetstream run
    /// locally or behind a debugging proxy has no certificate, but it is not allowed to pass silently.</para>
    /// </remarks>
    private void ValidateJetstreamUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("The jetstream uri must be absolute.", nameof(uri));
        }

        bool isSecure = uri.Scheme.Equals(Uri.UriSchemeWss, StringComparison.OrdinalIgnoreCase) ||
                        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

        bool isInsecure = uri.Scheme.Equals(Uri.UriSchemeWs, StringComparison.OrdinalIgnoreCase) ||
                          uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);

        if (!isSecure && !isInsecure)
        {
            throw new ArgumentException(
                $"'{uri.Scheme}' is not a web socket scheme. A jetstream uri must use ws, wss, http or https.",
                nameof(uri));
        }

        if (isInsecure)
        {
            JetStreamLogger.ConnectingWithoutTransportSecurity(_logger, uri);
        }
    }

    /// <summary>
    /// Disposes all resources.
    /// </summary>
    /// <param name="disposing">Flag indicating whether managed resources should be disposed.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // The flag is set, and the socket disposed, under the lock the connect path creates sockets under, so a
                // connect running alongside this either creates its socket first, and has it disposed here, or sees the
                // flag and stops. Doing it outside the lock leaves a window where a socket, and a receive loop reading
                // it, are created after disposal and never cleaned up.
                lock (_syncLock)
                {
                    _disposed = true;

                    _client.Dispose();
                }

                // Disposed under the lock the receive loop decompresses under, and after the flag the receive loop
                // checks inside that lock has been set. A decompressor holds a native context which Unwrap uses
                // without checking whether it has been freed, so freeing it whilst a decompression is inside it
                // hands native code a dangling pointer.
                lock (_decompressorLock)
                {
                    _decompressor?.Dispose();
                }

                // An in-flight connection attempt is using the HttpClient as its invoker, so it is waited for rather
                // than having the client disposed underneath it. Disposing the socket above aborts that attempt, so
                // the wait is normally over at once, and it is bounded so a connection which does not notice cannot
                // hold disposal open indefinitely.
                if (_connectSemaphore.Wait(Options.CloseTimeout))
                {
                    _connectSemaphore.Release();
                }

                _httpClient.Dispose();
                _serviceProvider?.Dispose();
            }
            else
            {
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Frees resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Closes any open connection and frees resources.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>Preferred over <see cref="Dispose()"/> when the jetstream may still be connected. Disposing synchronously
    /// cannot wait for a close handshake, so it drops the connection and leaves the server to notice, whereas this
    /// closes it the way <see cref="CloseAsync(WebSocketCloseStatus, string, CancellationToken)"/> would first.</para>
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);

        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs the asynchronous part of disposal.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failure to close cleanly must not stop disposal.")]
    [SuppressMessage("Minor Code Smell", "S2486:Generic exceptions should not be ignored", Justification = "A failure to close cleanly must not stop disposal.")]
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await CloseAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            JetStreamLogger.CloseError(_logger, ex);
        }
    }

    /// <summary>
    /// The keep-alive interval applied when <see cref="WebSocketOptions.KeepAliveInterval"/> does not supply one.
    /// </summary>
    private static readonly TimeSpan s_defaultKeepAliveInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The keep-alive timeout applied when <see cref="WebSocketOptions.KeepAliveTimeout"/> does not supply one.
    /// </summary>
    /// <remarks>
    /// <para>A default is applied because without a timeout a keep-alive ping is sent and never acted on, so a connection
    /// lost to a network failure leaves the socket reporting itself as open and the read loop waiting on it forever.</para>
    /// </remarks>
#if NET9_0_OR_GREATER
    private static readonly TimeSpan s_defaultKeepAliveTimeout = TimeSpan.FromSeconds(30);
#endif

    private ClientWebSocket CreateWebSocketClient()
    {
        var client = new ClientWebSocket();
        JetStreamLogger.InternalClientWebSocketCreated(_logger);

        // Kept so a server which refuses the upgrade can be told apart from one which could not be reached.
        client.Options.CollectHttpResponseDetails = true;

        if (Options.ProtocolVersion == JetstreamProtocolVersion.V2)
        {
            client.Options.AddSubProtocol(SubscribeEventsSubProtocol);
        }

        if (WebSocketOptions is not null)
        {
            if (WebSocketOptions.Proxy is not null)
            {
                client.Options.Proxy = WebSocketOptions.Proxy;
            }

            client.Options.KeepAliveInterval = WebSocketOptions.KeepAliveInterval ?? s_defaultKeepAliveInterval;

#if NET9_0_OR_GREATER
            client.Options.KeepAliveTimeout = WebSocketOptions.KeepAliveTimeout ?? s_defaultKeepAliveTimeout;
#endif
        }

        return client;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catch all for logging.")]
    [SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler", Justification = "A scheduler can be configured on the TaskFactory in Options.")]
    private async Task ReceiveLoop(ClientWebSocket client, CancellationToken cancellationToken)
    {
        WebSocketMessageType expectedMessageType = Options.UseCompression ? WebSocketMessageType.Binary : WebSocketMessageType.Text;

        // Tracks whether the state the loop ended on has already been announced, so the close path which raises it
        // itself is not followed by a second event saying the same thing.
        bool finalStateRaised = false;

        // Counts failures which left the connection usable, so a fault which recurs is backed off rather than retried
        // as fast as the machine allows. Reset by any read which succeeds.
        int consecutiveFailures = 0;

        while (client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                WebSocketReceiveResult webSocketReceiveResult;
                byte[] message;

                try
                {
                    (webSocketReceiveResult, message) =
                        await client.ReceiveNextMessageAsync(
                            bufferSize: Options.BufferSize,
                            maxMessageSize: Options.MaxMessageSize,
                            logger: _logger,
                            cancellationToken: cancellationToken).ConfigureAwait(false);

                    consecutiveFailures = 0;
                }
                catch (WebSocketMessageAbandonedException ex)
                {
                    // The rest of the abandoned message is still queued on the socket and there is no way to skip it, so
                    // reading on would return its tail as though it were a message of its own. The connection is finished,
                    // and the peer is told why rather than simply being dropped.
                    JetStreamLogger.MessageLoopError(_logger, ex);
                    LogFault(ForLogging(ex.Message));

                    await CloseSocketAsync(
                        client,
                        ex.CloseStatus,
                        ex.Message,
                        recordAsGracefulDisconnection: false,
                        stateChanges: null,
                        cancellationToken).ConfigureAwait(false);

                    // CloseSocketAsync raises the state change itself.
                    finalStateRaised = true;

                    break;
                }

                if (webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
                {
                    // A close frame is a write, so it is serialised with the other writes to the socket. Both the wait
                    // and the write are given a deadline of their own, because a write which is already in flight
                    // against a peer that has stopped reading never completes by itself, and without a deadline it
                    // would hold this reply, and so this loop, for as long as the peer cared to leave it there.
                    using CancellationTokenSource sendCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                    sendCancellationTokenSource.CancelAfter(Options.SendTimeout);

                    try
                    {
                        await _sendSemaphore.WaitAsync(sendCancellationTokenSource.Token).ConfigureAwait(false);

                        try
                        {
                            await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken: sendCancellationTokenSource.Token).ConfigureAwait(false);
                        }
                        finally
                        {
                            _sendSemaphore.Release();
                        }

                        // The server asked to close and the close was completed by replying to it, which is as graceful
                        // as a disconnection gets. The flag records how the connection ended, not which end ended it.
                        DisconnectedGracefully = true;
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // The reply could not be sent within the deadline, so the connection is dropped rather than
                        // left waiting on a server which has stopped reading it.
                        JetStreamLogger.CloseReplyTimedOut(_logger, Options.SendTimeout);
                        client.Abort();
                    }

                    JetStreamLogger.CloseMessageReceived(_logger);

                    _metrics.ConnectionsClosed.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));

                    // A connection being replaced to apply updated filters is announced by the reconnection instead.
                    if (!ReferenceEquals(client, _replacedClient))
                    {
                        OnConnectionStateChanged(new ConnectionStateChangedEventArgs(client.State));
                    }

                    finalStateRaised = true;

                    break;
                }

                if (webSocketReceiveResult.MessageType != expectedMessageType)
                {
                    JetStreamLogger.UnexpectedMessageType(_logger, webSocketReceiveResult.MessageType);
                }

                byte[] receivedData;
                bool disposedDuringDecompression = false;

                if (Options.UseCompression)
                {
                    try
                    {
                        Span<byte> bufferAsSpan = message.AsSpan(0, message.Length);

                        // Unwrap defaults to allowing 2GB of decompressed output, so without a limit of our own the
                        // maximum message size would only bound the compressed frame. A 24KB frame can declare, and
                        // expand to, hundreds of megabytes, so the limit has to be applied to what comes out of it.
                        //
                        // A Decompressor holds the decompression context, so it cannot be used from two places at once.
                        // A reconnection started from a ConnectionStateChanged handler can leave a second receive loop
                        // running before this one has noticed its own socket closing, and both would reach this.
                        lock (_decompressorLock)
                        {
                            // Checked under the lock the decompressor is disposed under. Unwrap does not check whether
                            // the native context it uses has been freed, so a dispose which is not seen here would be
                            // handing native code a dangling pointer rather than throwing.
                            if (_disposed)
                            {
                                disposedDuringDecompression = true;
                                receivedData = [];
                            }
                            else
                            {
                                receivedData = _decompressor!.Unwrap(bufferAsSpan, Options.MaxMessageSize).ToArray();
                            }
                        }
                    }
                    catch (ZstdException ex)
                    {
                        // Can't decompress so ignore this message.
                        _metrics.MessageDecompressionFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
                        JetStreamLogger.DecompressionException(_logger, ex);
                        continue;
                    }

                    if (disposedDuringDecompression)
                    {
                        break;
                    }
                }
                else
                {
                    // ReceiveNextMessageAsync allocates the array it returns, so the message can be used as it is.
                    receivedData = message;
                }

                string? messageAsString = default;

                // Now convert to a string
                try
                {
                    messageAsString = Encoding.UTF8.GetString(receivedData);
                }
                catch
                {
                    _metrics.MessageParsingFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
                    throw;
                }

                if (!string.IsNullOrEmpty(messageAsString))
                {
                    _metrics.MessagesReceived.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));

                    OnMessageReceived(new MessageReceivedEventArgs(messageAsString));

                    // Parsing runs away from this loop, so nothing here waits for it and a server which sends faster than
                    // the parsing keeps up with would otherwise have every message it sent queued, unbounded, behind the
                    // ones still being parsed. Waiting for a slot stops reading from the socket instead, which is what
                    // lets the transport apply the back pressure the server needs to see.
                    await _parseSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                    try
                    {
                        // Deliberately started with no cancellation token. A token which is already cancelled leaves StartNew
                        // never running the delegate, and the slot taken above is only given back by running it.
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        Options.TaskFactory.StartNew(() => ParseMessageAndReleaseSlot(messageAsString), CancellationToken.None);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                    catch (Exception ex)
                    {
                        // The slot is only ever given back by the delegate, so a factory which refuses to run it keeps
                        // the slot for good. Enough of those and this loop waits above forever with the socket still
                        // open, reading nothing and reporting nothing.
                        _parseSemaphore.Release();

                        JetStreamLogger.CouldNotStartMessageParser(_logger, ex);
                        _metrics.MessageParsingFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));

                        throw;
                    }
                }
                else
                {
                    if (client.State == WebSocketState.Open)
                    {
                        LogFault("Message conversion to string failed.");
                        _metrics.MessageParsingFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
                        JetStreamLogger.MessageLoopFailedToConvert(_logger);
                    }
                }
            }
            catch (Exception) when (ReferenceEquals(client, _replacedClient) && client.State != WebSocketState.Open)
            {
                // The socket was closed, and possibly disposed, by a reconnection applying updated filters before this
                // loop noticed. Nothing has gone wrong, so the loop just ends.
                break;
            }
            catch (Exception e)
            {
                JetStreamLogger.MessageLoopError(_logger, e);
                LogFault(ForLogging(e.Message));

                // A failure which does not change the socket state leaves the loop free to retry immediately, so a
                // fault which recurs spins here at whatever rate the machine allows, raising an unbounded stream of
                // events and metrics. Successive failures are backed off, and a run of them ends the connection
                // rather than being retried for ever.
                consecutiveFailures++;

                if (consecutiveFailures > MaximumConsecutiveReceiveFailures)
                {
                    JetStreamLogger.TooManyConsecutiveReceiveFailures(_logger, MaximumConsecutiveReceiveFailures);
                    break;
                }

                try
                {
                    await Task.Delay(s_receiveFailureBackoff, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        if (client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            // CloseSocketAsync raises the state change itself. The socket this loop read is closed rather than the
            // field, which a reconnection may already have replaced with a socket this loop knows nothing about.
            await CloseSocketAsync(
                client,
                WebSocketCloseStatus.NormalClosure,
                "Client disconnect",
                recordAsGracefulDisconnection: true,
                stateChanges: null,
                cancellationToken).ConfigureAwait(false);
        }
        else if (!finalStateRaised && client.State != WebSocketState.Open && !ReferenceEquals(client, _replacedClient))
        {
            // The loop has stopped because the socket is no longer usable, which for a dropped connection is the only
            // thing which tells a consumer the jetstream needs reconnecting. Without this a connection lost to the
            // network ends the loop silently, and a caller waiting for a state change to reconnect on waits forever.
            OnConnectionStateChanged(new ConnectionStateChangedEventArgs(client.State));
        }
    }

    /// <summary>
    /// The maximum number of characters of a received message to include in a log entry.
    /// </summary>
    /// <remarks>
    /// <para>A message is remote input, and can be as large as <see cref="JetstreamOptions.MaxMessageSize"/> allows, so
    /// logging one whole lets a server which sends nothing but unparsable messages decide how much is written to the log.</para>
    /// </remarks>
    private const int MaximumLoggedMessageLength = 1024;

    /// <summary>
    /// The maximum number of consecutive failures the receive loop tolerates before ending the connection.
    /// </summary>
    private const int MaximumConsecutiveReceiveFailures = 16;

    /// <summary>
    /// How long the receive loop waits after a failure before reading again.
    /// </summary>
    private static readonly TimeSpan s_receiveFailureBackoff = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Prepares remote input for logging, by bounding its length and removing the characters which would let it forge
    /// log entries of its own.
    /// </summary>
    /// <param name="message">The text to prepare.</param>
    /// <returns>Text safe to write to a log.</returns>
    /// <remarks>
    /// <para>A log which is read as lines of text cannot tell a line break inside a logged value apart from the end of
    /// the entry, so remote input carrying one can append whatever it likes as though the library had logged it.</para>
    /// </remarks>
    private static string ForLogging(string message)
    {
        string bounded = message.Length <= MaximumLoggedMessageLength
            ? message
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{message[..MaximumLoggedMessageLength]}… (truncated, {message.Length} characters)");

        if (!ContainsControlCharacters(bounded))
        {
            return bounded;
        }

        return string.Create(bounded.Length, bounded, static (destination, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                destination[i] = char.IsControl(source[i]) ? ' ' : source[i];
            }
        });
    }

    private static bool ContainsControlCharacters(string value) => value.Any(char.IsControl);

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catch all for logging.")]
    private Task ParseMessage(string json, ILogger logger)
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(json);
        }
        catch (Exception ex)
        {
            _metrics.MessageParsingFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
            JetStreamLogger.ParseMessageGotNullOrEmptyMessage(logger);
            return Task.FromException(ex);
        }

        try
        {
            AtJetstreamEvent? atJetstreamEvent;
            AtJetstreamEvent? derivedEvent;

            if (Options.ProtocolVersion == JetstreamProtocolVersion.V2)
            {
                if (!TryDeriveV2Event(json, out derivedEvent))
                {
                    // A notice, an error, or an event already delivered, none of which is raised as a record.
                    return Task.CompletedTask;
                }

                atJetstreamEvent = derivedEvent;
            }
            else
            {
                atJetstreamEvent = JsonSerializer.Deserialize<AtJetstreamEvent>(
                    json,
                    SourceGenerationContext.Default.AtJetstreamEvent);

                derivedEvent = atJetstreamEvent is null ? null : DeriveEvent(atJetstreamEvent);
            }

            if (atJetstreamEvent is not null)
            {
                if (derivedEvent is not null)
                {
                    // Raised outside the parsing catches below. A handler is application code, and an exception out of
                    // one says nothing about the message, so letting it fall into them would both count an application
                    // bug as a message parsing failure and log it as though the server had sent something unparsable.
                    try
                    {
                        OnRecordReceived(new RecordReceivedEventArgs(derivedEvent));
                    }
                    catch (Exception ex)
                    {
                        JetStreamLogger.RecordReceivedHandlerThrew(logger, ex);

                        return Task.FromException(ex);
                    }
                }
                else
                {
                    JetStreamLogger.ParseMessageDeserializationReturnedNull(logger, ForLogging(json));
                    _metrics.MessageParsingFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
                }
            }
            else
            {
                JetStreamLogger.ParseMessageDeserializationReturnedNull(logger, ForLogging(json));
                _metrics.MessageParsingFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
            }

            return Task.CompletedTask;
        }
        catch (JsonException ex)
        {
            JetStreamLogger.ParseMessageCouldNotProcessAsJson(logger, ForLogging(json), ex);
            _metrics.MessageParsingFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
            return Task.FromException(ex);
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _metrics.MessageParsingFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
            JetStreamLogger.ParseMessageThrewException(logger, ex);
            return Task.FromException(ex);
        }

    }

    /// <summary>
    /// Parses the specified <paramref name="json"/> and gives back the slot the receive loop took for it.
    /// </summary>
    /// <param name="json">The message to parse.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task ParseMessageAndReleaseSlot(string json)
    {
        try
        {
            await ParseMessage(json, _logger).FireAndForgetAsync(_logger).ConfigureAwait(false);
        }
        finally
        {
            _parseSemaphore.Release();
        }
    }

    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "The socket can close whilst a send waits on the semaphore, which the analyzer cannot see.")]
    private async Task SendOptionsUpdateMessage()
    {
        // Captured once, so the state which is checked and the socket which is written to are the same one.
        ClientWebSocket client = _client;

        if (client is null || client.State != WebSocketState.Open)
        {
            return;
        }

        OptionsUpdatePayload payload = new()
        {
            // The server is being told the largest message it should send, which is the maximum message size rather
            // than the size of the blocks that message is read in.
            MaxMessageSizeBytes = Options.MaxMessageSize
        };

        List<Nsid> collections;
        List<Did> dids;

        lock (_syncLock)
        {
            // Both filters are copied under the lock so the server is sent a consistent pair, rather than one filter
            // from before a concurrent update and one from after it.
            collections = [.. _collections];
            dids = [.. _dids];
        }

        if (collections.Count > 0)
        {
            payload.WantedCollections = [.. collections];
        }

        if (dids.Count > 0)
        {
            payload.WantedDIDs = [.. dids];
        }

        OptionsUpdateMessage optionsUpdateMessage = new()
        {
            Payload = payload
        };

        string message = JsonSerializer.Serialize(optionsUpdateMessage, SourceGenerationContext.Default.OptionsUpdateMessage);
        byte[] messageAsBytes = Encoding.UTF8.GetBytes(message);

        // Both the wait and the send are given a deadline. A send against a peer which has stopped reading never
        // completes by itself, and this send holds the semaphore every other write to the socket queues behind,
        // including the reply which completes a close handshake the server started.
        using CancellationTokenSource sendCancellationTokenSource = new(Options.SendTimeout);

        try
        {
            await _sendSemaphore.WaitAsync(sendCancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            JetStreamLogger.OptionsUpdateMessageTimedOut(_logger, Options.SendTimeout);
            return;
        }

        try
        {
            // The state is re-checked now the socket is held, as it can have closed whilst this send was queued behind
            // another one, and sending on a closed socket throws.
            if (client.State != WebSocketState.Open)
            {
                return;
            }

            await client.SendAsync(messageAsBytes, WebSocketMessageType.Text, true, sendCancellationTokenSource.Token).FireAndForgetAsync(_logger).ConfigureAwait(false);
        }
        finally
        {
            _sendSemaphore.Release();
        }

        JetStreamLogger.OptionsUpdateMessageSent(_logger);
    }

    internal AtJetstreamEvent? DeriveEvent(AtJetstreamEvent atJetstreamEvent)
    {
        AtJetstreamEvent derivedEvent = atJetstreamEvent;

        // The kind and the payload it names are two independent pieces of remote input, so an event whose kind names a
        // payload it does not carry is treated as an event which could not be derived, in the same way as one whose
        // payload is present but does not deserialize.
        IDictionary<string, JsonElement>? extensionData = atJetstreamEvent.ExtensionData;

        // As System.Text.Json polymorphism expects that the type identifier property name to begin with a $
        // we have to do this manually.
        switch (atJetstreamEvent.Kind)
        {
            case JetStreamEventKind.Account:
                if (extensionData is null || !extensionData.TryGetValue("account", out JsonElement accountElement))
                {
                    return null;
                }

                AtJetstreamAccount? account = JsonSerializer.Deserialize<AtJetstreamAccount>(
                    accountElement,
                    SourceGenerationContext.Default.AtJetstreamAccount);

                if (account is null)
                {
                    return null;
                }

                derivedEvent = new AtJetstreamAccountEvent()
                {
                    Did = atJetstreamEvent.Did,
                    TimeStamp = atJetstreamEvent.TimeStamp,
                    Kind = atJetstreamEvent.Kind,
                    Account = account,
                    ExtensionData = ExtensionDataExcept(extensionData, "account")
                };

                _metrics.EventsParsed.Add(1, new KeyValuePair<string, object?>("event_type", "account"), new KeyValuePair<string, object?>("server", _server?.ToString()));
                break;

            case JetStreamEventKind.Commit:
                if (extensionData is null || !extensionData.TryGetValue("commit", out JsonElement commitElement))
                {
                    return null;
                }

                AtJetstreamCommit? commit = JsonSerializer.Deserialize<AtJetstreamCommit>(
                    commitElement,
                    SourceGenerationContext.Default.AtJetstreamCommit);

                if (commit is null)
                {
                    return null;
                }

                derivedEvent = new AtJetstreamCommitEvent()
                {
                    Did = atJetstreamEvent.Did,
                    TimeStamp = atJetstreamEvent.TimeStamp,
                    Kind = atJetstreamEvent.Kind,
                    Commit = commit,
                    ExtensionData = ExtensionDataExcept(extensionData, "commit")
                };

                _metrics.EventsParsed.Add(1, new KeyValuePair<string, object?>("event_type", "commit"), new KeyValuePair<string, object?>("server", _server?.ToString()));

                break;

            case JetStreamEventKind.Identity:
                if (extensionData is null || !extensionData.TryGetValue("identity", out JsonElement identityElement))
                {
                    return null;
                }

                AtJetStreamIdentity? identity = JsonSerializer.Deserialize<AtJetStreamIdentity>(
                    identityElement,
                    SourceGenerationContext.Default.AtJetStreamIdentity);

                if (identity is null)
                {
                    return null;
                }

                derivedEvent = new AtJetstreamIdentityEvent()
                {
                    Did = atJetstreamEvent.Did,
                    TimeStamp = atJetstreamEvent.TimeStamp,
                    Kind = atJetstreamEvent.Kind,
                    Identity = identity,
                    ExtensionData = ExtensionDataExcept(extensionData, "identity")
                };

                _metrics.EventsParsed.Add(1, new KeyValuePair<string, object?>("event_type", "identity"), new KeyValuePair<string, object?>("server", _server?.ToString()));

                break;

            default:
                _metrics.UnknownEventsReceived.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
                break;
        }

        return derivedEvent;
    }

    /// <summary>
    /// Parses a <see cref="JetstreamProtocolVersion.V2"/> message, raising any notice or error it carries.
    /// </summary>
    /// <param name="json">The message to parse.</param>
    /// <param name="derivedEvent">The event the message carries, or <see langword="null"/> if it could not be derived.</param>
    /// <returns>
    ///   <see langword="true"/> if the message should be raised as a record, in which case <paramref name="derivedEvent"/> is
    ///   <see langword="null"/> when it could not be derived, or <see langword="false"/> if the message was a notice, an error,
    ///   or an event which has already been delivered.
    /// </returns>
    /// <exception cref="JsonException">Thrown when <paramref name="json"/> is not valid JSON, or an event it carries is malformed.</exception>
    internal bool TryDeriveV2Event(string json, out AtJetstreamEvent? derivedEvent)
    {
        derivedEvent = null;

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string? frameType = GetStringProperty(root, "$type");

        if (string.Equals(frameType, "error", StringComparison.Ordinal))
        {
            string error = ForLogging(GetStringProperty(root, "error") ?? "Unknown");
            string? message = GetStringProperty(root, "message");

            JetStreamLogger.ErrorReceived(_logger, error);

            OnFaultRaised(new FaultRaisedEventArgs(message is null ? error : ForLogging($"{error}: {message}")) { Error = error });

            return false;
        }

        if (!string.Equals(frameType, "message", StringComparison.Ordinal) ||
            !root.TryGetProperty("payload", out JsonElement payload) ||
            payload.ValueKind != JsonValueKind.Object)
        {
            // Not a frame this version of the protocol defines, so it is unparsable rather than unknown.
            return true;
        }

        string? payloadType = GetStringProperty(payload, "$type");
        string? kind = payloadType is not null && payloadType.StartsWith(SubscribeEventsTypePrefix, StringComparison.Ordinal)
            ? payloadType[SubscribeEventsTypePrefix.Length..]
            : null;

        if (string.Equals(kind, "info", StringComparison.Ordinal))
        {
            string name = ForLogging(GetStringProperty(payload, "name") ?? "Unknown");
            string? message = GetStringProperty(payload, "message");

            JetStreamLogger.InfoReceived(_logger, name);

            OnInfoReceived(new InfoReceivedEventArgs(name, message is null ? null : ForLogging(message)));

            return false;
        }

        JetstreamV2EventPayload? eventPayload = payload.Deserialize(SourceGenerationContext.Default.JetstreamV2EventPayload);

        if (eventPayload is null)
        {
            return true;
        }

        RecordSequence(eventPayload.Sequence);

        if (eventPayload.Sequence <= Interlocked.Read(ref _skipAtOrBelowSequence))
        {
            JetStreamLogger.DuplicateEventSkipped(_logger, eventPayload.Sequence);

            return false;
        }

        long timeStamp = (eventPayload.Time.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) / TimeSpan.TicksPerMicrosecond;

        // The $type has been consumed to pick the kind, so it is left out along with the properties the event carries.
        Dictionary<string, JsonElement> extensionData = eventPayload.ExtensionData is null
            ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            : ExtensionDataExcept(eventPayload.ExtensionData, "$type");

        switch (kind)
        {
            case "commit":
                if (eventPayload.Operation is null ||
                    eventPayload.Collection is null ||
                    eventPayload.Rev is null ||
                    eventPayload.RKey is null)
                {
                    return true;
                }

                derivedEvent = new AtJetstreamCommitEvent()
                {
                    Did = eventPayload.Did,
                    TimeStamp = timeStamp,
                    Kind = JetStreamEventKind.Commit,
                    Sequence = eventPayload.Sequence,
                    WitnessedAt = eventPayload.WitnessedAt,
                    Commit = new AtJetstreamCommit()
                    {
                        Operation = eventPayload.Operation.Value,
                        Collection = eventPayload.Collection,
                        Rev = eventPayload.Rev,
                        RKey = eventPayload.RKey,
                        Record = eventPayload.Record,
                        Cid = eventPayload.Cid
                    },
                    ExtensionData = extensionData
                };

                RecordEventParsed("commit");
                break;

            case "identity":
                if (eventPayload.Identity is null)
                {
                    return true;
                }

                derivedEvent = new AtJetstreamIdentityEvent()
                {
                    Did = eventPayload.Did,
                    TimeStamp = timeStamp,
                    Kind = JetStreamEventKind.Identity,
                    Sequence = eventPayload.Sequence,
                    WitnessedAt = eventPayload.WitnessedAt,
                    Identity = eventPayload.Identity,
                    ExtensionData = extensionData
                };

                RecordEventParsed("identity");
                break;

            case "account":
                if (eventPayload.Account is null)
                {
                    return true;
                }

                derivedEvent = new AtJetstreamAccountEvent()
                {
                    Did = eventPayload.Did,
                    TimeStamp = timeStamp,
                    Kind = JetStreamEventKind.Account,
                    Sequence = eventPayload.Sequence,
                    WitnessedAt = eventPayload.WitnessedAt,
                    Account = eventPayload.Account,
                    ExtensionData = extensionData
                };

                RecordEventParsed("account");
                break;

            case "sync":
                if (eventPayload.Sync is null)
                {
                    return true;
                }

                derivedEvent = new AtJetstreamSyncEvent()
                {
                    Did = eventPayload.Did,
                    TimeStamp = timeStamp,
                    Kind = JetStreamEventKind.Sync,
                    Sequence = eventPayload.Sequence,
                    WitnessedAt = eventPayload.WitnessedAt,
                    Sync = eventPayload.Sync,
                    ExtensionData = extensionData
                };

                RecordEventParsed("sync");
                break;

            default:
                // An event kind added to the jetstream after this library was built. It is raised as it is, in the
                // same way as an unknown kind from a version 1 server, so a consumer can still see it and its sequence.
                _metrics.UnknownEventsReceived.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));

                if (payloadType is not null)
                {
                    extensionData["$type"] = payload.GetProperty("$type").Clone();
                }

                derivedEvent = new AtJetstreamEvent()
                {
                    Did = eventPayload.Did,
                    TimeStamp = timeStamp,
                    Kind = JetStreamEventKind.Unknown,
                    Sequence = eventPayload.Sequence,
                    WitnessedAt = eventPayload.WitnessedAt,
                    ExtensionData = extensionData
                };
                break;
        }

        return true;
    }

    /// <summary>
    /// Records <paramref name="sequence"/> as seen, if it is larger than any sequence seen so far.
    /// </summary>
    /// <param name="sequence">The sequence number of an event.</param>
    /// <remarks>
    /// <para>Messages are parsed concurrently, so they do not finish in the order they arrived, and a later event can
    /// be recorded before an earlier one. Only ever moving forwards keeps <see cref="LastSequence"/> at the furthest point reached.</para>
    /// </remarks>
    private void RecordSequence(long sequence)
    {
        long current = Interlocked.Read(ref _lastSequence);

        while (sequence > current)
        {
            long previous = Interlocked.CompareExchange(ref _lastSequence, sequence, current);

            if (previous == current)
            {
                break;
            }

            current = previous;
        }
    }

    private void RecordEventParsed(string eventType) =>
        _metrics.EventsParsed.Add(1, new KeyValuePair<string, object?>("event_type", eventType), new KeyValuePair<string, object?>("server", _server?.ToString()));

    private static string? GetStringProperty(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out JsonElement property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    /// <summary>
    /// Copies <paramref name="extensionData"/>, leaving out the entry named by <paramref name="consumedKey"/>.
    /// </summary>
    /// <param name="extensionData">The extension data to copy.</param>
    /// <param name="consumedKey">The key whose value has already been turned into a strongly typed property.</param>
    /// <returns>The remaining extension data.</returns>
    /// <remarks>
    /// <para>A derived event is built from a new object rather than from the event it was derived from, so without this
    /// any property the jetstream sent which this library does not know about would be present on the event handed to
    /// <see cref="MessageReceived"/> and missing from the one handed to <see cref="RecordReceived"/>. The key the
    /// derived type was built from is left out, as its value is already available as a property.</para>
    /// </remarks>
    private static Dictionary<string, JsonElement> ExtensionDataExcept(IDictionary<string, JsonElement> extensionData, string consumedKey)
    {
        Dictionary<string, JsonElement> remaining = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, JsonElement> entry in extensionData.Where(entry => !string.Equals(entry.Key, consumedKey, StringComparison.Ordinal)))
        {
            remaining.Add(entry.Key, entry.Value);
        }

        return remaining;
    }

    private sealed record ConnectionSettings(HttpClient? HttpClient, CancellationToken CancellationToken);
}
