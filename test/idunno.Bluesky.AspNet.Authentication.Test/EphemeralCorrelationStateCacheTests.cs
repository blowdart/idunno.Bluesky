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
