// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Time.Testing;

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
        FakeTimeProvider timeProvider = new();
        DistributedCacheCorrelationStateCache cache = new(
            new TimeProviderDistributedCache(timeProvider),
            entryTimeToLive: TimeSpan.FromMilliseconds(50));

        Guid correlationId = Guid.NewGuid();
        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId), TestContext.Current.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMilliseconds(50));

        Assert.Null(await cache.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CacheExpirationIsRelativeToTheBackendClock()
    {
        FakeTimeProvider backendTime = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        DistributedCacheCorrelationStateCache cache = new(
            new TimeProviderDistributedCache(backendTime),
            entryTimeToLive: TimeSpan.FromMinutes(1));

        Guid correlationId = Guid.NewGuid();
        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId), TestContext.Current.CancellationToken);

        Assert.NotNull(await cache.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken));

        backendTime.Advance(TimeSpan.FromMinutes(1));

        Assert.Null(await cache.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken));
    }
}
