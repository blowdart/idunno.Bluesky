// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.BotSdk;

/// <summary>Provides read-only, in-memory observations of sequentially handled replay events.</summary>
/// <remarks>
/// <para>Resolve this singleton from dependency injection after registering the bot. Reading
/// <see cref="Snapshot"/> does not contact storage or remote services.</para>
/// <para>Handled progress is not a durable checkpoint: ignored events may be coalesced before persistence.
/// It does not describe action dispatch, worker liveness, live-stream mode, or socket connectivity.</para>
/// <para>Quiet reads and recovery retain the last observation without refreshing its time. A new ownership
/// period resets progress to unknown; restored checkpoints and replay duplicates do not establish observations.</para>
/// </remarks>
public sealed class BotReplayProgress
{
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif
    private CancellationToken? _ownershipPeriod;

    internal BotReplayProgress()
    {
    }

    /// <summary>Gets an immutable, atomic copy of the last handled-event observation.</summary>
    /// <value>The observation, with all fields <see langword="null"/> before any event is handled in this ownership period.</value>
    public ReplayProgressSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return field;
            }
        }

        private set;
    }

    internal void BeginOwnership(CancellationToken ownershipPeriod)
    {
        lock (_gate)
        {
            if (_ownershipPeriod != ownershipPeriod)
            {
                _ownershipPeriod = ownershipPeriod;
                Snapshot = default;
            }
        }
    }

    internal void Observe(long sequence, DateTimeOffset witnessedAt, DateTimeOffset observedAt)
    {
        lock (_gate)
        {
            Snapshot = new(sequence, witnessedAt, observedAt);
        }
    }
}

/// <summary>Describes one handled replay event without asserting durability or connectivity.</summary>
/// <remarks>
/// <para>All fields are unknown in the default snapshot. Known fields always describe the same event.
/// Subtract <see cref="WitnessedAt"/> from the current UTC wall-clock time to estimate replay lag.
/// This estimate can be negative because the service and local clocks can differ, and does not prove
/// live-stream mode or socket connectivity.</para>
/// </remarks>
public readonly record struct ReplayProgressSnapshot
{
    internal ReplayProgressSnapshot(long sequence, DateTimeOffset witnessedAt, DateTimeOffset observedAt)
    {
        Sequence = sequence;
        WitnessedAt = witnessedAt;
        ObservedAt = observedAt;
    }

    /// <summary>Gets the last successfully handled event sequence.</summary>
    /// <value>The sequence, or <see langword="null"/> before the first handled event.</value>
    public long? Sequence { get; }

    /// <summary>Gets the service's witnessed timestamp of the handled event.</summary>
    /// <value>The service timestamp, or <see langword="null"/> before the first handled event.</value>
    public DateTimeOffset? WitnessedAt { get; }

    /// <summary>Gets the local UTC time when successful handling was observed.</summary>
    /// <value>The observation time from the injected clock, or <see langword="null"/> before the first handled event.</value>
    public DateTimeOffset? ObservedAt { get; }
}
