// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

using Microsoft.AspNetCore.DataProtection;

namespace idunno.Bluesky.AspNet.Authentication.Test;

/// <summary>
/// The behaviour every <see cref="IIdentityStore"/> implementation in the package is expected to share.
/// </summary>
public abstract class IdentityStoreTests
{
    protected abstract IIdentityStore CreateStore();

    [Fact]
    public async Task AddThenGetIdentityRoundTripsTheClaims()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        await store.Add(TestData.ClaimsIdentity(did, accessToken: "the-access-token"), cancellationToken);

        ClaimsIdentity? retrieved = await store.GetIdentity(did, cancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("Bluesky", retrieved.AuthenticationType);
        Assert.Equal(did.ToString(), retrieved.FindFirst(AtProtoClaims.Did)?.Value);
        Assert.Equal("the-access-token", retrieved.FindFirst(AtProtoClaims.AccessToken)?.Value);
        Assert.Equal("refresh-token", retrieved.FindFirst(AtProtoClaims.RefreshToken)?.Value);
        Assert.Equal("https://bsky.social", retrieved.FindFirst(AtProtoClaims.Did)?.Issuer);
    }

    [Fact]
    public async Task GetIdentityReturnsNullForADidWhichWasNeverStored()
    {
        IIdentityStore store = CreateStore();

        Assert.Null(await store.GetIdentity(TestData.NewDid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddThrowsWhenTheIdentityIsNull()
    {
        IIdentityStore store = CreateStore();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.Add(null!, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-did")]
    [InlineData("did:")]
    [InlineData("urn:atproto:did")]
    public async Task AddThrowsWhenTheDidClaimIsMissingOrInvalid(string? didClaimValue)
    {
        IIdentityStore store = CreateStore();

        List<Claim> claims = [new Claim(AtProtoClaims.AccessToken, "access-token")];

        if (didClaimValue is not null)
        {
            claims.Add(new Claim(AtProtoClaims.Did, didClaimValue));
        }

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.Add(new ClaimsIdentity(claims, "Bluesky"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveRemovesTheStoredIdentity()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        await store.Add(TestData.ClaimsIdentity(did), cancellationToken);
        Assert.NotNull(await store.GetIdentity(did, cancellationToken));

        await store.Remove(did, cancellationToken);

        Assert.Null(await store.GetIdentity(did, cancellationToken));
    }

    [Fact]
    public async Task UpdateReplacesTheStoredIdentity()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        await store.Add(TestData.ClaimsIdentity(did, accessToken: "first-token"), cancellationToken);
        await store.Update(TestData.ClaimsIdentity(did, accessToken: "second-token"), cancellationToken);

        ClaimsIdentity? retrieved = await store.GetIdentity(did, cancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("second-token", retrieved.FindFirst(AtProtoClaims.AccessToken)?.Value);
    }

    [Fact]
    public async Task GetIdentityRethrowsWhenTheCancellationTokenIsCancelled()
    {
        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        await store.Add(TestData.ClaimsIdentity(did), TestContext.Current.CancellationToken);

        using CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();

        // A cancelled request must not look like a user whose identity has gone from the store.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.GetIdentity(did, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task GetIdentityReturnsNullWhenTheStoredIdentityIsCorrupt()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        store.Events = new IdentityStoreEvents
        {
            OnStoring = context =>
            {
                context.ReplaceIdentity([0x01, 0x02, 0x03]);
                return Task.CompletedTask;
            }
        };

        await store.Add(TestData.ClaimsIdentity(did), cancellationToken);

        Assert.Null(await store.GetIdentity(did, cancellationToken));
    }

    [Fact]
    public async Task StoringEventSeesTheIdentityBeforeItIsProtectedAndRetrievalSeesItAfterItIsUnprotected()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        byte[]? seenWhenStoring = null;
        byte[]? seenWhenRetrieved = null;

        store.Events = new DataProtectingIdentityStoreEvents(new EphemeralDataProtectionProvider())
        {
            OnStoring = context =>
            {
                seenWhenStoring = context.Identity.ToArray();
                return Task.CompletedTask;
            },
            OnRetrieved = context =>
            {
                seenWhenRetrieved = context.Identity.ToArray();
                return Task.CompletedTask;
            }
        };

        await store.Add(TestData.ClaimsIdentity(did), cancellationToken);
        ClaimsIdentity? retrieved = await store.GetIdentity(did, cancellationToken);

        Assert.NotNull(retrieved);
        Assert.NotNull(seenWhenStoring);
        Assert.NotNull(seenWhenRetrieved);

        // Protection is the outermost layer, so a user's delegates see the same plaintext on both legs.
        Assert.Equal(seenWhenStoring, seenWhenRetrieved);
    }

    [Fact]
    public async Task GetIdentityRemovesTheEntryWhenItCannotBeUnprotected()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        IdentityStoreEvents readable = new DataProtectingIdentityStoreEvents(new EphemeralDataProtectionProvider());

        store.Events = readable;
        await store.Add(TestData.ClaimsIdentity(did), cancellationToken);
        Assert.NotNull(await store.GetIdentity(did, cancellationToken));

        // A different provider means a different key ring, which is what a rolled or lost key looks like to the store.
        store.Events = new DataProtectingIdentityStoreEvents(new EphemeralDataProtectionProvider());
        Assert.Null(await store.GetIdentity(did, cancellationToken));

        // The unreadable entry should have been dropped rather than left for every subsequent request to fail on,
        // so even the events which could read it a moment ago no longer find anything.
        store.Events = readable;
        Assert.Null(await store.GetIdentity(did, cancellationToken));
    }

    [Fact]
    public async Task StartRefreshReturnsATokenWhenNoRefreshIsInProgress()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        Assert.False(await store.IsRefreshing(did, cancellationToken));

        string? refreshLockToken = await store.StartRefresh(did, cancellationToken);

        Assert.NotNull(refreshLockToken);
        Assert.NotEmpty(refreshLockToken);
        Assert.True(await store.IsRefreshing(did, cancellationToken));
    }

    [Fact]
    public async Task StartRefreshIsDeniedWhileARefreshIsAlreadyInProgress()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        // AT Proto refresh tokens are single use, so a second caller allowed in would spend a token the first
        // caller has already consumed.
        Assert.NotNull(await store.StartRefresh(did, cancellationToken));
        Assert.Null(await store.StartRefresh(did, cancellationToken));
    }

    [Fact]
    public async Task EndRefreshReleasesTheLockWhenTheTokenMatches()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        string? refreshLockToken = await store.StartRefresh(did, cancellationToken);
        await store.EndRefresh(did, refreshLockToken, cancellationToken);

        Assert.False(await store.IsRefreshing(did, cancellationToken));
        Assert.NotNull(await store.StartRefresh(did, cancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a-token-belonging-to-somebody-else")]
    public async Task EndRefreshDoesNotReleaseALockItDoesNotOwn(string? refreshLockToken)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did did = TestData.NewDid();

        Assert.NotNull(await store.StartRefresh(did, cancellationToken));

        await store.EndRefresh(did, refreshLockToken, cancellationToken);

        Assert.True(await store.IsRefreshing(did, cancellationToken));
    }

    [Fact]
    public async Task RefreshLocksAreHeldPerDid()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IIdentityStore store = CreateStore();
        Did first = TestData.NewDid();
        Did second = TestData.NewDid();

        Assert.NotNull(await store.StartRefresh(first, cancellationToken));
        Assert.NotNull(await store.StartRefresh(second, cancellationToken));

        Assert.True(await store.IsRefreshing(first, cancellationToken));
    }
}
