// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Archive;

using idunno.Bluesky.BotSdk.DataProviders;

namespace idunno.Bluesky.BotSdk;

// This is the reading half of the bot, not the posting half. ReplayAsync starts from saved
// history, catches up, and then watches new events. For a matching post, we save complete
// action intents in the chosen database. SenderWorker executes them later; that durable queue is the "outbox".
// We also save how far we have read, but only after saving any work those events require.
// OwnershipWorker tells us when this process has permission to read.
/// <summary>Consumes replay sequentially and commits complete action batches before saving progress.</summary>
/// <param name="database">The chosen durable storage provider.</param>
/// <param name="ownership">The local permission signal backed by the database lease.</param>
/// <param name="options">The replay and account configuration.</param>
/// <param name="health">The in-memory worker deadline tracker.</param>
/// <param name="logger">The reader diagnostic logger.</param>
/// <param name="loggerFactory">The factory for SDK diagnostics and shared correlation scopes.</param>
/// <param name="time">The clock used for progress diagnostics and recovery delays.</param>
/// <param name="lookback">The optional one-time instruction to deliberately skip older history.</param>
/// <param name="policy">The application policy that chooses complete actions without remote writes.</param>
/// <param name="handlers">The registry used to validate policy output before durable handling.</param>
internal sealed class ReaderWorker(
    IDatabaseProvider database,
    Ownership ownership,
    BotOptions options,
    WorkerHealth health,
    ILogger<ReaderWorker> logger,
    ILoggerFactory loggerFactory,
    TimeProvider time,
    LookbackOverride lookback,
    IBotPolicy policy,
    ActionHandlerRegistry handlers) : BackgroundService
{
    private static readonly TimeSpan s_readSlice = TimeSpan.FromMinutes(5);
    private readonly GenerationRecovery _generationRecovery = new();

    /// <summary>Audits explicit JSON source-data failures without swallowing policy validation errors.</summary>
    /// <param name="policy">The application's event policy.</param>
    /// <param name="item">The replay event.</param>
    /// <param name="startedAt">The durable eligibility boundary.</param>
    /// <param name="malformed">The source-data exception, if parsing failed.</param>
    /// <returns>The actions selected by the policy, or an empty collection for malformed JSON.</returns>
    internal static IReadOnlyList<BotAction> EvaluatePolicy(
        IBotPolicy policy,
        JetstreamEvent item,
        DateTimeOffset startedAt,
        out JsonException? malformed)
    {
        malformed = null;
        try
        {
            return policy.Evaluate(item, startedAt);
        }
        catch (JsonException exception)
        {
            malformed = exception;
            return [];
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                CancellationToken leaseToken = await ownership.WaitAsync("reader", health, stoppingToken).ConfigureAwait(false);

                // Losing ownership cancels this enumeration just like shutdown. Any uncommitted
                // event will be delivered again from the durable checkpoint on the next attempt.
                using CancellationTokenSource active = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, leaseToken);
                ReplayDiagnostics diagnostics = new(time, logger);
                using IDisposable? scope = diagnostics.BeginScope();

                try
                {
                    await ReadAsync(diagnostics, stoppingToken, leaseToken, active.Token).ConfigureAwait(false);
                    throw new InvalidOperationException("Replay ended unexpectedly.");
                }
                catch (InvalidDataException exception) when (ReplyRecords.IsGenerationMismatch(exception) && _generationRecovery.Retries < 5)
                {
                    LogExit(exception, diagnostics, stoppingToken, leaseToken);

                    // Compaction may change an archive segment between planning and downloading.
                    // Start a fresh enumeration from saved state, with a bounded retry count.
                    // Other InvalidDataExceptions are not assumed to be generation races.
                    _generationRecovery.Retry();
                    BotLog.GenerationChanged(logger, _generationRecovery.Retries);
                    await RecoverAsync("archive generation", TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception) when (leaseToken.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
                {
                    LogExit(exception, diagnostics, stoppingToken, leaseToken);
                    await RecoverAsync("ownership", TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
                }
                catch (RateWaitException exception)
                {
                    LogExit(exception, diagnostics, stoppingToken, leaseToken);
                    await RecoverAsync(
                        "archive bandwidth",
                        TimeSpan.FromSeconds(
                        Math.Clamp((exception.Until - time.GetUtcNow()).TotalSeconds, 1, 30)),
                        stoppingToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is OwnershipLostException or TimeoutException ||
                    IsTransientHttpException(exception) || database.IsTransientDatabaseException(exception))
                {
                    LogExit(exception, diagnostics, stoppingToken, leaseToken);
                    await RecoverAsync(
                        database.IsDatabaseException(exception) ? "database" : "replay transport",
                        TimeSpan.FromSeconds(10),
                        stoppingToken,
                        exception).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception) when (stoppingToken.IsCancellationRequested)
                {
                    LogExit(exception, diagnostics, stoppingToken, leaseToken);
                    throw;
                }
                catch (Exception exception)
                {
                    diagnostics.ExitReason = "unexpected failure";
                    diagnostics.Failure("unexpected reader failure; propagating to StopHost", exception);
                    throw;
                }
                finally
                {
                    // Covers orderly shutdown/recovery, not hard process termination or native crashes.
                    diagnostics.Report(final: true);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            BotLog.Stopped(logger, "reader");
        }
        catch (Exception exception)
        {
            health.Fail("reader", exception.Message);
            BotLog.Failed(logger, "reader", exception.Message, exception);
            throw;
        }
    }

    /// <summary>Restores complete replay state, consumes events in order, and drains an outstanding read before cleanup.</summary>
    /// <param name="diagnostics">The bounded counters and correlation scope for this enumeration.</param>
    /// <param name="stoppingToken">The host shutdown token, retained to classify cancellation.</param>
    /// <param name="leaseToken">The ownership-loss token, retained to classify cancellation.</param>
    /// <param name="cancellationToken">The combined token controlling this replay attempt.</param>
    private async Task ReadAsync(
        ReplayDiagnostics diagnostics,
        CancellationToken stoppingToken,
        CancellationToken leaseToken,
        CancellationToken cancellationToken)
    {
        health.Set("reader", "restoring replay state", TimeSpan.FromSeconds(60));

        ReplayState state = await diagnostics.DatabaseAsync(() => database.LoadStateAsync(cancellationToken)).ConfigureAwait(false);
        diagnostics.Restored(state.ProcessedThrough);

        DateTimeOffset archiveCooldown = await diagnostics.DatabaseAsync(() => database.ArchiveCooldownAsync(cancellationToken)).ConfigureAwait(false);

        if (archiveCooldown > time.GetUtcNow())
        {
            throw new RateWaitException(archiveCooldown);
        }

        if (lookback.IgnoreIfUninitialized(state))
        {
            BotLog.LookbackIgnored(logger);
        }

        if (lookback.Cutoff is DateTimeOffset cutoff)
        {
            // This opt-in operation sacrifices unread history. Clear the in-memory override
            // only after its transaction commits, so recovery does not apply it repeatedly.
            if (cutoff <= state.StartedAt)
            {
                throw new InvalidOperationException("Forward-only --lookback requires existing replay state and a cutoff later than its eligibility start.");
            }

            SnapshotRequest replacement;

            try
            {
                long boundary = await TimestampBoundaryAsync(cutoff, state.Service, diagnostics, cancellationToken).ConfigureAwait(false);
                replacement = new SnapshotRequest { AfterSeq = boundary };
            }
            catch (JetstreamConnectionException exception) when (exception.ErrorDetail?.Error == "CursorTooOld")
            {
                BotLog.LookbackArchiveFallback(logger, cutoff, exception);
                replacement = await InitialRequestAsync(cutoff, diagnostics, cancellationToken).ConfigureAwait(false);
            }

            long after = await diagnostics.DatabaseAsync(() => database.OverrideLookbackAsync(cutoff, replacement, cancellationToken)).ConfigureAwait(false);
            lookback.Applied();
            BotLog.LookbackApplied(logger, cutoff, state.ProcessedThrough, after);
            state = await diagnostics.DatabaseAsync(() => database.LoadStateAsync(cancellationToken)).ConfigureAwait(false);
        }

        if (state.RequestJson is null)
        {
            SnapshotRequest initial = await InitialRequestAsync(state.StartedAt, diagnostics, cancellationToken).ConfigureAwait(false);
            await diagnostics.DatabaseAsync(() => database.SaveRequestAsync(initial, cancellationToken)).ConfigureAwait(false);
            state = await diagnostics.DatabaseAsync(() => database.LoadStateAsync(cancellationToken)).ConfigureAwait(false);
        }

        // Restore the request as well as the checkpoint: the SDK binds progress to its original
        // bounds, filters, and host. Reconstructing "similar" settings can invalidate recovery.
        diagnostics.Phase = "validating restored request and checkpoint";

        SnapshotRequest request = JsonSerializer.Deserialize(state.RequestJson!, BotJson.SnapshotRequest)
            ?? throw new InvalidDataException("Stored replay request is null.");

        if (request.AfterSeq is not > 0)
        {
            throw new InvalidDataException("Stored replay request has no deliberate positive starting boundary.");
        }

        SnapshotCheckpoint? saved = state.CheckpointJson is null ? null :
            JsonSerializer.Deserialize(state.CheckpointJson, BotJson.SnapshotCheckpoint)
                ?? throw new InvalidDataException("Stored replay checkpoint is null.");

        long processed = state.ProcessedThrough;
        _generationRecovery.Observe(processed);
        long checkpointSavedAt = time.GetTimestamp();
        diagnostics.Start(state.Service, request, processed, saved);
        BotLog.ReplayStarting(logger, request.AfterSeq.Value, state.StartedAt, state.Service);
        BotLog.ReplayRestored(logger, processed, saved?.ReplayAfterSeq ?? saved?.PlanAfterSeq, saved?.LiveAfterSeq);

        ReplayProgress progress = new(time, logger);

        ReplayClock clock = new(time);
#pragma warning disable CA2000 // ReplayCleanup performs bounded, instrumented asynchronous disposal in finally.
        AtProtoJetstream jetstream = new(
            uri: new Uri(state.Service),
            options: new JetstreamOptions
            {
                ApiKey = options.ArchiveApiKey,
                ReplayLiveBufferCapacity = options.ReplayLiveBufferCapacity,
                LoggerFactory = loggerFactory,
                TimeProvider = clock,
                ArchiveReadTimeout = TimeSpan.FromSeconds(30),
                CloseTimeout = TimeSpan.FromSeconds(10)
            });
#pragma warning restore CA2000

        try
        {
            void Save(SnapshotCheckpoint checkpoint)
            {
                // The SDK calls this after preceding yielded events have been consumed. Matching work
                // is already committed; ignored events can be replayed safely. Throttling checkpoint
                // writes avoids database overhead making the live consumer fall behind its buffer.
                if (time.GetElapsedTime(checkpointSavedAt) < TimeSpan.FromSeconds(1))
                {
                    return;
                }

                health.Set("reader", "persisting complete checkpoint", TimeSpan.FromSeconds(45));
                diagnostics.SaveCheckpoint(
                    checkpoint,
                    processed,
                    () => database.SaveCheckpoint(checkpoint, cancellationToken, processed));
                checkpointSavedAt = time.GetTimestamp();
                saved = checkpoint;
            }

            JetstreamArchiveErrorAction Skip(long? sequence, Exception exception)
            {
                // A null sequence means the SDK could not decode a whole block. Skipping it may lose
                // valid posts too; this is the explicit application policy, not a replay requirement.
                string scope = sequence is null ? "archive block" : "archive record";
                diagnostics.DatabaseWork(() => database.RecordSkip(sequence, scope, exception.Message));
                BotLog.Skipped(logger, scope, sequence, exception.Message);

                return sequence is null ? JetstreamArchiveErrorAction.SkipBlock : JetstreamArchiveErrorAction.SkipRecord;
            }

            using CancellationTokenSource enumeration = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Replay handles transient live disconnects and expired-cursor archive fallback internally.
            // An explicit enumerator lets us bound idle/recovery reads and close them before retrying.
            IAsyncEnumerator<JetstreamEvent> enumerator = jetstream.ReplayAsync(
                request,
                saved,
                Save,
                enumeration.Token,
                onArchiveError: Skip).GetAsyncEnumerator(enumeration.Token);

            Task<bool>? outstandingRead = null;

            try
            {
                while (true)
                {
                    DateTimeOffset baseDeadline = time.GetUtcNow() + s_readSlice;
                    diagnostics.BeginRead();

                    Task<bool> next = enumerator.MoveNextAsync().AsTask();
                    outstandingRead = next;

                    while (!next.IsCompleted)
                    {
                        // A quiet stream is not evidence of a hang. Allow a bounded read slice and
                        // real SDK retry/quota waits, without using last-reply age as a health signal.
                        progress.Waiting();
                        diagnostics.Report();

                        DateTimeOffset deadline = clock.Deadline(baseDeadline);
                        health.Set("reader", "replaying / waiting for events or SDK recovery", deadline - time.GetUtcNow());

                        if (time.GetUtcNow() >= deadline)
                        {
                            BotLog.ReadSliceEnded(logger);
                            diagnostics.ExitReason = "read-slice timeout";
                            throw new TimeoutException("Replay read slice expired; cancel and resume durable progress.");
                        }

                        await Task.WhenAny(next, Task.Delay(TimeSpan.FromSeconds(1), cancellationToken)).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (!await next.ConfigureAwait(false))
                    {
                        throw new InvalidOperationException("Replay stopped unexpectedly.");
                    }

                    diagnostics.EndRead();

                    JetstreamEvent item = enumerator.Current;
                    diagnostics.Consumed();
                    diagnostics.Phase = "handling event";

                    long handlingStarted = time.GetTimestamp();

                    try
                    {
                        long sequence = item.Sequence ?? throw new InvalidDataException("A replay event lacks a sequence.");

                        if (sequence <= processed)
                        {
                            // Still consume duplicates so SDK checkpoints can advance. Never repeat the
                            // application side effects just because archive recovery repeats an event.
                            continue;
                        }

                        health.Set("reader", "handling event", TimeSpan.FromSeconds(45));

                        IReadOnlyList<BotAction> work = EvaluatePolicy(policy, item, state.StartedAt, out JsonException? malformedException);
                        string? malformed = malformedException?.Message;

                        // Invalid policy output is a programming/configuration failure, not malformed source data.
                        foreach (BotAction action in work)
                        {
                            handlers.Validate(action);
                        }

                        AtUri? deletedUri = item is JetstreamCommitEvent { Commit.Operation: JetstreamCommitOperation.Delete } deleted
                            ? new AtUri($"at://{deleted.Did}/{deleted.Commit.Collection}/{deleted.Commit.RKey}") : null;

                        int inserted = 0;

                        if (work.Count > 0 || malformed is not null || deletedUri is not null)
                        {
                            // Commit every side effect before requesting the next event. Other events
                            // only advance our in-memory boundary until the next saved checkpoint.
                            diagnostics.Phase = "event database transaction";
                            inserted = await diagnostics.DatabaseAsync(
                                () => database.HandleAsync(sequence, work, malformed, cancellationToken, deletedUri)).ConfigureAwait(false);
                            diagnostics.DurableEvent(sequence);
                            diagnostics.Phase = "handling event";
                        }

                        processed = sequence;
                        _generationRecovery.Observe(processed);
                        diagnostics.Processed(sequence, item.WitnessedAt);

                        if (malformed is not null)
                        {
                            if (item is JetstreamCommitEvent post)
                            {
                                string sourceUri = $"at://{post.Did}/{post.Commit.Collection}/{post.Commit.RKey}";
                                BotLog.PostSkipped(logger, sourceUri, sequence, malformed, malformedException!);
                            }
                            else
                            {
                                BotLog.Skipped(logger, "post", sequence, malformed);
                            }
                        }

                        if (inserted > 0)
                        {
                            BotLog.Enqueued(logger, sequence, inserted);
                        }

                        progress.Observe(
                            sequence,
                            item.WitnessedAt
                            ?? throw new InvalidDataException("A replay event lacks a witnessed timestamp."));
                    }
                    finally
                    {
                        diagnostics.Handled(handlingStarted);
                        diagnostics.Report();
                    }
                }
            }
            catch (Exception exception)
            {
                // Record the initiating failure before cleanup can throw a second exception.
                diagnostics.EndRead();
                LogExit(exception, diagnostics, stoppingToken, leaseToken);
                throw;
            }
            finally
            {
                // MoveNextAsync and DisposeAsync must not race on the enumerator. Cancel the pending
                // read, wait for it to finish, then dispose. Cleanup has its own bounded wait rather
                // than inheriting the already-cancelled shutdown/lease token.
                health.Set("reader", "closing replay", TimeSpan.FromSeconds(45));
                await ReplayCleanup.CloseAsync(enumeration, outstandingRead, enumerator.DisposeAsync, diagnostics, time).ConfigureAwait(false);
            }
        }
        finally
        {
            // The SDK has its own asynchronous cleanup after the enumerator/CTS are finished.
            // Observe its duration without imposing a new timeout or changing disposal order.
            await ReplayCleanup.DisposeSdkAsync(jetstream.DisposeAsync, diagnostics, time).ConfigureAwait(false);
        }
    }

    /// <summary>Records the initiating failure once without confusing host shutdown, ownership loss, and read-slice expiry.</summary>
    /// <param name="exception">The initiating exception, preserved with its stack.</param>
    /// <param name="diagnostics">The enumeration's correlation and cleanup state.</param>
    /// <param name="stoppingToken">The token indicating host shutdown.</param>
    /// <param name="leaseToken">The token indicating lost ownership.</param>
    private void LogExit(
        Exception exception,
        ReplayDiagnostics diagnostics,
        CancellationToken stoppingToken,
        CancellationToken leaseToken)
    {
        if (ReferenceEquals(diagnostics.LoggedExit, exception))
        {
            return;
        }

        diagnostics.LoggedExit = exception;

        if (exception is OperationCanceledException)
        {
            diagnostics.ExitReason = ReplayDiagnostics.CancellationReason(
                stoppingToken.IsCancellationRequested,
                leaseToken.IsCancellationRequested,
                diagnostics.ExitReason == "read-slice timeout");

            if (diagnostics.ExitReason != "unexpected cancellation")
            {
                BotLog.EnumerationCancelled(
                    logger,
                    diagnostics.EnumerationId,
                    diagnostics.ExitReason,
                    diagnostics.Phase,
                    diagnostics.ProcessedThrough,
                    diagnostics.DurableThrough);

                return;
            }
        }
        else
        {
            string? recovery = exception switch
            {
                InvalidDataException invalid when ReplyRecords.IsGenerationMismatch(invalid) && _generationRecovery.Retries < 5 => "archive generation",
                RateWaitException => "archive bandwidth",
                OwnershipLostException => "ownership",
                _ when database.IsTransientDatabaseException(exception) => "database",
                HttpRequestException
                {
                    StatusCode: null or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
                    HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                } => "replay transport",
                TimeoutException => diagnostics.ExitReason == "read-slice timeout" ? "read-slice timeout" : "replay transport",
                _ => null
            };

            if (recovery is not null)
            {
                diagnostics.ExitReason = $"existing recovery action: {recovery}";
                BotLog.EnumerationRecovery(
                    logger,
                    diagnostics.EnumerationId,
                    recovery,
                    diagnostics.Phase,
                    diagnostics.ProcessedThrough,
                    diagnostics.DurableThrough,
                    diagnostics.SavedCheckpoint?.PlanAfterSeq,
                    diagnostics.SavedCheckpoint?.ReplayAfterSeq,
                    diagnostics.SavedCheckpoint?.LiveAfterSeq,
                    exception);

                return;
            }

            diagnostics.ExitReason = "unexpected failure";
        }

        diagnostics.Failure("read failed", exception);
    }

    private static bool IsTransientHttpException(Exception exception) =>
        exception is HttpRequestException requestException &&
        requestException.StatusCode is null or
            HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    /// <summary>Probes the selected service for a sequence boundary before the explicit lookback cutoff.</summary>
    /// <param name="cutoff">The fixed user-requested time boundary.</param>
    /// <param name="service">The original Jetstream host; cursors are not portable between hosts.</param>
    /// <param name="diagnostics">The enumeration's dependency timing tracker.</param>
    /// <param name="cancellationToken">The active enumeration token.</param>
    /// <returns>The exclusive archive boundary that still allows the probe event to be replayed.</returns>
    private async Task<long> TimestampBoundaryAsync(
        DateTimeOffset cutoff,
        string service,
        ReplayDiagnostics diagnostics,
        CancellationToken cancellationToken)
    {
        health.Set("reader", "resolving lookback timestamp", TimeSpan.FromSeconds(60));
        diagnostics.Phase = "resolving lookback timestamp";

        using CancellationTokenSource lookup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lookup.CancelAfter(TimeSpan.FromSeconds(30));

        // Probe without filters so a quiet account/collection cannot stall the lookup.
        // This client uses the same host and protected SDK transport as normal replay.
#pragma warning disable CA2000 // The explicit async enumerator cleanup below owns the SDK client lifetime.
        AtProtoJetstream jetstream = new(
            uri: new Uri(service),
            options: new JetstreamOptions
            {
                LoggerFactory = loggerFactory,
                CloseTimeout = TimeSpan.FromSeconds(10)
            });
#pragma warning restore CA2000
        await using (jetstream.ConfigureAwait(false))
        {
            try
            {
                IAsyncEnumerator<JetstreamEvent> enumerator = jetstream.StreamAsync(
                    cursor: LookbackOverride.TimestampCursor(cutoff),
                    cancellationToken: lookup.Token).GetAsyncEnumerator(lookup.Token);
                await using (enumerator.ConfigureAwait(false))
                {
                    if (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        return LookbackOverride.BoundaryBefore(enumerator.Current);
                    }
                }
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && lookup.IsCancellationRequested)
            {
                throw new TimeoutException("Timed out resolving the lookback timestamp; saved replay state was not changed.", exception);
            }
        }

        throw new InvalidDataException("Timestamp lookup ended without an event; saved replay state was not changed.");
    }

    /// <summary>Finds archive coverage for first startup without silently jumping over unavailable history.</summary>
    /// <param name="startedAt">The durable witnessed-time eligibility boundary.</param>
    /// <param name="diagnostics">The enumeration's dependency timing tracker.</param>
    /// <param name="cancellationToken">The active enumeration token.</param>
    /// <returns>The complete starting request, including source filters.</returns>
    private async Task<SnapshotRequest> InitialRequestAsync(
        DateTimeOffset startedAt,
        ReplayDiagnostics diagnostics,
        CancellationToken cancellationToken)
    {
        // Sequence values are not timestamps. Inspect segment metadata only; never enumerate history from zero.
        long startMicroseconds = (startedAt - DateTimeOffset.UnixEpoch).Ticks / 10;
        long? firstMatchingSequence = null;
        long maxSealedSequence = 0;
        long? earliestTime = null;
        string? cursor = null;
        do
        {
            health.Set("reader", "selecting first-run archive boundary", TimeSpan.FromSeconds(60));
            diagnostics.Phase = "selecting first-run archive boundary";

            AtProtoHttpResult<SegmentList> result = await AtProtoServer.ListSegments(
                new Uri(options.JetstreamService),
                options.ArchiveApiKey,
                limit: 1000,
                httpClientOptions: new HttpClientOptions(timeout: TimeSpan.FromSeconds(30)),
                cursor: cursor,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.StatusCode == HttpStatusCode.TooManyRequests)
            {
                DateTimeOffset until = result.HttpResponseHeaders?.RetryAfter?.Date ??
                    time.GetUtcNow() + (result.HttpResponseHeaders?.RetryAfter?.Delta ?? TimeSpan.FromMinutes(5));

                if (until <= time.GetUtcNow())
                {
                    until = time.GetUtcNow().AddSeconds(1);
                }

                if (until > time.GetUtcNow().AddDays(1))
                {
                    throw new InvalidOperationException("Archive Retry-After exceeds the one-day recovery deadline.");
                }

                await diagnostics.DatabaseAsync(() => database.SaveArchiveCooldownAsync(until, cancellationToken)).ConfigureAwait(false);
                throw new RateWaitException(until);
            }

            result.EnsureSucceeded();

            foreach (SegmentInfo segment in result.Result.Segments)
            {
                earliestTime = earliestTime is null ? segment.MinWitnessedAt : Math.Min(earliestTime.Value, segment.MinWitnessedAt);
                maxSealedSequence = Math.Max(maxSealedSequence, segment.MaxSeq);

                if (segment.MaxWitnessedAt >= startMicroseconds)
                {
                    firstMatchingSequence = firstMatchingSequence is null ? segment.MinSeq :
                        Math.Min(firstMatchingSequence.Value, segment.MinSeq);
                }
            }

            if (result.Result.Cursor == cursor && cursor is not null)
            {
                throw new InvalidDataException("Archive segment pagination did not advance.");
            }

            cursor = result.Result.Cursor;
        } while (cursor is not null);

        if (earliestTime is null || maxSealedSequence <= 0 || earliestTime > startMicroseconds)
        {
            throw new InvalidDataException("The archive cannot cover the recorded first-run time. Refusing to silently move the starting point.");
        }

        // Start just before a covering segment, then filter by witnessed time in the event policy.
        // If all sealed segments predate startup, replay starts at their tip and continues live.
        // Choosing sequence zero or jumping past unavailable coverage would hide data loss.
        long after = firstMatchingSequence is long first ? first - 1 : maxSealedSequence;

        if (after <= 0)
        {
            throw new InvalidDataException("The first-run boundary would start replay at zero. Explicit operator reconciliation is required.");
        }

        return new SnapshotRequest
        {
            AfterSeq = after,
            Collections = [new CollectionSelector("app.bsky.feed.post")],
            Kinds = [JetStreamEventKind.Commit]
        };
    }

    /// <summary>Reports a bounded dependency-recovery wait without changing durable replay progress.</summary>
    /// <param name="dependency">The unavailable dependency.</param>
    /// <param name="delay">The delay before another attempt.</param>
    /// <param name="cancellationToken">The host or ownership cancellation token.</param>
    /// <param name="exception">The original dependency exception, or <see langword="null"/> for an ordinary retry wait.</param>
    private async Task RecoverAsync(
        string dependency,
        TimeSpan delay,
        CancellationToken cancellationToken,
        Exception? exception = null)
    {
        health.Set("reader", "recovering", delay + TimeSpan.FromSeconds(10), dependency);
        BotLog.Recovering(logger, "reader", dependency, time.GetUtcNow() + delay, exception);
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }
}
