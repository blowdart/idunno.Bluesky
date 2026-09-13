// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication.Test;

public class DistributedCacheIdentityStoreTests : IdentityStoreTests
{
    protected override IIdentityStore CreateStore() =>
        new DistributedCacheIdentityStore(TestData.DistributedCache(), NullLoggerFactory.Instance);

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
