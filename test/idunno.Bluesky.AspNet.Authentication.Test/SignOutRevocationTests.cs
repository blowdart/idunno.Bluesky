// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using idunno.AtProto;

using Microsoft.AspNetCore.Http;

using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace idunno.Bluesky.AspNet.Authentication.Test;

/// <summary>
/// Covers the credential revocation a sign out performs against the authorization server.
/// </summary>
/// <remarks>
/// <para>
///   Revocation is the only part of signing out which leaves the process, and it is best effort, so a failure has to
///   leave the user signed out locally all the same. These tests run the whole sign out against a fake PDS so both
///   outcomes are exercised through the handler rather than asserted on in isolation.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class SignOutRevocationTests
{
    [Fact]
    public async Task SigningOutRevokesBothTheRefreshTokenAndTheAccessTokenAtTheAuthorizationServer()
    {
        await using FakePds pds = await FakePds.Create();
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(httpClientFactory: pds.HttpClientFactory);

        Did did = TestData.NewDid();
        ClaimsIdentity identity = TestData.AuthenticatedClaimsIdentity(did, signableProofKey: true);

        string cookie = await host.SignInAndCaptureCookie(identity);

        using HttpResponseMessage response = await host.GetWithCookie("/test/signout", cookie);

        Assert.Equal(["refresh_token", "access_token"], pds.RevokedTokenTypeHints);
    }

    [Fact]
    public async Task SigningOutRemovesTheStoredIdentityWhenRevocationSucceeds()
    {
        await using FakePds pds = await FakePds.Create();
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(httpClientFactory: pds.HttpClientFactory);

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did, signableProofKey: true));

        using HttpResponseMessage response = await host.GetWithCookie("/test/signout", cookie);

        Assert.Null(await host.IdentityStore.GetIdentity(did, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    public async Task SigningOutStillSignsTheUserOutLocallyWhenTheAuthorizationServerRejectsTheRevocation(int revocationStatusCode)
    {
        await using FakePds pds = await FakePds.Create(revocationStatusCode: revocationStatusCode);
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(httpClientFactory: pds.HttpClientFactory);

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did, signableProofKey: true));

        using HttpResponseMessage response = await host.GetWithCookie("/test/signout", cookie);

        // A PDS which cannot be reached, or which refuses, must not strand the user in a signed in state locally.
        Assert.Null(await host.IdentityStore.GetIdentity(did, TestContext.Current.CancellationToken));
        Assert.Equal(string.Empty, AuthenticationTestHost.ExtractCookie(response, AuthenticationTestHost.CookieName));
    }

    [Fact]
    public async Task AFailedRevocationIsCounted()
    {
        await using FakePds pds = await FakePds.Create(revocationStatusCode: StatusCodes.Status400BadRequest);
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(httpClientFactory: pds.HttpClientFactory);

        using MetricCollector<long> collector = new(
            host.MeterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.credentialrevocations.failures.total");

        string cookie = await host.SignInAndCaptureCookie(
            TestData.AuthenticatedClaimsIdentity(TestData.NewDid(), signableProofKey: true));

        using HttpResponseMessage response = await host.GetWithCookie("/test/signout", cookie);

        Assert.Equal(1, Assert.Single(collector.GetMeasurementSnapshot()).Value);
    }

    [Fact]
    public async Task ASuccessfulRevocationIsNotCountedAsAFailure()
    {
        await using FakePds pds = await FakePds.Create();
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(httpClientFactory: pds.HttpClientFactory);

        using MetricCollector<long> collector = new(
            host.MeterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.credentialrevocations.failures.total");

        string cookie = await host.SignInAndCaptureCookie(
            TestData.AuthenticatedClaimsIdentity(TestData.NewDid(), signableProofKey: true));

        using HttpResponseMessage response = await host.GetWithCookie("/test/signout", cookie);

        Assert.Empty(collector.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task ARevocationWhichFailsBeforeTheRevocationEndpointIsReachedIsCounted()
    {
        // Discovery is two requests before the first revocation, and a failure in either of them has to be caught the
        // same way as a refused revocation.
        await using FakePds pds = await FakePds.Create(serveAuthorizationServerMetadata: false);
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(httpClientFactory: pds.HttpClientFactory);

        using MetricCollector<long> collector = new(
            host.MeterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.credentialrevocations.failures.total");

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did, signableProofKey: true));

        using HttpResponseMessage response = await host.GetWithCookie("/test/signout", cookie);

        Assert.Empty(pds.RevokedTokenTypeHints);
        Assert.Equal(1, Assert.Single(collector.GetMeasurementSnapshot()).Value);
        Assert.Null(await host.IdentityStore.GetIdentity(did, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SigningOutDoesNotReachTheAuthorizationServerWhenTheStoreHoldsNoIdentity()
    {
        await using FakePds pds = await FakePds.Create();
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(httpClientFactory: pds.HttpClientFactory);

        // Signing out without ever having signed in, so there are no stored credentials to revoke.
        using HttpResponseMessage response = await host.GetWithCookie("/test/signout", cookie: null);

        Assert.Empty(pds.RevokedTokenTypeHints);
    }
}
