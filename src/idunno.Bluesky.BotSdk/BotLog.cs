// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

namespace idunno.Bluesky.BotSdk;

// Source-generated methods keep event IDs, severity, and named properties in one place.
// Console output is readable, but providers still receive structured values rather than
// preformatted interpolated strings. Never pass credentials or secret-bearing configuration.
/// <summary>Defines stable structured log events without retaining secrets or discarding exception details.</summary>
internal static partial class BotLog
{
    // The shared factory lets an enumeration's scope flow to SDK logs too. SimpleConsole
    // includes scopes and renders exception.ToString(), retaining inner exceptions and stacks.
    /// <summary>Configures readable UTC console logs with correlation scopes and complete exception details.</summary>
    /// <param name="logging">The host logging builder shared by application and SDK code.</param>
    public static void ConfigureConsole(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddSimpleConsole(console =>
        {
            console.IncludeScopes = true;
            console.TimestampFormat = "O";
            console.UseUtcTimestamp = true;
        });
    }

    /// <summary>Reports acquired ownership and the overlap-safety delay before work begins.</summary>
    [LoggerMessage(1, LogLevel.Information, "Ownership acquired by {Owner}; waiting until {ReadyAt} before processing.")]
    public static partial void Acquired(ILogger logger, Guid owner, DateTimeOffset readyAt);

    /// <summary>Reports a dependency recovery deadline and preserves an optional original exception.</summary>
    [LoggerMessage(2, LogLevel.Warning, "{Worker} is recovering from {Dependency}; retry at {RetryAt}.")]
    public static partial void Recovering(
        ILogger logger,
        string worker,
        string dependency,
        DateTimeOffset retryAt,
        Exception? exception = null);

    /// <summary>Reports an unexpected worker stop with its original failure details.</summary>
    [LoggerMessage(3, LogLevel.Error, "{Worker} stopped unexpectedly: {Reason}.")]
    public static partial void Failed(ILogger logger, string worker, string reason, Exception? exception);

    /// <summary>Warns that bot state is held only in the current process and is not production durable.</summary>
    [LoggerMessage(420, LogLevel.Warning, "The in-memory bot storage provider is not suitable for production. Replay progress, actions, credentials, leases, and rate limits are lost when the process exits.")]
    public static partial void InMemoryStorageNotSuitableForProduction(ILogger logger);

    /// <summary>Reports the saved replay start boundary and first-run eligibility cutoff.</summary>
    [LoggerMessage(4, LogLevel.Information, "Replay starts at sequence {AfterSequence}, with first-run eligibility time {StartedAt}, on {Service}.")]
    public static partial void ReplayStarting(ILogger logger, long afterSequence, DateTimeOffset startedAt, string service);

    /// <summary>Reports the number of actions committed together with their source event progress.</summary>
    [LoggerMessage(5, LogLevel.Information, "Enqueued {ActionCount} actions atomically at sequence {Sequence}.")]
    public static partial void Enqueued(
        ILogger logger,
        long sequence,
        int actionCount);

    /// <summary>Explicitly identifies intentional malformed-data loss rather than disguising it as recovery.</summary>
    /// <param name="logger">The structured diagnostic logger.</param>
    /// <param name="scope">The malformed-data scope being skipped.</param>
    /// <param name="sequence">The source sequence, if the malformed data provided one.</param>
    /// <param name="reason">The audited reason for data loss.</param>
    public static void Skipped(ILogger logger, string scope, long? sequence, string reason) =>
        SkippedCore(logger, scope, Optional(sequence, "unavailable in malformed data"), reason);

    /// <summary>Emits data-loss diagnostics with an explicit explanation when the sequence is unavailable.</summary>
    [LoggerMessage(6, LogLevel.Warning, "DATA LOSS: skipping {Scope} at sequence {Sequence}: {Reason}.")]
    private static partial void SkippedCore(ILogger logger, string scope, object sequence, string reason);

    /// <summary>Reports an audited malformed-post skip with the original exception.</summary>
    [LoggerMessage(31, LogLevel.Warning, "DATA LOSS: skipping post {SourceUri} at sequence {Sequence}: {Reason}.")]
    public static partial void PostSkipped(ILogger logger, string sourceUri, long sequence, string reason, Exception exception);

    /// <summary>Reports bounded recovery from an archive generation change using durable state.</summary>
    [LoggerMessage(7, LogLevel.Warning, "Archive generation mismatch; re-enumeration {Attempt}/5 uses the latest durable checkpoint.")]
    public static partial void GenerationChanged(ILogger logger, int attempt);

    /// <summary>Describes a simulated action without claiming any remote effect occurred.</summary>
    [LoggerMessage(8, LogLevel.Information, "Developer mode: would perform {ActionKind} for {SourceUri}, action key {ActionKey}. No remote action was performed.")]
    public static partial void WouldAct(
        ILogger logger,
        string actionKind,
        string sourceUri,
        string actionKey);

    /// <summary>Reports a newly committed terminal action outcome, including its remote URI and reconciliation reason.</summary>
    public static void Outcome(
        ILogger logger,
        string actionKind,
        string actionKey,
        string sourceUri,
        string outcome,
        string? remoteUri,
        string? reason) =>
        OutcomeCore(
            logger,
            actionKind,
            actionKey,
            sourceUri,
            outcome,
            remoteUri ?? "not applicable; no remote URI",
            reason ?? "not provided");

    /// <summary>Emits action outcomes with explicit descriptions for absent remote identities and reasons.</summary>
    [LoggerMessage(9, LogLevel.Information, "Action {ActionKind}/{ActionKey} for {SourceUri} has outcome {Outcome}; remote URI {RemoteUri}; reason {Reason}.")]
    private static partial void OutcomeCore(
        ILogger logger,
        string actionKind,
        string actionKey,
        string sourceUri,
        string outcome,
        string remoteUri,
        string reason);

    /// <summary>Reports a durably scheduled action retry without treating an uncertain write as absent.</summary>
    [LoggerMessage(10, LogLevel.Warning, "Action for {SourceUri} is uncertain or retryable; attempt {Attempt}, retry at {RetryAt}, reason {Reason}.")]
    public static partial void Retrying(ILogger logger, string sourceUri, int attempt, DateTimeOffset retryAt, string reason);

    /// <summary>Reports the verified bot identity and whether saved credentials were restored.</summary>
    [LoggerMessage(11, LogLevel.Information, "Authenticated bot {Did}; session restored: {Restored}.")]
    public static partial void Authenticated(ILogger logger, string did, bool restored);

    /// <summary>Reports credential persistence without logging any tokens or encrypted bytes.</summary>
    [LoggerMessage(12, LogLevel.Debug, "Persisted refreshed credentials for {Did}.")]
    public static partial void CredentialsSaved(ILogger logger, string did);

    /// <summary>Reports HTTP status and cooldown advice without exposing headers or credentials.</summary>
    /// <param name="logger">The structured HTTP logger.</param>
    /// <param name="operation">The protocol operation name without query strings or credentials.</param>
    /// <param name="statusCode">The observed HTTP response status.</param>
    /// <param name="cooldownUntil">The server-advised future deadline, if one exists.</param>
    public static void HttpResult(ILogger logger, string operation, int statusCode, DateTimeOffset? cooldownUntil)
    {
        if (cooldownUntil is DateTimeOffset deadline)
        {
            HttpCooldown(logger, operation, statusCode, deadline);
        }
        else
        {
            HttpResultCore(logger, operation, statusCode);
        }
    }

    /// <summary>Emits an ordinary HTTP outcome without an inapplicable cooldown clause.</summary>
    [LoggerMessage(13, LogLevel.Information, "Outbound {Operation} returned HTTP {StatusCode}.")]
    private static partial void HttpResultCore(ILogger logger, string operation, int statusCode);

    /// <summary>Emits an HTTP outcome with the actual advised deadline as a typed structured property.</summary>
    [LoggerMessage(33, LogLevel.Information, "Outbound {Operation} returned HTTP {StatusCode}; cooldown until {CooldownUntil:O}.")]
    private static partial void HttpCooldown(ILogger logger, string operation, int statusCode, DateTimeOffset cooldownUntil);

    /// <summary>Reports pruning of expired accounting, audits, and terminal actions covered by durable progress.</summary>
    [LoggerMessage(14, LogLevel.Information, "Pruned {Count} expired rate reservations, skip audits, and safely covered terminal actions.")]
    public static partial void Pruned(ILogger logger, int count);

    /// <summary>Reports bounded read-slice expiration without claiming a specific network failure.</summary>
    [LoggerMessage(15, LogLevel.Warning, "Replay read slice completed without an event; reconnecting from durable progress.")]
    public static partial void ReadSliceEnded(ILogger logger);

    /// <summary>Reports an intentional worker stop.</summary>
    [LoggerMessage(16, LogLevel.Information, "{Worker} stopped gracefully.")]
    public static partial void Stopped(ILogger logger, string worker);

    /// <summary>Reports observed event-time lag and interval throughput, not inferred sequence arithmetic.</summary>
    [LoggerMessage(17, LogLevel.Information, "Jetstream is delivering events: {Position}. Processed sequence {Sequence}, event time {WitnessedAt:O}; {Lag} behind current UTC time. Consumed {EventCount} events in this interval ({EventsPerSecond:F1}/second).")]
    public static partial void ReplayProgress(
        ILogger logger,
        string position,
        long sequence,
        DateTimeOffset witnessedAt,
        TimeSpan lag,
        long eventCount,
        double eventsPerSecond);

    /// <summary>Reports a quiet read without claiming the socket is connected.</summary>
    public static void ReplayWaiting(ILogger logger, long? sequence, DateTimeOffset? witnessedAt, TimeSpan quietFor) =>
        ReplayWaitingCore(
            logger,
            Optional(sequence, "not yet observed"),
            Optional(witnessedAt, "not yet observed"),
            quietFor);

    /// <summary>Emits quiet-read observations without inventing an event before the first one arrives.</summary>
    [LoggerMessage(18, LogLevel.Information, "Waiting for the next Jetstream event or SDK recovery. Last consumed sequence {Sequence}, event time {WitnessedAt:O}; last event received {QuietFor} ago. This does not confirm the socket is connected.")]
    private static partial void ReplayWaitingCore(ILogger logger, object sequence, object witnessedAt, TimeSpan quietFor);

    /// <summary>Emits restored progress using explicitly labelled missing-cursor descriptions.</summary>
    /// <param name="logger">The structured replay logger.</param>
    /// <param name="processedThrough">The durable application processing boundary.</param>
    /// <param name="archiveCursor">The formatted saved archive fallback or a missing-cursor explanation.</param>
    /// <param name="liveCursor">The formatted saved live cursor or a missing-cursor explanation.</param>
    [LoggerMessage(19, LogLevel.Information, "Restored replay state: durable processed-through sequence {ProcessedThrough}, saved archive cursor {ArchiveCursor}, saved live cursor {LiveCursor}. Event-time lag will be reported once events arrive; a live cursor can still be catching up.")]
    private static partial void ReplayRestoredCore(ILogger logger, long processedThrough, string archiveCursor, string liveCursor);

    /// <summary>Formats optional saved cursors without inventing progress when no checkpoint has been saved.</summary>
    /// <param name="logger">The structured replay logger.</param>
    /// <param name="processedThrough">The saved processed-through sequence.</param>
    /// <param name="archiveCursor">The saved archive fallback cursor, if any.</param>
    /// <param name="liveCursor">The saved live cursor, if any.</param>
    public static void ReplayRestored(ILogger logger, long processedThrough, long? archiveCursor, long? liveCursor) =>
        ReplayRestoredCore(
            logger,
            processedThrough,
            archiveCursor?.ToString(CultureInfo.InvariantCulture) ?? "not yet saved; using the saved request boundary",
            liveCursor?.ToString(CultureInfo.InvariantCulture) ?? "not yet saved; replay will establish live progress");

    /// <summary>Reports an explicitly requested forward-only history skip while noting retained queued work.</summary>
    [LoggerMessage(20, LogLevel.Warning, "USER-DIRECTED HISTORY SKIP: eligibility cutoff changed to {Cutoff:O}; processed boundary {PreviousSequence} -> {AfterSequence}. Old checkpoint cleared, original filters preserved, queued replies retained. This override will not be reapplied during this process.")]
    public static partial void LookbackApplied(ILogger logger, DateTimeOffset cutoff, long previousSequence, long afterSequence);

    /// <summary>Reports archive fallback when an explicit cutoff is outside live retention.</summary>
    [LoggerMessage(30, LogLevel.Warning, "Lookback cutoff {Cutoff:O} is outside live retention. Resolving a conservative archive boundary instead; earlier events may still need to be read.")]
    public static partial void LookbackArchiveFallback(ILogger logger, DateTimeOffset cutoff, Exception exception);

    /// <summary>Explains why a first-run bot has no existing replay history to override.</summary>
    [LoggerMessage(32, LogLevel.Information, "Ignoring --lookback because no replay request has been saved. Starting normally with first-run eligibility.")]
    public static partial void LookbackIgnored(ILogger logger);

    /// <summary>Reports SDK identity and the saved replay plan for a correlated enumeration.</summary>
    public static void EnumerationStarting(
        ILogger logger,
        Guid replayEnumerationId,
        string? assemblyVersion,
        string? informationalVersion,
        string host,
        long? afterSequence,
        long? beforeSequence,
        string collections,
        string kinds,
        string dids,
        long processedThrough,
        long? planAfterSeq,
        long? replayAfterSeq,
        long? liveAfterSeq) =>
        EnumerationStartingCore(
            logger,
            replayEnumerationId,
            assemblyVersion ?? "unavailable",
            informationalVersion ?? "unavailable",
            host,
            Optional(afterSequence, "unbounded"),
            Optional(beforeSequence, "unbounded"),
            collections,
            kinds,
            dids,
            processedThrough,
            Optional(planAfterSeq, "not yet saved"),
            Optional(replayAfterSeq, "not yet saved"),
            Optional(liveAfterSeq, "not yet saved"));

    /// <summary>Emits replay bounds and saved cursors while retaining the numeric types of available values.</summary>
    [LoggerMessage(21, LogLevel.Information, "Replay enumeration {ReplayEnumerationId} starting. SDK assembly {AssemblyVersion}, informational version {InformationalVersion}; host {Host}; bounds ({AfterSequence}, {BeforeSequence}]; collections [{Collections}], kinds [{Kinds}], DIDs [{Dids}]. Resume processed {ProcessedThrough}, checkpoint plan {PlanAfterSeq}, replay fallback {ReplayAfterSeq}, live {LiveAfterSeq}.")]
    private static partial void EnumerationStartingCore(
        ILogger logger,
        Guid replayEnumerationId,
        string assemblyVersion,
        string informationalVersion,
        string host,
        object afterSequence,
        object beforeSequence,
        string collections,
        string kinds,
        string dids,
        long processedThrough,
        object planAfterSeq,
        object replayAfterSeq,
        object liveAfterSeq);

    /// <summary>Reports bounded cumulative work and wait measurements, distinguishing memory from durable progress.</summary>
    public static void EnumerationSummary(
        ILogger logger,
        Guid replayEnumerationId,
        bool final,
        string phase,
        string exitReason,
        double elapsedSeconds,
        long eventsConsumed,
        long relevantEvents,
        double eventsPerSecond,
        long? processedThrough,
        long? durableThrough,
        long? planAfterSeq,
        long? replayAfterSeq,
        long? liveAfterSeq,
        long? sealedTipSeq,
        string? segmentName,
        int? nextBlockIndex,
        long? nextByteOffset,
        long checkpointSaves,
        long checkpointFailures,
        long handlingCount,
        double handlingTotalMs,
        double handlingMaxMs,
        long databaseCount,
        double databaseTotalMs,
        double databaseMaxMs,
        long checkpointCount,
        double checkpointTotalMs,
        double checkpointMaxMs,
        long readCount,
        double readWaitTotalMs,
        double readWaitMaxMs,
        DateTimeOffset? witnessedAt,
        TimeSpan? witnessedLag) =>
        EnumerationSummaryCore(
            logger,
            replayEnumerationId,
            final,
            phase,
            exitReason,
            elapsedSeconds,
            eventsConsumed,
            relevantEvents,
            eventsPerSecond,
            Optional(processedThrough, "not yet established"),
            Optional(durableThrough, "not yet established"),
            Optional(planAfterSeq, "not yet saved"),
            Optional(replayAfterSeq, "not yet saved"),
            Optional(liveAfterSeq, "not yet saved"),
            Optional(sealedTipSeq, "not yet saved"),
            segmentName ?? "no saved archive segment",
            Optional(nextBlockIndex, "no saved archive position"),
            Optional(nextByteOffset, "no saved archive position"),
            checkpointSaves,
            checkpointFailures,
            handlingCount,
            handlingTotalMs,
            handlingMaxMs,
            databaseCount,
            databaseTotalMs,
            databaseMaxMs,
            checkpointCount,
            checkpointTotalMs,
            checkpointMaxMs,
            readCount,
            readWaitTotalMs,
            readWaitMaxMs,
            Optional(witnessedAt, "not yet observed"),
            Optional(witnessedLag, "unavailable; no witnessed event time"));

    /// <summary>Emits aggregate timings with explicit unavailable-state descriptions and typed available measurements.</summary>
    [LoggerMessage(22, LogLevel.Information, "Replay enumeration {ReplayEnumerationId} summary (final={Final}, phase={Phase}, exit={ExitReason}): {ElapsedSeconds:F1}s, cumulative consumed {EventsConsumed}, relevant committed {RelevantEvents}, interval rate {EventsPerSecond:F1}/s; in-memory processed {ProcessedThrough}, durable processed {DurableThrough}; last saved checkpoint plan {PlanAfterSeq}, replay fallback {ReplayAfterSeq}, live {LiveAfterSeq}, sealed tip {SealedTipSeq}, segment {SegmentName}, next block {NextBlockIndex}, byte offset {NextByteOffset}; saves {CheckpointSaves}, failures {CheckpointFailures}. Cumulative timings count/total-ms/max-ms: event handling {HandlingCount}/{HandlingTotalMs:F1}/{HandlingMaxMs:F1} (includes event DB work); database {DatabaseCount}/{DatabaseTotalMs:F1}/{DatabaseMaxMs:F1} (includes checkpoints); checkpoints {CheckpointCount}/{CheckpointTotalMs:F1}/{CheckpointMaxMs:F1}; read waits {ReadCount}/{ReadWaitTotalMs:F1}/{ReadWaitMaxMs:F1} (count completed; total/max include pending read, exclude DB callbacks; not evidence of slow consumption). Last processed event witnessed at {WitnessedAt:O}; wall-clock minus witnessed-time lag {WitnessedLag}.")]
    private static partial void EnumerationSummaryCore(
        ILogger logger,
        Guid replayEnumerationId,
        bool final,
        string phase,
        string exitReason,
        double elapsedSeconds,
        long eventsConsumed,
        long relevantEvents,
        double eventsPerSecond,
        object processedThrough,
        object durableThrough,
        object planAfterSeq,
        object replayAfterSeq,
        object liveAfterSeq,
        object sealedTipSeq,
        string segmentName,
        object nextBlockIndex,
        object nextByteOffset,
        long checkpointSaves,
        long checkpointFailures,
        long handlingCount,
        double handlingTotalMs,
        double handlingMaxMs,
        long databaseCount,
        double databaseTotalMs,
        double databaseMaxMs,
        long checkpointCount,
        double checkpointTotalMs,
        double checkpointMaxMs,
        long readCount,
        double readWaitTotalMs,
        double readWaitMaxMs,
        object witnessedAt,
        object witnessedLag);

    /// <summary>Reports the original enumeration exception and progress at the observed failure phase.</summary>
    public static void EnumerationFailure(
        ILogger logger,
        Guid replayEnumerationId,
        string phase,
        string reason,
        long? processedThrough,
        long? durableThrough,
        long? planAfterSeq,
        long? replayAfterSeq,
        long? liveAfterSeq,
        Exception exception) =>
        EnumerationFailureCore(
            logger,
            replayEnumerationId,
            phase,
            reason,
            Optional(processedThrough, "not yet established"),
            Optional(durableThrough, "not yet established"),
            Optional(planAfterSeq, "not yet saved"),
            Optional(replayAfterSeq, "not yet saved"),
            Optional(liveAfterSeq, "not yet saved"),
            exception);

    /// <summary>Emits the original replay failure with explicit missing-progress explanations.</summary>
    [LoggerMessage(23, LogLevel.Error, "Replay enumeration {ReplayEnumerationId}: {Reason}, phase {Phase}; in-memory processed {ProcessedThrough}, durable processed {DurableThrough}; saved checkpoint plan {PlanAfterSeq}, replay fallback {ReplayAfterSeq}, live {LiveAfterSeq}.")]
    private static partial void EnumerationFailureCore(
        ILogger logger,
        Guid replayEnumerationId,
        string phase,
        string reason,
        object processedThrough,
        object durableThrough,
        object planAfterSeq,
        object replayAfterSeq,
        object liveAfterSeq,
        Exception exception);

    /// <summary>Reports cleanup milestones and elapsed time while retaining associated exception details.</summary>
    [LoggerMessage(24, LogLevel.Information, "Replay enumeration {ReplayEnumerationId} cleanup: {Step}; reason {Reason}; elapsed {Elapsed}.")]
    public static partial void EnumerationCleanup(
        ILogger logger,
        Guid replayEnumerationId,
        string step,
        string reason,
        TimeSpan elapsed,
        Exception? exception = null);

    /// <summary>Reports observed cancellation signals before cleanup begins.</summary>
    public static void EnumerationCancelled(
        ILogger logger,
        Guid replayEnumerationId,
        string reason,
        string phase,
        long? processedThrough,
        long? durableThrough) =>
        EnumerationCancelledCore(
            logger,
            replayEnumerationId,
            reason,
            phase,
            Optional(processedThrough, "not yet established"),
            Optional(durableThrough, "not yet established"));

    /// <summary>Emits cancellation observations without inventing a progress boundary.</summary>
    [LoggerMessage(25, LogLevel.Information, "Replay enumeration {ReplayEnumerationId} cancellation observed before cleanup: {Reason}, phase {Phase}; in-memory processed {ProcessedThrough}, durable processed {DurableThrough}.")]
    private static partial void EnumerationCancelledCore(
        ILogger logger,
        Guid replayEnumerationId,
        string reason,
        string phase,
        object processedThrough,
        object durableThrough);

    /// <summary>Reports an existing recovery decision without turning a hypothesis into an observed cause.</summary>
    public static void EnumerationRecovery(
        ILogger logger,
        Guid replayEnumerationId,
        string reason,
        string phase,
        long? processedThrough,
        long? durableThrough,
        long? planAfterSeq,
        long? replayAfterSeq,
        long? liveAfterSeq,
        Exception? exception) =>
        EnumerationRecoveryCore(
            logger,
            replayEnumerationId,
            reason,
            phase,
            Optional(processedThrough, "not yet established"),
            Optional(durableThrough, "not yet established"),
            Optional(planAfterSeq, "not yet saved"),
            Optional(replayAfterSeq, "not yet saved"),
            Optional(liveAfterSeq, "not yet saved"),
            exception);

    /// <summary>Emits an existing recovery decision with the original exception and explicit missing-cursor descriptions.</summary>
    [LoggerMessage(26, LogLevel.Warning, "Replay enumeration {ReplayEnumerationId} existing recovery action: {Reason}, phase {Phase}; in-memory processed {ProcessedThrough}, durable processed {DurableThrough}; saved checkpoint plan {PlanAfterSeq}, replay fallback {ReplayAfterSeq}, live {LiveAfterSeq}.")]
    private static partial void EnumerationRecoveryCore(
        ILogger logger,
        Guid replayEnumerationId,
        string reason,
        string phase,
        object processedThrough,
        object durableThrough,
        object planAfterSeq,
        object replayAfterSeq,
        object liveAfterSeq,
        Exception? exception);

    /// <summary>Keeps present measurements typed while providing a readable description for absent ones.</summary>
    /// <typeparam name="T">The numeric, timestamp, or duration measurement type.</typeparam>
    /// <param name="value">The observed value, if available.</param>
    /// <param name="description">The explanation of why no value is available in this context.</param>
    /// <returns>The original boxed value, or its explicit absence description.</returns>
    private static object Optional<T>(T? value, string description) where T : struct =>
        value is T present ? present : description;
}
