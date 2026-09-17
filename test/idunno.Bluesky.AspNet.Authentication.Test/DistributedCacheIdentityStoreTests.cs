// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

using idunno.AtProto;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication.Test;

public class DistributedCacheIdentityStoreTests : IdentityStoreTests
{
    protected override IIdentityStore CreateStore() =>
        new DistributedCacheIdentityStore(TestData.DistributedCache(), NullLoggerFactory.Instance);

    protected override IIdentityStore CreateStore(IMeterFactory meterFactory) =>
        new DistributedCacheIdentityStore(TestData.DistributedCache(), NullLoggerFactory.Instance, meterFactory: meterFactory);

    [Fact]
    public void ASubclassCanRecordAgainstTheProtectedMetricsProperty()
    {
        // The Metrics property is protected so a custom store can record against the package's instruments. This
        // demonstrates that usage; BlueskyAuthenticationMetricsTests.InstrumentsArePubliclyReadable is what pins the
        // accessibility, as this assembly has InternalsVisibleTo access and so would compile either way.
        ServiceCollection serviceCollection = new();
        serviceCollection.AddMetrics();
        using ServiceProvider services = serviceCollection.BuildServiceProvider();

        IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
        var collector = new MetricCollector<long>(
            meterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.dataprotection.failures.total");

        MetricsRecordingIdentityStore store = new(TestData.DistributedCache(), NullLoggerFactory.Instance, meterFactory);

        store.RecordDataProtectionFailure();

        CollectedMeasurement<long> measurement = Assert.Single(collector.GetMeasurementSnapshot());

        Assert.Equal(1, measurement.Value);
    }

    private sealed class MetricsRecordingIdentityStore(
        IDistributedCache cache,
        ILoggerFactory loggerFactory,
        IMeterFactory meterFactory) : DistributedCacheIdentityStore(cache, loggerFactory, meterFactory: meterFactory)
    {
        public void RecordDataProtectionFailure() =>
            Metrics.DataProtectionFailures.Add(
                1,
                new KeyValuePair<string, object?>(
                    BlueskyAuthenticationMetrics.DataProtectionSourceTagName,
                    BlueskyAuthenticationMetrics.DataProtectionSourceIdentityStore));
    }

    [Fact]
    public void ConstructorThrowsWhenTheCacheIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DistributedCacheIdentityStore(null!, NullLoggerFactory.Instance));
    }

    [Fact]
    public async Task EachStoreUsesItsOwnCacheSoSchemesDoNotShareIdentities()
    {
        // The cache is an instance member precisely so an application can register a different one per scheme.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DistributedCacheIdentityStore first = new(TestData.DistributedCache(), NullLoggerFactory.Instance);
        DistributedCacheIdentityStore second = new(TestData.DistributedCache(), NullLoggerFactory.Instance);

        Did did = TestData.NewDid();
        await first.Add(TestData.ClaimsIdentity(did), cancellationToken);

        Assert.NotNull(await first.GetIdentity(did, cancellationToken));
        Assert.Null(await second.GetIdentity(did, cancellationToken));
    }

    [Fact]
    public async Task OptionsSupplyTheEntryAndRefreshLockLifetimes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DistributedCacheIdentityStore store = new(
            TestData.DistributedCache(),
            NullLoggerFactory.Instance,
            Options.Create(new BlueskyAuthenticationOptions
            {
                IdentityStoreEntryTimeToLive = TimeSpan.FromMilliseconds(50),
                RefreshLockLength = TimeSpan.FromMilliseconds(50)
            }));

        Did did = TestData.NewDid();

        await store.Add(TestData.ClaimsIdentity(did), cancellationToken);
        Assert.NotNull(await store.StartRefresh(did, cancellationToken));

        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);

        Assert.Null(await store.GetIdentity(did, cancellationToken));
        Assert.False(await store.IsRefreshing(did, cancellationToken));
    }

    [Fact]
    public async Task EndRefreshDoesNotRemoveALockAcquiredWhileItWasFindingItsOwnHadExpired()
    {
        // IDistributedCache has no atomic compare and delete, so EndRefresh reads the lock and then removes it. When this
        // caller's own lock has already expired the read finds nothing, and another node can acquire the lock in the gap
        // before the removal. Removing the key then releases a refresh which is genuinely in progress, and both callers
        // go on to spend the same single use refresh token.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        LockStealingCache cache = new(TestData.DistributedCache());
        DistributedCacheIdentityStore store = new(cache, NullLoggerFactory.Instance);

        Did did = TestData.NewDid();

        await store.EndRefresh(did, "a-token-whose-lock-has-already-expired", cancellationToken);

        Assert.True(cache.Stole, "the cache did not steal the lock, so the race this test covers was never run");
        Assert.True(await store.IsRefreshing(did, cancellationToken));
    }

    /// <summary>
    /// An <see cref="IDistributedCache"/> which, the first time a refresh lock is read and found to be absent, writes a lock
    /// for another caller before returning. That stands in for a second node acquiring the lock in the window between the
    /// read and the removal which follows it.
    /// </summary>
    private sealed class LockStealingCache(IDistributedCache inner) : IDistributedCache
    {
        private const string RefreshStorePrefix = "_tokenRefreshLock:";

        internal bool Stole { get; private set; }

        private void StealIfAbsent(string key)
        {
            if (Stole || !key.StartsWith(RefreshStorePrefix, StringComparison.Ordinal) || inner.Get(key) is not null)
            {
                return;
            }

            Stole = true;
            inner.Set(key, System.Text.Encoding.UTF8.GetBytes("a-token-belonging-to-another-node"), new DistributedCacheEntryOptions());
        }

        public byte[]? Get(string key)
        {
            byte[]? value = inner.Get(key);
            StealIfAbsent(key);
            return value;
        }

        public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            byte[]? value = await inner.GetAsync(key, token);
            StealIfAbsent(key);
            return value;
        }

        public void Refresh(string key) => inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);

        public void Remove(string key) => inner.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => inner.Set(key, value, options);

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            inner.SetAsync(key, value, options, token);
    }
}
