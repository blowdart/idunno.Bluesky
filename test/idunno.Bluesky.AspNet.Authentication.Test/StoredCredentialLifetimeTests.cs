// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Authentication.Test;

/// <summary>
/// Covers the handling of the credentials the identity store holds, which outlive the request they were written by.
/// </summary>
[ExcludeFromCodeCoverage]
public class StoredCredentialLifetimeTests
{
    [Fact]
    public async Task TheIdentityStoreIsProtectedByDefault()
    {
        // Anything the store is given carries an access token, a refresh token and a DPoP private key, and a
        // distributed store puts all three somewhere the application does not solely control.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        Assert.IsType<DataProtectingIdentityStoreEvents>(host.Options.IdentityStoreEvents);
    }

    [Fact]
    public async Task TheCorrelationStateCacheIsProtectedByDefault()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        Assert.IsType<DataProtectingCorrelationStateCacheEvents>(host.Options.CorrelationStateCacheEvents);
    }

    [Fact]
    public async Task EventsSuppliedByTheApplicationAreNotReplacedByTheDefaults()
    {
        IdentityStoreEvents identityStoreEvents = new();
        CorrelationStateCacheEvents correlationStateCacheEvents = new();

        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(configureOptions: options =>
        {
            options.IdentityStoreEvents = identityStoreEvents;
            options.CorrelationStateCacheEvents = correlationStateCacheEvents;
        });

        Assert.Same(identityStoreEvents, host.Options.IdentityStoreEvents);
        Assert.Same(correlationStateCacheEvents, host.Options.CorrelationStateCacheEvents);
    }

    [Fact]
    public async Task SigningInAsADifferentDidRemovesThePreviousIdentityFromTheStore()
    {
        // The previous identity is still holding live credentials, and nothing is going to present its cookie again,
        // so leaving it behind means those credentials sit in the store until the entry expires.
        await using FakePds pds = await FakePds.Create();
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(httpClientFactory: pds.HttpClientFactory);

        Did first = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(first, signableProofKey: true));

        Did second = TestData.NewDid();

        using HttpResponseMessage response = await host.SignInCarryingCookie(
            TestData.AuthenticatedClaimsIdentity(second, signableProofKey: true),
            cookie);

        Assert.Null(await host.IdentityStore.GetIdentity(first, TestContext.Current.CancellationToken));
        Assert.NotNull(await host.IdentityStore.GetIdentity(second, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SigningInAsADifferentDidRevokesThePreviousCredentials()
    {
        await using FakePds pds = await FakePds.Create();
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(httpClientFactory: pds.HttpClientFactory);

        string cookie = await host.SignInAndCaptureCookie(
            TestData.AuthenticatedClaimsIdentity(TestData.NewDid(), signableProofKey: true));

        using HttpResponseMessage response = await host.SignInCarryingCookie(
            TestData.AuthenticatedClaimsIdentity(TestData.NewDid(), signableProofKey: true),
            cookie);

        Assert.Equal(["refresh_token", "access_token"], pds.RevokedTokenTypeHints);
    }

    [Fact]
    public async Task SigningInAsTheSameDidDoesNotRevokeAnything()
    {
        // Signing in again as the same user replaces the stored identity with fresher credentials, so revoking the
        // ones it just replaced would revoke the session it is in the middle of establishing.
        await using FakePds pds = await FakePds.Create();
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(httpClientFactory: pds.HttpClientFactory);

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did, signableProofKey: true));

        using HttpResponseMessage response = await host.SignInCarryingCookie(
            TestData.AuthenticatedClaimsIdentity(did, signableProofKey: true),
            cookie);

        Assert.Empty(pds.RevokedTokenTypeHints);
        Assert.NotNull(await host.IdentityStore.GetIdentity(did, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnExpiredTicketRevokesTheCredentialsItLeftInTheStore()
    {
        // Dropping the store entry only stops this application using the credentials. They stay usable at the PDS
        // until they are revoked or expire on their own.
        await using FakePds pds = await FakePds.Create();
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(httpClientFactory: pds.HttpClientFactory);

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(
            TestData.AuthenticatedClaimsIdentity(did, signableProofKey: true),
            expiresUtc: DateTimeOffset.UtcNow.AddMinutes(-1));

        using HttpResponseMessage response = await host.GetWithCookie("/test/authenticate", cookie);

        Assert.Contains(
            "succeeded=False",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
        Assert.Equal(["refresh_token", "access_token"], pds.RevokedTokenTypeHints);
        Assert.Null(await host.IdentityStore.GetIdentity(did, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AStoredIdentityWithNoReadableCredentialsDoesNotAuthenticate()
    {
        // Credentials which cannot be parsed cannot be checked for expiry either, so an identity carrying them has to
        // fail rather than be handed to the application unchecked.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did));

        // Replace the stored identity with one which carries no tokens at all, which is what a store entry written by
        // an older version, or corrupted in the store, looks like once it has been read back.
        await host.IdentityStore.Remove(did, TestContext.Current.CancellationToken);
        await host.IdentityStore.Add(
            new ClaimsIdentity(
                [new Claim(AtProtoClaims.Did, did, ClaimValueTypes.String, "https://bsky.social")],
                AuthenticationTestHost.Scheme),
            TestContext.Current.CancellationToken);

        using HttpResponseMessage response = await host.GetWithCookie("/test/authenticate", cookie);

        Assert.Contains(
            "succeeded=False",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
        Assert.Null(await host.IdentityStore.GetIdentity(did, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Bluesky", "store=scheme")]
    [InlineData("SomeOtherScheme", "store=scheme")]
    [InlineData("", "store=scheme")]
    public async Task AnAgentsCredentialUpdatesAreWrittenToTheRegisteredSchemesIdentityStore(string authenticationType, string expected)
    {
        // An identity issued by something other than a Bluesky scheme names a scheme the options monitor has never
        // been configured for. Taking it at face value builds a fresh options instance with an identity store of its
        // own, and every credential update the agent makes is then written somewhere nothing ever reads.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        using HttpResponseMessage response = await host.Client.GetAsync(
            new Uri($"/test/agent/store?scheme={Uri.EscapeDataString(authenticationType)}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
