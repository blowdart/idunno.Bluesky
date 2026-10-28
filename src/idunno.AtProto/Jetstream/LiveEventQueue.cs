// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Channels;

namespace idunno.AtProto.Jetstream;

internal sealed class LiveEventQueue : IDisposable
{
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif
    private readonly Channel<JetstreamEvent> _channel;
    private readonly CancellationTokenSource _cancellation;
    private readonly JetstreamMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly KeyValuePair<string, object?> _serverTag;
    private readonly int _capacity;
    private bool _stopped;

    internal LiveEventQueue(int capacity, JetstreamMetrics metrics, TimeProvider timeProvider,
        KeyValuePair<string, object?> serverTag, CancellationToken cancellationToken)
    {
        _capacity = capacity;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _serverTag = serverTag;
        EnumerationCancellationToken = cancellationToken;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken = _cancellation.Token;
        _channel = Channel.CreateBounded<JetstreamEvent>(
            new BoundedChannelOptions(capacity) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
        _metrics.StreamBufferCapacity.Add(capacity, serverTag);
    }

    internal CancellationToken CancellationToken { get; }

    internal CancellationToken EnumerationCancellationToken { get; }

    internal bool IsStopped => Volatile.Read(ref _stopped);

    internal Action? CapacityWaiting { get; init; }

    internal void Complete(Exception? error = null) => _channel.Writer.TryComplete(error);

    internal ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken) =>
        _channel.Reader.WaitToReadAsync(cancellationToken);

    internal bool TryRead(out JetstreamEvent? item)
    {
        lock (_gate)
        {
            if (_channel.Reader.TryRead(out item))
            {
                _metrics.StreamQueuedEvents.Add(-1, _serverTag);
                return true;
            }

            return false;
        }
    }

    internal async Task DeliverAsync(JetstreamEvent? parsedEvent)
    {
        try
        {
            await WriteAsync(parsedEvent).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
        {
            // Only queue delivery is canceled here; parser and virtual callback failures retain their diagnostics.
        }
    }

    internal async Task WriteAsync(JetstreamEvent? parsedEvent)
    {
        if (parsedEvent is not JetstreamEvent { Sequence: not null } item)
        {
            Complete(new InvalidDataException("The live Jetstream event has no sequence cursor."));
            return;
        }

        while (true)
        {
            lock (_gate)
            {
                CancellationToken.ThrowIfCancellationRequested();
                if (_channel.Writer.TryWrite(item))
                {
                    _metrics.StreamQueuedEvents.Add(1, _serverTag);
                    return;
                }
            }

            long start = _timeProvider.GetTimestamp();
            ValueTask<bool> writable = _channel.Writer.WaitToWriteAsync(CancellationToken);
            bool capacityWait = !writable.IsCompleted;
            if (capacityWait)
            {
                _metrics.StreamCapacityWaits.Add(1, _serverTag);
                CapacityWaiting?.Invoke();
            }

            try
            {
                if (!await writable.ConfigureAwait(false))
                {
                    // Normal completion ends delivery; faulted completion throws from the awaited readiness check.
                    return;
                }
            }
            finally
            {
                if (capacityWait)
                {
                    _metrics.StreamCapacityWaitDuration.Record(_timeProvider.GetElapsedTime(start).TotalSeconds, _serverTag);
                }
            }
        }
    }

    internal void Stop()
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            Complete();
            int remaining = 0;
            while (_channel.Reader.TryRead(out _))
            {
                remaining++;
            }

            _metrics.StreamQueuedEvents.Add(-remaining, _serverTag);
            _metrics.StreamBufferCapacity.Add(-_capacity, _serverTag);
            _cancellation.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            Stop();
            _cancellation.Dispose();
        }
    }
}
