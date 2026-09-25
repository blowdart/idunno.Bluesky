// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Authentication;

namespace idunno.Bluesky.AspNet.Authentication.Test;

/// <summary>
/// Covers the invariant that the authentication cookie only ever carries a reference to the stored identity.
/// </summary>
/// <remarks>
/// <para>
///   The handler keeps the access token, the refresh token and the DPoP proof key in the identity store and writes a
///   cookie holding nothing but the DID, so that a stolen cookie is worth nothing without the store behind it. A
///   renewal which re-issues the hydrated ticket would put all three in the browser instead, and would still work, so
///   nothing but an assertion on the cookie contents catches it.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class ReferenceCookieTests
{
    public static TheoryData<string> RenewalTriggers => new() { "validate", "sliding" };

    [Theory]
    [MemberData(nameof(RenewalTriggers))]
    public async Task ARenewedCookieCarriesOnlyTheDidClaim(string trigger)
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(configureOptions: options =>
        {
            if (trigger == "validate")
            {
                options.Events.OnValidatePrincipal = context =>
                {
                    context.ShouldRenew = true;
                    return Task.CompletedTask;
                };
            }
            else
            {
                options.Events.OnCheckSlidingExpiration = context =>
                {
                    context.ShouldRenew = true;
                    return Task.CompletedTask;
                };
            }
        });

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did));

        using HttpResponseMessage response = await host.GetWithCookie("/test/authenticate", cookie);

        string renewed = AuthenticationTestHost.ExtractCookie(response, AuthenticationTestHost.CookieName)
            ?? throw new InvalidOperationException("The request did not renew the authentication cookie.");

        AuthenticationTicket? ticket = host.Options.TicketDataFormat!.Unprotect(renewed);

        Assert.NotNull(ticket);
        Assert.Equal([AtProtoClaims.Did], ticket.Principal.Claims.Select(claim => claim.Type));
        Assert.Equal(did.Value, ticket.Principal.FindFirst(AtProtoClaims.Did)!.Value);
    }

    [Fact]
    public async Task TheCookieWrittenAtSignInCarriesOnlyTheDidClaim()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did));

        AuthenticationTicket? ticket = host.Options.TicketDataFormat!.Unprotect(cookie);

        Assert.NotNull(ticket);
        Assert.Equal([AtProtoClaims.Did], ticket.Principal.Claims.Select(claim => claim.Type));
    }

    [Fact]
    public async Task ARenewedCookieStillAuthenticates()
    {
        // The reduced principal has to be enough to rehydrate the identity from the store on the next request,
        // otherwise a renewal would quietly sign the user out.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(configureOptions: options =>
            options.Events.OnValidatePrincipal = context =>
            {
                context.ShouldRenew = true;
                return Task.CompletedTask;
            });

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did));

        using HttpResponseMessage firstResponse = await host.GetWithCookie("/test/authenticate", cookie);
        string renewed = AuthenticationTestHost.ExtractCookie(firstResponse, AuthenticationTestHost.CookieName)!;

        using HttpResponseMessage secondResponse = await host.GetWithCookie("/test/authenticate", renewed);

        Assert.Contains(
            $"succeeded=True",
            await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
    }
}
