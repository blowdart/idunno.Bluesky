// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Web;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using idunno.AtProto.Jetstream.Events;
using idunno.AtProto.Jetstream.Models;

using ZstdSharp;

namespace idunno.AtProto.Jetstream
{
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

        /// <summary>
        /// Fall back meter for non-DI aware scenarios.
        /// </summary>
        private static readonly Meter s_fallbackMeter = new(JetstreamMetrics.MeterName);

        private readonly JetstreamMetrics _metrics;

        private readonly ILogger<AtProtoJetstream> _logger;

        private readonly Uri _uri = new("wss://jetstream1.us-west.bsky.network");

        private readonly Decompressor? _decompressor;

        private List<Nsid> _collections = [];

        private List<Did> _dids = [];

        private ClientWebSocket _client;

        /// <summary>
        /// Creates a new instance of <see cref="Jetstream"/>.
        /// </summary>
        /// <param name="uri">The host uri to connection to. Defaults to wss://jetstream1.us-west.bsky.network/.</param>
        /// <param name="options">Any options to configure this instance of <see cref="Jetstream"/>.</param>
        /// <param name="webSocketOptions">Any <see cref="WebSocketOptions"/> to set on the underlying client WebSocket.</param>
        /// <param name="collections">The <see cref="Nsid"/>s of any collection types to subscribe to. If null or empty all collection types will be subscribed to.</param>
        /// <param name="dids">Any <see cref="Did"/>s to subscribe to. If null or empty all dids will be subscribed to.</param>
        public AtProtoJetstream(
            Uri? uri = null,
            JetstreamOptions? options = null,
            WebSocketOptions? webSocketOptions = null,
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

            if (Options.MeterFactory is not null)
            {
                _metrics = new JetstreamMetrics(Options.MeterFactory);
            }
            else
            {
                _metrics = new JetstreamMetrics(s_fallbackMeter);
            }

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
        }

        /// <summary>
        /// Gets a configured logger factory from which to create loggers.
        /// </summary>
        protected internal ILoggerFactory LoggerFactory { get; init; }

        /// <summary>
        /// Gets the configuration options for the agent.
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
        /// Gets the underlying <see cref="ClientWebSocket"/>.
        /// </summary>
        protected ClientWebSocket ClientWebSocket => _client;

        /// <summary>
        /// Gets the <see cref="WebSocketState"/> of the underlying <see cref="ClientWebSocket"/>.
        /// </summary>
        public WebSocketState ClientState => _client.State;

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> indicating when last time a message from the JetsStream was received.
        /// </summary>
        public DateTimeOffset? MessageLastReceived { get; private set; }

        /// <summary>
        /// Raised when this instance of <see cref="Jetstream"/> receives a message.
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

        /// <summary>
        /// Raised when this instance of <see cref="Jetstream"/> receives a message.
        /// </summary>
        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

        /// <summary>
        /// Raised when this instance of <see cref="Jetstream"/> parses a message and converts it to a record.
        /// </summary>
        public event EventHandler<RecordReceivedEventArgs>? RecordReceived;

        /// <summary>
        /// Raised when this instance of <see cref="Jetstream"/> encounters a fault.
        /// </summary>
        public event EventHandler<FaultRaisedEventArgs>? FaultRaised;

        /// <summary>
        /// Creates a new <see cref="AtProtoJetstreamBuilder"/>.
        /// </summary>
        /// <returns>A new <see cref="AtProtoJetstreamBuilder"/></returns>
        public static AtProtoJetstreamBuilder CreateBuilder() => AtProtoJetstreamBuilder.Create();

        /// <summary>
        /// Called to raise any <see cref="MessageReceived"/> events, if any.
        /// </summary>
        /// <param name="e">The <see cref="MessageReceivedEventArgs"/> for the event.</param>
        protected virtual void OnMessageReceived(MessageReceivedEventArgs e)
        {
            MessageLastReceived = DateTimeOffset.UtcNow;

            EventHandler<MessageReceivedEventArgs>? messageReceived = MessageReceived;

            if (!_disposed)
            {
                _metrics.MessagesReceived(1);
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
                _metrics.EventsParsed(1);
                messageParsed?.Invoke(this, e);
            }
        }

        /// <summary>
        /// Called to raise any <see cref="ConnectionStateChanged"/> events, if any.
        /// </summary>
        /// <param name="e">The <see cref="MessageReceivedEventArgs"/> for the event.</param>
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

            if (!_disposed)
            {
                faultRaised?.Invoke(this, e);
            }
        }

        /// <summary>
        /// Connect to the JetStream instance via a WebSocket connection.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MemberNotNull(nameof(_client))]
        public async Task ConnectAsync(
            CancellationToken? cancellationToken = default)
        {
            await ConnectAsync(
                uri: null,
                cursor: null,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Connect to the JetStream instance via a WebSocket connection.
        /// </summary>
        /// <param name="uri">The URI of the jetstream server to connection to. Defaults to the URI passed during construction</param>
        /// <param name="startFrom">A Unix microseconds timestamp cursor to begin playback from. A value of null results in live-tail operation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MemberNotNull(nameof(_client))]
        public async Task ConnectAsync(
            Uri? uri = null,
            DateTimeOffset? startFrom = null,
            CancellationToken? cancellationToken = default)
        {
            long? cursor = null;

            if (startFrom is not null)
            {
                cursor = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
            }

            await ConnectAsync(uri, cursor, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Connect to the JetStream instance via a WebSocket connection.
        /// </summary>
        /// <param name="uri">The URI of the jetstream server to connection to. Defaults to the URI passed during construction</param>
        /// <param name="cursor">A Unix microseconds timestamp cursor to begin playback from. A value of null results in live-tail operation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MemberNotNull(nameof(_client))]
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Suppressing dispose exceptions on purpose.")]
        [SuppressMessage("Minor Code Smell", "S2486:Generic exceptions should not be ignored", Justification = "Suppressing dispose exceptions on purpose.")]
        public async Task ConnectAsync(
            Uri? uri = null,
            long? cursor = null,
            CancellationToken? cancellationToken = default)
        {
            CancellationToken ctx = cancellationToken ?? CancellationToken.None;

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
                $"maximumMessageSizeBytes={Options.MaximumMessageSize}&");

            if (uriBuilder[^1] == '&')
            {
                uriBuilder.Length--;
            }

            if (uriBuilder[^1] == '?')
            {
                uriBuilder.Length--;
            }

            Uri serverUri = new(uriBuilder.ToString());

            JetStreamLogger.ConnectingTo(_logger, serverUri);

            await _client.ConnectAsync(serverUri, ctx).ConfigureAwait(false);

            OnConnectionStateChanged(new ConnectionStateChangedEventArgs(_client.State));

            ReceiveLoop(ctx).FireAndForget();
        }

        /// <summary>
        /// Closes the existing WebSocket connection.
        /// </summary>
        /// <param name="status">Status for the shutdown. Defaults to <see cref="WebSocketCloseStatus.NormalClosure"/>.</param>
        /// <param name="statusDescription">Reason for the shutdown.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catch all exceptions to avoid cancellation exceptions")]
        public async Task CloseAsync(
            WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure,
            string statusDescription = "Client disconnect",
            CancellationToken? cancellationToken = default)
        {
            CancellationToken ctx = cancellationToken ?? CancellationToken.None;

            if (_client.State == WebSocketState.Open)
            {
                try
                {
                    await _client.CloseAsync(status, statusDescription, ctx).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    JetStreamLogger.CloseError(_logger, ex);
                }
            }

            OnConnectionStateChanged(new ConnectionStateChangedEventArgs(_client.State));
        }

        /// <summary>
        /// Log a fault.
        /// </summary>
        /// <param name="fault">A description of the fault.</param>
        protected void LogFault(string fault="Unspecified fault")
        {
            OnFaultRaised(new FaultRaisedEventArgs(fault));
        }

        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _client.Dispose();
                    _decompressor?.Dispose();
                }

                _disposed = true;
            }
        }

        /// <inheritdoc/>
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

        [SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler", Justification = "A scheduler can be configured on the TaskFactory in Options.")]
        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[Options.MaximumMessageSize];

            while (!cancellationToken.IsCancellationRequested)
            {
                WebSocketMessageType expectedMessageType = Options.UseCompression ? WebSocketMessageType.Binary : WebSocketMessageType.Text;

                try
                {
                    if (_client.State != WebSocketState.Open)
                    {
                        await ConnectAsync(cancellationToken).ConfigureAwait(false);
                    }

                    ValueWebSocketReceiveResult result = await _client.ReceiveAsync(new Memory<byte>(buffer), cancellationToken).ConfigureAwait(false);

                    if (result.MessageType != expectedMessageType && !result.EndOfMessage)
                    {
                        continue;
                    }

                    byte[] receivedData;

                    if (Options.UseCompression)
                    {
                        Span<byte> bufferAsSpan = buffer.AsSpan(0, result.Count);
                        receivedData = _decompressor!.Unwrap(bufferAsSpan).ToArray();
                    }
                    else
                    {
                        receivedData = new byte[result.Count];
                        Array.Copy(buffer, 0, receivedData, 0, result.Count);
                    }

                    // Now convert to a string
                    string message = Encoding.UTF8.GetString(receivedData);

                    if (!string.IsNullOrEmpty(message))
                    {

                        OnMessageReceived(new MessageReceivedEventArgs(message));
                        _metrics.MessagesReceived(1);

                        // Now go to handle message in a new task.
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        Options.TaskFactory.StartNew(() => ParseMessage(message, _logger).FireAndForgetAsync(_logger), cancellationToken).ConfigureAwait(false);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                    else
                    {
                        if (_client.State == WebSocketState.Open)
                        {
                            LogFault("Message conversion to string failed.");
                            JetStreamLogger.MessageLoopFailedToConvert(_logger);
                        }
                    }
                }
                catch (WebSocketException ex)
                {
                    // Close the client and reopen.
                    LogFault(ex.Message);
                    _client.Dispose();
                    await ConnectAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    JetStreamLogger.MessageLoopCancellation(_logger);
                }
                catch (ObjectDisposedException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogFault(ex.Message);
                    JetStreamLogger.MessageLoopError(_logger, ex);
                    throw;
                }
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
                    }
                }
                else
                {
                    JetStreamLogger.ParseMessageDeserializationReturnedNull(logger, json);
                }

                return Task.CompletedTask;
            }
            catch (JsonException ex)
            {
                JetStreamLogger.ParseMessageCouldNotProcessAsJson(logger, json, ex);
                return Task.FromException(ex);
            }
            catch (ObjectDisposedException)
            {
                throw;
            }
            catch (Exception ex)
            {
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
                MaxMessageSizeBytes = Options.MaximumMessageSize
            };

            if (_collections is not null && _collections.Count > 0)
            {
                payload.WantedCollections = [.. _collections];
            }

            if (_dids is not null && _dids.Count > 0)
            {
                payload.WantedDIDs = [.. _dids];
            }

            OptionsUpdateMessage optionsUpdateMessage = new ()
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

                    _metrics.EventsParsed(1);
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

                    _metrics.EventsParsed(1);
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

                    _metrics.EventsParsed(1);
                    break;

                default:
                    break;
            }

            return derivedEvent;
        }
    }
}
