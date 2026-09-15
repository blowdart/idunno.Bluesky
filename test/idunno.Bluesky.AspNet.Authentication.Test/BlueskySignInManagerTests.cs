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

    private static async Task<string> CreateCorrelationCookie(AuthenticationTestHost host)
    {
        using HttpResponseMessage response = await host.Client.GetAsync(
            new Uri("/test/correlation/create", UriKind.Relative),
            TestContext.Current.CancellationToken);

        return AuthenticationTestHost.ExtractCookie(response, Constants.CorrelationCookieName)
            ?? throw new InvalidOperationException("No correlation cookie was written.");
    }

    private static async Task<string> LoadState(AuthenticationTestHost host, string? correlationCookie)
    {
        using HttpResponseMessage response = await host.GetWithCorrelationCookie("/test/correlation/load", correlationCookie);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheCorrelationCookieIsWrittenWithTheFlagsWhichKeepItOutOfReachOfScriptAndOffOtherSites()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        using HttpResponseMessage response = await host.Client.GetAsync(
            new Uri("/test/correlation/create", UriKind.Relative),
            TestContext.Current.CancellationToken);

        string setCookie = AuthenticationTestHost.SetCookieHeader(response, Constants.CorrelationCookieName)
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

        string cookie = await CreateCorrelationCookie(host);

        Assert.Equal("state=found", await LoadState(host, cookie));

        MetricCollector<long> rejections = RejectionCollector(host);

        Assert.Equal("state=null", await LoadState(host, cookie));

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

        Assert.Equal("state=null", await LoadState(host, "not-a-protected-value"));

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

        Assert.Equal("state=null", await LoadState(host, expired));

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

        Assert.Equal("state=null", await LoadState(host, host.ForgeMalformedCorrelationCookie()));

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

        using HttpResponseMessage response = await host.GetWithCorrelationCookie("/test/correlation/load", cookieValue);

        string? setCookie = AuthenticationTestHost.SetCookieHeader(response, Constants.CorrelationCookieName);

        Assert.NotNull(setCookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AConsumedCorrelationCookieIsDeletedEvenWhenItsStateWasFound()
    {
        // The state is taken on the first read, so leaving the cookie in place would leave the browser presenting a
        // cookie which can only ever be rejected from then on.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create();

        string cookie = await CreateCorrelationCookie(host);

        using HttpResponseMessage response = await host.GetWithCorrelationCookie("/test/correlation/load", cookie);

        Assert.Equal("state=found", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        string? setCookie = AuthenticationTestHost.SetCookieHeader(response, Constants.CorrelationCookieName);

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

        Assert.Equal("state=null", await LoadState(host, correlationCookie: null));

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
            "not-a-protected-value");

        Assert.Contains(
            "missingCorrelationState=True",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);

        Assert.Equal(
            "NoCorrelationState",
            Assert.Single(failures.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.SignInFailureReasonTagName]);
    }
}
