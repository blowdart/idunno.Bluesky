// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace idunno.Bluesky.AspNet.Authentication.Test;

/// <summary>
/// Covers how <see cref="BlueskySignInManager"/> handles the correlation state an OAuth callback arrives with.
/// </summary>
/// <remarks>
/// <para>
///   The correlation cookie and the login state it names are what tie a callback back to the request which started
///   the login. If a callback could be replayed, or could be processed without state, the callback endpoint would
///   accept an authorization code an attacker chose, so every one of these paths has to fail closed.
/// </para>
/// <para>
///   These tests stop at <c>LoadState</c>. Going further would need an authorization server to exchange a code with.
/// </para>
/// </remarks>
public class BlueskySignInManagerTests
{
    private static MetricCollector<long> RejectionCollector(AuthenticationTestHost host) =>
        new(host.MeterFactory, BlueskyAuthenticationMetrics.MeterName, "idunno.bluesky.aspnet.authentication.correlationstate.rejections.total");

    private static async Task<(string Name, string Value)> CreateCorrelationCookie(
        AuthenticationTestHost host,
        string oauthState = TestData.OAuthState)
    {
        using HttpResponseMessage response = await host.Client.GetAsync(
            new Uri($"/test/correlation/create?state={Uri.EscapeDataString(oauthState)}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        return AuthenticationTestHost.ExtractCorrelationCookie(response)
            ?? throw new InvalidOperationException("No correlation cookie was written.");
    }

    private static async Task<string> LoadState(
        AuthenticationTestHost host,
        string cookieName,
        string? correlationCookie,
        string oauthState = TestData.OAuthState)
    {
        using HttpResponseMessage response = await host.GetWithCorrelationCookie(
            $"/test/correlation/load?state={Uri.EscapeDataString(oauthState)}",
            cookieName,
            correlationCookie);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheCorrelationCookieIsWrittenWithTheFlagsWhichKeepItOutOfReachOfScriptAndOffOtherSites()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        using HttpResponseMessage response = await host.Client.GetAsync(
            new Uri("/test/correlation/create", UriKind.Relative),
            TestContext.Current.CancellationToken);

        string setCookie = AuthenticationTestHost.CorrelationSetCookieHeader(response)
            ?? throw new InvalidOperationException("No correlation cookie was written.");

        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);

        // The state the cookie names ages out of the correlation cache, so the cookie has to carry an expiry rather
        // than living until the browser is closed and being presented against state which is no longer there.
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CorrelationStateIsSingleUseSoAReplayedCallbackFindsNothing()
    {
        // The authorization code in a callback is single use at the authorization server, but a replayed callback that
        // still found its state would let the same login be processed twice.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        (string cookieName, string cookie) = await CreateCorrelationCookie(host);

        Assert.Equal("state=found", await LoadState(host, cookieName, cookie));

        MetricCollector<long> rejections = RejectionCollector(host);

        Assert.Equal("state=null", await LoadState(host, cookieName, cookie));

        Assert.Equal(
            BlueskyAuthenticationMetrics.CorrelationStateRejectionStateNotFound,
            Assert.Single(rejections.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.CorrelationStateRejectionReasonTagName]);
    }

    [Fact]
    public async Task ACorrelationCookieWhichCannotBeUnprotectedIsRejectedAndCounted()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        MetricCollector<long> rejections = RejectionCollector(host);
        var dataProtectionFailures = new MetricCollector<long>(
            host.MeterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.dataprotection.failures.total");

        Assert.Equal("state=null", await LoadState(host, await host.CorrelationCookieName(), "not-a-protected-value"));

        Assert.Equal(
            BlueskyAuthenticationMetrics.CorrelationStateRejectionUnprotectFailed,
            Assert.Single(rejections.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.CorrelationStateRejectionReasonTagName]);

        Assert.Equal(
            BlueskyAuthenticationMetrics.DataProtectionSourceCorrelationCookie,
            Assert.Single(dataProtectionFailures.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.DataProtectionSourceTagName]);
    }

    [Fact]
    public async Task ACorrelationCookieWhichHasPassedItsExpiryYieldsNoState()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        MetricCollector<long> rejections = RejectionCollector(host);
        var dataProtectionFailures = new MetricCollector<long>(
            host.MeterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.dataprotection.failures.total");

        string expired = host.ForgeCorrelationCookie(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-5));

        Assert.Equal("state=null", await LoadState(host, await host.CorrelationCookieName(), expired));

        Assert.Equal(
            BlueskyAuthenticationMetrics.CorrelationStateRejectionExpiredCookie,
            Assert.Single(rejections.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.CorrelationStateRejectionReasonTagName]);

        // A user who left the login page open is not a key ring problem, and counting it as one would make a real
        // data protection failure harder to see.
        Assert.Empty(dataProtectionFailures.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task ACorrelationCookieWhichUnprotectsToSomethingUnparseableIsRejectedAndCounted()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        MetricCollector<long> rejections = RejectionCollector(host);
        var dataProtectionFailures = new MetricCollector<long>(
            host.MeterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.dataprotection.failures.total");

        Assert.Equal(
            "state=null",
            await LoadState(host, await host.CorrelationCookieName(), host.ForgeMalformedCorrelationCookie()));

        Assert.Equal(
            BlueskyAuthenticationMetrics.CorrelationStateRejectionMalformedCookie,
            Assert.Single(rejections.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.CorrelationStateRejectionReasonTagName]);

        Assert.Empty(dataProtectionFailures.GetMeasurementSnapshot());
    }

    [Theory]
    [InlineData("not-a-protected-value")]
    [InlineData("")]
    public async Task ARejectedCorrelationCookieIsDeletedSoItCannotBePresentedAgain(string cookieValue)
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        using HttpResponseMessage response = await host.GetWithCorrelationCookie(
            $"/test/correlation/load?state={TestData.OAuthState}",
            await host.CorrelationCookieName(),
            cookieValue);

        string? setCookie = AuthenticationTestHost.CorrelationSetCookieHeader(response);

        Assert.NotNull(setCookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AConsumedCorrelationCookieIsDeletedEvenWhenItsStateWasFound()
    {
        // The state is taken on the first read, so leaving the cookie in place would leave the browser presenting a
        // cookie which can only ever be rejected from then on.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        (string cookieName, string cookie) = await CreateCorrelationCookie(host);

        using HttpResponseMessage response = await host.GetWithCorrelationCookie(
            $"/test/correlation/load?state={TestData.OAuthState}",
            cookieName,
            cookie);

        Assert.Equal("state=found", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        string? setCookie = AuthenticationTestHost.CorrelationSetCookieHeader(response);

        Assert.NotNull(setCookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACallbackWithNoCorrelationCookieAtAllIsRefused()
    {
        // A callback arriving with no cookie cannot be tied to a login this application started, so it has to be
        // refused. It is not an error in the application though, so it is a handled failure rather than an exception
        // which would reach the user as a server error.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        MetricCollector<long> rejections = RejectionCollector(host);

        Assert.Equal("state=null", await LoadState(host, await host.CorrelationCookieName(), correlationCookie: null));

        Assert.Equal(
            BlueskyAuthenticationMetrics.CorrelationStateRejectionMissingCookie,
            Assert.Single(rejections.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.CorrelationStateRejectionReasonTagName]);
    }

    [Fact]
    public async Task ACallbackWithNoQueryStringFailsAndIsCountedWithItsReason()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        var failures = new MetricCollector<long>(
            host.MeterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.signins.total.failure");

        using HttpResponseMessage response = await host.Client.GetAsync(
            new Uri("/test/callback", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Contains(
            "missingQueryString=True",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);

        Assert.Equal(
            "NoQueryString",
            Assert.Single(failures.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.SignInFailureReasonTagName]);
    }

    [Fact]
    public async Task ACallbackWhoseCorrelationStateIsGoneFailsAndIsCountedWithItsReason()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        var failures = new MetricCollector<long>(
            host.MeterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.signins.total.failure");

        // A query string is present, so the callback gets as far as looking for its correlation state, which a cookie
        // that cannot be unprotected will never produce.
        using HttpResponseMessage response = await host.GetWithCorrelationCookie(
            "/test/callback?code=whatever&state=whatever",
            await host.CorrelationCookieName("whatever"),
            "not-a-protected-value");

        Assert.Contains(
            "missingCorrelationState=True",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);

        Assert.Equal(
            "NoCorrelationState",
            Assert.Single(failures.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.SignInFailureReasonTagName]);
    }

    [Fact]
    public async Task LoginsStartedInTwoTabsOfTheSameBrowserBothKeepTheirOwnCorrelationState()
    {
        // A single correlation cookie name would mean the second login overwrote the first login's cookie. The first
        // callback would then take the second login's state, fail on the state check, and leave nothing behind for the
        // second callback, so both logins would fail.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        (string firstName, string firstCookie) = await CreateCorrelationCookie(host, "state-from-the-first-tab");
        (string secondName, string secondCookie) = await CreateCorrelationCookie(host, "state-from-the-second-tab");

        Assert.NotEqual(firstName, secondName);

        // Finishing the login which started last must leave the one which started first able to finish too.
        Assert.Equal("state=found", await LoadState(host, secondName, secondCookie, "state-from-the-second-tab"));
        Assert.Equal("state=found", await LoadState(host, firstName, firstCookie, "state-from-the-first-tab"));
    }

    [Fact]
    public async Task ACallbackCarryingAnotherLoginsStateDoesNotConsumeTheStateOfTheLoginItNames()
    {
        // The cookie a callback presents is only ever read against the state that callback carries, so a callback
        // aimed at a login it does not belong to finds nothing rather than consuming that login's state.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        (string name, string cookie) = await CreateCorrelationCookie(host, "the-real-state");

        Assert.Equal("state=null", await LoadState(host, name, cookie, "a-state-from-somewhere-else"));

        // The login it actually belongs to is untouched.
        Assert.Equal("state=found", await LoadState(host, name, cookie, "the-real-state"));
    }

    [Theory]
    [InlineData("/test/correlation/load")]
    [InlineData("/test/correlation/load?state=")]
    [InlineData("/test/correlation/load?state=oauth-state&state=oauth-state")]
    public async Task ACallbackWhichDoesNotCarryExactlyOneStateParameterIsRefused(string path)
    {
        // Without exactly one state parameter there is nothing which says which login in flight the callback belongs
        // to, and guessing at one would let a duplicated parameter aim a callback at another login's cookie.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        MetricCollector<long> rejections = RejectionCollector(host);

        (string name, string cookie) = await CreateCorrelationCookie(host);

        using HttpResponseMessage response = await host.GetWithCorrelationCookie(path, name, cookie);

        Assert.Equal("state=null", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            BlueskyAuthenticationMetrics.CorrelationStateRejectionMissingState,
            Assert.Single(rejections.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.CorrelationStateRejectionReasonTagName]);
    }

    [Fact]
    public async Task TheCorrelationCookieNameCarriesTheSchemeSoTwoSchemesCannotShareIt()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        Assert.StartsWith(
            $"{Constants.CorrelationCookieName}.{AuthenticationTestHost.Scheme}.",
            await host.CorrelationCookieName(),
            StringComparison.Ordinal);
    }
}
