// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto.Firehose;

/// <summary>
/// Reads an AT Protocol event stream, handling framing, sequencing, error frames and reconnection, and hands each
/// message payload to an <see cref="IFirehosePayloadDecoder"/>.
/// </summary>
/// <remarks>
/// <para>See https://atproto.com/specs/event-stream. Unknown operations and message types are skipped or surfaced, as the
/// specification requires. Invalid framing, invalid DAG-CBOR and sequence violations are hard errors which drop the connection
/// and end the enumeration, as silently resynchronizing after them could skip or replay events.</para>
/// </remarks>
internal sealed class EventStreamReader
{
    /// <summary>
    /// The largest sequence number allowed, 2^53 - 1, the largest integer a JavaScript number holds safely.
    /// </summary>
    internal const long MaximumSequence = 9_007_199_254_740_991;

    /// <summary>
    /// The longest error or message accepted from a server before it is truncated.
    /// </summary>
    internal const int MaximumServerTextLength = 1024;

    private const string InfoType = "#info";

    private const string ConsumerTooSlow = "ConsumerTooSlow";

    // The payload fields read here, for errors, info messages and sequence numbers, rather than by the decoder.
    private static readonly CborFieldNames s_readerPayloadFields = new("error", "message", "name", "seq");

    private readonly Uri _host;
    private readonly FirehoseOptions _options;
    private readonly WebSocketOptions? _webSocketOptions;
    private readonly HttpMessageInvoker _invoker;
    private readonly IFirehosePayloadDecoder _decoder;
    private readonly ILogger _logger;
    private readonly FirehoseMetrics _metrics;
    private readonly KeyValuePair<string, object?> _serverTag;
    private readonly CborFieldNames _payloadFields;

    // Zero is a valid starting cursor, so a negative value means there is no sequence to resume from.
    private const long NoSequence = -1;

    private long _lastSequence = NoSequence;
    private int _active;

    /// <summary>
    /// Creates a new instance of <see cref="EventStreamReader"/>.
    /// </summary>
    /// <param name="host">The host to connect to.</param>
    /// <param name="options">The firehose options.</param>
    /// <param name="webSocketOptions">Any web socket options.</param>
    /// <param name="invoker">The HTTP invoker to connect the web socket through, which rejects a redirected upgrade.</param>
    /// <param name="decoder">The payload decoder for the endpoint.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="metrics">The metrics to record.</param>
    internal EventStreamReader(
        Uri host,
        FirehoseOptions options,
        WebSocketOptions? webSocketOptions,
        HttpMessageInvoker invoker,
        IFirehosePayloadDecoder decoder,
        ILogger logger,
        FirehoseMetrics metrics)
    {
        _host = host;
        _options = options;
        _webSocketOptions = webSocketOptions;
        _invoker = invoker;
        _decoder = decoder;
        _logger = logger;
        _metrics = metrics;
        _serverTag = new KeyValuePair<string, object?>("server", host.GetLeftPart(UriPartial.Authority));
        _payloadFields = s_readerPayloadFields.Union(decoder.PayloadFields);
    }

    /// <summary>
    /// Gets or sets the delay before the first reconnection attempt, which doubles on each consecutive attempt.
    /// </summary>
    internal TimeSpan InitialReconnectDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the longest delay between reconnection attempts.
    /// </summary>
    internal TimeSpan MaximumReconnectDelay { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the longest <c>Retry-After</c> a server can ask for before it is capped.
    /// </summary>
    internal TimeSpan MaximumRetryAfter { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets the sequence number of the last sequenced event yielded, if any.
    /// </summary>
    internal long? LastSequence
    {
        get
        {
            long value = Interlocked.Read(ref _lastSequence);
            return value == NoSequence ? null : value;
        }
    }

    /// <summary>
    /// Builds the web socket uri for the endpoint.
    /// </summary>
    /// <param name="host">The configured host.</param>
    /// <param name="nsid">The NSID of the endpoint.</param>
    /// <param name="cursor">The cursor, if any.</param>
    /// <returns>The subscription uri.</returns>
    internal static Uri BuildSubscriptionUri(Uri host, string nsid, long? cursor)
    {
        UriBuilder builder = new(host)
        {
            Scheme = host.Scheme.ToUpperInvariant() switch
            {
                "HTTP" or "WS" => Uri.UriSchemeWs,
                _ => Uri.UriSchemeWss
            },
            Path = "/xrpc/" + nsid,
            Query = cursor is null ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"cursor={cursor.Value}"),
            Fragment = string.Empty
        };

        return builder.Uri;
    }

    /// <summary>
    /// Strips control and bidirectional formatting characters from, and truncates, text supplied by a server.
    /// </summary>
    /// <param name="value">The server supplied text.</param>
    /// <returns>The sanitized text, or <see langword="null"/> if <paramref name="value"/> is <see langword="null"/>.</returns>
    /// <remarks>
    /// <para>This is light sanitization, to keep server text from corrupting logs or disguising itself when displayed. The result is
    /// still untrusted: it is not encoded for HTML, SQL, shells or any other context, and can contain any other characters, including
    /// ones which look like others.</para>
    /// </remarks>
    [return: NotNullIfNotNull(nameof(value))]
    internal static string? Sanitize(string? value)
    {
        if (value is null)
        {
            return null;
        }

        StringBuilder builder = new(Math.Min(value.Length, MaximumServerTextLength));

        foreach (char c in value)
        {
            if (builder.Length == MaximumServerTextLength)
            {
                break;
            }

            if (!char.IsControl(c) && !IsBidirectionalFormattingCharacter(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    // The explicit directional marks, embeddings, overrides and isolates, which can make text display in a different order to how it is stored.
    private static bool IsBidirectionalFormattingCharacter(char c) => c is '\u061C' or '\u200E' or '\u200F' or (>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069');

    /// <summary>
    /// Streams events from the endpoint, reconnecting when the connection is lost.
    /// </summary>
    /// <param name="cursor">The cursor to start from, if any.</param>
    /// <param name="maximumReconnectAttempts">The maximum consecutive reconnection attempts, or <see langword="null"/> for unlimited.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The events.</returns>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Each socket is disposed by CloseAsync in the finally block.")]
    internal async IAsyncEnumerable<FirehoseEvent> ReadAsync(
        long? cursor,
        int? maximumReconnectAttempts,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
        {
            throw new InvalidOperationException($"Only one enumeration of {_decoder.Nsid} can be active at once.");
        }

        try
        {
            SequenceState state = new(cursor is > 0 ? cursor.Value : 0);
            Interlocked.Exchange(ref _lastSequence, cursor ?? NoSequence);

            int attempts = 0;

            while (true)
            {
                long? connectCursor = state.Last > 0 ? state.Last : cursor;
                state.BeginConnection(connectCursor);

                ClientWebSocket socket = CreateWebSocket();
                ConnectionFailure? failure;

                try
                {
                    failure = await ConnectAsync(socket, connectCursor, cancellationToken).ConfigureAwait(false);

                    if (failure is null)
                    {
                        using CancellationTokenSource idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                        while (true)
                        {
                            ReceiveStep step = await ReceiveAsync(socket, state, idle, cancellationToken).ConfigureAwait(false);

                            if (step.Event is not null)
                            {
                                if (step.Event.Sequence is long sequence)
                                {
                                    Interlocked.Exchange(ref _lastSequence, sequence);

                                    // Only a sequenced event shows the stream is making progress. A server which sends an info or
                                    // unknown frame and then drops the connection would otherwise keep the enumeration reconnecting forever.
                                    attempts = 0;
                                }

                                yield return step.Event;
                            }
                            else if (step.Failure is not null)
                            {
                                failure = step.Failure;
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    await CloseAsync(socket).ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (maximumReconnectAttempts is int maximum && attempts >= maximum)
                {
                    throw new IOException(
                        string.Create(CultureInfo.InvariantCulture, $"The {_decoder.Nsid} connection failed after {attempts} reconnection attempts."),
                        failure.Exception);
                }

                TimeSpan delay = GetReconnectDelay(attempts, failure.RetryAfter);
                attempts++;

                _metrics.Reconnections.Add(1, _serverTag);
                FirehoseLogger.Reconnecting(_logger, attempts, delay);

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _active, 0);
        }
    }

    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Jitter for reconnection backoff is not security sensitive.")]
    private TimeSpan GetReconnectDelay(int attempts, TimeSpan? retryAfter)
    {
        double exponential = InitialReconnectDelay.TotalMilliseconds * Math.Pow(2, Math.Min(attempts, 30));
        double capped = Math.Min(exponential, MaximumReconnectDelay.TotalMilliseconds);

        // Full jitter over the upper half, so a fleet of clients dropped together does not reconnect together.
        TimeSpan delay = TimeSpan.FromMilliseconds(capped * (0.5 + (Random.Shared.NextDouble() / 2)));

        if (retryAfter is TimeSpan requested)
        {
            TimeSpan bounded = requested > MaximumRetryAfter ? MaximumRetryAfter : requested;

            if (bounded > delay)
            {
                delay = bounded;
            }
        }

        return delay;
    }

    private ClientWebSocket CreateWebSocket()
    {
        ClientWebSocket socket = new();

        // Kept so a server which refuses the upgrade can be told apart from one which could not be reached,
        // without making a second request to ask it why.
        socket.Options.CollectHttpResponseDetails = true;

        if (_webSocketOptions?.Proxy is not null)
        {
            socket.Options.Proxy = _webSocketOptions.Proxy;
        }

        socket.Options.KeepAliveInterval = _webSocketOptions?.KeepAliveInterval ?? TimeSpan.FromSeconds(30);

#if NET9_0_OR_GREATER
        socket.Options.KeepAliveTimeout = _webSocketOptions?.KeepAliveTimeout ?? TimeSpan.FromSeconds(30);
#endif

        return socket;
    }

    private async Task<ConnectionFailure?> ConnectAsync(ClientWebSocket socket, long? cursor, CancellationToken cancellationToken)
    {
        Uri uri = BuildSubscriptionUri(_host, _decoder.Nsid, cursor);

        if (uri.Scheme == Uri.UriSchemeWs)
        {
            FirehoseLogger.InsecureConnection(_logger, uri);
        }

        FirehoseLogger.ConnectingTo(_logger, uri);

        try
        {
            await socket.ConnectAsync(uri, _invoker, cancellationToken).ConfigureAwait(false);
        }
        catch (WebSocketException exception) when (!cancellationToken.IsCancellationRequested &&
            exception.InnerException is FirehoseConnectionException { StatusCode: null } redirected)
        {
            // The invoker followed a redirect before the reader could refuse it, so there is no redirect status to report.
            _metrics.ConnectionFailures.Add(1, _serverTag);
            FirehoseLogger.ConnectionRedirected(_logger, null);
            throw new FirehoseConnectionException(null, redirected.Message, exception);
        }
        catch (WebSocketException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _metrics.ConnectionFailures.Add(1, _serverTag);

            HttpStatusCode statusCode = socket.HttpStatusCode;

            if (statusCode == 0 || statusCode == HttpStatusCode.SwitchingProtocols)
            {
                FirehoseLogger.ConnectionFailed(_logger, exception);
                return new ConnectionFailure(exception, null);
            }

            if ((int)statusCode is >= 300 and <= 399)
            {
                // Following a redirect would connect to a host the caller did not configure, and a redirect is as likely
                // to be answered again on a reconnection, so it ends the enumeration rather than being retried.
                FirehoseLogger.ConnectionRedirected(_logger, (int)statusCode);
                throw new FirehoseConnectionException(statusCode, RedirectRejectingHandler.RedirectedMessage, exception);
            }

            FirehoseLogger.ConnectionRefused(_logger, (int)statusCode);

            TimeSpan? retryAfter = GetRetryAfter(socket.HttpResponseHeaders);
            FirehoseConnectionException refused = new(statusCode, null, retryAfter, exception);

            if (statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError or
                HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
            {
                return new ConnectionFailure(refused, retryAfter);
            }

            throw refused;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
            exception is HttpRequestException or IOException or OperationCanceledException)
        {
            _metrics.ConnectionFailures.Add(1, _serverTag);
            FirehoseLogger.ConnectionFailed(_logger, exception);
            return new ConnectionFailure(exception, null);
        }

        _metrics.ConnectionsOpened.Add(1, _serverTag);
        FirehoseLogger.Connected(_logger, uri);

        return null;
    }

    private static TimeSpan? GetRetryAfter(IReadOnlyDictionary<string, IEnumerable<string>>? headers)
    {
        if (headers is null)
        {
            return null;
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
        {
            if (!string.Equals(header.Key, "Retry-After", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? value = header.Value.FirstOrDefault()?.Trim();

            if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds))
            {
                return seconds > int.MaxValue ? TimeSpan.FromSeconds(int.MaxValue) : TimeSpan.FromSeconds(seconds);
            }

            if (DateTimeOffset.TryParseExact(value, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset date))
            {
                TimeSpan wait = date - DateTimeOffset.UtcNow;
                return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
            }

            return null;
        }

        return null;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A close failure is logged and the socket aborted.")]
    private async Task CloseAsync(ClientWebSocket socket)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                // CloseAsync, unlike CloseOutputAsync, waits for the server's answer, so the timeout bounds the whole handshake.
                using CancellationTokenSource timeout = new(_options.CloseTimeout);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            FirehoseLogger.CloseError(_logger, exception);
            socket.Abort();
        }
        finally
        {
            if (socket.State != WebSocketState.None)
            {
                _metrics.ConnectionsClosed.Add(1, _serverTag);
            }

            socket.Dispose();
        }
    }

    private async Task<ReceiveStep> ReceiveAsync(
        ClientWebSocket socket,
        SequenceState state,
        CancellationTokenSource idle,
        CancellationToken cancellationToken)
    {
        WebSocketReceiveResult result;
        byte[] message;

        idle.CancelAfter(_options.IdleTimeout);

        try
        {
            (result, message) = await socket.ReceiveNextMessageAsync(
                _options.BufferSize,
                _options.MaxMessageSize,
                _logger,
                idle.Token).ConfigureAwait(false);
        }
        catch (WebSocketMessageAbandonedException exception)
        {
            // The rest of the oversized frame is still queued on the socket, and a reconnection would be sent the same
            // frame again, so the enumeration ends rather than loops.
            throw ProtocolError(new InvalidDataException("The firehose sent a frame larger than the maximum message size.", exception));
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested && idle.IsCancellationRequested &&
            exception is OperationCanceledException or WebSocketException)
        {
            FirehoseLogger.IdleTimeout(_logger, _options.IdleTimeout);
            return ReceiveStep.Disconnected(new TimeoutException("No frame was received from the firehose within the idle timeout.", exception));
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
            exception is WebSocketException or IOException or HttpRequestException)
        {
            FirehoseLogger.ConnectionFailed(_logger, exception);
            return ReceiveStep.Disconnected(exception);
        }

        if (result.MessageType == WebSocketMessageType.Close)
        {
            string? description = Sanitize(result.CloseStatusDescription);
            FirehoseLogger.ServerClosed(_logger, result.CloseStatus, description);
            return ReceiveStep.Disconnected(new WebSocketException(WebSocketError.ConnectionClosedPrematurely, "The firehose server closed the connection."));
        }

        _metrics.MessagesReceived.Add(1, _serverTag);

        if (result.MessageType != WebSocketMessageType.Binary)
        {
            throw ProtocolError(new InvalidDataException("The firehose sent a text frame, event stream frames must be binary."));
        }

        FirehoseFrame frame;

        try
        {
            frame = FirehoseFrame.Parse(message);
        }
        catch (InvalidDataException exception)
        {
            throw ProtocolError(exception);
        }

        return frame.Operation switch
        {
            FirehoseFrame.ErrorOperation => HandleError(ReadPayload(frame)),
            FirehoseFrame.MessageOperation => await HandleMessageAsync(frame, ReadPayload(frame), state, cancellationToken).ConfigureAwait(false),
            _ => SkipUnknownOperation(frame.Operation)
        };
    }

    private CborFields ReadPayload(FirehoseFrame frame)
    {
        try
        {
            return FirehoseCbor.ReadFields(frame.Payload, _payloadFields);
        }
        catch (InvalidDataException exception)
        {
            throw ProtocolError(exception);
        }
    }

    private ReceiveStep SkipUnknownOperation(long operation)
    {
        _metrics.UnknownEventsReceived.Add(1, _serverTag);
        FirehoseLogger.UnknownOperation(_logger, operation);
        return ReceiveStep.Skip;
    }

    private ReceiveStep HandleError(CborFields payload)
    {
        string error;
        string? message;

        try
        {
            error = payload.GetString("error");
            message = payload.GetOptionalString("message");
        }
        catch (InvalidDataException exception)
        {
            throw ProtocolError(exception);
        }

        AtErrorDetail detail = new(Sanitize(error), Sanitize(message));
        FirehoseLogger.ErrorFrameReceived(_logger, detail.Error, detail.Message);

        FirehoseConnectionException serverError = new(null, detail, null, null);

        if (string.Equals(error, ConsumerTooSlow, StringComparison.Ordinal))
        {
            return ReceiveStep.Disconnected(serverError);
        }

        throw serverError;
    }

    private async Task<ReceiveStep> HandleMessageAsync(FirehoseFrame frame, CborFields payload, SequenceState state, CancellationToken cancellationToken)
    {
        string type = frame.Type!;
        string prefix = _decoder.Nsid + "#";

        if (type.StartsWith(prefix, StringComparison.Ordinal))
        {
            type = type[_decoder.Nsid.Length..];
        }

        if (type == InfoType)
        {
            return ReceiveStep.Deliver(Parsed(DecodeInfo(payload, frame.Payload)));
        }

        if (!_decoder.IsSequenced(type))
        {
            string sanitizedType = Sanitize(type);
            _metrics.UnknownEventsReceived.Add(1, _serverTag);
            FirehoseLogger.UnknownMessageType(_logger, sanitizedType);
            return ReceiveStep.Deliver(new FirehoseUnknownEvent(sanitizedType, frame.Payload));
        }

        long sequence;

        try
        {
            sequence = payload.GetInteger("seq");
        }
        catch (InvalidDataException exception)
        {
            throw ProtocolError(exception);
        }

        if (sequence < 1 || sequence > MaximumSequence)
        {
            throw ProtocolError(new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"The firehose sent sequence number {sequence}, which is outside the allowed range.")));
        }

        switch (state.Accept(sequence, type, frame.Payload))
        {
            case SequenceDecision.DropCursorEvent:
                FirehoseLogger.CursorEventDropped(_logger, sequence);
                return ReceiveStep.Skip;

            case SequenceDecision.DropRepeatedEvent:
                FirehoseLogger.RepeatedEventDropped(_logger, sequence);
                return ReceiveStep.Skip;

            case SequenceDecision.Violation:
                throw ProtocolError(new InvalidDataException(
                    string.Create(CultureInfo.InvariantCulture, $"The firehose sent sequence number {sequence} after {state.Last}; sequence numbers must increase.")));
        }

        try
        {
            return ReceiveStep.Deliver(Parsed(await _decoder.DecodeAsync(type, sequence, payload, cancellationToken).ConfigureAwait(false)));
        }
        catch (InvalidDataException exception)
        {
            // Decoders can include server-controlled text in their messages.
            string reason = Sanitize(exception.Message);
            _metrics.InvalidEventsReceived.Add(1, _serverTag);
            FirehoseLogger.InvalidEvent(_logger, type, sequence, reason);
            return ReceiveStep.Deliver(new FirehoseInvalidEvent(sequence, type, _decoder.GetSubject(type, payload), reason, frame.Payload));
        }
    }

    private FirehoseEvent DecodeInfo(CborFields payload, ReadOnlyMemory<byte> encoded)
    {
        try
        {
            return new FirehoseInfoEvent(Sanitize(payload.GetString("name")), Sanitize(payload.GetOptionalString("message")));
        }
        catch (InvalidDataException exception)
        {
            string reason = Sanitize(exception.Message);
            _metrics.InvalidEventsReceived.Add(1, _serverTag);
            FirehoseLogger.InvalidEvent(_logger, InfoType, null, reason);
            return new FirehoseInvalidEvent(null, InfoType, null, reason, encoded);
        }
    }

    private FirehoseEvent Parsed(FirehoseEvent parsedEvent)
    {
        if (parsedEvent is not FirehoseInvalidEvent)
        {
            _metrics.EventsParsed.Add(1, _serverTag);
        }

        return parsedEvent;
    }

    private InvalidDataException ProtocolError(InvalidDataException exception)
    {
        _metrics.ProtocolErrors.Add(1, _serverTag);
        FirehoseLogger.ProtocolError(_logger, exception);
        return exception;
    }

    /// <summary>
    /// What to do with a sequenced frame.
    /// </summary>
    internal enum SequenceDecision
    {
        /// <summary>
        /// Deliver the event.
        /// </summary>
        Accept,

        /// <summary>
        /// Drop the event, it is the cursor event an inclusive server sends again on resumption.
        /// </summary>
        DropCursorEvent,

        /// <summary>
        /// Drop the event, it is a byte for byte repeat of the last event accepted.
        /// </summary>
        DropRepeatedEvent,

        /// <summary>
        /// The sequence number is a duplicate or out of order.
        /// </summary>
        Violation
    }

    /// <summary>
    /// Tracks the sequence numbers seen across connections.
    /// </summary>
    /// <param name="last">The sequence number of the last event accepted, or the starting cursor.</param>
    internal sealed class SequenceState(long last)
    {
        private long? _connectionCursor;

        // The type and payload of the last event accepted, or of the cursor event dropped since, so a repeat of it can be recognised.
        // It holds at most one message, which is bounded by the maximum message size.
        private string? _lastType;
        private ReadOnlyMemory<byte> _lastPayload;

        /// <summary>
        /// Gets the sequence number of the last event accepted, or the starting cursor if none has been.
        /// </summary>
        public long Last { get; private set; } = last;

        /// <summary>
        /// Records the cursor a new connection was opened with.
        /// </summary>
        /// <param name="cursor">The cursor, if any.</param>
        /// <remarks>
        /// <para>Relays treat a cursor as inclusive, and send the event it names again, whilst labelers treat it as exclusive.
        /// So the first sequenced frame of a resumed connection may repeat the cursor, and only that one is dropped.</para>
        /// </remarks>
        public void BeginConnection(long? cursor) => _connectionCursor = cursor is > 0 ? cursor : null;

        /// <summary>
        /// Decides what to do with a frame with <paramref name="sequence"/>.
        /// </summary>
        /// <param name="sequence">The sequence number of the frame.</param>
        /// <param name="type">The message type of the frame, without the endpoint NSID.</param>
        /// <param name="payload">The DAG-CBOR encoded payload of the frame.</param>
        /// <returns>What to do with the frame.</returns>
        /// <remarks>
        /// <para>A relay resuming from a cursor replays the events it holds and then switches to live events, and at that switch it
        /// sends the last replayed event a second time, unchanged. A frame which repeats the last event's sequence number, with the same
        /// message type and an identical payload, is dropped, once. Any other repeated or earlier sequence number, including a second repeat,
        /// is a violation.</para>
        /// </remarks>
        public SequenceDecision Accept(long sequence, string type, ReadOnlyMemory<byte> payload)
        {
            long? cursor = _connectionCursor;
            _connectionCursor = null;

            if (cursor == sequence && sequence == Last)
            {
                _lastType = type;
                _lastPayload = payload;
                return SequenceDecision.DropCursorEvent;
            }

            if (sequence == Last && _lastType is not null && string.Equals(type, _lastType, StringComparison.Ordinal) && payload.Span.SequenceEqual(_lastPayload.Span))
            {
                // Only one repeat is allowed, so a server cannot stall the stream by sending the same frame forever.
                _lastType = null;
                _lastPayload = default;
                return SequenceDecision.DropRepeatedEvent;
            }

            if (sequence <= Last)
            {
                return SequenceDecision.Violation;
            }

            Last = sequence;
            _lastType = type;
            _lastPayload = payload;
            return SequenceDecision.Accept;
        }
    }

    private sealed record ConnectionFailure(Exception Exception, TimeSpan? RetryAfter);

    private sealed record ReceiveStep(FirehoseEvent? Event, ConnectionFailure? Failure)
    {
        public static readonly ReceiveStep Skip = new(null, null);

        public static ReceiveStep Deliver(FirehoseEvent firehoseEvent) => new(firehoseEvent, null);

        public static ReceiveStep Disconnected(Exception exception) => new(null, new ConnectionFailure(exception, null));
    }
}
