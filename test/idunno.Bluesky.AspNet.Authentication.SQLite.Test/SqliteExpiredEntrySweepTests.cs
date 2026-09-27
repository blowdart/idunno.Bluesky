// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using Duende.IdentityModel.OidcClient;

using idunno.AtProto;
using idunno.AtProto.Authentication;

namespace idunno.Bluesky.AspNet.Authentication.SQLite.Test;

/// <summary>
/// Tests that the stores delete the rows they have allowed to expire.
/// </summary>
/// <remarks>
/// <para>
///   Expiry is only ever a read filter, so these assert on the row counts in the tables rather than on what the stores
///   return. An unswept row is already invisible through the store, which is exactly the leak being tested for.
/// </para>
/// </remarks>
public class SqliteExpiredEntrySweepTests
{
    private const string IdentitiesTable = "idunno_bluesky_identities";
    private const string RefreshLocksTable = "idunno_bluesky_refresh_locks";
    private const string CorrelationStatesTable = "idunno_bluesky_correlation_states";

    private static readonly TimeSpan s_shortSweepInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan s_afterSweepIntervalElapses = TimeSpan.FromMilliseconds(250);

    [Fact]
    public async Task WritingAnIdentitySweepsIdentitiesWhichHaveExpired()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(
            database.ConnectionString,
            expiredEntrySweepInterval: s_shortSweepInterval);

        await store.Add(ClaimsIdentityFor(NewDid()), cancellationToken);
        Assert.Equal(1, database.CountRows(IdentitiesTable));

        database.ExpireRows(IdentitiesTable);
        await Task.Delay(s_afterSweepIntervalElapses, cancellationToken);

        await store.Add(ClaimsIdentityFor(NewDid()), cancellationToken);

        Assert.Equal(1, database.CountRows(IdentitiesTable));
    }

    [Fact]
    public async Task WritingAnIdentityLeavesIdentitiesWhichHaveNotExpired()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(
            database.ConnectionString,
            entryTimeToLive: TimeSpan.FromDays(1),
            expiredEntrySweepInterval: s_shortSweepInterval);

        await store.Add(ClaimsIdentityFor(NewDid()), cancellationToken);

        await Task.Delay(s_afterSweepIntervalElapses, cancellationToken);

        await store.Add(ClaimsIdentityFor(NewDid()), cancellationToken);

        Assert.Equal(2, database.CountRows(IdentitiesTable));
    }

    [Fact]
    public async Task WritingAnIdentitySweepsRefreshLocksWhichHaveBeenAbandoned()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(
            database.ConnectionString,
            expiredEntrySweepInterval: s_shortSweepInterval);

        Assert.NotNull(await store.StartRefresh(NewDid(), cancellationToken));
        Assert.Equal(1, database.CountRows(RefreshLocksTable));

        database.ExpireRows(RefreshLocksTable);
        await Task.Delay(s_afterSweepIntervalElapses, cancellationToken);

        await store.Add(ClaimsIdentityFor(NewDid()), cancellationToken);

        Assert.Equal(0, database.CountRows(RefreshLocksTable));
    }

    [Fact]
    public async Task WritingAnIdentityLeavesRefreshLocksWhichAreStillHeld()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(
            database.ConnectionString,
            refreshLockLength: TimeSpan.FromMinutes(5),
            expiredEntrySweepInterval: s_shortSweepInterval);

        Assert.NotNull(await store.StartRefresh(NewDid(), cancellationToken));

        await Task.Delay(s_afterSweepIntervalElapses, cancellationToken);

        await store.Add(ClaimsIdentityFor(NewDid()), cancellationToken);

        Assert.Equal(1, database.CountRows(RefreshLocksTable));
    }

    [Fact]
    public async Task AnIdentityStoreWithSweepingDisabledLeavesExpiredIdentities()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteIdentityStore store = new(
            database.ConnectionString,
            expiredEntrySweepInterval: TimeSpan.Zero);

        await store.Add(ClaimsIdentityFor(NewDid()), cancellationToken);

        database.ExpireRows(IdentitiesTable);

        await store.Add(ClaimsIdentityFor(NewDid()), cancellationToken);

        Assert.Equal(2, database.CountRows(IdentitiesTable));
    }

    [Fact]
    public async Task AddingCorrelationStateSweepsCorrelationStateWhichHasExpired()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteCorrelationStateCache cache = new(
            database.ConnectionString,
            expiredEntrySweepInterval: s_shortSweepInterval);

        await cache.AddOAuthLoginState(Guid.NewGuid(), LoginState(), cancellationToken);
        Assert.Equal(1, database.CountRows(CorrelationStatesTable));

        database.ExpireRows(CorrelationStatesTable);
        await Task.Delay(s_afterSweepIntervalElapses, cancellationToken);

        await cache.AddOAuthLoginState(Guid.NewGuid(), LoginState(), cancellationToken);

        Assert.Equal(1, database.CountRows(CorrelationStatesTable));
    }

    [Fact]
    public async Task AddingCorrelationStateLeavesCorrelationStateWhichHasNotExpired()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteCorrelationStateCache cache = new(
            database.ConnectionString,
            entryTimeToLive: TimeSpan.FromMinutes(15),
            expiredEntrySweepInterval: s_shortSweepInterval);

        await cache.AddOAuthLoginState(Guid.NewGuid(), LoginState(), cancellationToken);

        await Task.Delay(s_afterSweepIntervalElapses, cancellationToken);

        await cache.AddOAuthLoginState(Guid.NewGuid(), LoginState(), cancellationToken);

        Assert.Equal(2, database.CountRows(CorrelationStatesTable));
    }

    [Fact]
    public async Task ACorrelationStateCacheWithSweepingDisabledLeavesExpiredCorrelationState()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using TemporarySqliteDatabase database = new();
        SqliteCorrelationStateCache cache = new(
            database.ConnectionString,
            expiredEntrySweepInterval: TimeSpan.Zero);

        await cache.AddOAuthLoginState(Guid.NewGuid(), LoginState(), cancellationToken);

        database.ExpireRows(CorrelationStatesTable);

        await cache.AddOAuthLoginState(Guid.NewGuid(), LoginState(), cancellationToken);

        Assert.Equal(2, database.CountRows(CorrelationStatesTable));
    }

    [Fact]
    public void ANegativeSweepIntervalIsRejectedByTheIdentityStore()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SqliteIdentityStore("Data Source=:memory:", expiredEntrySweepInterval: TimeSpan.FromSeconds(-1)));

        Assert.Equal("expiredEntrySweepInterval", exception.ParamName);
    }

    [Fact]
    public void ANegativeSweepIntervalIsRejectedByTheCorrelationStateCache()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SqliteCorrelationStateCache("Data Source=:memory:", expiredEntrySweepInterval: TimeSpan.FromSeconds(-1)));

        Assert.Equal("expiredEntrySweepInterval", exception.ParamName);
    }

    private static Did NewDid() => new($"did:plc:{Guid.NewGuid():N}");

    private static ClaimsIdentity ClaimsIdentityFor(Did did)
    {
        List<Claim> claims =
        [
            new Claim(AtProtoClaims.Did, did, ClaimValueTypes.String, "https://bsky.social"),
            new Claim(AtProtoClaims.AccessToken, "access-token", ClaimValueTypes.String, "https://bsky.social"),
            new Claim(AtProtoClaims.RefreshToken, "refresh-token", ClaimValueTypes.String, "https://bsky.social"),
        ];

        return new ClaimsIdentity(claims, "Bluesky");
    }

    private static OAuthLoginState LoginState() =>
        new(
            new AuthorizeState
            {
                StartUrl = "https://bsky.social/oauth/authorize",
                State = "state",
                CodeVerifier = "code-verifier",
                RedirectUri = "https://localhost/callback"
            },
            "https://bsky.social",
            "https://bsky.social",
            "proof-key",
            Guid.NewGuid());
}
