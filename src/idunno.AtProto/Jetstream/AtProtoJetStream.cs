// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

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
public class AtProtoJetstream : IDisposable
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

    private const string SubscribeEndpoint = "/subscribe";

    private readonly JetstreamMetrics _metrics;

    private readonly ILogger<AtProtoJetstream> _logger;

    private readonly Uri _uri = new("wss://jetstream1.us-west.bsky.network");

    private readonly Decompressor? _decompressor;

    private List<Nsid> _collections = [];

    private List<Did> _dids = [];

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
    /// <param name="uri">The host uri to connection to. Defaults to wss://jetstream1.us-west.bsky.network/. Do not connect to untrusted jet stream servers.</param>
    /// <param name="options">Any options to configure this instance of <see cref="AtProtoJetstream"/>.</param>
    /// <param name="webSocketOptions">Any <see cref="AtProto.WebSocketOptions"/> to set on the underlying client WebSocket.</param>
    /// <param name="httpClientOptions">Any <see cref="HttpClientOptions"/> for the internal http client used to make HTTP requests.</param>
    /// <param name="collections">The <see cref="Nsid"/>s of any collection types to subscribe to. If <see langword="null"/> or empty all collection types will be subscribed to.</param>
    /// <param name="dids">Any <see cref="Did"/>s to subscribe to. If <see langword="null"/> or empty all dids will be subscribed to.</param>
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
    /// <param name="uri">The host uri to connection to. Defaults to wss://jetstream1.us-west.bsky.network/. Do not connect to untrusted jet stream servers.</param>
    /// <param name="options">Any options to configure this instance of <see cref="AtProtoJetstream"/>.</param>
    /// <param name="webSocketOptions">Any <see cref="AtProto.WebSocketOptions"/> to set on the underlying client WebSocket.</param>
    /// <param name="collections">The <see cref="Nsid"/>s of any collection types to subscribe to. If <see langword="null"/> or empty all collection types will be subscribed to.</param>
    /// <param name="dids">Any <see cref="Did"/>s to subscribe to. If <see langword="null"/> or empty all dids will be subscribed to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClientFactory"/> is <see langword="null"/>.</exception>
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
        if (uri is not null)
        {
            _uri = uri;
        }

        if (collections is not null)
        {
            _collections = [.. collections];
        }

        if (dids is not null)
        {
            _dids = [.. dids];
        }

        if (options is not null)
        {
            Options = options;
            LoggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
        }
        else
        {
            LoggerFactory = NullLoggerFactory.Instance;
        }

        _metrics = new JetstreamMetrics(Options.MeterFactory);

        if (Options.UseCompression)
        {
            _decompressor = new Decompressor();

            if (Options.Dictionary is not null)
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
    /// Gets or sets a list of <see cref="Did"/>s to filter commit operations on.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>If the jetstream is connected the updated filters are sent to the server in the background, so they may
    /// not take effect until after the property has been set.</para>
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

            lock (_syncLock)
            {
                _dids = [.. value];
            }

            SendOptionsUpdateMessage().FireAndForget();
        }
    }

    /// <summary>
    /// Gets or sets a list of <see cref="Nsid"/>s of collections to filter commit operations on.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>If the jetstream is connected the updated filters are sent to the server in the background, so they may
    /// not take effect until after the property has been set.</para>
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

            lock (_syncLock)
            {
                _collections = [.. value];
            }

            SendOptionsUpdateMessage().FireAndForget();
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
    /// <param name="startFrom">A Unix microseconds timestamp cursor to begin playback from. A value of <see langword="null"/> results in live-tail operation.</param>
    public async Task ConnectAsync(
        DateTimeOffset startFrom)
    {
        await ConnectAsync(
            uri: null,
            cursor: startFrom.ToUnixTimeMilliseconds() * 1000,
            httpClient: null,
            cancellationToken: default).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the JetStream instance via a WebSocket connection.
    /// </summary>
    /// <param name="startFrom">A Unix microseconds timestamp cursor to begin playback from. A value of <see langword="null"/> results in live-tail operation.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    public async Task ConnectAsync(
        DateTimeOffset? startFrom,
        CancellationToken cancellationToken)
    {
        long? cursor = startFrom.HasValue ? startFrom.Value.ToUnixTimeMilliseconds() * 1000 : null;

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
    /// <param name="startFrom">A Unix microseconds timestamp cursor to begin playback from. A value of <see langword="null"/> results in live-tail operation.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    public async Task ConnectAsync(
        Uri uri,
        DateTimeOffset? startFrom,
        CancellationToken cancellationToken)
    {
        long? cursor = startFrom.HasValue ? startFrom.Value.ToUnixTimeMilliseconds() * 1000 : null;

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
    /// <param name="cursor">A Unix microseconds timestamp cursor to begin playback from. A value of <see langword="null"/> results in live-tail operation.</param>
    /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for any HTTP requests. If <see langword="null"/> a default configured HttpClient from the internal <see cref="IHttpClientFactory"/> will be used.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <exception cref="WebSocketException">Thrown when the underlying web socket could not connect.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the jetstream has been disposed.</exception>
    /// <remarks>
    /// <para>Connection attempts are serialised. A caller which arrives whilst another connection attempt is in
    /// progress waits for it to complete, and returns without doing anything if it left the jetstream connected.</para>
    /// <para>The jetstream does not reconnect by itself. A caller which wants to reconnect after a disconnection can
    /// call this method again, including from a <see cref="ConnectionStateChanged"/> handler.</para>
    /// </remarks>
    public async Task ConnectAsync(
        Uri? uri,
        long? cursor,
        HttpClient? httpClient,
        CancellationToken cancellationToken = default)
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
                connectedClient = await ConnectInternalAsync(uri, cursor, httpClient, stateChanges, cancellationToken).ConfigureAwait(false);
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

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Suppressing dispose exceptions on purpose.")]
    [SuppressMessage("Minor Code Smell", "S2486:Generic exceptions should not be ignored", Justification = "Suppressing dispose exceptions on purpose.")]
    private async Task<ClientWebSocket?> ConnectInternalAsync(
        Uri? uri,
        long? cursor,
        HttpClient? httpClient,
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
            if (_client is null || _client.State == WebSocketState.Aborted || _client.State == WebSocketState.Closed)
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
        _server = uri;

        List<Nsid> collections;
        List<Did> dids;

        lock (_syncLock)
        {
            // The filters are copied under the lock, as a concurrent update to either of them would otherwise
            // be enumerated as the query string is built.
            collections = [.. _collections];
            dids = [.. _dids];
        }

        Uri endpoint = new(uri, SubscribeEndpoint);

        StringBuilder uriBuilder = new();

        uriBuilder.Append(endpoint);
        uriBuilder.Append('?');

        foreach (Nsid collection in collections)
        {
            uriBuilder.Append(
                CultureInfo.InvariantCulture,
                $"wantedCollections={HttpUtility.UrlEncode(collection.ToString())}&");
        }

        foreach (Did did in dids)
        {
            uriBuilder.Append(
                CultureInfo.InvariantCulture,
                $"wantedDids={HttpUtility.UrlEncode(did.ToString())}&");
        }

        if (cursor is not null)
        {
            uriBuilder.Append(
                CultureInfo.InvariantCulture,
                $"cursor={cursor}&");
        }

        if (Options.UseCompression)
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

        Uri jetStreamUri = new(uriBuilder.ToString());

        WebSocketState previousState = client.State;

        JetStreamLogger.ConnectingTo(_logger, jetStreamUri);

        httpClient ??= _httpClient;

        try
        {
            await client.ConnectAsync(
                uri: jetStreamUri,
                invoker: httpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _metrics.ConnectionsOpened.Add(1, new KeyValuePair<string, object?>("server", jetStreamUri.ToString()));
        }
        catch (Exception ex)
        {
            if (ex is WebSocketException webSocketException)
            {
                JetStreamLogger.WebSocketException(_logger, webSocketException);
            }

            // Counted once here, however the connection failed, rather than again below.
            _metrics.ConnectionFailures.Add(1, new KeyValuePair<string, object?>("server", jetStreamUri.ToString()));

            if (client.State != previousState)
            {
                stateChanges.Add(client.State);
            }

            throw;
        }

        if (client.State != previousState)
        {
            stateChanges.Add(client.State);
        }

        if (client.State == WebSocketState.Open)
        {
            return client;
        }

        JetStreamLogger.ConnectionFailed(_logger, client.State);
        _metrics.ConnectionFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));

        return null;
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
        ClientWebSocket client = _client;

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

                DisconnectedGracefully = true;
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
                // Swallow
            }
            catch (Exception ex)
            {
                JetStreamLogger.CloseError(_logger, ex);
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
            OnConnectionStateChanged(new ConnectionStateChangedEventArgs(client.State));
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

                _httpClient.Dispose();
                _decompressor?.Dispose();
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

    private ClientWebSocket CreateWebSocketClient()
    {
        var client = new ClientWebSocket();
        JetStreamLogger.InternalClientWebSocketCreated(_logger);

        if (WebSocketOptions is not null)
        {
            if (WebSocketOptions.Proxy is not null)
            {
                client.Options.Proxy = WebSocketOptions.Proxy;
            }

            if (WebSocketOptions.KeepAliveInterval is not null)
            {
                client.Options.KeepAliveInterval = WebSocketOptions.KeepAliveInterval.Value;
            }
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

        while (client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                (WebSocketReceiveResult webSocketReceiveResult, byte[] message) =
                    await client.ReceiveNextMessageAsync(
                        bufferSize: Options.BufferSize,
                        maxMessageSize: Options.MaxMessageSize,
                        logger: _logger,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                if (webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
                {
                    // A close frame is a write, so it is serialised with the other writes to the socket. The semaphore
                    // is released before the event below is raised, so a handler which closes or reconnects does not
                    // wait on a semaphore this loop is holding.
                    await _sendSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                    try
                    {
                        await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        _sendSemaphore.Release();
                    }

                    JetStreamLogger.CloseMessageReceived(_logger);

                    // The server asked to close and the close was completed by replying to it, which is as graceful as
                    // a disconnection gets. The flag records how the connection ended, not which end ended it.
                    DisconnectedGracefully = true;

                    _metrics.ConnectionsClosed.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
                    OnConnectionStateChanged(new ConnectionStateChangedEventArgs(client.State));
                    finalStateRaised = true;

                    break;
                }

                if (webSocketReceiveResult.MessageType != expectedMessageType)
                {
                    JetStreamLogger.UnexpectedMessageType(_logger, webSocketReceiveResult.MessageType);
                }

                byte[] receivedData;

                if (Options.UseCompression)
                {
                    try
                    {
                        Span<byte> bufferAsSpan = message.AsSpan(0, message.Length);

                        // Unwrap defaults to allowing 2GB of decompressed output, so without a limit of our own the
                        // maximum message size would only bound the compressed frame. A 24KB frame can declare, and
                        // expand to, hundreds of megabytes, so the limit has to be applied to what comes out of it.
                        receivedData = _decompressor!.Unwrap(bufferAsSpan, Options.MaxMessageSize).ToArray();
                    }
                    catch (ZstdException ex)
                    {
                        // Can't decompress so ignore this message.
                        _metrics.MessageDecompressionFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
                        JetStreamLogger.DecompressionException(_logger, ex);
                        continue;
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

                    // Now go to handle message in a new task.
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Options.TaskFactory.StartNew(() => ParseMessage(messageAsString, _logger).FireAndForgetAsync(_logger), cancellationToken).ConfigureAwait(false);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
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
            catch (Exception e)
            {
                JetStreamLogger.MessageLoopError(_logger, e);
                LogFault(e.Message);
            }
        }

        if (client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            // CloseAsync raises the state change itself.
            await CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else if (!finalStateRaised && client.State != WebSocketState.Open)
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

    private static string ForLogging(string message)
    {
        if (message.Length <= MaximumLoggedMessageLength)
        {
            return message;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{message[..MaximumLoggedMessageLength]}… (truncated, {message.Length} characters)");
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catch all for logging.")]
    private Task ParseMessage(string json, ILogger logger)
    {        try
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
            AtJetstreamEvent? atJetstreamEvent = JsonSerializer.Deserialize<AtJetstreamEvent>(
                json,
                SourceGenerationContext.Default.AtJetstreamEvent);

            if (atJetstreamEvent is not null)
            {
                AtJetstreamEvent? derivedEvent = DeriveEvent(atJetstreamEvent);

                if (derivedEvent is not null)
                {
                    OnRecordReceived(new RecordReceivedEventArgs(derivedEvent));
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

        await _sendSemaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            // The state is re-checked now the socket is held, as it can have closed whilst this send was queued behind
            // another one, and sending on a closed socket throws.
            if (client.State != WebSocketState.Open)
            {
                return;
            }

            await client.SendAsync(messageAsBytes, WebSocketMessageType.Text, true, CancellationToken.None).FireAndForgetAsync(_logger).ConfigureAwait(false);
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
                    Account = account
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
                    Commit = commit
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
                    Identity = identity
                };

                _metrics.EventsParsed.Add(1, new KeyValuePair<string, object?>("event_type", "identity"), new KeyValuePair<string, object?>("server", _server?.ToString()));

                break;

            default:
                _metrics.UnknownEventsReceived.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
                break;
        }

        return derivedEvent;
    }
}
