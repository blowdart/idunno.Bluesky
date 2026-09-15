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
}
