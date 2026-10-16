// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;

using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Archive;

namespace idunno.Bluesky.BotSdk;

// Fixed-size, enumeration-local counters. No event payloads are retained and no logging queue
// is added. Monotonic timestamps measure work; wall-clock time is only used for labelled event lag.
/// <summary>Measures one replay enumeration using bounded counters without retaining event payloads.</summary>
/// <param name="time">The clock providing monotonic durations and labelled wall-clock lag.</param>
/// <param name="logger">The structured logger sharing this enumeration's correlation scope.</param>
internal sealed class ReplayDiagnostics(
    TimeProvider time,
    ILogger logger)
{
    private static readonly Func<ILogger, string, IDisposable?> s_enumerationScope =
        LoggerMessage.DefineScope<string>("ReplayEnumerationId={ReplayEnumerationId}");

    private readonly long _started = time.GetTimestamp();
    private long _reportedAt = time.GetTimestamp();
    private long _reportedEvents;
    private long? _readStarted;
    private TimeSpan _readCallbacks;
    private long _events;
    private long _relevant;
    private long _checkpointSaves;
    private long _checkpointFailures;
    private readonly Timing _handling = new();
    private readonly Timing _database = new();
    private readonly Timing _checkpoints = new();
    private readonly Timing _reads = new();

    /// <summary>Gets the correlation identity shared by application and SDK logs for this enumeration.</summary>
    public Guid EnumerationId { get; } = Guid.NewGuid();
    /// <summary>Gets or sets the operation currently being measured.</summary>
    public string Phase { get; set; } = "restoring replay state";
    /// <summary>Gets or sets the observed reason the enumeration stopped.</summary>
    public string ExitReason { get; set; } = "running";
    /// <summary>Gets or sets the already logged exit exception to avoid duplicate failure reports.</summary>
    public Exception? LoggedExit { get; set; }
    /// <summary>Gets the latest event handled in memory, which may not yet be a saved checkpoint.</summary>
    public long? ProcessedThrough { get; private set; }
    /// <summary>Gets the boundary known to have committed to durable storage.</summary>
    public long? DurableThrough { get; private set; }
    /// <summary>Gets the complete most recently committed SDK checkpoint.</summary>
    public SnapshotCheckpoint? SavedCheckpoint { get; private set; }
    /// <summary>Gets the witnessed time of the latest processed event for lag reporting.</summary>
    public DateTimeOffset? LastWitnessedAt { get; private set; }

    /// <summary>Opens the correlation scope inherited by application and SDK logging.</summary>
    /// <returns>The scope to dispose after cleanup, or <see langword="null"/> if scopes are disabled.</returns>
    public IDisposable? BeginScope() =>
        s_enumerationScope(logger, EnumerationId.ToString());

    /// <summary>Initializes both progress observations from the durable restored boundary.</summary>
    /// <param name="processed">The saved processed-through sequence.</param>
    public void Restored(long processed) => ProcessedThrough = DurableThrough = processed;

    /// <summary>Logs the pinned SDK identity and restored replay plan without exposing archive credentials.</summary>
    /// <param name="service">The configured service, whose authority alone is logged.</param>
    /// <param name="request">The original replay bounds and filters.</param>
    /// <param name="processed">The committed application progress.</param>
    /// <param name="saved">The complete saved checkpoint, if one exists.</param>
    public void Start(string service, SnapshotRequest request, long processed, SnapshotCheckpoint? saved)
    {
        ProcessedThrough = DurableThrough = processed;
        SavedCheckpoint = saved;

        Assembly assembly = typeof(AtProtoJetstream).Assembly;

        // Log only the service authority, never URI user-info, query strings, or the archive key.
        Uri uri = new(service);
        BotLog.EnumerationStarting(
            logger,
            EnumerationId,
            assembly.GetName().Version?.ToString(),
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            $"{uri.Scheme}://{uri.IdnHost}:{uri.Port}",
            request.AfterSeq,
            request.BeforeSeq,
            string.Join(",", request.Collections ?? []),
            string.Join(",", request.Kinds ?? []),
            string.Join(",", request.Dids ?? []),
            processed,
            saved?.PlanAfterSeq,
            saved?.ReplayAfterSeq,
            saved?.LiveAfterSeq);
    }

    /// <summary>Starts timing the next enumeration read separately from synchronous storage callbacks.</summary>
    public void BeginRead()
    {
        Phase = "MoveNextAsync";
        _readStarted = time.GetTimestamp();
        _readCallbacks = TimeSpan.Zero;
    }

    /// <summary>Finishes read timing, subtracting measured callback storage time to avoid misleading network diagnostics.</summary>
    public void EndRead()
    {
        if (_readStarted is long started)
        {
            // Synchronous checkpoint/skip callbacks execute inside MoveNextAsync. Subtract their
            // measured database time rather than blaming that work on a quiet stream/network wait.
            _reads.Add(time.GetElapsedTime(started) - _readCallbacks);
            _readStarted = null;
        }
    }

    /// <summary>Counts one consumed event without changing durable progress.</summary>
    public void Consumed() => _events++;

    /// <summary>Records elapsed event handling, including storage performed while handling that event.</summary>
    /// <param name="started">The monotonic timestamp captured before handling began.</param>
    public void Handled(long started) => _handling.Add(time.GetElapsedTime(started));

    /// <summary>Observes in-memory processing and the source service's event time.</summary>
    /// <param name="sequence">The handled sequence, not a timestamp or event count.</param>
    /// <param name="witnessedAt">The optional witnessed timestamp used only for lag reporting.</param>
    public void Processed(long sequence, DateTimeOffset? witnessedAt)
    {
        ProcessedThrough = sequence;
        LastWitnessedAt = witnessedAt;
    }

    /// <summary>Counts a relevant event only after its effects and progress have committed together.</summary>
    /// <param name="sequence">The committed source event sequence.</param>
    public void DurableEvent(long sequence)
    {
        DurableThrough = sequence;
        _relevant++;
    }

    /// <summary>Measures an asynchronous storage operation, preserving its result and any exception.</summary>
    /// <typeparam name="T">The operation's result type.</typeparam>
    /// <param name="operation">The storage operation to await.</param>
    /// <returns>The unchanged storage result.</returns>
    public async Task<T> DatabaseAsync<T>(Func<Task<T>> operation)
    {
        long started = time.GetTimestamp();

        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            DatabaseElapsed(time.GetElapsedTime(started));
        }
    }

    /// <summary>Measures a synchronous storage callback without changing its failure behavior.</summary>
    /// <param name="operation">The storage operation to execute.</param>
    public void DatabaseWork(Action operation)
    {
        long started = time.GetTimestamp();

        try
        {
            operation();
        }
        finally
        {
            DatabaseElapsed(time.GetElapsedTime(started));
        }
    }

    /// <summary>Records storage time and identifies the portion executed inside a pending SDK read.</summary>
    /// <param name="elapsed">The monotonic duration of the completed storage operation.</param>
    private void DatabaseElapsed(TimeSpan elapsed)
    {
        _database.Add(elapsed);

        if (_readStarted is not null)
        {
            _readCallbacks += elapsed;
        }
    }

    /// <summary>Publishes diagnostic checkpoint progress only after the caller's save succeeds.</summary>
    /// <param name="checkpoint">The complete SDK checkpoint being committed.</param>
    /// <param name="processed">The progress covered by that transaction.</param>
    /// <param name="save">The synchronous durable-save callback.</param>
    public void SaveCheckpoint(SnapshotCheckpoint checkpoint, long processed, Action save)
    {
        Phase = "persisting complete checkpoint";

        long started = time.GetTimestamp();

        try
        {
            save();

            // Publish diagnostic durable progress only after the database transaction commits.
            SavedCheckpoint = checkpoint;
            DurableThrough = processed;
            _checkpointSaves++;
        }
        catch (Exception exception)
        {
            _checkpointFailures++;
            Failure("checkpoint save failed", exception);
            throw;
        }
        finally
        {
            TimeSpan elapsed = time.GetElapsedTime(started);
            _checkpoints.Add(elapsed);
            DatabaseElapsed(elapsed);
            Phase = "MoveNextAsync";
        }
    }

    /// <summary>Logs bounded aggregate measurements, throttled unless this is the final report.</summary>
    /// <param name="final"><see langword="true"/> to bypass throttling at exit; otherwise, <see langword="false"/>.</param>
    public void Report(bool final = false)
    {
        if (!final && time.GetElapsedTime(_reportedAt) < TimeSpan.FromSeconds(30))
        {
            return;
        }

        ReplayDiagnosticSummary summary = Snapshot();
        BotLog.EnumerationSummary(
            logger,
            EnumerationId,
            final,
            Phase,
            ExitReason,
            summary.Elapsed.TotalSeconds,
            summary.EventsConsumed,
            summary.RelevantEvents,
            summary.EventsPerSecond,
            ProcessedThrough,
            DurableThrough,
            SavedCheckpoint?.PlanAfterSeq,
            SavedCheckpoint?.ReplayAfterSeq,
            SavedCheckpoint?.LiveAfterSeq,
            SavedCheckpoint?.SealedTipSeq,
            SavedCheckpoint?.SegmentName,
            SavedCheckpoint?.NextBlockIndex,
            SavedCheckpoint?.NextByteOffset,
            _checkpointSaves,
            _checkpointFailures,
            _handling.Count,
            _handling.Total.TotalMilliseconds,
            _handling.Maximum.TotalMilliseconds,
            _database.Count,
            _database.Total.TotalMilliseconds,
            _database.Maximum.TotalMilliseconds,
            _checkpoints.Count,
            _checkpoints.Total.TotalMilliseconds,
            _checkpoints.Maximum.TotalMilliseconds,
            _reads.Count,
            summary.ReadWait.TotalMilliseconds,
            Math.Max(
    _reads.Maximum.TotalMilliseconds,
    _readStarted is long started ? (time.GetElapsedTime(started) - _readCallbacks).TotalMilliseconds : 0),
            LastWitnessedAt,
            LastWitnessedAt is DateTimeOffset witnessed ? time.GetUtcNow() - witnessed : null);
        _reportedAt = time.GetTimestamp();
        _reportedEvents = _events;
    }

    /// <summary>Captures counters and timings without changing the reporting interval.</summary>
    /// <returns>The cumulative measurements and current interval throughput.</returns>
    public ReplayDiagnosticSummary Snapshot()
    {
        TimeSpan interval = time.GetElapsedTime(_reportedAt);
        TimeSpan pendingRead = _readStarted is long started ? time.GetElapsedTime(started) - _readCallbacks : TimeSpan.Zero;

        return new(
            time.GetElapsedTime(_started),
            _events,
            _relevant,
            interval.TotalSeconds > 0 ? (_events - _reportedEvents) / interval.TotalSeconds : 0,
            _checkpointSaves,
            _checkpointFailures,
            _handling.Count,
            _handling.Total,
            _handling.Maximum,
            _database.Count,
            _database.Total,
            _database.Maximum,
            _checkpoints.Count,
            _checkpoints.Total,
            _checkpoints.Maximum,
            _reads.Total + pendingRead);
    }

    /// <summary>Logs an observed failure with its full exception and both progress boundaries.</summary>
    /// <param name="reason">The observed failure description, not a speculative reconnect cause.</param>
    /// <param name="exception">The original exception preserving its stack and inner exceptions.</param>
    public void Failure(string reason, Exception exception) =>
        BotLog.EnumerationFailure(
            logger,
            EnumerationId,
            Phase,
            reason,
            ProcessedThrough,
            DurableThrough,
            SavedCheckpoint?.PlanAfterSeq,
            SavedCheckpoint?.ReplayAfterSeq,
            SavedCheckpoint?.LiveAfterSeq,
            exception);

    /// <summary>Logs a cleanup milestone and its elapsed time without swallowing errors.</summary>
    /// <param name="step">The cleanup operation or observation.</param>
    /// <param name="started">The monotonic cleanup start time.</param>
    /// <param name="exception">The optional exception associated with this milestone.</param>
    public void CleanupStep(string step, long started, Exception? exception = null) =>
        BotLog.EnumerationCleanup(logger, EnumerationId, step, ExitReason, time.GetElapsedTime(started), exception);

    /// <summary>Labels cancellation using observed signals rather than inferring a network cause.</summary>
    /// <param name="hostShutdown">Whether the host requested shutdown.</param>
    /// <param name="ownershipLost">Whether the process lost permission to work.</param>
    /// <param name="readSliceExpired">Whether the bounded read slice elapsed.</param>
    /// <returns>The first applicable cancellation reason in shutdown, ownership, then slice order.</returns>
    public static string CancellationReason(bool hostShutdown, bool ownershipLost, bool readSliceExpired)
    {
        if (hostShutdown)
        {
            return "host shutdown";
        }

        if (ownershipLost)
        {
            return "existing recovery action: ownership loss";
        }

        return readSliceExpired ? "read-slice timeout" : "unexpected cancellation";
    }

    /// <summary>Accumulates operation count, total duration, and maximum duration in constant memory.</summary>
    private sealed class Timing
    {
        /// <summary>Gets the number of completed operations.</summary>
        public long Count { get; private set; }
        /// <summary>Gets the sum of completed operation durations.</summary>
        public TimeSpan Total { get; private set; }
        /// <summary>Gets the longest completed operation duration.</summary>
        public TimeSpan Maximum { get; private set; }

        /// <summary>Adds a completed duration without retaining individual samples.</summary>
        /// <param name="elapsed">The duration of the completed operation.</param>
        public void Add(TimeSpan elapsed)
        {
            Count++;
            Total += elapsed;
            Maximum = elapsed > Maximum ? elapsed : Maximum;
        }
    }
}

/// <summary>Captures bounded replay measurements, separating event work, storage, checkpoints, and read waits.</summary>
/// <param name="Elapsed">The duration of this enumeration.</param>
/// <param name="EventsConsumed">The cumulative consumed event count.</param>
/// <param name="RelevantEvents">The cumulative count of events with committed effects.</param>
/// <param name="EventsPerSecond">The throughput since the previous report.</param>
/// <param name="CheckpointSaves">The successful checkpoint-save count.</param>
/// <param name="CheckpointFailures">The failed checkpoint-save count.</param>
/// <param name="HandlingCount">The completed handling sample count.</param>
/// <param name="HandlingTotal">The cumulative handling time, including event storage.</param>
/// <param name="HandlingMaximum">The maximum completed handling duration.</param>
/// <param name="DatabaseCount">The completed storage sample count.</param>
/// <param name="DatabaseTotal">The cumulative storage time, including checkpoints.</param>
/// <param name="DatabaseMaximum">The maximum completed storage duration.</param>
/// <param name="CheckpointCount">The checkpoint timing sample count.</param>
/// <param name="CheckpointTotal">The cumulative checkpoint-save time.</param>
/// <param name="CheckpointMaximum">The maximum completed checkpoint-save duration.</param>
/// <param name="ReadWait">The read time including a pending read, excluding storage callbacks.</param>
internal sealed record ReplayDiagnosticSummary(
    TimeSpan Elapsed,
    long EventsConsumed,
    long RelevantEvents,
    double EventsPerSecond,
    long CheckpointSaves,
    long CheckpointFailures,
    long HandlingCount,
    TimeSpan HandlingTotal,
    TimeSpan HandlingMaximum,
    long DatabaseCount,
    TimeSpan DatabaseTotal,
    TimeSpan DatabaseMaximum,
    long CheckpointCount,
    TimeSpan CheckpointTotal,
    TimeSpan CheckpointMaximum,
    TimeSpan ReadWait);

/// <summary>Closes replay resources in cancel, await-read, then dispose order and preserves cleanup failures.</summary>
internal static class ReplayCleanup
{
    /// <summary>Awaits SDK disposal and records milestones while preserving the original exception on failure.</summary>
    /// <param name="dispose">The SDK's asynchronous disposal operation.</param>
    /// <param name="diagnostics">The enumeration-local diagnostic state.</param>
    /// <param name="time">The monotonic clock used for cleanup durations.</param>
    public static async Task DisposeSdkAsync(Func<ValueTask> dispose, ReplayDiagnostics diagnostics, TimeProvider time)
    {
        long started = time.GetTimestamp();
        diagnostics.Phase = "SDK asynchronous disposal";
        diagnostics.CleanupStep("SDK disposal start", started);

        try
        {
            await dispose().ConfigureAwait(false);
            diagnostics.CleanupStep("SDK disposal completed", started);
        }
        catch (Exception exception)
        {
            diagnostics.Failure("SDK disposal failed", exception);
            diagnostics.CleanupStep("SDK disposal failed", started, exception);
            throw;
        }
    }

    // Keep the existing cancel -> await pending read -> dispose sequence and its deadlines.
    // If awaiting the read fails, do not start a potentially concurrent enumerator disposal.
    /// <summary>Cancels replay, waits for an outstanding read, and only then disposes the enumerator.</summary>
    /// <param name="enumeration">The cancellation source owned by this enumeration.</param>
    /// <param name="outstandingRead">The most recent SDK read, which must finish before disposal.</param>
    /// <param name="dispose">The enumerator's asynchronous disposal operation.</param>
    /// <param name="diagnostics">The enumeration-local diagnostic state.</param>
    /// <param name="time">The monotonic clock used for cleanup durations.</param>
    /// <exception cref="TimeoutException">A pending read or disposal exceeds its existing 30-second bound.</exception>
    public static async Task CloseAsync(
        CancellationTokenSource enumeration,
        Task<bool>? outstandingRead,
        Func<ValueTask> dispose,
        ReplayDiagnostics diagnostics,
        TimeProvider time)
    {
        long started = time.GetTimestamp();
        diagnostics.Phase = "cleanup cancellation";
        diagnostics.CleanupStep("start; requesting cancellation", started);

        try
        {
            await enumeration.CancelAsync().ConfigureAwait(false);

            if (outstandingRead is not null)
            {
                if (!outstandingRead.IsCompleted)
                {
                    diagnostics.Phase = "cleanup awaiting outstanding MoveNextAsync";
                    diagnostics.CleanupStep("awaiting outstanding MoveNextAsync", started);
                }

                try
                {
                    await outstandingRead.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception) when (enumeration.IsCancellationRequested)
                {
                    // Preserve the existing cleanup cancellation filter, but do not assert an
                    // unrelated/default-token cancellation was caused by our enumeration token.
                    diagnostics.CleanupStep(
                        exception.CancellationToken == enumeration.Token
                        ? "outstanding read observed enumeration cancellation"
                        : "outstanding read cancelled after cleanup request; cancellation token not verified",
                        started,
                        exception);
                }
            }

            diagnostics.EndRead();
            diagnostics.Phase = "cleanup disposing enumerator";
            diagnostics.CleanupStep("disposing enumerator", started);
            await dispose().AsTask().WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false);
            diagnostics.CleanupStep("completed", started);
        }
        catch (Exception exception)
        {
            diagnostics.Failure("cleanup failed", exception);
            diagnostics.CleanupStep("failed", started, exception);
            throw;
        }
        finally
        {
            diagnostics.EndRead();
        }
    }
}
