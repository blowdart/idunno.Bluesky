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

    private const string SubscribeEndpoint = "/subscribe";

    private readonly JetstreamMetrics _metrics;

    private readonly ILogger<AtProtoJetstream> _logger;

    private readonly Uri _uri = new("wss://jetstream1.us-west.bsky.network");

    private readonly Decompressor? _decompressor;

    private List<Nsid> _collections = [];

    private List<Did> _dids = [];

    private ClientWebSocket _client;

    private const string HttpClientName = "idunno.atproto.jetstream";
    internal HttpClientOptions? _httpClientOptions;
    private readonly ServiceProvider? _serviceProvider;
    private readonly HttpClient _httpClient;

    private Uri? _server;

    /// <summary>
    /// Creates a new instance of <see cref="Jetstream"/>.
    /// </summary>
    /// <param name="uri">The host uri to connection to. Defaults to wss://jetstream1.us-west.bsky.network/. Do not connect to untrusted jet stream servers.</param>
    /// <param name="options">Any options to configure this instance of <see cref="AtProtoJetstream"/>.</param>
    /// <param name="webSocketOptions">Any <see cref="AtProto.WebSocketOptions"/> to set on the underlying client WebSocket.</param>
    /// <param name="httpClientOptions">Any <see cref="HttpClientOptions"/> for the internal http client used to make HTTP requests.</param>
    /// <param name="collections">The <see cref="Nsid"/>s of any collection types to subscribe to. If <see langword="null"/> or empty all collection types will be subscribed to.</param>
    /// <param name="dids">Any <see cref="Did"/>s to subscribe to. If <see langword="null"/> or empty all dids will be subscribed to.</param>
    public AtProtoJetstream(
        Uri? uri = null,
        JetstreamOptions? options = null,
        WebSocketOptions? webSocketOptions = null,
        HttpClientOptions? httpClientOptions = null,
        ICollection<Nsid>? collections = null,
        ICollection<Did>? dids = null)
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

        IServiceCollection services = new ServiceCollection();
        _httpClientOptions = httpClientOptions;

        services
            .AddHttpClient(HttpClientName, client => InternalConfigureHttpClient(client, _httpClientOptions?.HttpUserAgent, _httpClientOptions?.Timeout))
            .ConfigurePrimaryHttpMessageHandler(() => CreateHttpMessageHandler(_httpClientOptions));

        _serviceProvider = services.BuildServiceProvider();
        HttpClientFactory = _serviceProvider.GetService<IHttpClientFactory>()!;

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

    private WebSocketOptions WebSocketOptions { get; init; } = new WebSocketOptions();

    /// <summary>
    /// Gets or sets a list of <see cref="Did"/>s to filter commit operations on.
    /// </summary>
    public IReadOnlyCollection<Did> DidFilter
    {
        get
        {
            return _dids.AsReadOnly();
        }

        set
        {
            _dids = [.. value];
            SendOptionsUpdateMessage().FireAndForget();
        }
    }

    /// <summary>
    /// Gets or sets a list of <see cref="Nsid"/>s of collections to filter commit operations on.
    /// </summary>
    public IReadOnlyCollection<Nsid> CollectionFilter
    {
        get
        {
            return _collections.AsReadOnly();
        }

        set
        {
            _collections = [.. value];
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
    public bool DisconnectedGracefully { get => _client.State == WebSocketState.Closed && field; private set; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> indicating when last time a message from the JetsStream was received.
    /// </summary>
    public DateTimeOffset? MessageLastReceived { get; private set; }

    /// <summary>
    /// Raised when this instance of <see cref="AtProtoJetstream"/> receives a message.
    /// </summary>
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Raised when this instance of <see cref="AtProtoJetstream"/> receives a message.
    /// </summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Raised when this instance of <see cref="AtProtoJetstream"/> parses a message and converts it to a record.
    /// </summary>
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
    protected virtual void OnConnectionStateChanged(ConnectionStateChangedEventArgs e)
    {
        EventHandler<ConnectionStateChangedEventArgs>? connectionStatusChanged = ConnectionStateChanged;

        if (!_disposed)
        {
            connectionStatusChanged?.Invoke(this, e);

            if (e is not null)
            {
                JetStreamLogger.ClientStateChanged(_logger, e.State);
            }
            else
            {
                JetStreamLogger.ClientStateChanged(_logger, WebSocketState.None);
            }
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
    [MemberNotNull(nameof(_client))]
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
    [MemberNotNull(nameof(_client))]
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
    [MemberNotNull(nameof(_client))]
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
    [MemberNotNull(nameof(_client))]
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
    [MemberNotNull(nameof(_client))]
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
    [MemberNotNull(nameof(_client))]
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
    [MemberNotNull(nameof(_client))]
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
    /// <exception cref="Exception">Thrown when the underlying web socket throw an exception when connecting.</exception>
    [MemberNotNull(nameof(_client))]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Suppressing dispose exceptions on purpose.")]
    [SuppressMessage("Minor Code Smell", "S2486:Generic exceptions should not be ignored", Justification = "Suppressing dispose exceptions on purpose.")]
    public async Task ConnectAsync(
        Uri? uri,
        long? cursor,
        HttpClient? httpClient,
        CancellationToken cancellationToken = default)
    {
        if (_client is not null && _client.State == WebSocketState.Open)
        {
            return;
        }

        if (_client is null || _client.State == WebSocketState.Aborted || _client.State == WebSocketState.Closed)
        {
            lock (_syncLock)
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
                OnConnectionStateChanged(new ConnectionStateChangedEventArgs(_client.State));
            }
        }

        uri ??= _uri;
        _server = uri;

        Uri endpoint = new(uri, SubscribeEndpoint);

        StringBuilder uriBuilder = new();

        uriBuilder.Append(endpoint);
        uriBuilder.Append('?');

        foreach (Nsid collection in _collections)
        {
            uriBuilder.Append(
                CultureInfo.InvariantCulture,
                $"wantedCollections={HttpUtility.UrlEncode(collection.ToString())}&");
        }

        foreach (Did did in _dids)
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

        uriBuilder.Append(
            CultureInfo.InvariantCulture,
            $"maximumMessageSizeBytes={Options.BufferSize}&");

        if (uriBuilder[^1] == '&')
        {
            uriBuilder.Length--;
        }

        if (uriBuilder[^1] == '?')
        {
            uriBuilder.Length--;
        }

        Uri jetStreamUri = new(uriBuilder.ToString());

        WebSocketState previousState = _client.State;

        JetStreamLogger.ConnectingTo(_logger, jetStreamUri);

        httpClient ??= _httpClient;

        try
        {
            await _client.ConnectAsync(
                uri: jetStreamUri,
                invoker: httpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _metrics.ConnectionsOpened.Add(1, new KeyValuePair<string, object?>("server", jetStreamUri.ToString()));
        }
        catch (WebSocketException ex)
        {
            JetStreamLogger.WebSocketException(_logger, ex);
            _metrics.ConnectionFailures.Add(1, new KeyValuePair<string, object?>("server", jetStreamUri.ToString()));
        }
        catch
        {
            _metrics.ConnectionFailures.Add(1, new KeyValuePair<string, object?>("server", jetStreamUri.ToString()));
            throw;
        }

        if (_client.State != previousState)
        {
            OnConnectionStateChanged(new ConnectionStateChangedEventArgs(_client.State));
        }

        if (_client.State == WebSocketState.Open)
        {
            ReceiveLoop(cancellationToken).FireAndForget();
        }
        else
        {
            JetStreamLogger.ConnectionFailed(_logger, _client.State);
            _metrics.ConnectionFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
        }
    }

    /// <summary>
    /// Closes the existing WebSocket connection.
    /// </summary>
    /// <param name="status">Status for the shutdown. Defaults to <see cref="WebSocketCloseStatus.NormalClosure"/>.</param>
    /// <param name="statusDescription">Reason for the shutdown.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the underlying WebSocket has been disposed.</exception>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catch all exceptions to avoid cancellation exceptions")]
    public async Task CloseAsync(
        WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure,
        string statusDescription = "Client disconnect",
        CancellationToken cancellationToken = default)
    {
        WebSocketState startingState = _client.State;

        if (_client.State == WebSocketState.Closed)
        {
            return;
        }
        else if (_client.State == WebSocketState.Open)
        {
            try
            {
                await _client.CloseAsync(status, statusDescription, cancellationToken).ConfigureAwait(false);
                DisconnectedGracefully = true;
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
        else if (_client.State == WebSocketState.Connecting)
        {
            try
            {
                _client.Abort();
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

        if (_client.State != startingState)
        {
            OnConnectionStateChanged(new ConnectionStateChangedEventArgs(_client.State));
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
                _client.Dispose();
                _httpClient.Dispose();
                _decompressor?.Dispose();
                _serviceProvider?.Dispose();
            }

            _disposed = true;
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

    private HttpMessageHandler CreateHttpMessageHandler(HttpClientOptions? httpClientOptions)
    {
        SslClientAuthenticationOptions? sslOptions = null;
        bool checkCrl = true;

        if (httpClientOptions is not null)
        {
            checkCrl = httpClientOptions.CheckCertificateRevocationList;
        }

        if (!checkCrl)
        {
            sslOptions = new SslClientAuthenticationOptions
            {
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            };
        }

        return httpClientOptions?.ProxyUri is not null
            ? new ProxiedSsrfDelegatingHandler(
                proxy: new WebProxy(httpClientOptions.ProxyUri),
                connectionStrategy: ConnectionStrategy.None,
                additionalUnsafeNetworks: null,
                additionalUnsafeIpAddresses: null,
                connectTimeout: httpClientOptions?.Timeout,
                allowInsecureProtocols: false,
                allowLoopback: false,
                failMixedResults: true,
                allowAutoRedirect: false,
                automaticDecompression: DecompressionMethods.All,
                sslOptions: sslOptions,
                loggerFactory: LoggerFactory)
            : SsrfSocketsHttpHandlerFactory.Create(
                connectionStrategy: ConnectionStrategy.None,
                additionalUnsafeNetworks: null,
                additionalUnsafeIpAddresses: null,
                connectTimeout: httpClientOptions?.Timeout,
                allowInsecureProtocols: false,
                allowLoopback: false,
                failMixedResults: true,
                allowAutoRedirect: false,
                automaticDecompression: DecompressionMethods.All,
                sslOptions: sslOptions,
                loggerFactory: LoggerFactory);
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
    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[Options.BufferSize];
        WebSocketMessageType expectedMessageType = Options.UseCompression ? WebSocketMessageType.Binary : WebSocketMessageType.Text;

        while (_client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                (WebSocketReceiveResult webSocketReceiveResult, byte[] message) =
                    await _client.ReceiveNextMessageAsync(
                        bufferSize: Options.BufferSize,
                        maxMessageSize: Options.MaxMessageSize,
                        logger: _logger,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                if (webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await _client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken: cancellationToken).ConfigureAwait(false);
                    JetStreamLogger.CloseMessageReceived(_logger);
                    DisconnectedGracefully = false;
                    continue;
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
                        receivedData = _decompressor!.Unwrap(bufferAsSpan).ToArray();
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
                    receivedData = new byte[message.Length];
                    Array.Copy(buffer, 0, receivedData, 0, message.Length);
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
                    if (_client.State == WebSocketState.Open)
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

        if (_client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            await CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

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
                    JetStreamLogger.ParseMessageDeserializationReturnedNull(logger, json);
                    _metrics.MessageParsingFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
                }
            }
            else
            {
                JetStreamLogger.ParseMessageDeserializationReturnedNull(logger, json);
                _metrics.MessageParsingFailures.Add(1, new KeyValuePair<string, object?>("server", _server?.ToString()));
            }

            return Task.CompletedTask;
        }
        catch (JsonException ex)
        {
            JetStreamLogger.ParseMessageCouldNotProcessAsJson(logger, json, ex);
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

    private async Task SendOptionsUpdateMessage()
    {
        if (_client is null || _client.State != WebSocketState.Open)
        {
            return;
        }

        OptionsUpdatePayload payload = new()
        {
            MaxMessageSizeBytes = Options.BufferSize
        };

        if (_collections is not null && _collections.Count > 0)
        {
            payload.WantedCollections = [.. _collections];
        }

        if (_dids is not null && _dids.Count > 0)
        {
            payload.WantedDIDs = [.. _dids];
        }

        OptionsUpdateMessage optionsUpdateMessage = new()
        {
            Payload = payload
        };

        string message = JsonSerializer.Serialize(optionsUpdateMessage, SourceGenerationContext.Default.OptionsUpdateMessage);
        byte[] messageAsBytes = Encoding.UTF8.GetBytes(message);

        await _client.SendAsync(messageAsBytes, WebSocketMessageType.Text, true, CancellationToken.None).FireAndForgetAsync(_logger).ConfigureAwait(false);
        JetStreamLogger.OptionsUpdateMessageSent(_logger);
    }

    internal AtJetstreamEvent? DeriveEvent(AtJetstreamEvent atJetstreamEvent)
    {
        AtJetstreamEvent derivedEvent = atJetstreamEvent;

        // As System.Text.Json polymorphism expects that the type identifier property name to begin with a $
        // we have to do this manually.
        switch (atJetstreamEvent.Kind)
        {
            case JetStreamEventKind.Account:
                AtJetstreamAccount? account = JsonSerializer.Deserialize<AtJetstreamAccount>(
                    atJetstreamEvent.ExtensionData["account"],
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
                AtJetstreamCommit? commit = JsonSerializer.Deserialize<AtJetstreamCommit>(
                    atJetstreamEvent.ExtensionData["commit"],
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
                AtJetStreamIdentity? identity = JsonSerializer.Deserialize<AtJetStreamIdentity>(
                    atJetstreamEvent.ExtensionData["identity"],
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

    private static void InternalConfigureHttpClient(HttpClient client, string? httpUserAgent = null, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        client.DefaultRequestVersion = HttpVersion.Version20;

        Assembly assembly = typeof(Agent).Assembly;
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (httpUserAgent is null)
        {
            if (string.IsNullOrEmpty(version))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("idunno.AtProto");
            }
            else
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("idunno.AtProto/" + version);
            }
        }
        else
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(httpUserAgent);
        }

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));

        if (timeout is null)
        {
            client.Timeout = new(0, 5, 0);
        }
        else
        {
            client.Timeout = (TimeSpan)timeout;
        }
    }
}
