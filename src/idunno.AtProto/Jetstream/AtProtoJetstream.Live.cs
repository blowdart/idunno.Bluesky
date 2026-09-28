// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using idunno.AtProto.Jetstream.Events;

namespace idunno.AtProto.Jetstream;

public partial class AtProtoJetstream
{
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
    /// <remarks>
    /// <para>Start enumeration with <c>await foreach</c>; disposing the enumerator closes its connection.
    /// This mode cannot be combined with event subscriptions or <see cref="ConnectAsync(Uri?, long?, HttpClient?, CancellationToken)"/>.
    /// The live service has limited cursor lookback: an expired cursor raises <see cref="JetstreamConnectionException"/>
    /// rather than silently skipping events. Use <see cref="ReplayAsync"/> for archive-backed recovery.</para>
    /// <para>The cursor is inclusive on the server; events at or below the last yielded sequence are suppressed
    /// on reconnection. Persist a cursor only after processing its event. A process restart may repeat the last event.</para>
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
                Channel<JetstreamEvent> channel = Channel.CreateBounded<JetstreamEvent>(
                    new BoundedChannelOptions(1024) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
                void Record(object? _, RecordReceivedEventArgs args)
                {
                    if (args.ParsedEvent is not JetstreamEvent { Sequence: not null } item)
                    {
                        channel.Writer.TryComplete(new InvalidDataException("The live Jetstream event has no sequence cursor."));
                    }
                    else if (!channel.Writer.TryWrite(item))
                    {
                        channel.Writer.TryComplete(new IOException("The live Jetstream consumer fell behind; reconnecting from the last yielded sequence."));
                    }
                }

                void StateChanged(object? _, ConnectionStateChangedEventArgs args)
                {
                    if (args.State is WebSocketState.Closed or WebSocketState.Aborted)
                    {
                        channel.Writer.TryComplete();
                    }
                }

                void Fault(object? _, FaultRaisedEventArgs args) =>
                    channel.Writer.TryComplete(new IOException($"The live Jetstream reported an error: {args.Fault}"));

                void Info(object? _, InfoReceivedEventArgs args)
                {
                    if (args.Name == "OutdatedCursor")
                    {
                        channel.Writer.TryComplete(new InvalidDataException("The live Jetstream cursor is outside the server's retained events."));
                    }
                }

                _recordReceived += Record;
                _connectionStateChanged += StateChanged;
                _faultRaised += Fault;
                _infoReceived += Info;
                try
                {
                    bool connected = await TryConnectLiveAsync(lastYielded == long.MinValue ? cursor : lastYielded,
                        cancellationToken).ConfigureAwait(false);
                    while (connected)
                    {
                        bool readable;
                        try
                        {
                            readable = await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch (IOException) when (!cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        if (!readable)
                        {
                            break;
                        }

                        while (channel.Reader.TryRead(out JetstreamEvent? item))
                        {
                            if (item.Sequence is long sequence && sequence > lastYielded)
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
                    try
                    {
                        if (_client.State is WebSocketState.Open or WebSocketState.CloseReceived or WebSocketState.CloseSent)
                        {
                            await CloseAsync(cancellationToken: CancellationToken.None).ConfigureAwait(false);
                        }

                        await DrainMessageParsersAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    finally
                    {
                        _recordReceived -= Record;
                        _connectionStateChanged -= StateChanged;
                        _faultRaised -= Fault;
                        _infoReceived -= Info;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (maximumReconnectAttempts is int maximum && reconnectAttempts >= maximum)
                {
                    throw new IOException($"The live Jetstream disconnected after {reconnectAttempts} reconnection attempts.");
                }

                reconnectAttempts++;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
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
