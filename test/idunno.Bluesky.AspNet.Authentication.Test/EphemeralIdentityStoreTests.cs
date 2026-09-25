// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;

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
    public async Task ADisposedStoreThrowsFromEveryOperationRatherThanUsingADisposedCache()
    {
        // MemoryCache throws on its own once disposed, but names itself when it does, which sends someone looking at
        // the wrong lifetime. The guards here name the store.
        EphemeralIdentityStore store = new(NullLoggerFactory.Instance);

        Did did = TestData.NewDid();
        await store.Add(TestData.ClaimsIdentity(did), TestContext.Current.CancellationToken);

        store.Dispose();

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        List<Func<Task>> operations =
        [
            () => store.Add(TestData.ClaimsIdentity(did), cancellationToken),
            () => store.GetIdentity(did, cancellationToken),
            () => store.Remove(did, cancellationToken),
            () => store.Update(TestData.ClaimsIdentity(did), cancellationToken),
            () => store.StartRefresh(did, cancellationToken),
            () => store.EndRefresh(did, "token", cancellationToken),
            () => store.IsRefreshing(did, cancellationToken),
        ];

        foreach (Func<Task> operation in operations)
        {
            ObjectDisposedException thrown = await Assert.ThrowsAsync<ObjectDisposedException>(operation);

            Assert.Equal(typeof(EphemeralIdentityStore).FullName, thrown.ObjectName);
        }
    }

    [Fact]
    public async Task ACancelledTokenIsHonouredByEveryOperationWhichAcceptsOne()
    {
        // Nothing here does real I/O, so without an explicit check a cancelled request would carry on writing to and
        // reading from the store after the caller had given up on it.
        using EphemeralIdentityStore store = new(NullLoggerFactory.Instance);

        using CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();

        CancellationToken cancelled = cancellationTokenSource.Token;
        Did did = TestData.NewDid();
        ClaimsIdentity identity = TestData.ClaimsIdentity(did);

        List<Func<Task>> operations =
        [
            () => store.Add(identity, cancelled),
            () => store.GetIdentity(did, cancelled),
            () => store.Remove(did, cancelled),
            () => store.Update(identity, cancelled),
            () => store.StartRefresh(did, cancelled),
            () => store.EndRefresh(did, "token", cancelled),
            () => store.IsRefreshing(did, cancelled),
        ];

        foreach (Func<Task> operation in operations)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(operation);
        }
    }

    [Fact]
    public void ADisposedStoreThrowsBeforeReturningItsTaskSoANonAwaitingCallerStillSees()
    {
        // The refresh lock methods are not async, so the guard fires eagerly. If they were async the exception would
        // be parked in the returned Task and a caller which never awaits would carry on against a disposed store.
        EphemeralIdentityStore store = new(NullLoggerFactory.Instance);

        store.Dispose();

        Did did = TestData.NewDid();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Assert.Throws<ObjectDisposedException>(() => { Task unawaited = store.StartRefresh(did, cancellationToken); });
        Assert.Throws<ObjectDisposedException>(() => { Task unawaited = store.EndRefresh(did, "token", cancellationToken); });
        Assert.Throws<ObjectDisposedException>(() => { Task unawaited = store.IsRefreshing(did, cancellationToken); });
    }

    [Fact]
    public void DisposingTheStoreTwiceIsHarmless()
    {
        EphemeralIdentityStore store = new(NullLoggerFactory.Instance);

        store.Dispose();

        Assert.Null(Xunit.Record.Exception(store.Dispose));
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

        using EphemeralIdentityStore store = new(NullLoggerFactory.Instance, sizeLimit: 8, meterFactory: meterFactory);

        for (int identity = 0; identity < 24; identity++)
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

    [Fact]
    public async Task AnIdentityAddedWhenTheStoreIsAtItsSizeLimitIsStoredRatherThanDiscarded()
    {
        // A size limited MemoryCache refuses the incoming entry when it is full rather than making room for it, and
        // says nothing about having done so, which loses the sign-in the identity belongs to.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using EphemeralIdentityStore store = new(NullLoggerFactory.Instance, sizeLimit: 8);

        for (int identity = 0; identity < 8; identity++)
        {
            await store.Add(TestData.ClaimsIdentity(TestData.NewDid()), cancellationToken);
        }

        for (int identity = 0; identity < 8; identity++)
        {
            Did did = TestData.NewDid();

            await store.Add(TestData.ClaimsIdentity(did), cancellationToken);

            Assert.NotNull(await store.GetIdentity(did, cancellationToken));
        }
    }

    [Fact]
    public async Task RefreshedCredentialsAreStoredWhenTheStoreIsAtItsSizeLimit()
    {
        // Losing an update is worse than losing an add, as the refresh token which produced the credentials being
        // stored has already been spent by the time they are discarded.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using EphemeralIdentityStore store = new(NullLoggerFactory.Instance, sizeLimit: 4);

        Did did = TestData.NewDid();
        await store.Add(TestData.ClaimsIdentity(did, accessToken: "first-token"), cancellationToken);

        for (int identity = 0; identity < 8; identity++)
        {
            await store.Add(TestData.ClaimsIdentity(TestData.NewDid()), cancellationToken);
        }

        await store.Update(TestData.ClaimsIdentity(did, accessToken: "second-token"), cancellationToken);

        ClaimsIdentity? retrieved = await store.GetIdentity(did, cancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("second-token", retrieved.FindFirst(AtProtoClaims.AccessToken)?.Value);
    }

    [Fact]
    public async Task TwoStoresDoNotShareTheIdentitiesTheyHold()
    {
        // An application configuring more than one scheme gets a store for each of them. Sharing identities between
        // them would let one scheme authenticate a user with credentials issued to another.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using EphemeralIdentityStore first = new(NullLoggerFactory.Instance);
        using EphemeralIdentityStore second = new(NullLoggerFactory.Instance);

        Did did = TestData.NewDid();
        await first.Add(TestData.ClaimsIdentity(did), cancellationToken);

        Assert.NotNull(await first.GetIdentity(did, cancellationToken));
        Assert.Null(await second.GetIdentity(did, cancellationToken));
    }

    [Fact]
    public async Task EachStoreHasItsOwnSizeLimit()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using EphemeralIdentityStore limited = new(NullLoggerFactory.Instance, sizeLimit: 2);
        using EphemeralIdentityStore unlimited = new(NullLoggerFactory.Instance, sizeLimit: 32);

        List<Did> dids = [TestData.NewDid(), TestData.NewDid(), TestData.NewDid()];

        foreach (Did did in dids)
        {
            await limited.Add(TestData.ClaimsIdentity(did), cancellationToken);
            await unlimited.Add(TestData.ClaimsIdentity(did), cancellationToken);
        }

        int heldByLimited = 0;
        int heldByUnlimited = 0;

        foreach (Did did in dids)
        {
            if (await limited.GetIdentity(did, cancellationToken) is not null)
            {
                heldByLimited++;
            }

            if (await unlimited.GetIdentity(did, cancellationToken) is not null)
            {
                heldByUnlimited++;
            }
        }

        Assert.Equal(2, heldByLimited);
        Assert.Equal(3, heldByUnlimited);
    }
}
