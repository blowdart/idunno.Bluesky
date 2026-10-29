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
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif
    private readonly Dictionary<long, DateTimeOffset> _waits = [];
    private DateTimeOffset _resumeDeadline;
    private long _nextId;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();
    /// <inheritdoc/>
    public override long GetTimestamp() => inner.GetTimestamp();
    /// <inheritdoc/>
    public override long TimestampFrequency => inner.TimestampFrequency;
    /// <inheritdoc/>
    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    /// <summary>Extends the normal read deadline for an outstanding SDK wait and its bounded resume margin.</summary>
    /// <param name="ordinaryDeadline">The worker's deadline without a quota or recovery timer.</param>
    /// <returns>The later deadline, allowing a short resume margin after an actual timer wait.</returns>
    public DateTimeOffset Deadline(DateTimeOffset ordinaryDeadline)
    {
        lock (_gate)
        {
            DateTimeOffset latest = _waits.Values.DefaultIfEmpty(ordinaryDeadline).Max();
            DateTimeOffset deadline = latest > ordinaryDeadline ? latest.AddSeconds(60) : ordinaryDeadline;

            return _resumeDeadline > deadline ? _resumeDeadline : deadline;
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
            Fired(id);
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

    /// <summary>Preserves a bounded resume deadline only when an outstanding timer actually fires.</summary>
    /// <param name="id">The completed timer identity.</param>
    private void Fired(long id)
    {
        lock (_gate)
        {
            if (_waits.Remove(id))
            {
                DateTimeOffset resumeDeadline = inner.GetUtcNow().AddSeconds(60);
                if (resumeDeadline > _resumeDeadline)
                {
                    _resumeDeadline = resumeDeadline;
                }
            }
        }
    }

    /// <summary>Removes a disposed or cancelled timer without granting a resume margin.</summary>
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
