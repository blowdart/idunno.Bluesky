// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Security.Claims;

using idunno.AtProto;

using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace idunno.Bluesky.AspNet.Authentication.Test;

/// <summary>
/// Covers <see cref="BlueskyAuthenticationHandler"/> driven through a real request pipeline.
/// </summary>
/// <remarks>
/// <para>
///   The handler's behaviour is almost entirely in what it writes to the response, so these tests assert on status
///   codes, redirects, cookies and the outcome the handler reported, rather than on its internals.
/// </para>
/// </remarks>
public class BlueskyAuthenticationHandlerTests
{
    private static MetricCollector<long> AuthenticationOutcomeCollector(AuthenticationTestHost host) =>
        new(host.MeterFactory, BlueskyAuthenticationMetrics.MeterName, "idunno.bluesky.aspnet.authentication.authentications.total");

    private static string OutcomeOf(MetricCollector<long> collector) =>
        Assert.Single(collector.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.AuthenticationResultTagName] as string
            ?? throw new InvalidOperationException("The authentication outcome was not tagged with a result.");

    [Fact]
    public async Task ARequestCarryingNoAuthenticationCookieIsNotCountedAsAnAuthenticationAttempt()
    {
        // Anonymous traffic to a page which merely asks who the user is would otherwise swamp the counter and make the
        // failure rate meaningless.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        MetricCollector<long> collector = AuthenticationOutcomeCollector(host);

        using HttpResponseMessage response = await host.GetWithCookie("/test/authenticate", cookie: null);

        Assert.Contains("none=True", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Empty(collector.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task AValidCookieAuthenticatesAndRehydratesTheFullIdentityFromTheStore()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did));

        MetricCollector<long> collector = AuthenticationOutcomeCollector(host);

        using HttpResponseMessage response = await host.GetWithCookie("/test/authenticate", cookie);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("succeeded=True", body, StringComparison.Ordinal);

        // The cookie only carries the DID, so seeing it on the principal proves the handler went back to the store.
        Assert.Contains($"did={did}", body, StringComparison.Ordinal);
        Assert.Equal("success", OutcomeOf(collector));
    }

    [Fact]
    public async Task ATamperedCookieIsRejected()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(TestData.NewDid()));

        MetricCollector<long> collector = AuthenticationOutcomeCollector(host);

        // Flipping a character in the payload breaks the authentication tag the ticket format applies.
        string tampered = cookie[..^4] + (cookie[^4] == 'A' ? 'B' : 'A') + cookie[^3..];

        using HttpResponseMessage response = await host.GetWithCookie("/test/authenticate", tampered);

        Assert.Contains("succeeded=False", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal("unprotect_ticket_failed", OutcomeOf(collector));
    }

    [Fact]
    public async Task AnIdentityWhichHasLeftTheStoreFailsAuthenticationAndIsCountedAsAStoreMiss()
    {
        // The identity store aging an entry out, or evicting it, silently signs a user out even though their cookie is
        // still valid, so the miss has to be visible.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did));

        await host.IdentityStore.Remove(did, TestContext.Current.CancellationToken);

        MetricCollector<long> outcomes = AuthenticationOutcomeCollector(host);
        var misses = new MetricCollector<long>(
            host.MeterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.identitystore.misses.total");

        using HttpResponseMessage response = await host.GetWithCookie("/test/authenticate", cookie);

        Assert.Contains("succeeded=False", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal("identity_missing_in_store", OutcomeOf(outcomes));

        Assert.Equal(
            BlueskyAuthenticationMetrics.IdentityStoreMissPhaseAuthentication,
            Assert.Single(misses.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.IdentityStoreMissPhaseTagName]);
    }

    [Fact]
    public async Task AnExpiredTicketFailsAuthenticationAndClearsTheStoredIdentity()
    {
        // Leaving the identity behind would keep live credentials in the store for a session which can no longer be
        // used, until the store's own expiry got round to them.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(
            TestData.AuthenticatedClaimsIdentity(did),
            expiresUtc: DateTimeOffset.UtcNow.AddMinutes(-5));

        MetricCollector<long> collector = AuthenticationOutcomeCollector(host);

        using HttpResponseMessage response = await host.GetWithCookie("/test/authenticate", cookie);

        Assert.Contains("succeeded=False", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal("ticket_expired", OutcomeOf(collector));
        Assert.Null(await host.IdentityStore.GetIdentity(did, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AChallengeRedirectsToTheLoginPathCarryingTheRequestedUrlAsTheReturnUrl()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        using HttpResponseMessage response = await host.Client.GetAsync(
            new Uri("/test/challenge?page=2", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        string location = response.Headers.Location?.ToString() ?? string.Empty;

        Assert.StartsWith($"https://localhost{AuthenticationTestHost.LoginPath}", location, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("/test/challenge?page=2"), location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/account/profile")]
    [InlineData("/")]
    public async Task AHostRelativeReturnUrlIsHonouredAfterSignIn(string returnUrl)
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        using HttpResponseMessage response = await host.SignInFromLoginPath(
            TestData.AuthenticatedClaimsIdentity(TestData.NewDid()),
            returnUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(returnUrl, response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("https://evil.example/steal")]
    [InlineData("//evil.example/steal")]
    [InlineData(@"/\evil.example/steal")]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("http://localhost/looks-local")]
    public async Task AReturnUrlWhichIsNotHostRelativeIsNotHonouredAfterSignIn(string returnUrl)
    {
        // The return URL arrives on the query string of the login callback, so it is attacker controlled. Honouring
        // anything which is not host relative turns the login endpoint into an open redirect, and honouring a
        // javascript: URI turns it into stored XSS against anyone who follows the link.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        using HttpResponseMessage response = await host.SignInFromLoginPath(
            TestData.AuthenticatedClaimsIdentity(TestData.NewDid()),
            returnUrl);

        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task SigningOutDeletesTheCookieRemovesTheStoredIdentityAndIsCounted()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did));

        var collector = new MetricCollector<long>(
            host.MeterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.signouts.total");

        using HttpResponseMessage response = await host.GetWithCookie("/test/signout", cookie);

        Assert.Null(await host.IdentityStore.GetIdentity(did, TestContext.Current.CancellationToken));
        Assert.Equal(1, Assert.Single(collector.GetMeasurementSnapshot()).Value);

        string? setCookie = AuthenticationTestHost.SetCookieHeader(response, AuthenticationTestHost.CookieName);

        Assert.NotNull(setCookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SigningOutFromAnEndpointWhichNeverAuthenticatedStillClearsTheStoredIdentity()
    {
        // A dedicated sign out endpoint does not have to be behind authorization, and an application which
        // authenticates explicitly rather than through a default scheme never has the handler read the cookie. Leaving
        // the identity in the store would leave live credentials behind.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(authenticateByDefault: false);

        Did did = TestData.NewDid();
        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(did));

        Assert.NotNull(await host.IdentityStore.GetIdentity(did, TestContext.Current.CancellationToken));

        // /test/signout calls SignOutAsync without ever calling AuthenticateAsync, so the handler has to recover the
        // DID from the request cookie.
        using HttpResponseMessage response = await host.GetWithCookie("/test/signout", cookie);

        Assert.Null(await host.IdentityStore.GetIdentity(did, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SigningOutWithNoCookieAtAllIsHarmless()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        using HttpResponseMessage response = await host.GetWithCookie("/test/signout", cookie: null);

        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Redirect);
    }

    [Theory]
    [InlineData("/signed-out", true)]
    [InlineData("https://evil.example/steal", false)]
    [InlineData("//evil.example/steal", false)]
    [InlineData("javascript:alert(1)", false)]
    public async Task ASignOutOnlyHonoursAHostRelativeReturnUrl(string returnUrl, bool shouldRedirect)
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        string cookie = await host.SignInAndCaptureCookie(TestData.AuthenticatedClaimsIdentity(TestData.NewDid()));

        using HttpResponseMessage response = await host.GetWithCookie(
            $"{AuthenticationTestHost.LogoutPath}?ReturnUrl={Uri.EscapeDataString(returnUrl)}",
            cookie);

        if (shouldRedirect)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(returnUrl, response.Headers.Location?.ToString());
        }
        else
        {
            Assert.Null(response.Headers.Location);
        }
    }

    [Fact]
    public async Task TheAuthenticationCookieIsWrittenWithTheFlagsWhichKeepItOutOfReachOfScript()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        using HttpResponseMessage response = await host.SignIn(TestData.AuthenticatedClaimsIdentity(TestData.NewDid()));

        string setCookie = AuthenticationTestHost.SetCookieHeader(response, AuthenticationTestHost.CookieName)
            ?? throw new InvalidOperationException("Sign in did not write an authentication cookie.");

        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }
}
