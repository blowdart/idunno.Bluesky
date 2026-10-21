// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;

using idunno.AtProto.Jetstream.Events;

namespace idunno.AtProto.Jetstream;

public partial class AtProtoJetstream
{
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Owned and disposed by the iterator after draining its receive loop. Jetstream disposal stops the queue before draining.")]
    private LiveEventQueue? _liveQueue;

    internal Action? StreamCapacityWaiting { get; set; }

    /// <summary>
    /// Streams decoded events from the live version 2 Jetstream, reconnecting from the last yielded sequence.
    /// </summary>
    /// <param name="cursor">An optional inclusive sequence cursor; <see langword="null"/> starts at the live tip.</param>
    /// <param name="maximumReconnectAttempts">The maximum consecutive reconnection attempts, or <see langword="null"/> for unlimited retries. A delivered event resets the count.</param>
    /// <param name="cancellationToken">A token which stops the connection and enumeration.</param>
    /// <returns>A single, ordered sequence of live events.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The maximum reconnection count is negative.</exception>
    /// <exception cref="NotSupportedException">The Jetstream uses version 1.</exception>
    /// <exception cref="InvalidOperationException">An event handler, connection, or another enumerator is active.</exception>
    /// <exception cref="ObjectDisposedException">The Jetstream has been disposed.</exception>
    /// <exception cref="JetstreamConnectionException">The server rejects an expired cursor during the connection handshake.</exception>
    /// <exception cref="InvalidDataException">The server reports an <c>OutdatedCursor</c> notice after the connection is established, or an event has no sequence cursor.</exception>
    /// <exception cref="IOException">The stream disconnects with SDK-managed reconnection disabled or its attempt limit exhausted. The inner exception contains the current attempt's channel error, if captured.</exception>
    /// <remarks>
    /// <para>Start enumeration with <c>await foreach</c>; disposing the enumerator closes its connection.
    /// This mode cannot be combined with event subscriptions or <see cref="ConnectAsync(Uri?, long?, HttpClient?, CancellationToken)"/>.
    /// The live service has limited cursor lookback: an expired cursor raises <see cref="JetstreamConnectionException"/>
    /// during the handshake or <see cref="InvalidDataException"/> if reported by an <c>OutdatedCursor</c> notice
    /// after connection, rather than silently skipping events. Use <see cref="ReplayAsync"/> for archive-backed recovery.</para>
    /// <para>The cursor is inclusive on the server; events at or below the last yielded sequence are suppressed
    /// on reconnection. Persist a cursor only after processing its event. A process restart may repeat the last event.</para>
    /// <para>Buffers at most <see cref="JetstreamOptions.StreamBufferCapacity"/> decoded events, excluding the event
    /// already yielded. A full queue asynchronously pauses receiving rather than triggering reconnection.
    /// Backpressure does not guarantee that a slow consumer can keep up with server retention or avoid server timeouts.</para>
    /// </remarks>
    public IAsyncEnumerable<JetstreamEvent> StreamAsync(
        long? cursor = null,
        int? maximumReconnectAttempts = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumReconnectAttempts ?? 0);
        return StreamCoreAsync(cursor, maximumReconnectAttempts, cancellationToken);
    }

    private async IAsyncEnumerable<JetstreamEvent> StreamCoreAsync(
        long? cursor,
        int? maximumReconnectAttempts,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (Options.ProtocolVersion != JetstreamProtocolVersion.V2)
        {
            throw new NotSupportedException("Live async enumeration requires a version 2 Jetstream.");
        }

        lock (_syncLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_consumptionMode != ConsumptionMode.None || _client.State == WebSocketState.Open ||
                _messageReceived is not null || _connectionStateChanged is not null ||
                _recordReceived is not null || _faultRaised is not null || _infoReceived is not null)
            {
                throw new InvalidOperationException("Live async enumeration requires an unconnected Jetstream without event handlers or another enumerator.");
            }

            _consumptionMode = ConsumptionMode.AsyncEnumeration;
        }

        long lastYielded = cursor is >= 0 and < TimestampCursorThreshold ? cursor.Value : long.MinValue;
        int reconnectAttempts = 0;
        try
        {
            await DrainMessageParsersAsync(cancellationToken).ConfigureAwait(false);

            while (true)
            {
                IOException? interruptionCause = null;
                using LiveEventQueue queue = new(Options.StreamBufferCapacity, _metrics, Options.TimeProvider,
                    new KeyValuePair<string, object?>("server", ArchiveServerTag(_uri)), cancellationToken)
                {
                    CapacityWaiting = StreamCapacityWaiting
                };
                lock (_syncLock)
                {
                    ObjectDisposedException.ThrowIf(_disposed || _disposing, this);
                    _liveQueue = queue;
                }

                void StateChanged(object? _, ConnectionStateChangedEventArgs args)
                {
                    if (args.State is WebSocketState.Closed or WebSocketState.Aborted)
                    {
                        queue.Complete();
                    }
                }

                void Fault(object? _, FaultRaisedEventArgs args) =>
                    queue.Complete(new IOException($"The live Jetstream reported an error: {args.Fault}"));

                void Info(object? _, InfoReceivedEventArgs args)
                {
                    if (args.Name == "OutdatedCursor")
                    {
                        queue.Complete(new InvalidDataException("The live Jetstream cursor is outside the server's retained events."));
                    }
                }

                _connectionStateChanged += StateChanged;
                _faultRaised += Fault;
                _infoReceived += Info;
                try
                {
                    bool connected = await TryConnectLiveAsync(lastYielded == long.MinValue ? cursor : lastYielded,
                        queue.CancellationToken).ConfigureAwait(false);
                    while (connected)
                    {
                        bool readable;
                        try
                        {
                            readable = await queue.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch (IOException ex) when (!cancellationToken.IsCancellationRequested)
                        {
                            interruptionCause = ex;
                            break;
                        }

                        if (!readable)
                        {
                            break;
                        }

                        while (queue.TryRead(out JetstreamEvent? item))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (item?.Sequence is long sequence && sequence > lastYielded)
                            {
                                lastYielded = sequence;
                                reconnectAttempts = 0;
                                yield return item;
                            }
                        }
                    }
                }
                finally
                {
                    queue.Stop();
                    try
                    {
                        if (_client.State is WebSocketState.Open or WebSocketState.CloseReceived or WebSocketState.CloseSent)
                        {
                            await CloseAsync(cancellationToken: CancellationToken.None).ConfigureAwait(false);
                        }

                        Task receiveLoops;
                        lock (_syncLock)
                        {
                            receiveLoops = Task.WhenAll(_receiveLoopTasks);
                        }

                        await receiveLoops.ConfigureAwait(false);
                        await DrainMessageParsersAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    finally
                    {
                        lock (_syncLock)
                        {
                            _liveQueue = null;
                        }
                        _connectionStateChanged -= StateChanged;
                        _faultRaised -= Fault;
                        _infoReceived -= Info;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (maximumReconnectAttempts is int maximum && reconnectAttempts >= maximum)
                {
                    string message = maximum == 0
                        ? "The live Jetstream disconnected. SDK-managed automatic reconnection is disabled."
                        : $"The live Jetstream disconnected after the SDK exhausted its {reconnectAttempts} reconnection attempts.";
                    throw new IOException(message, interruptionCause);
                }

                reconnectAttempts++;
                await Task.Delay(TimeSpan.FromSeconds(1), Options.TimeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            lock (_syncLock)
            {
                _consumptionMode = ConsumptionMode.None;
            }
        }
    }

    private async Task<bool> TryConnectLiveAsync(long? cursor, CancellationToken cancellationToken)
    {
        try
        {
            await ConnectCoreAsync(null, cursor, null, reconnectForUpdatedFilters: false, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (JetstreamConnectionException ex) when (!cancellationToken.IsCancellationRequested &&
            ex.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
                HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
        {
            return false;
        }
        catch (IOException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (WebSocketException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException ex) when (!cancellationToken.IsCancellationRequested &&
            (ex.StatusCode is null or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
                HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout))
        {
            return false;
        }
    }
}
