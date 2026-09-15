// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

using idunno.AtProto;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
namespace idunno.Bluesky.AspNet.Authentication.Test;

public class EphemeralIdentityStoreTests : IdentityStoreTests
{
    protected override IIdentityStore CreateStore() => new EphemeralIdentityStore(NullLoggerFactory.Instance);

    protected override IIdentityStore CreateStore(IMeterFactory meterFactory) =>
        new EphemeralIdentityStore(NullLoggerFactory.Instance, meterFactory: meterFactory);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ConstructorRejectsASizeLimitWhichIsNotGreaterThanZero(int sizeLimit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EphemeralIdentityStore(NullLoggerFactory.Instance, sizeLimit: sizeLimit));
    }

    [Fact]
    public void ConstructorAcceptsAnOmittedSizeLimit()
    {
        EphemeralIdentityStore store = new(NullLoggerFactory.Instance, sizeLimit: null);

        Assert.NotNull(store);
    }

    [Fact]
    public async Task OnlyOneConcurrentCallerAcquiresTheRefreshLock()
    {
        // The ephemeral store holds the read and the write under a lock, so unlike the distributed store it can
        // guarantee a single winner. A second winner would spend a refresh token the first one had already used.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        EphemeralIdentityStore store = new(NullLoggerFactory.Instance);
        Did did = TestData.NewDid();

        string?[] results = await Task.WhenAll(
            Enumerable.Range(0, 64).Select(_ => Task.Run(() => store.StartRefresh(did, cancellationToken), cancellationToken)));

        Assert.Single(results, token => token is not null);
    }

    [Fact]
    public async Task AnExpiredRefreshLockIsNotReleasedByItsOriginalOwner()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        EphemeralIdentityStore store = new(
            NullLoggerFactory.Instance,
            refreshLockExpiration: TimeSpan.FromMilliseconds(50));

        Did did = TestData.NewDid();

        string? expiredToken = await store.StartRefresh(did, cancellationToken);
        Assert.NotNull(expiredToken);

        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);

        string? newOwnersToken = await store.StartRefresh(did, cancellationToken);
        Assert.NotNull(newOwnersToken);
        Assert.NotEqual(expiredToken, newOwnersToken);

        // The original owner finishing late must not release the lock the new owner is now holding.
        await store.EndRefresh(did, expiredToken, cancellationToken);

        Assert.True(await store.IsRefreshing(did, cancellationToken));
    }

    [Fact]
    public async Task EvictionForCapacityIsCounted()
    {
        // The capacity warning is logged once for the lifetime of the process, so the counter is the only thing which
        // shows how often the store is silently signing users out by evicting their identity.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        ServiceCollection serviceCollection = new();
        serviceCollection.AddMetrics();
        using ServiceProvider services = serviceCollection.BuildServiceProvider();

        IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
        var collector = new MetricCollector<long>(
            meterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.identitystore.evictions.total");

        // The backing cache is static and its size limit is fixed by whichever store was constructed first, so the
        // limit cannot be lowered here. Overfill it instead.
        EphemeralIdentityStore store = new(NullLoggerFactory.Instance, meterFactory: meterFactory);

        for (int identity = 0; identity < EphemeralIdentityStore.DefaultSizeLimit * 3; identity++)
        {
            await store.Add(TestData.ClaimsIdentity(TestData.NewDid()), cancellationToken);
        }

        // MemoryCache compacts on a thread pool thread once it is over capacity, so the eviction callbacks run after
        // the call to Add which triggered them has returned.
        using CancellationTokenSource compactionTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        compactionTimeout.CancelAfter(TimeSpan.FromSeconds(30));

        await collector.WaitForMeasurementsAsync(1, compactionTimeout.Token);

        Assert.All(collector.GetMeasurementSnapshot(), measurement => Assert.Equal(1, measurement.Value));
    }
}
