// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.BotSdk;

// The SDK uses this provider for real quota/retry waits, not a synthetic worker heartbeat.
// Observe timer deadlines while delegating actual timing to the underlying provider. This lets
// the reader allow legitimate waits without granting an unbounded exemption from liveness.
/// <summary>Tracks actual SDK timer waits so liveness permits bounded recovery without inventing heartbeats.</summary>
/// <param name="inner">The underlying provider responsible for real timestamps and timer execution.</param>
internal sealed class ReplayClock(TimeProvider inner) : TimeProvider
{
    private readonly object _gate = new();
    private readonly Dictionary<long, DateTimeOffset> _waits = [];
    private long _nextId;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();
    /// <inheritdoc/>
    public override long GetTimestamp() => inner.GetTimestamp();
    /// <inheritdoc/>
    public override long TimestampFrequency => inner.TimestampFrequency;
    /// <inheritdoc/>
    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    /// <summary>Extends the normal read deadline only while a real SDK wait is outstanding.</summary>
    /// <param name="ordinaryDeadline">The worker's deadline without a quota or recovery timer.</param>
    /// <returns>The later deadline, allowing a short resume margin after an actual timer wait.</returns>
    public DateTimeOffset Deadline(DateTimeOffset ordinaryDeadline)
    {
        lock (_gate)
        {
            // Allow the timer's requested wait plus a short resume margin. Once it fires or is
            // disposed, it stops extending the reader's deadline; it cannot keep a hang healthy.
            DateTimeOffset latest = _waits.Values.DefaultIfEmpty(ordinaryDeadline).Max();

            return latest > ordinaryDeadline ? latest.AddSeconds(60) : ordinaryDeadline;
        }
    }

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        long id = Interlocked.Increment(ref _nextId);
        Track(id, dueTime);

        ITimer timer = inner.CreateTimer(
            _ =>
        {
            Remove(id);
            callback(state);
        },
            null,
            dueTime,
            period);

        return new TrackedTimer(timer, this, id);
    }

    /// <summary>Records a positive timer deadline and rejects waits that exceed the recovery policy.</summary>
    /// <param name="id">The unique timer identity.</param>
    /// <param name="dueTime">The requested delay before the next callback.</param>
    /// <exception cref="InvalidOperationException">The requested wait exceeds one day.</exception>
    private void Track(long id, TimeSpan dueTime)
    {
        // An unexpectedly huge server/SDK wait needs operator attention, not a healthy worker
        // that disappears indefinitely. The one-day bound is an application policy.
        if (dueTime > TimeSpan.FromDays(1))
        {
            throw new InvalidOperationException("An SDK wait exceeded the one-day recovery deadline.");
        }

        lock (_gate)
        {
            if (dueTime > TimeSpan.Zero)
            {
                _waits[id] = inner.GetUtcNow() + dueTime;
            }
            else
            {
                _waits.Remove(id);
            }
        }
    }

    /// <summary>Removes a completed or disposed timer so it no longer extends worker liveness.</summary>
    /// <param name="id">The timer identity to remove.</param>
    private void Remove(long id)
    {
        lock (_gate)
        {
            _waits.Remove(id);
        }
    }

    /// <summary>Forwards timer operations while keeping recovery deadline tracking synchronized.</summary>
    /// <param name="timer">The underlying timer owned by this wrapper.</param>
    /// <param name="clock">The owner tracking outstanding waits.</param>
    /// <param name="id">The unique tracking identity.</param>
    private sealed class TrackedTimer(
        ITimer timer,
        ReplayClock clock,
        long id) : ITimer
    {
        /// <inheritdoc/>
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            clock.Track(id, dueTime);

            return timer.Change(dueTime, period);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            clock.Remove(id);
            timer.Dispose();
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            clock.Remove(id);

            return timer.DisposeAsync();
        }
    }
}
