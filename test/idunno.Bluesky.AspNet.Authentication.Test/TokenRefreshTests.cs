// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace idunno.Bluesky.AspNet.Authentication.Test;

/// <summary>
/// Covers what <see cref="BlueskyAuthenticationHandler"/> does when the credentials it reads out of the identity store
/// have expired and a refresh is needed.
/// </summary>
/// <remarks>
/// <para>
///   A real refresh talks to the authorization server, so these tests drive the handler with a store which reports a
///   refresh is already under way. That is the path a second concurrent request takes, and it runs entirely against
///   the store without any network access.
/// </para>
/// </remarks>
public class TokenRefreshTests
{
    /// <summary>
    /// An identity store which reports that a refresh is permanently in progress, so a request which needs a refresh
    /// takes the wait path rather than performing one.
    /// </summary>
    private sealed class RefreshingIdentityStore : IIdentityStore
    {
        private readonly ConcurrentDictionary<string, ClaimsIdentity> _identities = new(StringComparer.Ordinal);

        private readonly TaskCompletionSource _refreshDecisionReached = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Whether <see cref="IsRefreshing"/> reports a refresh is under way. When <see langword="false"/> the handler
        /// will try to take the refresh lock, which <see cref="StartRefresh"/> then denies.
        /// </summary>
        internal bool ReportRefreshInProgress { get; set; } = true;

        /// <summary>
        /// Completes once the handler has asked whether a refresh is under way.
        /// </summary>
        /// <remarks>
        /// <para>
        ///   The handler only asks after it has read the identity out of the store and found its credentials expired,
        ///   so a test which waits for this before changing the store cannot race that first lookup. Sleeping instead
        ///   leaves the change landing first on a loaded machine, which turns a refresh the request never reached into
        ///   an authentication phase miss.
        /// </para>
        /// </remarks>
        internal Task RefreshDecisionReached => _refreshDecisionReached.Task;

        internal int StartRefreshCallCount => _startRefreshCallCount;

        private int _startRefreshCallCount;

        public IdentityStoreEvents Events { get; set; } = new();

        public Task Add(ClaimsIdentity claimsIdentity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(claimsIdentity);
            _identities[claimsIdentity.FindFirst(AtProtoClaims.Did)!.Value] = claimsIdentity;
            return Task.CompletedTask;
        }

        public Task<ClaimsIdentity?> GetIdentity(Did did, CancellationToken cancellationToken = default) =>
            Task.FromResult(_identities.TryGetValue(did.ToString(), out ClaimsIdentity? identity) ? identity : null);

        public Task Remove(Did did, CancellationToken cancellationToken = default)
        {
            _identities.TryRemove(did.ToString(), out _);
            return Task.CompletedTask;
        }

        public Task Update(ClaimsIdentity identity, CancellationToken cancellationToken) => Add(identity, cancellationToken);

        // The lock is never granted, so every request which needs a refresh ends up waiting for one it believes is
        // already running.
        public Task<string?> StartRefresh(Did did, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _startRefreshCallCount);
            return Task.FromResult<string?>(null);
        }

        public Task<bool> EndRefresh(Did did, string? refreshLockToken, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> IsRefreshing(Did did, CancellationToken cancellationToken = default)
        {
            // Read the flag before releasing anyone waiting on it, so a test which flips it the moment it is released
            // cannot change the answer this call is in the middle of giving.
            bool isRefreshing = ReportRefreshInProgress;

            _refreshDecisionReached.TrySetResult();

            return Task.FromResult(isRefreshing);
        }
    }

    private static ClaimsIdentity IdentityWithExpiredCredentials(Did did)
    {
        // The handler decides a refresh is needed from the expiry inside the access token, so the token has to be well
        // formed and already expired.
        List<Claim> claims =
        [
            new Claim(AtProtoClaims.Did, did, ClaimValueTypes.String, "https://bsky.social"),
            new Claim(AtProtoClaims.AccessToken, TestData.Jwt(did, TimeSpan.FromMinutes(-30)), ClaimValueTypes.String, "https://bsky.social"),
            new Claim(AtProtoClaims.RefreshToken, TestData.Jwt(did), ClaimValueTypes.String, "https://bsky.social"),
            new Claim(AtProtoClaims.DPoPProof, "proof-key", ClaimValueTypes.String, "https://bsky.social"),
            new Claim(AtProtoClaims.DPoPNonce, "nonce", ClaimValueTypes.String, "https://bsky.social"),
        ];

        return new ClaimsIdentity(claims, AuthenticationTestHost.Scheme);
    }

    private static async Task<(AuthenticationTestHost Host, string Cookie, RefreshingIdentityStore Store)> HostNeedingARefresh(
        bool reportRefreshInProgress)
    {
        RefreshingIdentityStore store = new() { ReportRefreshInProgress = reportRefreshInProgress };

        AuthenticationTestHost host = await AuthenticationTestHost.Create(store, options =>
        {
            // Keep the wait loop short. The point of these tests is which path is taken, not how long the handler is
            // prepared to wait for a refresh which is never going to finish.
            options.MaxRefreshChecks = 2;
            options.RefreshCheckWait = TimeSpan.FromMilliseconds(10);
        });

        Did did = TestData.NewDid();

        // Sign in with unexpired credentials so the cookie is written, then swap the stored identity for one whose
        // credentials have expired. That is what the store looks like once a token ages out mid-session.
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did));

        await store.Add(IdentityWithExpiredCredentials(did), TestContext.Current.CancellationToken);

        return (host, cookie, store);
    }

    [Fact]
    public async Task ARequestWhichQueuesBehindARefreshAlreadyUnderWayIsCountedAsOrdinaryQueueing()
    {
        (AuthenticationTestHost host, string cookie, RefreshingIdentityStore store) = await HostNeedingARefresh(reportRefreshInProgress: true);

        await using (host)
        {
            var waits = new MetricCollector<long>(
                host.MeterFactory,
                BlueskyAuthenticationMetrics.MeterName,
                "idunno.bluesky.aspnet.authentication.tokenrefreshwaits.total");

            using HttpResponseMessage response = await host.GetWithCookie("/test/authenticate", cookie);

            Assert.Equal(
                BlueskyAuthenticationMetrics.TokenRefreshWaitReasonRefreshInProgress,
                Assert.Single(waits.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.TokenRefreshWaitReasonTagName]);

            // A request which can see a refresh running does not try to take the lock.
            Assert.Equal(0, store.StartRefreshCallCount);
        }
    }

    [Fact]
    public async Task ARequestWhichLosesTheRaceForTheRefreshLockIsCountedAsContention()
    {
        // Ordinary queueing and genuine contention both end up waiting, but only the second says requests are arriving
        // closely enough together to collide, which is what a shared refresh lock is there to handle.
        (AuthenticationTestHost host, string cookie, RefreshingIdentityStore store) = await HostNeedingARefresh(reportRefreshInProgress: false);

        await using (host)
        {
            var waits = new MetricCollector<long>(
                host.MeterFactory,
                BlueskyAuthenticationMetrics.MeterName,
                "idunno.bluesky.aspnet.authentication.tokenrefreshwaits.total");

            using HttpResponseMessage response = await host.GetWithCookie("/test/authenticate", cookie);

            Assert.Equal(
                BlueskyAuthenticationMetrics.TokenRefreshWaitReasonLockDenied,
                Assert.Single(waits.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.TokenRefreshWaitReasonTagName]);

            Assert.Equal(1, store.StartRefreshCallCount);
        }
    }

    [Fact]
    public async Task AWaitForARefreshWhichNeverFinishesFailsAuthenticationRatherThanHangingTheRequest()
    {
        (AuthenticationTestHost host, string cookie, _) = await HostNeedingARefresh(reportRefreshInProgress: true);

        await using (host)
        {
            var outcomes = new MetricCollector<long>(
                host.MeterFactory,
                BlueskyAuthenticationMetrics.MeterName,
                "idunno.bluesky.aspnet.authentication.authentications.total");

            using HttpResponseMessage response = await host.GetWithCookie("/test/authenticate", cookie);

            Assert.Contains("succeeded=False", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

            Assert.Equal(
                "token_refresh_wait_expired",
                Assert.Single(outcomes.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.AuthenticationResultTagName]);
        }
    }

    [Fact]
    public async Task TheTimeARequestSpendsWaitingForARefreshIsRecorded()
    {
        (AuthenticationTestHost host, string cookie, _) = await HostNeedingARefresh(reportRefreshInProgress: true);

        await using (host)
        {
            var duration = new MetricCollector<double>(
                host.MeterFactory,
                BlueskyAuthenticationMetrics.MeterName,
                "idunno.bluesky.aspnet.authentication.tokenrefreshwaits.duration");

            using HttpResponseMessage response = await host.GetWithCookie("/test/authenticate", cookie);

            // Two checks ten milliseconds apart, so the wait is real but small. Asserting it is positive is enough to
            // show the histogram is measuring the wait rather than being recorded with a constant.
            Assert.True(Assert.Single(duration.GetMeasurementSnapshot()).Value > 0);
        }
    }

    [Fact]
    public async Task ARefreshWhichCompletesWhileTheRequestIsWaitingIsPickedUpFromTheStore()
    {
        // The refresh finishing between the handler deciding to wait and its next check must not fail the request,
        // because the credentials it needs are sitting in the store.
        RefreshingIdentityStore store = new() { ReportRefreshInProgress = true };

        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(store, options =>
        {
            options.MaxRefreshChecks = 10;
            options.RefreshCheckWait = TimeSpan.FromMilliseconds(20);
        });

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did));

        await store.Add(IdentityWithExpiredCredentials(did), TestContext.Current.CancellationToken);

        // Let the request reach the point where it decides to wait, then complete the refresh underneath it.
        Task<HttpResponseMessage> authenticate = host.GetWithCookie("/test/authenticate", cookie);

        await store.RefreshDecisionReached.WaitAsync(TestContext.Current.CancellationToken);

        await store.Add(TestData.AuthenticatedClaimsIdentity(did), TestContext.Current.CancellationToken);
        store.ReportRefreshInProgress = false;

        using HttpResponseMessage response = await authenticate;

        Assert.Contains(
            "succeeded=True",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnIdentityWhichDisappearsWhileARefreshIsWaitedOnIsCountedAsAStoreMiss()
    {
        RefreshingIdentityStore store = new() { ReportRefreshInProgress = true };

        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(store, options =>
        {
            options.MaxRefreshChecks = 10;
            options.RefreshCheckWait = TimeSpan.FromMilliseconds(20);
        });

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did));

        await store.Add(IdentityWithExpiredCredentials(did), TestContext.Current.CancellationToken);

        var misses = new MetricCollector<long>(
            host.MeterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.identitystore.misses.total");

        Task<HttpResponseMessage> authenticate = host.GetWithCookie("/test/authenticate", cookie);

        await store.RefreshDecisionReached.WaitAsync(TestContext.Current.CancellationToken);

        // The refresh finishes, but the identity it should have stored is gone.
        await store.Remove(did, TestContext.Current.CancellationToken);
        store.ReportRefreshInProgress = false;

        using HttpResponseMessage response = await authenticate;

        Assert.Equal(
            BlueskyAuthenticationMetrics.IdentityStoreMissPhaseTokenRefresh,
            Assert.Single(misses.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.IdentityStoreMissPhaseTagName]);
    }
}
