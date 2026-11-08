// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.BotSdk.DataProviders;

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

public sealed partial class RelationalProviderTests
{
    /// <summary>Uses database time for enqueue and preserves it through retry and provider recreation before one terminal observation.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task DurableTimingSurvivesRetryAndRestart(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        BotAction work = TestRecords.Work() with { EnqueuedAt = TestRecords.Start.AddDays(-1) };
        await store.Database.HandleAsync(sequence: 11, actions: [work], malformedReason: null, cancellationToken: CancellationToken.None);
        DateTimeOffset enqueue = await store.TimestampAsync("enqueued_at", "reply_work");
        Assert.Equal(enqueue, await store.TimestampAsync("next_retry", "reply_work"));
        Assert.NotEqual(work.EnqueuedAt, enqueue);
        await store.Database.MarkAttemptAsync(work, CancellationToken.None);
        await store.Database.RetryAsync(
            work: work,
            retryAt: enqueue.AddDays(1),
            reason: "reconcile later",
            cancellationToken: CancellationToken.None);
        Assert.Equal(enqueue, await store.TimestampAsync("enqueued_at", "reply_work"));

        await store.Database.ReleaseAsync(CancellationToken.None);
        IDatabaseProvider restarted = store.NewProvider();
        await restarted.InitializeAsync(CancellationToken.None);
        Assert.NotNull(await restarted.AcquireOrRenewAsync(CancellationToken.None));
        await store.SetReadyNowAsync();
        await store.SetRetryReadyAsync();
        BotAction restored = Assert.IsType<BotAction>(await restarted.NextWorkAsync(CancellationToken.None));
        Assert.Equal(enqueue, restored.EnqueuedAt);
        List<(string Status, double Seconds)> timings = [];
        ((IBotActionTimingSource)restarted).Timing.Completed += (status, seconds) => timings.Add((status, seconds));
        Assert.Equal(1, await restarted.FinishAsync(
            work: work with { EnqueuedAt = null },
            status: "sent",
            remoteReference: null,
            reason: "reconciled",
            cancellationToken: CancellationToken.None));
        DateTimeOffset completed = await store.TimestampAsync("completed_at", "reply_work");
        Assert.Equal(("sent", (completed - enqueue).TotalSeconds), Assert.Single(timings));
        Assert.Equal(0, await restarted.FinishAsync(
            work: restored,
            status: "sent",
            remoteReference: null,
            reason: "already terminal",
            cancellationToken: CancellationToken.None));
        Assert.Equal(0, await restarted.FinishAsync(
            work: restored with { ActionKey = "missing" },
            status: "not-sent",
            remoteReference: null,
            reason: "missing",
            cancellationToken: CancellationToken.None));
        Assert.Single(timings);
    }

    /// <summary>Distinguishes an empty queue, future retry backlog, and an eligible retry using the database clock.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task QueueAgesDistinguishFutureAndEligibleRetries(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        IBotDatabaseDiagnostics diagnostics = (IBotDatabaseDiagnostics)store.Database;
        BotOperationalSnapshot empty = await diagnostics.GetOperationalSnapshotAsync(CancellationToken.None);
        Assert.Null(empty.OldestUnfinishedAgeSeconds);
        Assert.Null(empty.OldestEligibleOverdueSeconds);
        Assert.Equal(0, empty.UnfinishedActionsWithUnknownEnqueueTime);
        BotAction work = TestRecords.Work();
        await store.Database.HandleAsync(sequence: 11, actions: [work], malformedReason: null, cancellationToken: CancellationToken.None);
        DateTimeOffset enqueue = await store.TimestampAsync("enqueued_at", "reply_work");
        await store.Database.RetryAsync(
            work: work,
            retryAt: enqueue.AddDays(1),
            reason: "future retry",
            cancellationToken: CancellationToken.None);
        BotOperationalSnapshot future = await diagnostics.GetOperationalSnapshotAsync(CancellationToken.None);
        Assert.NotNull(future.OldestUnfinishedAgeSeconds);
        Assert.Null(future.OldestEligibleOverdueSeconds);
        Assert.Equal(0, future.RetryDueActions);
        await store.Database.MarkAttemptAsync(work, CancellationToken.None);
        await store.SqlAsync(provider == "Postgres"
            ? "UPDATE reply_work SET enqueued_at = now() - interval '60 seconds', next_retry = now() - interval '30 seconds'"
            : provider == "Sqlite"
                ? "UPDATE reply_work SET enqueued_at = CAST(unixepoch('subsec') * 1000000 AS INTEGER) - 60000000, next_retry = CAST(unixepoch('subsec') * 1000000 AS INTEGER) - 30000000"
                : "UPDATE reply_work SET enqueued_at = TIMESTAMPDIFF(MICROSECOND, '1970-01-01', UTC_TIMESTAMP(6)) - 60000000, next_retry = TIMESTAMPDIFF(MICROSECOND, '1970-01-01', UTC_TIMESTAMP(6)) - 30000000");
        BotOperationalSnapshot due = await diagnostics.GetOperationalSnapshotAsync(CancellationToken.None);
        Assert.Equal(0, due.PendingActions);
        Assert.Equal(1, due.UncertainActions);
        Assert.Equal(1, due.RetryDueActions);
        Assert.Equal(30d, due.OldestUnfinishedAgeSeconds!.Value - due.OldestEligibleOverdueSeconds!.Value, precision: 5);
    }

    /// <summary>Leaves pre-migration enqueue time unknown without fabricating age or terminal latency.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task EnqueueMigrationKeepsOldRowsUnknown(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        BotAction work = TestRecords.Work();
        await store.Database.HandleAsync(sequence: 11, actions: [work], malformedReason: null, cancellationToken: CancellationToken.None);
        await store.SqlAsync("ALTER TABLE reply_work DROP COLUMN enqueued_at");
        await store.Database.InitializeAsync(CancellationToken.None);
        await store.Database.InitializeAsync(CancellationToken.None);
        BotAction old = Assert.IsType<BotAction>(await store.Database.NextWorkAsync(CancellationToken.None));
        Assert.Null(old.EnqueuedAt);
        BotOperationalSnapshot snapshot = await ((IBotDatabaseDiagnostics)store.Database).GetOperationalSnapshotAsync(CancellationToken.None);
        Assert.Equal(1, snapshot.UnfinishedActionsWithUnknownEnqueueTime);
        Assert.Null(snapshot.OldestUnfinishedAgeSeconds);
        Assert.NotNull(snapshot.OldestEligibleOverdueSeconds);
        BotAction fresh = work with { SourceSequence = 12, ActionKey = "fresh" };
        await store.Database.HandleAsync(sequence: 12, actions: [fresh], malformedReason: null, cancellationToken: CancellationToken.None);
        BotOperationalSnapshot mixed = await ((IBotDatabaseDiagnostics)store.Database).GetOperationalSnapshotAsync(CancellationToken.None);
        Assert.Equal(2, mixed.PendingActions);
        Assert.Equal(1, mixed.UnfinishedActionsWithUnknownEnqueueTime);
        Assert.Null(mixed.OldestUnfinishedAgeSeconds);
        Assert.NotNull(mixed.OldestEligibleOverdueSeconds);
        List<double> timings = [];
        ((IBotActionTimingSource)store.Database).Timing.Completed += (_, seconds) => timings.Add(seconds);
        await store.Database.FinishAsync(
            work: old,
            status: "not-sent",
            remoteReference: null,
            reason: "legacy",
            cancellationToken: CancellationToken.None);
        Assert.Empty(timings);
        Assert.Equal(0, (await ((IBotDatabaseDiagnostics)store.Database).GetOperationalSnapshotAsync(CancellationToken.None))
            .UnfinishedActionsWithUnknownEnqueueTime);
        Assert.NotNull((await store.Database.NextWorkAsync(CancellationToken.None))!.EnqueuedAt);
        Assert.NotNull((await ((IBotDatabaseDiagnostics)store.Database).GetOperationalSnapshotAsync(CancellationToken.None)).OldestUnfinishedAgeSeconds);
    }

    /// <summary>Omits observations for rejected writes, lost ownership, and negative database clock intervals.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task FailedOrSkewedCompletionDoesNotPublishTiming(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        BotAction work = TestRecords.Work();
        await store.Database.HandleAsync(sequence: 11, actions: [work], malformedReason: null, cancellationToken: CancellationToken.None);
        List<double> timings = [];
        ((IBotActionTimingSource)store.Database).Timing.Completed += (_, seconds) => timings.Add(seconds);
        Exception failure = await Assert.ThrowsAnyAsync<Exception>(() => store.Database.FinishAsync(
            work: work,
            status: "invalid",
            remoteReference: null,
            reason: "invalid status",
            cancellationToken: CancellationToken.None));
        Assert.True(store.Database.IsDatabaseException(failure));
        Assert.Empty(timings);
        Assert.Equal(1, await store.CountAsync("reply_work WHERE status = 'pending' AND completed_at IS NULL"));
        await store.ExpireLeaseAsync();
        await Assert.ThrowsAsync<OwnershipLostException>(() => store.Database.FinishAsync(
            work: work,
            status: "sent",
            remoteReference: null,
            reason: "lost ownership",
            cancellationToken: CancellationToken.None));
        Assert.Empty(timings);
        Assert.NotNull(await store.Database.AcquireOrRenewAsync(CancellationToken.None));
        await store.SetReadyNowAsync();
        DateTimeOffset enqueue = await store.TimestampAsync("enqueued_at", "reply_work");
        await store.SqlAsync(provider == "Postgres"
            ? "UPDATE reply_work SET enqueued_at = now() + interval '1 day'"
            : "UPDATE reply_work SET enqueued_at = enqueued_at + 86400000000");
        Assert.Equal(1, await store.Database.FinishAsync(
            work: work,
            status: "simulated",
            remoteReference: null,
            reason: "clock skew",
            cancellationToken: CancellationToken.None));
        Assert.Empty(timings);
        Assert.True(await store.TimestampAsync("enqueued_at", "reply_work") > enqueue);
    }
}

/// <summary>Checks timing observations for unknown timestamps, clock skew, and nonterminal outcomes.</summary>
public sealed class ActionTimingStorageTests
{
    /// <summary>Omits unknown and negative intervals without discarding valid zero-duration outcomes.</summary>
    [Fact]
    public void TimingOmitsUnknownAndNegativeIntervals()
    {
        BotActionTiming timing = new();
        List<(string Status, double Seconds)> completed = [];
        timing.Completed += (status, seconds) => completed.Add((status, seconds));
        timing.Record("sent", null, TestRecords.Start);
        timing.Record("sent", TestRecords.Start.AddSeconds(1), TestRecords.Start);
        timing.Record("pending", TestRecords.Start, TestRecords.Start);
        Assert.Empty(completed);
        timing.Record("simulated", TestRecords.Start, TestRecords.Start);
        Assert.Equal(("simulated", 0d), Assert.Single(completed));
    }
}
