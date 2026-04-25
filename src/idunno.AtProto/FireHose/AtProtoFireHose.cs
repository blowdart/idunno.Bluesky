// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Web;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using PeterO.Cbor;

using idunno.AtProto.FireHose.Events;

namespace idunno.AtProto.FireHose;

/// <summary>
/// Represents a connection to the AtProto FireHose service, allowing for real-time data streaming via WebSocket.
/// This class provides methods to connect, receive, and manage messages from the Firehose, as well as events for
/// handling received data and connection state changes.
/// </summary>
/// <remarks>
///<para>
/// See https://atproto.com/specs/event-stream for formal documentation, and
/// https://github.com/bluesky-social/atproto/blob/main/lexicons/com/atproto/sync/subscribeRepos.json for the record definitions.</para>
/// </remarks>
public class AtProtoFireHose : IDisposable
{
#if NET9_0_OR_GREATER
    private readonly Lock _syncLock = new ();
#else
    private readonly object _syncLock = new();
#endif

    private volatile bool _disposed;

    private const string SubscribeReposEndpoint = "/xrpc/com.atproto.sync.subscribeRepos";

    private readonly Uri _uri = new("wss://bsky.network/");

    private readonly ILogger<AtProtoFireHose> _logger;

    private static readonly CBOREncodeOptions s_cborEncodeOptions = new("useIndefLengthStrings=true;float64=true;allowduplicatekeys=true;allowEmpty=true");

    private ClientWebSocket _client;

    private bool _subscribedToRepoMessages;

    /// <summary>
    /// Creates a new instance of <see cref="AtProtoFireHose"/>.
    /// </summary>
    /// <param name="uri">The host uri to connection to. Defaults to wss://bsky.network/.</param>
    /// <param name="options">Any options to configure this instance of <see cref="AtProtoFireHose"/>.</param>
    /// <param name="webSocketOptions">Any <see cref="AtProto.WebSocketOptions"/> to set on the underlying client WebSocket.</param>
    public AtProtoFireHose(
        Uri? uri = null,
        FireHoseOptions? options = null,
        WebSocketOptions? webSocketOptions = null)
    {
        if (uri is not null)
        {
            _uri = uri;
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

        if (webSocketOptions is not null)
        {
            WebSocketOptions = webSocketOptions;
        }

        _logger = LoggerFactory.CreateLogger<AtProtoFireHose>();

        _client = CreateWebSocketClient();
    }

    /// <summary>
    /// Gets a configured logger factory from which to create loggers.
    /// </summary>
    protected internal ILoggerFactory LoggerFactory { get; init; }

    /// <summary>
    /// Gets the configuration options for the firehose.
    /// </summary>
    protected internal FireHoseOptions Options { get; init; } = new FireHoseOptions();

    private WebSocketOptions WebSocketOptions { get; init; } = new WebSocketOptions();

    /// <summary>
    /// Gets a flag indicating whether the underlying WebSocket is connected to the jetstream.
    /// </summary>
    public bool IsConnected => _client.State == WebSocketState.Open;

    /// <summary>
    /// Gets the <see cref="WebSocketState"/> of the underlying <see cref="System.Net.WebSockets.ClientWebSocket"/>.
    /// </summary>
    public WebSocketState State => _client.State;

    /// <summary>
    /// Gets a flag indicating whether the underlying WebSocket was disconnected gracefully,
    /// by requesting a cancellation on the <see cref="CancellationToken"/> passed to the ConnectAsync methods or
    /// by calling <see cref="CloseAsync(WebSocketCloseStatus, string, CancellationToken)"/>.
    /// </summary>
    public bool DisconnectedGracefully { get => _client.State == WebSocketState.Closed && field; private set; }

    /// <summary>
    /// Gets the underlying <see cref="ClientWebSocket"/>.
    /// </summary>
    protected ClientWebSocket ClientWebSocket => _client;

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> indicating when last time a message from the Firehose was received.
    /// </summary>
    public DateTimeOffset? MessageLastReceived { get; private set; }

    /// <summary>
    /// Raised when this instance of <see cref="AtProtoFireHose"/> receives a message.
    /// </summary>
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Raised when this instance of <see cref="AtProtoFireHose"/> decodes a repo message.
    /// </summary>
    public event EventHandler<RepoMessageReceivedEventArgs>? RepoMessageReceived;

    /// <summary>
    /// Raised when this instance of <see cref="AtProtoFireHose"/> receives a message.
    /// </summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Raised when this instance of <see cref="AtProtoFireHose"/> encounters a fault.
    /// </summary>
    public event EventHandler<FaultRaisedEventArgs>? FaultRaised;

    /// <summary>
    /// Called to raise any <see cref="MessageReceived"/> events, if any.
    /// </summary>
    /// <param name="e">The <see cref="MessageReceivedEventArgs"/> for the event.</param>
    protected virtual void OnMessageReceived(MessageReceivedEventArgs e)
    {
        EventHandler<MessageReceivedEventArgs>? messageReceived = MessageReceived;

        if (!_disposed)
        {
            MessageLastReceived = DateTimeOffset.UtcNow;
            messageReceived?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Called to raise any <see cref="RepoMessageReceived"/> events, if any.
    /// </summary>
    /// <param name="e">The <see cref="RepoMessageReceivedEventArgs"/> for the event.</param>
    protected virtual void OnRepoMessageReceived(RepoMessageReceivedEventArgs e)
    {
        EventHandler<RepoMessageReceivedEventArgs>? repoMessageReceived = RepoMessageReceived;

        if (!_disposed)
        {
            repoMessageReceived?.Invoke(this, e);
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
                FireHoseLogger.ClientStateChanged(_logger, e.State);
            }
            else
            {
                FireHoseLogger.ClientStateChanged(_logger, WebSocketState.None);
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

        if (!_disposed)
        {
            faultRaised?.Invoke(this, e);
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
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Disposes resources held by this instance.
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
        FireHoseLogger.InternalClientWebSocketCreated(_logger);

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

    /// <summary>
    /// Connect to the Firehose instance via a WebSocket connection and subscribe to repo messages.
    /// </summary>
    /// <param name="startFrom">The <see cref="DateTimeOffset"/> to rewind the fire hose to before starting.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task ConnectToRepoMessagesAsync(DateTimeOffset startFrom) =>  await ConnectToRepoMessagesAsync(
            uri: null,
            startFrom: startFrom,
            collections: null,
            cancellationToken: default).ConfigureAwait(false);


    /// <summary>
    /// Connect to the Firehose instance via a WebSocket connection and subscribe to repo messages.
    /// </summary>
    /// <param name="startFrom">The <see cref="DateTimeOffset"/> to rewind the fire hose to before starting.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task ConnectToRepoMessagesAsync(DateTimeOffset startFrom, CancellationToken cancellationToken) => await ConnectToRepoMessagesAsync(
            uri: null,
            startFrom: startFrom,
            collections: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Connect to the Firehose instance via a WebSocket connection and subscribe to repo messages.
    /// </summary>
    /// <param name="startFrom">The <see cref="DateTimeOffset"/> to rewind the fire hose to before starting.</param>
    /// <param name="collections">The collections to listen to events for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task ConnectToRepoMessagesAsync(
        DateTimeOffset startFrom,
        ICollection<Nsid>? collections,
        CancellationToken cancellationToken) => await ConnectToRepoMessagesAsync(
            uri: null,
            startFrom: startFrom,
            collections: collections,
            cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Connect to the Firehose instance via a WebSocket connection and subscribe to repo messages.
    /// </summary>
    /// <param name="uri">The URI of the firehose server to connection to.</param>
    /// <param name="startFrom">The <see cref="DateTimeOffset"/> to rewind the fire hose to before starting.</param>
    /// <param name="collections">The collections to listen to events for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task ConnectToRepoMessagesAsync(
        Uri? uri,
        DateTimeOffset startFrom,
        ICollection<Nsid>? collections,
        CancellationToken cancellationToken)
    {
        long cursor = startFrom.ToUnixTimeMilliseconds() * 1000;

        await ConnectToRepoMessagesAsync(
            uri: uri,
            collections: collections,
            cursor: cursor,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connect to the firehose instance via a WebSocket connection.
    /// </summary>
    /// <param name="uri">The URI of the firehose server to connection to. Defaults to the URI passed during construction</param>
    /// <param name="cursor">A Unix microseconds timestamp cursor to begin playback from. A value of <see langword="null" /> results in live-tail operation.</param>
    /// <param name="collections">The collections to listen to events for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [MemberNotNull(nameof(_client))]
    [SuppressMessage("Minor Code Smell", "S2486:Generic exceptions should not be ignored", Justification = "Suppressing dispose exceptions on purpose.")]
    [SuppressMessage("ApiDesign", "RS0027:API with optional parameter(s) should have the most parameters amongst its public overloads", Justification = "Fine as is")]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Deliberate suppression of exceptions from dispose.")]
    public async Task ConnectToRepoMessagesAsync(
        Uri? uri = null,
        long? cursor = null,
        ICollection<Nsid>? collections = null,
        CancellationToken cancellationToken = default)
    {
        _subscribedToRepoMessages = true;

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
                        FireHoseLogger.ErrorDisposingClientInConnectAsync(_logger, ex);
                    }
                }

                _client = CreateWebSocketClient();
                OnConnectionStateChanged(new ConnectionStateChangedEventArgs(_client.State));
            }
        }

        uri ??= _uri;
        Uri endpoint = new(uri, SubscribeReposEndpoint);

        StringBuilder uriBuilder = new();
        uriBuilder.Append(endpoint);
        uriBuilder.Append('?');

        if (collections is not null)
        {
            foreach (Nsid collection in collections)
            {
                uriBuilder.Append(
                    CultureInfo.InvariantCulture,
                    $"wantedCollections={HttpUtility.UrlEncode(collection.ToString())}&");
            }
        }

        if (cursor is not null)
        {
            uriBuilder.Append(
                CultureInfo.InvariantCulture,
                $"cursor={cursor}&");
        }

        if (uriBuilder[^1] == '&')
        {
            uriBuilder.Length--;
        }

        if (uriBuilder[^1] == '?')
        {
            uriBuilder.Length--;
        }

        Uri serverUri = new(uriBuilder.ToString());

        WebSocketState previousState = _client.State;

        FireHoseLogger.ConnectingTo(_logger, serverUri);

        try
        {
            await _client.ConnectAsync(serverUri, cancellationToken).ConfigureAwait(false);
        }
        catch (WebSocketException ex)
        {
            FireHoseLogger.WebSocketException(_logger, ex);
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
            FireHoseLogger.ConnectionFailed(_logger, _client.State);
        }
    }

    /// <summary>
    /// Closes the existing WebSocket connection.
    /// </summary>
    /// <param name="status">Status for the shutdown. Defaults to <see cref="WebSocketCloseStatus.NormalClosure"/>.</param>
    /// <param name="statusDescription">Reason for the shutdown.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown when this instance has already been disposed.</exception>
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
                FireHoseLogger.CloseError(_logger, ex);
            }
        }
        else if (_client.State == WebSocketState.Connecting)
        {
            try
            {
                _client.Abort();
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
                FireHoseLogger.CloseError(_logger, ex);
            }
        }

        if (_client.State != startingState)
        {
            OnConnectionStateChanged(new ConnectionStateChangedEventArgs(_client.State));
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catch all for logging.")]
    [SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler", Justification = "A scheduler can be configured on the TaskFactory in Options.")]
    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        WebSocketMessageType expectedMessageType = WebSocketMessageType.Binary;

        while (_client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                (WebSocketReceiveResult webSocketReceiveResult, byte[] frame) =
                    await _client.ReceiveNextMessageAsync(
                        Options.BufferSize,
                        maxMessageSize: Options.MaxMessageSize,
                        logger: _logger,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                if (webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await _client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken: cancellationToken).ConfigureAwait(false);
                    FireHoseLogger.CloseMessageReceived(_logger);
                    DisconnectedGracefully = false;
                    continue;
                }

                if (webSocketReceiveResult.MessageType != expectedMessageType)
                {
                    FireHoseLogger.UnexpectedMessageType(_logger, webSocketReceiveResult.MessageType);
                }

                OnMessageReceived(new MessageReceivedEventArgs(frame));

                if (frame.Length == 0)
                {
                    FireHoseLogger.WebSocketMessageHadZeroLength(_logger);
                }
                else if (_subscribedToRepoMessages && RepoMessageReceived is not null)
#pragma warning disable S125 // Sections of code should not be commented out
                {
                    // Only go through the decode and parse work if anyone is listening

                    // TODO: Switch to fire and forget

                    await ParseRepoMessage(frame, OnRepoMessageReceived, _logger, cancellationToken).ConfigureAwait(false);

//#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
//                        Options.TaskFactory.StartNew(() => ParseRepoMessage(message, OnRepoMessageReceived, _logger)
//                            .FireAndForgetAsync(_logger), cancellationToken)
//                            .ConfigureAwait(false);
//#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                }
#pragma warning restore S125 // Sections of code should not be commented out
            }
            catch (Exception e)
            {
                FireHoseLogger.MessageLoopError(_logger, e);
                LogFault(e.Message);
            }

        }

        if (_client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            await CloseAsync(cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catch all for logging.")]
    private static async Task ParseRepoMessage(byte[] frame, Action<RepoMessageReceivedEventArgs> raiseEvent, ILogger logger, CancellationToken cancellationToken = default)
    {
        CBORObject[]? decodedFrame;

        try
        {
            decodedFrame = CBORObject.DecodeSequenceFromBytes(frame, s_cborEncodeOptions);

            if (decodedFrame is null)
            {
                FireHoseLogger.WebSocketMessageDecodeNullStructure(logger);
            }
            else if (decodedFrame.Length != 2)
            {
                FireHoseLogger.WebSocketMessageDecodeUnexpectedStructure(logger, decodedFrame.Length);
            }
            else
            {
                RepoMessageReceivedEventArgs? eventArgs;
                FrameHeader header = new(decodedFrame[0]);

                switch (header.Operation)
                {
                    case HeaderOperation.Unknown:
                        eventArgs = null;
                        break;

                    case HeaderOperation.Error:
                        FrameError frameError = FrameError.FromCBORObject(decodedFrame[1]);
                        eventArgs = new(header, frameError);
                        break;

                    case HeaderOperation.Frame:
                        switch (header.Type)
                        {
                            case "#commit":
                                {
                                    CommitPayload payload = CommitPayload.FromCBORObject(decodedFrame[1]);

                                    // Now for the hard part.
                                    //if (payload.RawBlocks is not null &&
                                    //    payload.RawBlocks.HasValue &&
                                    //    payload.RawBlocks.Value.Length != 0)
                                    //{
                                    //    await AtContentAddressableArchive.DecodeAsync(
                                    //        payload.RawBlocks.Value,
                                    //        cancellationToken: cancellationToken).ConfigureAwait(false);
                                    //}

                                    eventArgs = new (header, payload);
                                    break;
                                }

                            case "#account":
                                {
                                    AccountPayload payload = AccountPayload.FromCBORObject(decodedFrame[1]);
                                    eventArgs = new(header, payload);
                                    break;
                                }

                            case "#handle":
                                {
#pragma warning disable CS0618 // Type or member is obsolete
                                    HandlePayload payload = HandlePayload.FromCBORObject(decodedFrame[1]);
                                    eventArgs = new(header, payload);
#pragma warning restore CS0618 // Type or member is obsolete
                                    break;
                                }

                            case "#identity":
                                {
                                    IdentityPayload payload = IdentityPayload.FromCBORObject(decodedFrame[1]);
                                    eventArgs = new(header, payload);
                                    break;
                                }

                            case "#info":
                                {
                                    InfoPayload payload = InfoPayload.FromCBORObject(decodedFrame[1]);
                                    eventArgs = new(header, payload);
                                    break;
                                }

                            case "#migrate":
                                {
#pragma warning disable CS0618 // Type or member is obsolete
                                    MigratePayload payload = MigratePayload.FromCBORObject(decodedFrame[1]);
                                    eventArgs = new(header, payload);
#pragma warning restore CS0618 // Type or member is obsolete
                                    break;
                                }

                            case "#repoOp":
                                {
                                    RepoOp payload = RepoOp.FromCBORObject(decodedFrame[1]);
                                    eventArgs = new(header, payload);
                                    break;
                                }

                            case "#sync":
                                {
                                    SyncPayload payload = SyncPayload.FromCBORObject(decodedFrame[1]);
                                    eventArgs = new(header, payload);
                                    break;
                                }

                            case "#tombstone":
                                {
#pragma warning disable CS0618 // Type or member is obsolete
                                    TombstonePayload payload = TombstonePayload.FromCBORObject(decodedFrame[1]);
#pragma warning restore CS0618 // Type or member is obsolete
                                    eventArgs = new(header, payload);
                                    break;
                                }

                            case null:
                                {
                                    eventArgs = null;
                                    FireHoseLogger.RepoMessageFrameHeaderTypeNull(logger);
                                    break;
                                }

                            default:
                                {
                                    UnknownPayload payload = new(frame);
                                    eventArgs = new(header, payload);
                                    FireHoseLogger.RepoMessageUnknownFrameHeaderType(logger, header.Type);
                                    break;
                                }
                        }
                        break;

                    default:
                        FireHoseLogger.RepoMessageUnknownOperation(logger);
                        eventArgs = new(header, null);
                        break;
                }

                if (eventArgs is not null)
                {
                    raiseEvent(eventArgs);
                }
            }

        }
        catch (CBORException ex)
        {
            FireHoseLogger.CborCannotDecodeMessage(logger, ex);
            throw;
        }
        catch (Exception ex)
        {
            FireHoseLogger.WebSocketParsingException(logger, ex);
            throw;
        }

        return;
    }
}
