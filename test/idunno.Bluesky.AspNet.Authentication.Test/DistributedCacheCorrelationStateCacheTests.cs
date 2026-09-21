// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Authentication.Test;

public class DistributedCacheCorrelationStateCacheTests : CorrelationStateCacheTests
{
    protected override ICorrelationStateCache CreateCache() =>
        new DistributedCacheCorrelationStateCache(TestData.DistributedCache());

    [Fact]
    public async Task EachCacheUsesItsOwnBackingStore()
    {
        DistributedCacheCorrelationStateCache first = new(TestData.DistributedCache());
        DistributedCacheCorrelationStateCache second = new(TestData.DistributedCache());

        Guid correlationId = Guid.NewGuid();
        await first.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId), TestContext.Current.CancellationToken);

        Assert.NotNull(await first.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken));
        Assert.Null(await second.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StateExpiresAfterItsTimeToLive()
    {
        DistributedCacheCorrelationStateCache cache = new(
            TestData.DistributedCache(),
            entryTimeToLive: TimeSpan.FromMilliseconds(50));

        Guid correlationId = Guid.NewGuid();
        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId), TestContext.Current.CancellationToken);

        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);

        Assert.Null(await cache.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken));
    }
}
