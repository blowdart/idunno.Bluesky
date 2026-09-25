// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.AspNet.Authentication.Test;

public class EphemeralProfileCacheTests
{
    private static ProfileCacheEntry Profile(string handle = "test.bsky.social") =>
        new(
            Handle: new Handle(handle),
            DisplayName: "Test",
            Description: null,
            Pronouns: null,
            Website: null,
            Avatar: null,
            Banner: null,
            Issuer: "https://bsky.social");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ConstructorRejectsASizeLimitWhichIsNotGreaterThanZero(int sizeLimit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EphemeralProfileCache(NullLoggerFactory.Instance, sizeLimit: sizeLimit));
    }

    [Fact]
    public async Task TwoCachesDoNotShareTheProfilesTheyHold()
    {
        // The cache used to be backed by a process wide static, so every instance saw every other instance's profiles,
        // and nothing a test or a host teardown did could clear them.
        using EphemeralProfileCache first = new(NullLoggerFactory.Instance);
        using EphemeralProfileCache second = new(NullLoggerFactory.Instance);

        Did did = TestData.NewDid();
        await first.Add(did, Profile());

        Assert.NotNull(await first.GetCachedValue(did));
        Assert.Null(await second.GetCachedValue(did));
    }

    [Fact]
    public async Task ACacheHoldsItsOwnTimeToLiveRatherThanTheOneAnotherCacheWasBuiltWith()
    {
        // Sharing storage meant sharing the expiry stamped on an entry at write time, so a cache configured with a
        // short timeout would happily serve an entry a long lived cache had written.
        using EphemeralProfileCache longLived = new(NullLoggerFactory.Instance, entryTimeToLive: TimeSpan.FromMinutes(30));
        using EphemeralProfileCache shortLived = new(NullLoggerFactory.Instance, entryTimeToLive: TimeSpan.FromMilliseconds(50));

        Did did = TestData.NewDid();

        await longLived.Add(did, Profile());
        await shortLived.Add(did, Profile());

        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);

        Assert.Null(await shortLived.GetCachedValue(did));
        Assert.NotNull(await longLived.GetCachedValue(did));
    }

    [Fact]
    public async Task AProfileExpiresAfterItsTimeToLive()
    {
        using EphemeralProfileCache cache = new(NullLoggerFactory.Instance, entryTimeToLive: TimeSpan.FromMilliseconds(50));

        Did did = TestData.NewDid();
        await cache.Add(did, Profile());

        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);

        Assert.Null(await cache.GetCachedValue(did));
    }

    [Fact]
    public async Task AProfileAddedWhenTheCacheIsAtItsSizeLimitIsStoredRatherThanDiscarded()
    {
        // A size limited MemoryCache refuses the incoming entry when it is full rather than making room for it, which
        // left the newest profile as the one which was never cached, and the hit rate falling away with no diagnostic.
        using EphemeralProfileCache cache = new(NullLoggerFactory.Instance, sizeLimit: 8);

        for (int entry = 0; entry < 8; entry++)
        {
            await cache.Add(TestData.NewDid(), Profile());
        }

        for (int entry = 0; entry < 8; entry++)
        {
            Did did = TestData.NewDid();

            await cache.Add(did, Profile());

            Assert.NotNull(await cache.GetCachedValue(did));
        }
    }

    [Fact]
    public async Task AddRejectsANullProfile()
    {
        using EphemeralProfileCache cache = new(NullLoggerFactory.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.Add(TestData.NewDid(), null!));
    }

    [Fact]
    public async Task ADisposedCacheThrowsRatherThanUsingAMemoryCacheWhichHasBeenDisposed()
    {
        // MemoryCache throws on its own once disposed, so the guards here are about which object the caller is told
        // about. Naming the inner cache sends someone looking at the wrong lifetime.
        EphemeralProfileCache cache = new(NullLoggerFactory.Instance);

        Did did = TestData.NewDid();
        await cache.Add(did, Profile());

        cache.Dispose();

        ObjectDisposedException onGet = await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.GetCachedValue(did));
        ObjectDisposedException onAdd = await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.Add(did, Profile()));

        Assert.Equal(typeof(EphemeralProfileCache).FullName, onGet.ObjectName);
        Assert.Equal(typeof(EphemeralProfileCache).FullName, onAdd.ObjectName);
    }

    [Fact]
    public void ADisposedCacheThrowsBeforeReturningItsTaskSoANonAwaitingCallerStillSees()
    {
        // The guard only fires eagerly because these methods are not async. If they were, the exception would be
        // parked in the returned Task and a caller which never awaits would carry on against a disposed cache.
        EphemeralProfileCache cache = new(NullLoggerFactory.Instance);

        cache.Dispose();

        Did did = TestData.NewDid();

        Assert.Throws<ObjectDisposedException>(() => { Task unawaited = cache.Add(did, Profile()); });
        Assert.Throws<ObjectDisposedException>(() => { Task unawaited = cache.GetCachedValue(did); });
    }

    [Fact]
    public void DisposingTwiceIsHarmless()
    {
        EphemeralProfileCache cache = new(NullLoggerFactory.Instance);

        cache.Dispose();

        Xunit.Record.Exception(cache.Dispose);
    }
}
