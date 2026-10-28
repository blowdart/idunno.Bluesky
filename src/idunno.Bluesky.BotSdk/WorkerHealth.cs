// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.BotSdk;

// Workers publish the phase they are actually executing and its bounded deadline. There is
// no independent heartbeat: a timer ticking while a worker is stuck would give false confidence.
// Dependency outages are reported separately so active recovery need not cause restart loops.
/// <summary>Tracks actual worker phases and deadlines without contacting dependencies from health probes.</summary>
/// <param name="time">The clock used to evaluate operation deadlines.</param>
internal sealed class WorkerHealth(TimeProvider time)
{
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif
    private readonly Dictionary<string, HealthState> _states = new(StringComparer.Ordinal)
    {
        ["ownership"] = new("starting", time.GetUtcNow().AddSeconds(60), null),
        ["reader"] = new("starting", time.GetUtcNow().AddSeconds(60), null),
        ["sender"] = new("starting", time.GetUtcNow().AddSeconds(60), null)
    };

    /// <summary>Records the current operation and the maximum healthy time it may take.</summary>
    /// <param name="worker">The worker name.</param>
    /// <param name="phase">The current activity.</param>
    /// <param name="deadline">The allowed duration starting now.</param>
    /// <param name="dependency">The recovering dependency, or <see langword="null"/> for ordinary work.</param>
    public void Set(string worker, string phase, TimeSpan deadline, string? dependency = null)
    {
        lock (_gate)
        {
            _states[worker] = new(phase, time.GetUtcNow() + deadline, dependency);
        }
    }

    /// <summary>Marks a worker unhealthy after an unrecoverable failure.</summary>
    /// <param name="worker">The failing worker name.</param>
    public void Fail(string worker)
    {
        lock (_gate)
        {
            _states[worker] = new("failed", DateTimeOffset.MinValue, null);
        }
    }

    /// <summary>Copies a consistent health view under the lock for fast HTTP probe serialization.</summary>
    /// <returns>The overall liveness result and a detached worker-state dictionary.</returns>
    public (bool Healthy, Dictionary<string, HealthState> Workers) Snapshot()
    {
        lock (_gate)
        {
            // Copy under the lock so fast HTTP probes see a consistent snapshot without holding
            // the lock during serialization. Probes never wait for a database or Bluesky.
            Dictionary<string, HealthState> states = new(_states, StringComparer.Ordinal);

            return (states.Values.All(state => state.Deadline > time.GetUtcNow()), states);
        }
    }
}

/// <summary>Describes an actual worker operation and its liveness deadline.</summary>
/// <param name="Phase">The current activity or generic failure state.</param>
/// <param name="Deadline">The time after which the operation is overdue.</param>
/// <param name="Dependency">The recovering dependency, or <see langword="null"/>.</param>
internal sealed record HealthState(
    string Phase,
    DateTimeOffset Deadline,
    string? Dependency);

/// <summary>Signals that a durable outbound rate budget is unavailable until a specified time.</summary>
public sealed class RateWaitException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="RateWaitException"/> class.</summary>
    public RateWaitException() : this(DateTimeOffset.MaxValue)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RateWaitException"/> class.</summary>
    /// <param name="until">The earliest time at which the request budget is available.</param>
    public RateWaitException(DateTimeOffset until) : base("Waiting for the durable outbound rate budget.")
    {
        Until = until;
    }

    /// <summary>Initializes a new instance of the <see cref="RateWaitException"/> class.</summary>
    /// <param name="message">The message that describes the error.</param>
    public RateWaitException(string message) : base(message)
    {
        Until = DateTimeOffset.MaxValue;
    }

    /// <summary>Initializes a new instance of the <see cref="RateWaitException"/> class.</summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public RateWaitException(string message, Exception innerException) : base(message, innerException)
    {
        Until = DateTimeOffset.MaxValue;
    }

    /// <summary>Gets the earliest time at which the request budget is available.</summary>
    public DateTimeOffset Until { get; }
}
