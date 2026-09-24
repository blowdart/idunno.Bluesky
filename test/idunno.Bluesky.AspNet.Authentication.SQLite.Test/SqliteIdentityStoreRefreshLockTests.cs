// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace idunno.Bluesky.AspNet.Authentication.SQLite.Test;

public class SqliteIdentityStoreRefreshLockTests
{
    private static readonly Did s_did = new("did:plc:ec72yg6n2sydzjvtovvdlxrk");
    private static readonly Did s_otherDid = new("did:plc:hfgp6pj3akhqxntgqwramlbg");

    private static readonly TimeSpan s_shortLockLength = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan s_afterShortLockExpires = TimeSpan.FromMilliseconds(750);

    [Fact]
    public async Task StartRefreshReturnsALockTokenWhenNoLockIsHeld()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(database.ConnectionString);

        Assert.NotNull(await store.StartRefresh(s_did, cancellationToken));
    }

    [Fact]
    public async Task StartRefreshIsDeniedWhileAnotherCallerHoldsTheLock()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(database.ConnectionString);

        Assert.NotNull(await store.StartRefresh(s_did, cancellationToken));
        Assert.Null(await store.StartRefresh(s_did, cancellationToken));
    }

    [Fact]
    public async Task StartRefreshSucceedsOnceTheHeldLockHasExpired()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(database.ConnectionString, refreshLockLength: s_shortLockLength);

        Assert.NotNull(await store.StartRefresh(s_did, cancellationToken));

        await Task.Delay(s_afterShortLockExpires, cancellationToken);

        Assert.NotNull(await store.StartRefresh(s_did, cancellationToken));
    }

    [Fact]
    public async Task EndRefreshReleasesTheLockAndReportsSuccessForItsOwner()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(database.ConnectionString);

        string? refreshLockToken = await store.StartRefresh(s_did, cancellationToken);
        Assert.NotNull(refreshLockToken);

        Assert.True(await store.EndRefresh(s_did, refreshLockToken, cancellationToken));
        Assert.False(await store.IsRefreshing(s_did, cancellationToken));
    }

    [Fact]
    public async Task EndRefreshReportsFailureAndLeavesTheLockInPlaceForANonOwner()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(database.ConnectionString);

        Assert.NotNull(await store.StartRefresh(s_did, cancellationToken));

        Assert.False(await store.EndRefresh(s_did, Guid.NewGuid().ToString("N"), cancellationToken));
        Assert.True(await store.IsRefreshing(s_did, cancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task EndRefreshReportsFailureForAMissingLockToken(string? refreshLockToken)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(database.ConnectionString);

        Assert.NotNull(await store.StartRefresh(s_did, cancellationToken));

        Assert.False(await store.EndRefresh(s_did, refreshLockToken, cancellationToken));
        Assert.True(await store.IsRefreshing(s_did, cancellationToken));
    }

    [Fact]
    public async Task EndRefreshReportsFailureWhenNoLockIsHeld()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(database.ConnectionString);

        Assert.False(await store.EndRefresh(s_did, Guid.NewGuid().ToString("N"), cancellationToken));
    }

    [Fact]
    public async Task EndRefreshDoesNotReleaseALockWhichExpiredAndWasReacquiredByAnotherCaller()
    {
        // The case the lock token exists for. A caller whose lock expires mid refresh must not release the lock the
        // caller which took over now holds, or a third caller could start refreshing the same DID alongside it.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();

        // Only the lock which is meant to expire is taken with a short lock length. The lock which takes over is taken
        // through a store using the default length, so the assertions which follow cannot race its expiry on a loaded
        // machine and see the takeover lock disappear rather than the behaviour under test.
        SqliteIdentityStore expiringStore = new(database.ConnectionString, refreshLockLength: s_shortLockLength);
        SqliteIdentityStore store = new(database.ConnectionString);

        string? expiredLockToken = await expiringStore.StartRefresh(s_did, cancellationToken);
        Assert.NotNull(expiredLockToken);

        await Task.Delay(s_afterShortLockExpires, cancellationToken);

        string? reacquiredLockToken = await store.StartRefresh(s_did, cancellationToken);
        Assert.NotNull(reacquiredLockToken);
        Assert.NotEqual(expiredLockToken, reacquiredLockToken);

        Assert.False(await store.EndRefresh(s_did, expiredLockToken, cancellationToken));
        Assert.True(await store.IsRefreshing(s_did, cancellationToken));

        Assert.True(await store.EndRefresh(s_did, reacquiredLockToken, cancellationToken));
    }

    [Fact]
    public async Task EndRefreshLogsAWarningWhenTheLockIsNotOwned()
    {
        // Losing the lock is invisible to an operator unless the store says so, and it is the signal that two requests
        // refreshed the same DID at once.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        using FakeLoggerProvider loggerProvider = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(
            builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddProvider(loggerProvider);
            });

        SqliteIdentityStore store = new(database.ConnectionString, loggerFactory: loggerFactory);

        Assert.NotNull(await store.StartRefresh(s_did, cancellationToken));
        Assert.False(await store.EndRefresh(s_did, Guid.NewGuid().ToString("N"), cancellationToken));

        FakeLogRecord record = Assert.Single(
            loggerProvider.Collector.GetSnapshot(),
            candidate => candidate.Level == LogLevel.Warning);

        Assert.Equal(264, record.Id.Id);
    }

    [Fact]
    public async Task EndRefreshDoesNotLogAWarningForItsOwner()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        using FakeLoggerProvider loggerProvider = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(
            builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddProvider(loggerProvider);
            });

        SqliteIdentityStore store = new(database.ConnectionString, loggerFactory: loggerFactory);

        string? refreshLockToken = await store.StartRefresh(s_did, cancellationToken);
        Assert.NotNull(refreshLockToken);
        Assert.True(await store.EndRefresh(s_did, refreshLockToken, cancellationToken));

        Assert.DoesNotContain(loggerProvider.Collector.GetSnapshot(), record => record.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task IsRefreshingReportsWhetherALockIsHeld()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(database.ConnectionString);

        Assert.False(await store.IsRefreshing(s_did, cancellationToken));

        string? refreshLockToken = await store.StartRefresh(s_did, cancellationToken);
        Assert.NotNull(refreshLockToken);
        Assert.True(await store.IsRefreshing(s_did, cancellationToken));

        Assert.True(await store.EndRefresh(s_did, refreshLockToken, cancellationToken));
        Assert.False(await store.IsRefreshing(s_did, cancellationToken));
    }

    [Fact]
    public async Task IsRefreshingReportsAnExpiredLockAsNotRefreshing()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(database.ConnectionString, refreshLockLength: s_shortLockLength);

        Assert.NotNull(await store.StartRefresh(s_did, cancellationToken));

        await Task.Delay(s_afterShortLockExpires, cancellationToken);

        Assert.False(await store.IsRefreshing(s_did, cancellationToken));
    }

    [Fact]
    public async Task LocksAreHeldIndependentlyForEachDid()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(database.ConnectionString);

        string? refreshLockToken = await store.StartRefresh(s_did, cancellationToken);
        Assert.NotNull(refreshLockToken);

        Assert.NotNull(await store.StartRefresh(s_otherDid, cancellationToken));

        Assert.True(await store.EndRefresh(s_did, refreshLockToken, cancellationToken));
        Assert.True(await store.IsRefreshing(s_otherDid, cancellationToken));
    }

    [Fact]
    public async Task OnlyOneOfManyConcurrentCallersAcquiresTheLock()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(database.ConnectionString);

        IEnumerable<Task<string?>> attempts = Enumerable
            .Range(0, 16)
            .Select(_ => Task.Run(async () => await store.StartRefresh(s_did, cancellationToken), cancellationToken));

        string?[] results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result is not null);
    }
}
