// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Events;
using idunno.Bluesky.AspNet.Authentication.Events;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace idunno.Bluesky.AspNet.Authentication.Test;

/// <summary>
/// The behaviour every <see cref="IIdentityStore"/> implementation in the package is expected to share.
/// </summary>
public abstract class IdentityStoreTests
{
    protected abstract IIdentityStore CreateStore();

    protected abstract IIdentityStore CreateStore(IMeterFactory meterFactory);

    [Fact]
    public async Task IdentityStoreOperationsAreTimedAndTaggedWithTheOperationTheyMeasure()
    {
        // Every authenticated request reads the store, so its latency is added to the application's. A single
        // untagged duration would hide a slow write behind a fast read.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        ServiceCollection serviceCollection = new();
        serviceCollection.AddMetrics();
        using ServiceProvider services = serviceCollection.BuildServiceProvider();

        IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
        var collector = new MetricCollector<double>(
            meterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.identitystore.operations.duration");

        IIdentityStore store = CreateStore(meterFactory);
        Did did = TestData.NewDid();

        await store.Add(TestData.ClaimsIdentity(did), cancellationToken);
        await store.GetIdentity(did, cancellationToken);
        await store.Update(TestData.ClaimsIdentity(did, accessToken: "updated"), cancellationToken);
        await store.Remove(did, cancellationToken);

        IReadOnlyList<CollectedMeasurement<double>> measurements = collector.GetMeasurementSnapshot();

        // An update is a store in terms of what it does, so it would be easy to record it as an add and leave the two
        // indistinguishable.
        Assert.Equal(
            [
                BlueskyAuthenticationMetrics.IdentityStoreOperationAdd,
                BlueskyAuthenticationMetrics.IdentityStoreOperationGet,
                BlueskyAuthenticationMetrics.IdentityStoreOperationUpdate,
                BlueskyAuthenticationMetrics.IdentityStoreOperationRemove,
            ],
            measurements.Select(measurement => measurement.Tags[BlueskyAuthenticationMetrics.IdentityStoreOperationTagName]));

        Assert.All(measurements, measurement => Assert.InRange(measurement.Value, 0, 60));
    }

    [Fact]
    public async Task AGetWhichFindsNothingIsStillTimed()
    {
        // A store which has started failing answers misses, so timing only the hits would hide it going slow.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        ServiceCollection serviceCollection = new();
        serviceCollection.AddMetrics();
        using ServiceProvider services = serviceCollection.BuildServiceProvider();

        IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
        var collector = new MetricCollector<double>(
            meterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.identitystore.operations.duration");

        IIdentityStore store = CreateStore(meterFactory);

        Assert.Null(await store.GetIdentity(TestData.NewDid(), cancellationToken));

        CollectedMeasurement<double> measurement = Assert.Single(collector.GetMeasurementSnapshot());

        Assert.Equal(
            BlueskyAuthenticationMetrics.IdentityStoreOperationGet,
            measurement.Tags[BlueskyAuthenticationMetrics.IdentityStoreOperationTagName]);
    }

    [Fact]
    public async Task GetIdentityTagsAnUnprotectableEntryAsAnIdentityStoreFailure()
    {
        // Correlation cookie failures land on the same counter, so without the tag an operator cannot tell a rolled
        // data protection key in the identity store from one in the sign-in path.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        ServiceCollection serviceCollection = new();
        serviceCollection.AddMetrics();
        using ServiceProvider services = serviceCollection.BuildServiceProvider();

        IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
        var collector = new MetricCollector<long>(
            meterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.dataprotection.failures.total");

        IIdentityStore store = CreateStore(meterFactory);
        Did did = TestData.NewDid();

        store.Events = new DataProtectingIdentityStoreEvents(new EphemeralDataProtectionProvider());
        await store.Add(TestData.ClaimsIdentity(did), cancellationToken);

        store.Events = new DataProtectingIdentityStoreEvents(new EphemeralDataProtectionProvider());
        Assert.Null(await store.GetIdentity(did, cancellationToken));

        CollectedMeasurement<long> measurement = Assert.Single(collector.GetMeasurementSnapshot());

        Assert.Equal(1, measurement.Value);
        Assert.True(measurement.ContainsTags(BlueskyAuthenticationMetrics.DataProtectionSourceTagName));
        Assert.Equal(
            BlueskyAuthenticationMetrics.DataProtectionSourceIdentityStore,
            measurement.Tags[BlueskyAuthenticationMetrics.DataProtectionSourceTagName]);
    }

    [Fact]
    public async Task AddThenGetIdentityRoundTripsTheClaims()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        await store.Add(TestData.ClaimsIdentity(did, accessToken: "the-access-token"), cancellationToken);

        ClaimsIdentity? retrieved = await store.GetIdentity(did, cancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("Bluesky", retrieved.AuthenticationType);
        Assert.Equal(did.ToString(), retrieved.FindFirst(AtProtoClaims.Did)?.Value);
        Assert.Equal("the-access-token", retrieved.FindFirst(AtProtoClaims.AccessToken)?.Value);
        Assert.Equal("refresh-token", retrieved.FindFirst(AtProtoClaims.RefreshToken)?.Value);
        Assert.Equal("https://bsky.social", retrieved.FindFirst(AtProtoClaims.Did)?.Issuer);
    }

    [Fact]
    public async Task GetIdentityReturnsNullForADidWhichWasNeverStored()
    {
        IIdentityStore store = CreateStore();

        Assert.Null(await store.GetIdentity(TestData.NewDid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddThrowsWhenTheIdentityIsNull()
    {
        IIdentityStore store = CreateStore();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.Add(null!, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-did")]
    [InlineData("did:")]
    [InlineData("urn:atproto:did")]
    public async Task AddThrowsWhenTheDidClaimIsMissingOrInvalid(string? didClaimValue)
    {
        IIdentityStore store = CreateStore();

        List<Claim> claims = [new Claim(AtProtoClaims.AccessToken, "access-token")];

        if (didClaimValue is not null)
        {
            claims.Add(new Claim(AtProtoClaims.Did, didClaimValue));
        }

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.Add(new ClaimsIdentity(claims, "Bluesky"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveRemovesTheStoredIdentity()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        await store.Add(TestData.ClaimsIdentity(did), cancellationToken);
        Assert.NotNull(await store.GetIdentity(did, cancellationToken));

        await store.Remove(did, cancellationToken);

        Assert.Null(await store.GetIdentity(did, cancellationToken));
    }

    [Fact]
    public async Task UpdateReplacesTheStoredIdentity()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        await store.Add(TestData.ClaimsIdentity(did, accessToken: "first-token"), cancellationToken);
        await store.Update(TestData.ClaimsIdentity(did, accessToken: "second-token"), cancellationToken);

        ClaimsIdentity? retrieved = await store.GetIdentity(did, cancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("second-token", retrieved.FindFirst(AtProtoClaims.AccessToken)?.Value);
    }

    [Fact]
    public async Task GetIdentityRethrowsWhenTheCancellationTokenIsCancelled()
    {
        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        await store.Add(TestData.ClaimsIdentity(did), TestContext.Current.CancellationToken);

        using CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();

        // A cancelled request must not look like a user whose identity has gone from the store.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.GetIdentity(did, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task GetIdentityReturnsNullWhenTheStoredIdentityIsCorrupt()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        store.Events = new IdentityStoreEvents
        {
            OnStoring = context =>
            {
                context.ReplaceIdentity([0x01, 0x02, 0x03]);
                return Task.CompletedTask;
            }
        };

        await store.Add(TestData.ClaimsIdentity(did), cancellationToken);

        Assert.Null(await store.GetIdentity(did, cancellationToken));
    }

    [Fact]
    public async Task StoringEventSeesTheIdentityBeforeItIsProtectedAndRetrievalSeesItAfterItIsUnprotected()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        byte[]? seenWhenStoring = null;
        byte[]? seenWhenRetrieved = null;

        store.Events = new DataProtectingIdentityStoreEvents(new EphemeralDataProtectionProvider())
        {
            OnStoring = context =>
            {
                seenWhenStoring = context.Identity.ToArray();
                return Task.CompletedTask;
            },
            OnRetrieved = context =>
            {
                seenWhenRetrieved = context.Identity.ToArray();
                return Task.CompletedTask;
            }
        };

        await store.Add(TestData.ClaimsIdentity(did), cancellationToken);
        ClaimsIdentity? retrieved = await store.GetIdentity(did, cancellationToken);

        Assert.NotNull(retrieved);
        Assert.NotNull(seenWhenStoring);
        Assert.NotNull(seenWhenRetrieved);

        // Protection is the outermost layer, so a user's delegates see the same plaintext on both legs.
        Assert.Equal(seenWhenStoring, seenWhenRetrieved);
    }

    [Fact]
    public async Task GetIdentityRemovesTheEntryWhenItCannotBeUnprotected()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        IdentityStoreEvents readable = new DataProtectingIdentityStoreEvents(new EphemeralDataProtectionProvider());

        store.Events = readable;
        await store.Add(TestData.ClaimsIdentity(did), cancellationToken);
        Assert.NotNull(await store.GetIdentity(did, cancellationToken));

        // A different provider means a different key ring, which is what a rolled or lost key looks like to the store.
        store.Events = new DataProtectingIdentityStoreEvents(new EphemeralDataProtectionProvider());
        Assert.Null(await store.GetIdentity(did, cancellationToken));

        // The unreadable entry should have been dropped rather than left for every subsequent request to fail on,
        // so even the events which could read it a moment ago no longer find anything.
        store.Events = readable;
        Assert.Null(await store.GetIdentity(did, cancellationToken));
    }

    [Fact]
    public async Task StartRefreshReturnsATokenWhenNoRefreshIsInProgress()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        Assert.False(await store.IsRefreshing(did, cancellationToken));

        string? refreshLockToken = await store.StartRefresh(did, cancellationToken);

        Assert.NotNull(refreshLockToken);
        Assert.NotEmpty(refreshLockToken);
        Assert.True(await store.IsRefreshing(did, cancellationToken));
    }

    [Fact]
    public async Task StartRefreshIsDeniedWhileARefreshIsAlreadyInProgress()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        // AT Proto refresh tokens are single use, so a second caller allowed in would spend a token the first
        // caller has already consumed.
        Assert.NotNull(await store.StartRefresh(did, cancellationToken));
        Assert.Null(await store.StartRefresh(did, cancellationToken));
    }

    [Fact]
    public async Task EndRefreshReleasesTheLockWhenTheTokenMatches()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        string? refreshLockToken = await store.StartRefresh(did, cancellationToken);
        await store.EndRefresh(did, refreshLockToken, cancellationToken);

        Assert.False(await store.IsRefreshing(did, cancellationToken));
        Assert.NotNull(await store.StartRefresh(did, cancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a-token-belonging-to-somebody-else")]
    public async Task EndRefreshDoesNotReleaseALockItDoesNotOwn(string? refreshLockToken)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        Assert.NotNull(await store.StartRefresh(did, cancellationToken));

        await store.EndRefresh(did, refreshLockToken, cancellationToken);

        Assert.True(await store.IsRefreshing(did, cancellationToken));
    }

    [Fact]
    public async Task RefreshLocksAreHeldPerDid()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did first = TestData.NewDid();
        Did second = TestData.NewDid();

        Assert.NotNull(await store.StartRefresh(first, cancellationToken));
        Assert.NotNull(await store.StartRefresh(second, cancellationToken));

        Assert.True(await store.IsRefreshing(first, cancellationToken));
    }

    [Fact]
    public async Task OnCredentialsUpdatedDoesNotOverwriteAnIdentityAnotherRequestHasAlreadyRefreshed()
    {
        // An agent raises CredentialsUpdated for a rotated DPoP nonce as well as for a refresh, and writes back the whole
        // credential. A request which only picks up a nonce does not hold the refresh lock, so it can be carrying tokens a
        // refresh on another request has already superseded. Writing those back would restore a refresh token the service
        // has spent, and the user would be signed out the next time the store's credentials needed refreshing.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();
        Uri service = new("https://bsky.social");

        DPoPAccessCredentials refreshed = new(
            service: service,
            accessJwt: TestData.Jwt(did, TimeSpan.FromHours(2)),
            refreshToken: "refresh-token-from-the-refresh",
            dPoPProofKey: "proof-key",
            dPoPNonce: "nonce-from-the-refresh");

        await store.Add(IIdentityStore.BuildClaimsIdentity(refreshed), cancellationToken);

        DPoPAccessCredentials superseded = new(
            service: service,
            accessJwt: TestData.Jwt(did, TimeSpan.FromHours(1)),
            refreshToken: "refresh-token-the-service-has-spent",
            dPoPProofKey: "proof-key",
            dPoPNonce: "nonce-rotated-on-the-in-flight-request");

        await store.OnCredentialsUpdated(new CredentialsUpdatedEventArgs(did, service, superseded), cancellationToken);

        ClaimsIdentity? stored = await store.GetIdentity(did, cancellationToken);

        Assert.NotNull(stored);
        Assert.Equal(refreshed.RefreshToken, stored.FindFirst(AtProtoClaims.RefreshToken)?.Value);
        Assert.Equal(refreshed.AccessJwt, stored.FindFirst(AtProtoClaims.AccessToken)?.Value);
    }

    [Fact]
    public async Task OnCredentialsUpdatedStoresCredentialsWhichSupersedeTheStoredIdentity()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();
        Uri service = new("https://bsky.social");

        DPoPAccessCredentials stale = new(
            service: service,
            accessJwt: TestData.Jwt(did, TimeSpan.FromHours(1)),
            refreshToken: "refresh-token-about-to-be-spent",
            dPoPProofKey: "proof-key",
            dPoPNonce: "nonce");

        await store.Add(IIdentityStore.BuildClaimsIdentity(stale), cancellationToken);

        DPoPAccessCredentials refreshed = new(
            service: service,
            accessJwt: TestData.Jwt(did, TimeSpan.FromHours(2)),
            refreshToken: "refresh-token-from-the-refresh",
            dPoPProofKey: "proof-key",
            dPoPNonce: "nonce-from-the-refresh");

        await store.OnCredentialsUpdated(new CredentialsUpdatedEventArgs(did, service, refreshed), cancellationToken);

        ClaimsIdentity? stored = await store.GetIdentity(did, cancellationToken);

        Assert.NotNull(stored);
        Assert.Equal(refreshed.RefreshToken, stored.FindFirst(AtProtoClaims.RefreshToken)?.Value);
        Assert.Equal(refreshed.AccessJwt, stored.FindFirst(AtProtoClaims.AccessToken)?.Value);
        Assert.Equal(refreshed.DPoPNonce, stored.FindFirst(AtProtoClaims.DPoPNonce)?.Value);
    }

    [Fact]
    public async Task UpdateIfNewerReportsThatItStoredCredentialsWhichSupersedeTheStoredIdentity()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();
        Uri service = new("https://bsky.social");

        DPoPAccessCredentials stale = new(
            service: service,
            accessJwt: TestData.Jwt(did, TimeSpan.FromHours(1)),
            refreshToken: "refresh-token-about-to-be-spent",
            dPoPProofKey: "proof-key",
            dPoPNonce: "nonce");

        await store.Add(IIdentityStore.BuildClaimsIdentity(stale), cancellationToken);

        DPoPAccessCredentials refreshed = new(
            service: service,
            accessJwt: TestData.Jwt(did, TimeSpan.FromHours(2)),
            refreshToken: "refresh-token-from-the-refresh",
            dPoPProofKey: "proof-key",
            dPoPNonce: "nonce-from-the-refresh");

        Assert.True(await store.UpdateIfNewer(refreshed, cancellationToken));
    }

    [Fact]
    public async Task UpdateIfNewerReportsThatItLeftCredentialsWhichTheStoredIdentitySupersedes()
    {
        // The handler reports this, as a write losing to a concurrent refresh means two requests refreshed the same DID.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();
        Uri service = new("https://bsky.social");

        DPoPAccessCredentials refreshed = new(
            service: service,
            accessJwt: TestData.Jwt(did, TimeSpan.FromHours(2)),
            refreshToken: "refresh-token-from-the-refresh",
            dPoPProofKey: "proof-key",
            dPoPNonce: "nonce-from-the-refresh");

        await store.Add(IIdentityStore.BuildClaimsIdentity(refreshed), cancellationToken);

        DPoPAccessCredentials superseded = new(
            service: service,
            accessJwt: TestData.Jwt(did, TimeSpan.FromHours(1)),
            refreshToken: "refresh-token-the-service-has-spent",
            dPoPProofKey: "proof-key",
            dPoPNonce: "nonce-rotated-on-the-in-flight-request");

        Assert.False(await store.UpdateIfNewer(superseded, cancellationToken));
    }
}
