// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

using idunno.Bluesky.BotSdk.DataProviders;

namespace idunno.Bluesky.BotSdk;

/// <summary>Publishes host-owned bot instrumentation without polling dependencies from observable callbacks.</summary>
internal sealed class BotMetrics : IDisposable
{
    internal const string MeterName = "idunno.Bluesky.BotSdk";
    internal const string Prefix = "idunno.bluesky.bot.";
    private readonly Meter _meter;
    private readonly TimeProvider _time;
    private readonly BotActionTiming? _timing;
    private readonly Ownership _ownership;
    private readonly WorkerHealth _health;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="BotMetrics"/> class.</summary>
    /// <param name="factory">The host's meter factory.</param>
    /// <param name="time">The clock for elapsed measurements.</param>
    /// <param name="database">The optional source of committed storage timings.</param>
    /// <param name="ownership">The local ownership signal.</param>
    /// <param name="health">The deadline-based worker state.</param>
    /// <param name="progress">The handled replay observations.</param>
    public BotMetrics(
        IMeterFactory factory,
        TimeProvider time,
        IDatabaseProvider database,
        Ownership ownership,
        WorkerHealth health,
        BotReplayProgress progress)
    {
        _time = time;
        _ownership = ownership;
        _health = health;
        _meter = factory.Create(MeterName, "1.0.0");
        EventsHandled = Counter("events.handled", "{events}", "Successfully handled nonduplicate events; coalesced progress need not be durable.");
        EventsDuplicates = Counter("events.duplicates", "{events}", "Replay events suppressed by the handled sequence boundary.");
        ActionsEnqueued = Counter("actions.enqueued", "{actions}", "New action rows inserted by successful event transactions.");
        ActionsCompleted = Counter("actions.completed", "{actions}", "Newly committed terminal outcomes, not remote write attempts.");
        RetriesScheduled = Counter("actions.retries.scheduled", "{retries}", "New retry schedules successfully persisted.");
        RateDeferrals = Counter("rate_limit.deferrals", "{deferrals}", "Outbound requests deferred by budget admission or HTTP 429.");
        OwnershipTransitions = Counter("ownership.transitions", "{transitions}", "Actual changes to local permission to work.");
        RecoveryEpisodes = Counter("worker.recovery.episodes", "{episodes}", "Entries into dependency recovery, excluding ownership standby.");
        WorkerFailures = Counter("worker.failures", "{failures}", "Unrecoverable worker failures, excluding ordinary shutdown.");
        EventDuration = Histogram("event.handling.duration", "Elapsed event handling attempts, including required persistence.");
        DispatchDuration = Histogram("action.dispatch.duration", "Elapsed dispatch including reconciliation and persistence, excluding prior authentication.");
        ReconciliationDuration = Histogram("action.reconciliation.duration", "Elapsed handler reconciliation calls.");
        RetryDelay = Histogram("action.retry.delay", "Scheduled retry delay at successful persistence, not elapsed waiting.");
        RateWaitDuration = Histogram("rate_limit.wait.duration", "Elapsed outbound pacing or sender rate backoff waits.");
        ActionLatency = Histogram("action.latency", "Database-clock enqueue to newly committed terminal confirmation.");
        _meter.CreateObservableGauge<int>(
            Prefix + "ownership.active",
            () => ownership.IsActive ? 1 : 0,
            description: "Local permission to work, not an independent database lease probe.");
        _meter.CreateObservableGauge<int>(
            Prefix + "worker.healthy",
            () => health.Snapshot().Workers.Select(pair =>
                new Measurement<int>(pair.Value.Deadline > time.GetUtcNow() ? 1 : 0, Tag("worker", pair.Key))),
            description: "Deadline-based worker liveness.");
        _meter.CreateObservableGauge<int>(
            Prefix + "worker.recovering",
            () => health.Snapshot().Workers.Select(pair =>
                new Measurement<int>(health.IsRecovering(pair.Key) ? 1 : 0, Tag("worker", pair.Key))),
            description: "Recorded dependency recovery, excluding ownership standby.");
        _meter.CreateObservableGauge<int>(
            Prefix + "replay.observation.available",
            () => progress.Snapshot.Sequence is null ? 0 : 1,
            description: "Availability of a handled-event observation in this ownership period.");
        _meter.CreateObservableGauge<double>(
            Prefix + "replay.witnessed.age",
            () => progress.Snapshot.WitnessedAt is DateTimeOffset witnessed
                ? [new Measurement<double>((time.GetUtcNow() - witnessed).TotalSeconds)]
                : Array.Empty<Measurement<double>>(),
            unit: "s",
            description: "Age of the last handled event's service timestamp; clock skew can make it negative.");
        _meter.CreateObservableGauge<double>(
            Prefix + "replay.observation.age",
            () => progress.GetObservedTimestamp() is long timestamp
                ? [new Measurement<double>(time.GetElapsedTime(timestamp).TotalSeconds)]
                : Array.Empty<Measurement<double>>(),
            unit: "s",
            description: "Monotonic time since the last handled event; absent when unknown.");
        if (database is IBotActionTimingSource source)
        {
            _timing = source.Timing;
            _timing.Completed += RecordLatency;
        }
        ownership.Transition += RecordOwnership;
        health.Recovery += RecordRecovery;
        health.Failure += RecordFailure;
    }

    internal Counter<long> EventsHandled { get; }
    internal Counter<long> EventsDuplicates { get; }
    internal Counter<long> ActionsEnqueued { get; }
    internal Counter<long> ActionsCompleted { get; }
    internal Counter<long> RetriesScheduled { get; }
    internal Counter<long> RateDeferrals { get; }
    internal Counter<long> OwnershipTransitions { get; }
    internal Counter<long> RecoveryEpisodes { get; }
    internal Counter<long> WorkerFailures { get; }
    internal Histogram<double> EventDuration { get; }
    internal Histogram<double> DispatchDuration { get; }
    internal Histogram<double> ReconciliationDuration { get; }
    internal Histogram<double> RetryDelay { get; }
    internal Histogram<double> RateWaitDuration { get; }
    internal Histogram<double> ActionLatency { get; }

    /// <summary>Creates one counter with a stable name and unit.</summary>
    private Counter<long> Counter(string suffix, string unit, string description) =>
        _meter.CreateCounter<long>(name: Prefix + suffix, unit: unit, description: description);

    /// <summary>Creates one seconds-based duration histogram.</summary>
    private Histogram<double> Histogram(string suffix, string description) =>
        _meter.CreateHistogram<double>(name: Prefix + suffix, unit: "s", description: description);

    /// <summary>Creates a tag whose values are selected from fixed runtime classifications.</summary>
    internal static KeyValuePair<string, object?> Tag(string name, string value) => new(name, value);

    /// <summary>Distinguishes dependency recovery from ordinary ownership standby.</summary>
    internal static bool IsRecovery(string? dependency) =>
        dependency is not null and not "database/ownership" and not "ownership";

    /// <summary>Gets the current monotonic timestamp for an operation boundary.</summary>
    internal long Timestamp => _time.GetTimestamp();

    /// <summary>Records elapsed time at an operation's exit.</summary>
    /// <param name="histogram">The duration instrument.</param>
    /// <param name="started">The monotonic start timestamp.</param>
    /// <param name="outcome">The fixed exit classification.</param>
    /// <param name="location">The optional fixed wait location.</param>
    internal void RecordDuration(
        Histogram<double> histogram,
        long started,
        string outcome,
        string? location = null)
    {
        double seconds = _time.GetElapsedTime(started).TotalSeconds;
        if (location is null)
        {
            histogram.Record(seconds, Tag("outcome", outcome));
        }
        else
        {
            histogram.Record(seconds, Tag("outcome", outcome), Tag("location", location));
        }
    }

    /// <summary>Publishes a committed database-clock latency.</summary>
    private void RecordLatency(string status, double seconds) =>
        ActionLatency.Record(seconds, Tag("status", status));

    /// <summary>Records one local ownership transition.</summary>
    private void RecordOwnership(string transition) => OwnershipTransitions.Add(1, Tag("transition", transition));

    /// <summary>Records entry into a bounded dependency recovery episode.</summary>
    private void RecordRecovery(string worker, string reason) =>
        RecoveryEpisodes.Add(1, Tag("worker", worker), Tag("reason", reason));

    /// <summary>Records an unrecoverable worker failure.</summary>
    private void RecordFailure(string worker) => WorkerFailures.Add(1, Tag("worker", worker));

    /// <summary>Unsubscribes runtime events; the host meter factory owns observable instrument disposal.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _ownership.Transition -= RecordOwnership;
        _health.Recovery -= RecordRecovery;
        _health.Failure -= RecordFailure;
        if (_timing is BotActionTiming timing)
        {
            timing.Completed -= RecordLatency;
        }
        _meter.Dispose();
    }
}
