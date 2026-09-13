// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.AspNet.Authentication.Test;

public class EphemeralIdentityStoreTests : IdentityStoreTests
{
    protected override IIdentityStore CreateStore() => new EphemeralIdentityStore(NullLoggerFactory.Instance);

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
}
