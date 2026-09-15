// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.AspNet.Authentication.Test;

public class EphemeralCorrelationStateCacheTests : CorrelationStateCacheTests
{
    protected override ICorrelationStateCache CreateCache() => new EphemeralCorrelationStateCache(NullLoggerFactory.Instance);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ConstructorRejectsASizeLimitWhichIsNotGreaterThanZero(int sizeLimit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EphemeralCorrelationStateCache(NullLoggerFactory.Instance, sizeLimit: sizeLimit));
    }

    [Fact]
    public async Task LoginStateAddedWhenTheCacheIsAtItsSizeLimitIsStoredRatherThanDiscarded()
    {
        // A size limited MemoryCache refuses the incoming entry when it is full rather than making room for it, and
        // says nothing about having done so, which fails the login at the callback for no visible reason.
        using EphemeralCorrelationStateCache cache = new(NullLoggerFactory.Instance, sizeLimit: 8);

        for (int state = 0; state < 8; state++)
        {
            Guid filler = Guid.NewGuid();
            await cache.AddOAuthLoginState(filler, TestData.LoginState(filler));
        }

        for (int state = 0; state < 8; state++)
        {
            Guid correlationId = Guid.NewGuid();

            await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId));

            Assert.NotNull(await cache.GetOAuthLoginState(correlationId));
        }
    }

    [Fact]
    public async Task TwoCachesDoNotShareTheLoginStatesTheyHold()
    {
        using EphemeralCorrelationStateCache first = new(NullLoggerFactory.Instance);
        using EphemeralCorrelationStateCache second = new(NullLoggerFactory.Instance);

        Guid correlationId = Guid.NewGuid();
        await first.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId));

        Assert.NotNull(await first.GetOAuthLoginState(correlationId));
        Assert.Null(await second.GetOAuthLoginState(correlationId));
    }

    [Fact]
    public async Task OnlyOneConcurrentCallerTakesTheLoginState()
    {
        // The ephemeral cache holds the read and the removal under a lock, so unlike the distributed cache it can
        // guarantee a single winner. Two winners would let a replayed callback complete somebody else's login.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        EphemeralCorrelationStateCache cache = new(NullLoggerFactory.Instance);
        Guid correlationId = Guid.NewGuid();

        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId));

        OAuthLoginState?[] results = await Task.WhenAll(
            Enumerable.Range(0, 64).Select(_ => Task.Run(() => cache.TakeOAuthLoginState(correlationId), cancellationToken)));

        Assert.Single(results, state => state is not null);
    }

    [Fact]
    public async Task StateExpiresAfterItsTimeToLive()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        EphemeralCorrelationStateCache cache = new(
            NullLoggerFactory.Instance,
            entryTimeToLive: TimeSpan.FromMilliseconds(50));

        Guid correlationId = Guid.NewGuid();
        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId));

        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);

        Assert.Null(await cache.GetOAuthLoginState(correlationId));
    }
}
