// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.BotSdk;

// Operational visibility only: observing a post neither commits progress nor proves worker
// liveness. Event timestamps, not sequence arithmetic, tell us how far behind wall-clock time we are.
/// <summary>Reports bounded replay lag and throughput without changing progress or asserting socket connectivity.</summary>
/// <param name="time">The clock used for elapsed time and event lag.</param>
/// <param name="logger">The progress diagnostic logger.</param>
/// <param name="observations">The process-local handled-event observations.</param>
internal sealed class ReplayProgress(
    TimeProvider time,
    ILogger logger,
    BotReplayProgress observations)
{
    private static readonly TimeSpan s_reportInterval = TimeSpan.FromSeconds(15);
    private long _reportedAt = time.GetTimestamp();
    private long _receivedAt = time.GetTimestamp();
    private long _events;
    private long? _sequence;
    private DateTimeOffset? _witnessedAt;
    private bool? _catchingUp;

    /// <summary>Observes a handled event and reports the first event, lag-state changes, and periodic progress.</summary>
    /// <param name="sequence">The event sequence, not an event count or timestamp.</param>
    /// <param name="witnessedAt">The service's observed event time used to estimate replay lag.</param>
    public void Observe(long sequence, DateTimeOffset witnessedAt)
    {
        _sequence = sequence;
        _witnessedAt = witnessedAt;
        _receivedAt = time.GetTimestamp();
        _events++;

        DateTimeOffset observedAt = time.GetUtcNow();
        observations.Observe(sequence, witnessedAt, observedAt);
        TimeSpan lag = observedAt - witnessedAt;

        // "Near current time" is a lag estimate, not proof that the SDK has left archive replay.
        // Negative lag is reported explicitly because clocks can differ.
        bool catchingUp = lag > TimeSpan.FromSeconds(30);
        TimeSpan elapsed = time.GetElapsedTime(_reportedAt);

        // Report the first event and mode changes immediately, but throttle ordinary progress
        // so the firehose does not turn console logging into the ingestion bottleneck.
        if (_catchingUp != catchingUp || elapsed >= s_reportInterval)
        {
            string position;
            if (lag < TimeSpan.Zero)
            {
                position = "event timestamp is ahead of the local clock";
            }
            else if (catchingUp)
            {
                position = "catching up";
            }
            else
            {
                position = "near current time (within 30 seconds)";
            }
            BotLog.ReplayProgress(
                logger,
                position,
                sequence,
                witnessedAt,
                lag,
                _events,
                elapsed.TotalSeconds > 0 ? _events / elapsed.TotalSeconds : 0);
            _catchingUp = catchingUp;
            _reportedAt = time.GetTimestamp();
            _events = 0;
        }
    }

    /// <summary>Reports a quiet read without claiming it proves an active connection.</summary>
    public void Waiting()
    {
        // Silence can mean no events, a quota wait, or reconnect recovery. Do not advertise
        // "connected" merely because an asynchronous read has not completed.
        if (time.GetElapsedTime(_reportedAt) >= s_reportInterval)
        {
            BotLog.ReplayWaiting(logger, _sequence, _witnessedAt, time.GetElapsedTime(_receivedAt));
            _reportedAt = time.GetTimestamp();
            _events = 0;
        }
    }
}
