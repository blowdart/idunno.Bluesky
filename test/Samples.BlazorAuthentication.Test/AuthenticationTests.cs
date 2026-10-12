// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Duende.IdentityModel.OidcClient.DPoP;

using idunno.AtProto.Authentication;
using idunno.Bluesky;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Samples.BlazorAuthentication.Test;

public partial class AuthenticationTests
{
    private const string Did = "did:plc:ewvi7nxzyoun6zhxrhs64oiz";
    private const string ProfileCid = "bafyreie5cvv4hro5oz3x5i5m5agzojwzwczl7ddsxv2gm7vby7i2dqu2yi";
    private const string ChangedCid = "bafyreig7t6q4x7zbibjnyxkzsde2ndvgc7jevdzicwagmg36nwcb6qhigy";

    [Theory]
    [InlineData("/")]
    [InlineData("/Privacy")]
    [InlineData("/Bluesky/Login")]
    [InlineData("/Bluesky/Logout")]
    [InlineData("/site.css")]
    [InlineData("/Bluesky/css/bluesky-auth.css")]
    public async Task AnonymousPagesAndLocalAssetsAreAvailable(string path)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var response = await browser.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        if (response.Content.Headers.ContentType?.MediaType == "text/html")
        {
            string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("Blazor Bluesky", html, StringComparison.Ordinal);
            Assert.Contains("class=\"sign-in-link\"", html, StringComparison.Ordinal);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
    }

    [Theory]
    [InlineData("/Claims")]
    [InlineData("/Manage")]
    public async Task ProtectedPagesChallengeAnonymousRequests(string path)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var response = await browser.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Bluesky/Login", response.Headers.Location?.AbsolutePath);
        Assert.Equal(path, QueryHelpers.ParseQuery(response.Headers.Location!.Query)["ReturnUrl"].ToString());
    }

    [Fact]
    public void ConfigurationMatchesDeclaredLocalhostPermissions()
    {
        using var factory = new SampleFactory();
        var oauth = factory.Services.GetRequiredService<IOptions<BlueskyAgentOptions>>().Value.OAuthOptions!;
        Assert.Equal(new[] { "atproto", BlueskyOAuthPermissionSets.FullApp.ToString() }, oauth.GetRequestedScopes());
        var query = QueryHelpers.ParseQuery(new Uri(oauth.ClientId).Query);
        Assert.Equal(string.Join(' ', oauth.GetRequestedScopes()), query["scope"].ToString());
        Assert.NotNull(oauth.ReturnUri);
        Assert.Equal(oauth.ReturnUri.ToString(), query["redirect_uri"].ToString());
        var options = factory.Services.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>()
            .Get(BlueskyAuthenticationDefaults.AuthenticationScheme);
        Assert.Equal(".AspNetCore.Bluesky.Blazor", options.Cookie.Name);
        Assert.Equal(".AspNetCore.Bluesky.Blazor.Correlation", options.CorrelationCookie.Name);
        Assert.False(factory.Services.GetRequiredService<IOptions<BlueskyAgentOptions>>().Value.EnableBackgroundTokenRefresh);
    }

    [Theory]
    [InlineData("https://images.example/avatar.jpg", true)]
    [InlineData(null, false)]
    [InlineData("javascript:alert(1)", false)]
    public async Task AuthenticatedTimelineAndClaimsUseTheRequestIdentity(string? avatar, bool showAvatar)
    {
        await using var factory = new SampleFactory();
        factory.Pds.Avatar = avatar;
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        string timeline = await browser.GetStringAsync("/", TestContext.Current.CancellationToken);
        Assert.Contains("Hello from the timeline", timeline, StringComparison.Ordinal);
        Assert.Contains("<blockquote>", timeline, StringComparison.Ordinal);
        Assert.Contains("In reply to", timeline, StringComparison.Ordinal);
        Assert.Contains("<strong>Parent author</strong>", timeline, StringComparison.Ordinal);
        Assert.Contains("parent.bsky.social", timeline, StringComparison.Ordinal);
        Assert.Contains("Text from the reply parent", timeline, StringComparison.Ordinal);
        Assert.Contains("src=\"https://images.example/first-thumb.jpg\" alt=\"First image description\"", timeline, StringComparison.Ordinal);
        Assert.Contains("src=\"https://images.example/second-thumb.jpg\" alt=\"Second image description\"", timeline, StringComparison.Ordinal);
        Assert.Contains("Manage profile", timeline, StringComparison.Ordinal);
        Assert.Contains("Sign out", timeline, StringComparison.Ordinal);
        Assert.Matches("<details class=\"account-menu\">[\\s\\S]*?<summary[^>]*>[\\s\\S]*?</summary>[\\s\\S]*?href=\"/Manage\"[\\s\\S]*?href=\"/Bluesky/Logout\\?returnUrl=%2F\"[\\s\\S]*?</details>", timeline);
        Assert.Equal(showAvatar, timeline.Contains("alt=\"Avatar\"", StringComparison.Ordinal));
        Assert.Contains("Test user", timeline, StringComparison.Ordinal);
        if (showAvatar)
        {
            Assert.Contains($"src=\"{avatar}\"", timeline, StringComparison.Ordinal);
        }

        string logout = await browser.GetStringAsync("/Bluesky/Logout", TestContext.Current.CancellationToken);
        Assert.Equal(showAvatar, logout.Contains("alt=\"Avatar\"", StringComparison.Ordinal));
        Assert.Contains("Test user", logout, StringComparison.Ordinal);
        Assert.Matches("<details class=\"account-menu\">[\\s\\S]*?<summary[^>]*>[\\s\\S]*?</summary>[\\s\\S]*?href=\"/Manage\"[\\s\\S]*?href=\"/Bluesky/Logout\\?returnUrl=%2F\"[\\s\\S]*?</details>", logout);
        if (showAvatar)
        {
            Assert.Contains($"src=\"{avatar}\"", logout, StringComparison.Ordinal);
        }
        Assert.Equal(1, factory.Pds.TimelineRequests);
        string claims = await browser.GetStringAsync("/Claims", TestContext.Current.CancellationToken);
        Assert.Contains(Did, claims, StringComparison.Ordinal);
        Assert.Contains("Access token expires on", claims, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-refresh-token", claims, StringComparison.Ordinal);
        Assert.DoesNotContain(AtProtoClaims.AccessToken, claims, StringComparison.Ordinal);
        Assert.DoesNotContain(AtProtoClaims.DPoPProof, claims, StringComparison.Ordinal);
        Assert.DoesNotContain(AtProtoClaims.DPoPNonce, claims, StringComparison.Ordinal);
        Assert.Matches("_framework/blazor\\.web(?:\\.[a-z0-9]+)?\\.js", timeline);
        Assert.Contains("\"type\":\"server\"", timeline, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-refresh-token", timeline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InteractiveServerTransportIsRegistered()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient();
        using var response = await browser.PostAsync("/_blazor/negotiate?negotiateVersion=1", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(json.RootElement.GetProperty("availableTransports").EnumerateArray(),
            transport => transport.GetProperty("transport").GetString() == "WebSockets");
    }

    [Fact]
    public async Task ActiveSessionRenewalExtendsCookieAndCircuitWithoutReloadingTheEditor()
    {
        var clock = new SessionClock();
        await using var factory = new SampleFactory(clock);
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        string token = await RenewalToken(browser);
        using var scope = await CreateCircuit(factory, browser);
        await using var editor = CreateEditor(scope);
        await editor.RenderAsync();
        editor.Input.DisplayName = "Unsaved edit";
        var provider = (CircuitAuthenticationStateProvider)scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();

        clock.Advance(TimeSpan.FromMinutes(6));
        using var renewed = await RenewSession(browser, token);
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        Assert.Contains(renewed.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Bluesky.Blazor=", StringComparison.Ordinal));
        string receipt = await renewed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(Did, receipt, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", receipt, StringComparison.Ordinal);
        await provider.AcceptRenewalAsync(receipt);

        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.True(await provider.IsCurrentAsync((await provider.GetAuthenticationStateAsync()).User, TestContext.Current.CancellationToken));
        Assert.Equal("Unsaved edit", editor.Input.DisplayName);
        using var claims = await browser.GetAsync("/Claims", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, claims.StatusCode);
        await editor.SubmitAsync();
        Assert.Equal(1, factory.Pds.Writes);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task IdleOrNonSlidingSessionsAreNotExtended(bool sliding, bool allowRefresh)
    {
        var clock = new SessionClock();
        await using var factory = new SampleFactory(clock, sliding, allowRefresh);
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        string token = await RenewalToken(browser);
        using var scope = await CreateCircuit(factory, browser);
        var provider = (CircuitAuthenticationStateProvider)scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        if (!sliding || !allowRefresh)
        {
            clock.Advance(TimeSpan.FromMinutes(6));
            using var response = await RenewSession(browser, token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            await provider.AcceptRenewalAsync(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            clock.Advance(TimeSpan.FromMinutes(5));
        }
        else
        {
            clock.Advance(TimeSpan.FromMinutes(11));
        }

        Assert.False(await provider.IsCurrentAsync((await provider.GetAuthenticationStateAsync()).User, TestContext.Current.CancellationToken));
        using var renewal = await RenewSession(browser, token);
        Assert.False(renewal.IsSuccessStatusCode);
        Assert.False(renewal.Headers.Contains("Set-Cookie"));
        using var protectedPage = await browser.GetAsync("/Claims", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
    }

    [Fact]
    public async Task RenewalDoesNotOverwriteCredentialsWhileAnotherOperationOwnsTheRefreshLock()
    {
        var clock = new SessionClock();
        await using var factory = new SampleFactory(clock);
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        string token = await RenewalToken(browser);
        clock.Advance(TimeSpan.FromMinutes(6));
        var store = GetStore(factory);
        var original = (await store.GetIdentity(new(Did), TestContext.Current.CancellationToken))!;
        var did = new idunno.AtProto.Did(Did);
        string? refreshLock = await store.StartRefresh(did, TestContext.Current.CancellationToken);
        Assert.NotNull(refreshLock);
        try
        {
            using var response = await RenewSession(browser, token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(await store.IsRefreshing(did, TestContext.Current.CancellationToken));
            var current = (await store.GetIdentity(did, TestContext.Current.CancellationToken))!;
            Assert.Equal(original.FindFirst(AtProtoClaims.AccessToken)?.Value, current.FindFirst(AtProtoClaims.AccessToken)?.Value);
            Assert.Equal(original.FindFirst(AtProtoClaims.DPoPProof)?.Value, current.FindFirst(AtProtoClaims.DPoPProof)?.Value);
        }
        finally
        {
            Assert.True(await store.EndRefresh(did, refreshLock, TestContext.Current.CancellationToken));
        }

        using var retry = await RenewSession(browser, token);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Theory]
    [InlineData("antiforgery")]
    [InlineData("logout")]
    [InlineData("replacement")]
    [InlineData("tampered")]
    public async Task RenewalDoesNotAuthorizeInvalidOrDifferentSessions(string reason)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        string token = await RenewalToken(browser);
        using var scope = await CreateCircuit(factory, browser);
        var provider = (CircuitAuthenticationStateProvider)scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        if (reason == "antiforgery")
        {
            using var rejected = await RenewSession(browser, "invalid-token");
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            return;
        }

        if (reason == "replacement")
        {
            await Login(browser);
            token = await RenewalToken(browser);
        }

        using var renewal = await RenewSession(browser, token);
        Assert.Equal(HttpStatusCode.OK, renewal.StatusCode);
        string receipt = await renewal.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (reason == "logout")
        {
            await GetStore(factory).Remove(new(Did), TestContext.Current.CancellationToken);
            using var rejected = await RenewSession(browser, token);
            Assert.False(rejected.IsSuccessStatusCode);
        }
        else if (reason == "tampered")
        {
            receipt = "not-a-protected-receipt";
        }

        await Assert.ThrowsAsync<AuthenticationException>(() => provider.AcceptRenewalAsync(receipt));
    }

    private static async Task<string> RenewalToken(HttpClient browser)
    {
        string html = await browser.GetStringAsync("/", TestContext.Current.CancellationToken);
        var match = Regex.Match(html, "<meta name=\"session-antiforgery\" content=\"([^\"]+)\"");
        Assert.True(match.Success);

        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenewalCannotRestoreASessionChangedAfterAuthentication(bool replacement)
    {
        var clock = new SessionClock();
        await using var factory = new SampleFactory(clock);
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        string token = await RenewalToken(browser);
        using var scope = await CreateCircuit(factory, browser);
        var provider = (CircuitAuthenticationStateProvider)scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        var store = GetStore(factory);
        ClaimsIdentity? newIdentity = null;
        factory.BeforeRenewalValidation = async currentStore =>
        {
            if (replacement)
            {
                newIdentity = new ClaimsIdentity((await currentStore.GetIdentity(new(Did), TestContext.Current.CancellationToken))!);
                newIdentity.RemoveClaim(newIdentity.FindFirst(AtProtoClaims.DPoPProof)!);
                newIdentity.AddClaim(new(AtProtoClaims.DPoPProof, JsonWebKeys.CreateRsaJson()));
                await currentStore.Add(newIdentity, TestContext.Current.CancellationToken);
            }
            else
            {
                await currentStore.Remove(new(Did), TestContext.Current.CancellationToken);
            }
        };

        clock.Advance(TimeSpan.FromMinutes(6));
        using var response = await RenewSession(browser, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await store.GetIdentity(new(Did), TestContext.Current.CancellationToken);
        Assert.Equal(newIdentity?.FindFirst(AtProtoClaims.DPoPProof)?.Value, stored?.FindFirst(AtProtoClaims.DPoPProof)?.Value);
        Assert.False(await provider.IsCurrentAsync((await provider.GetAuthenticationStateAsync()).User, TestContext.Current.CancellationToken));
        string receipt = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<AuthenticationException>(() => provider.AcceptRenewalAsync(receipt));
        if (!replacement)
        {
            using var claims = await browser.GetAsync("/Claims", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Redirect, claims.StatusCode);
        }
    }

    private static Task<HttpResponseMessage> RenewSession(HttpClient browser, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/session/renew");
        request.Headers.Add("RequestVerificationToken", token);

        return browser.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("replaced")]
    [InlineData("expired")]
    public async Task CircuitOperationsRejectInvalidSessions(string reason)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient();
        await Login(browser);
        using var scope = await CreateCircuit(factory, browser,
            reason == "expired" ? DateTimeOffset.UtcNow.AddMinutes(-1) : null);
        var store = GetStore(factory);
        if (reason == "removed")
        {
            await store.Remove(new(Did), TestContext.Current.CancellationToken);
        }
        else if (reason == "replaced")
        {
            var identity = (await store.GetIdentity(new(Did), TestContext.Current.CancellationToken))!;
            identity.RemoveClaim(identity.FindFirst(AtProtoClaims.DPoPProof)!);
            identity.AddClaim(new(AtProtoClaims.DPoPProof, JsonWebKeys.CreateRsaJson()));
            await store.Add(identity, TestContext.Current.CancellationToken);
        }

        var provider = (CircuitAuthenticationStateProvider)scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        Assert.False(await provider.IsCurrentAsync((await provider.GetAuthenticationStateAsync()).User, TestContext.Current.CancellationToken));
        var client = scope.ServiceProvider.GetRequiredService<CircuitBlueskyClient>();
        await Assert.ThrowsAsync<AuthenticationException>(() => client.ExecuteAsync(
            (agent, cancellationToken) => agent.GetTimeline(cancellationToken: cancellationToken), TestContext.Current.CancellationToken));
        Assert.Equal(0, factory.Pds.TimelineRequests);
    }

    [Fact]
    public async Task CircuitOperationsReadCurrentStoredCredentialsAndReleaseLocksOnFailure()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient();
        await Login(browser);
        using var scope = await CreateCircuit(factory, browser);
        var store = GetStore(factory);
        var identity = (await store.GetIdentity(new(Did), TestContext.Current.CancellationToken))!;
        string token = AccessToken(DateTimeOffset.UtcNow.AddHours(1));
        identity.RemoveClaim(identity.FindFirst(AtProtoClaims.AccessToken)!);
        identity.AddClaim(new(AtProtoClaims.AccessToken, token, ClaimValueTypes.String, "https://pds.example/"));
        await store.Update(identity, TestContext.Current.CancellationToken);
        var client = scope.ServiceProvider.GetRequiredService<CircuitBlueskyClient>();
        string currentToken = await client.ExecuteAsync((agent, _) => Task.FromResult(agent.Credentials!.AccessJwt), TestContext.Current.CancellationToken);
        Assert.Equal(token, currentToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ExecuteAsync<bool>(
            (_, _) => throw new InvalidOperationException("Test operation failure"), TestContext.Current.CancellationToken));
        Assert.False(await store.IsRefreshing(new(Did), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CircuitRefreshPersistsCredentialsAndDoesNotRunOperationsOnFailure(bool fails)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient();
        await Login(browser);
        using var scope = await CreateCircuit(factory, browser);
        var store = GetStore(factory);
        var identity = (await store.GetIdentity(new(Did), TestContext.Current.CancellationToken))!;
        await using var oauthServer = await factory.Pds.StartOAuthServer();
        identity.RemoveClaim(identity.FindFirst(AtProtoClaims.Did)!);
        identity.AddClaim(new(AtProtoClaims.Did, Did, ClaimValueTypes.String, factory.Pds.Authority));
        identity.RemoveClaim(identity.FindFirst(AtProtoClaims.AccessToken)!);
        identity.AddClaim(new(AtProtoClaims.AccessToken, AccessToken(DateTimeOffset.UtcNow.AddMinutes(1), factory.Pds.Authority),
            ClaimValueTypes.String, factory.Pds.Authority));
        await store.Update(identity, TestContext.Current.CancellationToken);
        factory.Pds.Failure = fails ? "refresh" : null;
        var client = scope.ServiceProvider.GetRequiredService<CircuitBlueskyClient>();
        if (fails)
        {
            await Assert.ThrowsAsync<AuthenticationException>(() => client.ExecuteAsync(
                (agent, cancellationToken) => agent.GetTimeline(cancellationToken: cancellationToken), TestContext.Current.CancellationToken));
            Assert.Equal(0, factory.Pds.TimelineRequests);
        }
        else
        {
            var result = await client.ExecuteAsync((agent, cancellationToken) => agent.GetTimeline(cancellationToken: cancellationToken), TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded);
            var refreshed = (await store.GetIdentity(new(Did), TestContext.Current.CancellationToken))!;
            Assert.Equal("rotated-refresh-token", refreshed.FindFirst(AtProtoClaims.RefreshToken)?.Value);
            Assert.Equal(factory.Pds.RefreshedAccessToken, refreshed.FindFirst(AtProtoClaims.AccessToken)?.Value);
            await client.ExecuteAsync((agent, cancellationToken) => agent.GetTimeline(cancellationToken: cancellationToken), TestContext.Current.CancellationToken);
            Assert.Equal(1, factory.Pds.RefreshRequests);
        }

        Assert.False(await store.IsRefreshing(new(Did), TestContext.Current.CancellationToken));
    }

    private static IIdentityStore GetStore(SampleFactory factory) =>
        factory.Services.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>()
            .Get(BlueskyAuthenticationDefaults.AuthenticationScheme).IdentityStore!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CircuitNonceUpdatesPersistOnlyWhileTheSessionExists(bool logoutDuringRequest)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient();
        await Login(browser);
        using var scope = await CreateCircuit(factory, browser);
        var store = GetStore(factory);
        factory.Pds.NonceChallenge = true;
        if (logoutDuringRequest)
        {
            factory.Pds.BeforeNonceChallenge = () => store.Remove(new(Did), TestContext.Current.CancellationToken);
        }

        var client = scope.ServiceProvider.GetRequiredService<CircuitBlueskyClient>();
        if (logoutDuringRequest)
        {
            await Assert.ThrowsAsync<AuthenticationException>(() => client.ExecuteAsync(
                (agent, cancellationToken) => agent.GetTimeline(cancellationToken: cancellationToken), TestContext.Current.CancellationToken));
            Assert.Null(await store.GetIdentity(new(Did), TestContext.Current.CancellationToken));
        }
        else
        {
            var result = await client.ExecuteAsync(
                (agent, cancellationToken) => agent.GetTimeline(cancellationToken: cancellationToken), TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded);
            var identity = await store.GetIdentity(new(Did), TestContext.Current.CancellationToken);
            Assert.Equal("rotated-nonce", identity?.FindFirst(AtProtoClaims.DPoPNonce)?.Value);
            Assert.Equal(2, factory.Pds.TimelineRequests);
        }

        Assert.False(await store.IsRefreshing(new(Did), TestContext.Current.CancellationToken));
    }

    private static string AccessToken(DateTimeOffset expiry, string authority = "https://pds.example/")
    {
        string scope = "atproto " + BlueskyOAuthPermissionSets.FullApp.ToString();
        string payload = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
            $$"""{"sub":"{{Did}}","iss":"{{authority}}","aud":"did:web:{{new Uri(authority).Host}}","exp":{{expiry.ToUnixTimeSeconds()}},"scope":"{{scope}}"}"""));

        return "eyJhbGciOiJSUzI1NiJ9." + payload + ".signature";
    }

    [Theory]
    [InlineData("images", "first-thumb.jpg")]
    [InlineData("quote-images", "quote-thumb.jpg")]
    [InlineData("gif", "tenor.gif")]
    [InlineData("video", "<video")]
    [InlineData("invalid-external", "External media unavailable")]
    public async Task MediaOnlyTimelinePostsRenderTheirMedia(string kind, string expected)
    {
        await using var factory = new SampleFactory();
        factory.Pds.MediaOnly = true;
        factory.Pds.EmbedJson = kind switch
        {
            "quote-images" => $$$"""
                {"$type":"app.bsky.embed.recordWithMedia#view",
                "record":{"record":{"$type":"app.bsky.embed.record#viewNotFound","uri":"at://{{{Did}}}/app.bsky.feed.post/quote","notFound":true}},
                "media":{"$type":"app.bsky.embed.images#view","images":[{"thumb":"https://images.example/quote-thumb.jpg","fullsize":"https://images.example/quote-full.jpg","alt":"Quoted image"}]}}
                """,
            "gif" => """
                {"$type":"app.bsky.embed.external#view","external":{"uri":"https://media.tenor.com/animation/tenor.gif?hh=100&ww=100","title":"Animated GIF","description":"Alt: A GIF post","thumb":"https://images.example/still.jpg"}}
                """,
            "invalid-external" => """
                {"$type":"app.bsky.embed.external#view","external":{"uri":"javascript:alert(1)","title":"","description":""}}
                """,
            "video" => $$"""
                {"$type":"app.bsky.embed.video#view","cid":"{{ProfileCid}}","playlist":"https://video.example/playlist.m3u8","thumbnail":"https://images.example/video.jpg","alt":"Post video description"}
                """,
            _ => null
        };
        using var browser = factory.CreateClient();
        await Login(browser);
        string html = await browser.GetStringAsync("/", TestContext.Current.CancellationToken);
        Assert.Contains(expected, html, StringComparison.Ordinal);
        Assert.DoesNotContain("Hello from the timeline", html, StringComparison.Ordinal);
        if (kind == "video")
        {
            Assert.Contains("https://video.example/playlist.m3u8", html, StringComparison.Ordinal);
            Assert.Contains("poster=\"https://images.example/video.jpg\"", html, StringComparison.Ordinal);
            Assert.Contains("Watch video on Bluesky", html, StringComparison.Ordinal);
        }
        if (kind == "invalid-external")
        {
            Assert.DoesNotContain("javascript:", html, StringComparison.Ordinal);
        }
        if (kind is "images" or "quote-images")
        {
            Assert.Contains("data-open-image", html, StringComparison.Ordinal);
            Assert.Contains("<dialog class=\"image-dialog\"", html, StringComparison.Ordinal);
            Assert.Contains("<form method=\"dialog\">", html, StringComparison.Ordinal);
            Assert.Contains(">Close</button>", html, StringComparison.Ordinal);
            string fullSize = kind == "images" ? "first.jpg" : "quote-full.jpg";
            string alt = kind == "images" ? "First image description" : "Quoted image";
            Assert.Contains($"src=\"https://images.example/{fullSize}\" alt=\"{alt}\"", html, StringComparison.Ordinal);
            Assert.Contains($"<figcaption>{alt}</figcaption>", html, StringComparison.Ordinal);
            Assert.DoesNotContain($"href=\"https://images.example/{fullSize}\"", html, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Alt: A dancing cat", "A dancing cat")]
    [InlineData("ALT: A dancing cat", "A dancing cat")]
    [InlineData("A dancing cat", "A dancing cat")]
    [InlineData("", "")]
    public async Task AnimatedGifsUseInlineMediaWithAltAndHoverText(string description, string alt)
    {
        await using var factory = new SampleFactory();
        factory.Pds.MediaOnly = true;
        factory.Pds.EmbedJson = $$$"""
            {"$type":"app.bsky.embed.external#view","external":{"uri":"https://media.tenor.com/animation/tenor.GIF?hh=100&ww=100","title":"GIF title","description":"{{{description}}}","thumb":"https://images.example/still.jpg"}}
            """;
        using var browser = factory.CreateClient();
        await Login(browser);
        string html = await browser.GetStringAsync("/", TestContext.Current.CancellationToken);
        Assert.Contains("class=\"post-image post-gif\" src=\"https://media.tenor.com/animation/tenor.GIF?hh=100&amp;ww=100\"", html, StringComparison.Ordinal);
        Assert.Contains($"alt=\"{alt}\" title=\"{alt}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"external-card\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("src=\"https://images.example/still.jpg\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("GIF title", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"<span>{description}</span>", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("preferences")]
    [InlineData("timeline")]
    public async Task TimelineFailuresAreVisible(string failure)
    {
        await using var factory = new SampleFactory();
        factory.Pds.Failure = failure;
        using var browser = factory.CreateClient();
        await Login(browser);
        string html = await browser.GetStringAsync("/", TestContext.Current.CancellationToken);
        Assert.Contains("Could not load", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Your timeline is empty", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InteractiveProfileSavesConditionallyAndPreservesOtherFields()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        using var scope = await CreateCircuit(factory, browser);
        await using var editor = CreateEditor(scope);
        await editor.RenderAsync();
        Assert.Equal("Original name", editor.Input.DisplayName);
        await PostEdit(editor);
        Assert.Equal(1, factory.Pds.Writes);
        using var write = JsonDocument.Parse(factory.Pds.LastWrite!);
        Assert.Equal(ProfileCid, write.RootElement.GetProperty("swapRecord").GetString());
        Assert.Equal(Did, write.RootElement.GetProperty("repo").GetString());
        var record = write.RootElement.GetProperty("record");
        Assert.Equal("Edited name", record.GetProperty("displayName").GetString());
        Assert.Equal("Edited description", record.GetProperty("description").GetString());
        Assert.Equal("they/them", record.GetProperty("pronouns").GetString());
        Assert.Equal("!no-unauthenticated", record.GetProperty("labels").GetProperty("values")[0].GetProperty("val").GetString());
        Assert.Contains("Your profile was saved", editor.Text, StringComparison.Ordinal);
        Assert.Equal(ChangedCid, editor.Input.Cid);
        await editor.SubmitAsync();
        Assert.Equal(2, factory.Pds.Writes);
        using var secondWrite = JsonDocument.Parse(factory.Pds.LastWrite!);
        Assert.Equal(ChangedCid, secondWrite.RootElement.GetProperty("swapRecord").GetString());
    }

    [Theory]
    [InlineData("Input.Pronouns", "123456789012345678901")]
    [InlineData("Input.Cid", "invalid")]
    [InlineData("Input.Cid", "")]
    public async Task InvalidFormValuesDoNotWrite(string field, string value)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient();
        await Login(browser);
        using var scope = await CreateCircuit(factory, browser);
        await using var editor = CreateEditor(scope);
        await editor.RenderAsync();
        await PostEdit(editor, field, value);
        Assert.NotEmpty(editor.ValidationErrors);
        Assert.Equal(0, factory.Pds.Writes);
        Assert.Equal("Edited name", editor.Input.DisplayName);
    }

    [Fact]
    public async Task EmptyProfileFieldsCanBeCleared()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        using var scope = await CreateCircuit(factory, browser);
        await using var editor = CreateEditor(scope);
        await editor.RenderAsync();
        await PostEdit(editor, "Input.Pronouns", "");
        Assert.Contains("Your profile was saved", editor.Text, StringComparison.Ordinal);
        using var write = JsonDocument.Parse(factory.Pds.LastWrite!);
        Assert.Equal("", write.RootElement.GetProperty("record").GetProperty("pronouns").GetString());
    }

    [Theory]
    [InlineData("conflict", "Your profile changed", 0)]
    [InlineData("write", "Could not save your profile", 1)]
    [InlineData("profile", "Could not load your profile", 0)]
    public async Task FailedSavesRetainEditsAndDoNotClaimSuccess(string failure, string message, int writes)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        using var scope = await CreateCircuit(factory, browser);
        await using var editor = CreateEditor(scope);
        await editor.RenderAsync();
        factory.Pds.Failure = failure;
        await PostEdit(editor);
        string html = editor.Text;
        Assert.Contains(message, html, StringComparison.Ordinal);
        Assert.Equal("Edited name", editor.Input.DisplayName);
        Assert.DoesNotContain("Your profile was saved", html, StringComparison.Ordinal);
        Assert.Equal(writes, factory.Pds.Writes);
    }

    [Theory]
    [InlineData("/Bluesky/Logout")]
    public async Task StateChangingPostsRequireAntiforgery(string path)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        using var response = await browser.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "profile",
            ["Input.Cid"] = ProfileCid,
            ["Input.DisplayName"] = "Tampered"
        }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Pds.Writes);
    }

    [Fact]
    public async Task LogoutRequiresPostAndRemovesAccessToProtectedPages()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        string logout = await browser.GetStringAsync("/Bluesky/Logout", TestContext.Current.CancellationToken);
        using var stillSignedIn = await browser.GetAsync("/Claims", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stillSignedIn.StatusCode);
        using var posted = await browser.PostAsync("/Bluesky/Logout?returnUrl=%2F", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = HiddenValue(logout, "__RequestVerificationToken")
        }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
        using var signedOut = await browser.GetAsync("/Claims", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void DisplayNameUsesGraphemesNotUtf16Length(int length, bool valid)
    {
        var input = new ProfileInput { Cid = ProfileCid, DisplayName = string.Concat(Enumerable.Repeat("\U0001F600", length)) };
        Assert.Equal(valid, Validator.TryValidateObject(input, new(input), [], validateAllProperties: true));
    }

    [Fact]
    public void ProfileValidationChecksByteLimitsEvenForOneGrapheme()
    {
        var input = new ProfileInput { Cid = ProfileCid, Pronouns = "a" + new string('\u0301', 100) };
        List<ValidationResult> errors = [];
        Assert.False(Validator.TryValidateObject(input, new(input), errors, validateAllProperties: true));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(ProfileInput.Pronouns)));
    }

    private static async Task Login(HttpClient browser)
    {
        using var response = await browser.GetAsync("/test/login", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task PostEdit(ProfileRenderer editor, string? field = null, string? value = null)
    {
        editor.Input.DisplayName = "Edited name";
        editor.Input.Description = "Edited description";
        editor.Input.Pronouns = "they/them";
        switch (field)
        {
            case "Input.Cid":
                editor.Input.Cid = value!;
                break;
            case "Input.Pronouns":
                editor.Input.Pronouns = value!;
                break;
        }

        return editor.SubmitAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoredDraftRequiresExplicitSaveAndRetainsItsOriginalVersion(bool conflict)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient();
        await Login(browser);
        using var scope = await CreateCircuit(factory, browser);
        if (conflict)
        {
            factory.Pds.CurrentProfileCid = ChangedCid;
        }

        await using var editor = CreateEditor(scope);
        await editor.RenderAsync();
        await editor.RestoreAsync(ProfileCid, "Restored name", "Restored description", "they/them");
        Assert.Equal(0, factory.Pds.Writes);
        Assert.Equal(ProfileCid, editor.Input.Cid);
        Assert.Equal("Restored name", editor.Input.DisplayName);
        await editor.SubmitAsync();
        Assert.Equal(conflict ? 0 : 1, factory.Pds.Writes);
        Assert.Contains(conflict ? "Your profile changed" : "Your profile was saved", editor.Text, StringComparison.Ordinal);
    }

    private static ProfileRenderer CreateEditor(IServiceScope scope) =>
        new(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());

    [Theory]
    [InlineData("/Claims", "%2FClaims")]
    [InlineData("/Manage?draft=1", "%2FManage%3Fdraft%3D1")]
    public async Task UnauthorizedInteractiveRoutesPreserveTheirReturnUrl(string route, string encoded)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient();
        await Login(browser);
        using var scope = await CreateCircuit(factory, browser);
        var provider = (IHostEnvironmentAuthenticationStateProvider)scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        provider.SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
        var navigation = new TestNavigation();
        navigation.NavigateTo(route);
        await using var renderer = new HtmlRenderer(new NavigationServices(scope.ServiceProvider, navigation),
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<Samples.BlazorAuthentication.Components.Routes>();
            return component.ToHtmlString();
        });
        Assert.Contains($"href=\"/Bluesky/Login?returnUrl={encoded}\"", html, StringComparison.Ordinal);
    }

    private static async Task<IServiceScope> CreateCircuit(SampleFactory factory, HttpClient browser, DateTimeOffset? expiresUtc = null)
    {
        string cookie = await browser.GetStringAsync("/test/cookie", TestContext.Current.CancellationToken);
        var scope = factory.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Headers.Cookie = cookie;
        var result = await context.AuthenticateAsync();
        Assert.True(result.Succeeded);
        if (expiresUtc is not null)
        {
            result.Properties!.ExpiresUtc = expiresUtc;
        }
        context.Features.Set<IAuthenticateResultFeature>(new TicketFeature { AuthenticateResult = result });
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        var provider = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        ((IHostEnvironmentAuthenticationStateProvider)provider).SetAuthenticationState(Task.FromResult(new AuthenticationState(result.Principal!)));
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = null;

        return scope;
    }

    private sealed class TicketFeature : IAuthenticateResultFeature
    {
        public AuthenticateResult? AuthenticateResult { get; set; }
    }

    private static string HiddenValue(string html, string name) =>
        WebUtility.HtmlDecode(HiddenInput().Matches(html).First(match => match.Groups[1].Value == name).Groups[2].Value);

    [GeneratedRegex("name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"")]
    private static partial Regex HiddenInput();

    private sealed class SampleFactory(SessionClock? clock = null, bool sliding = true, bool allowRefresh = true) : WebApplicationFactory<Program>
    {
        internal FakePds Pds { get; } = new();
        internal Func<IIdentityStore, Task>? BeforeRenewalValidation { get; set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(Pds);
                services.AddSingleton<IStartupFilter, TestLogin>();
                services.AddScoped<IJSRuntime, TestJavaScript>();
                services.Configure<BlueskyAuthenticationOptions>(BlueskyAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Events.OnValidatePrincipal = async context =>
                    {
                        if (context.Request.Path == "/session/renew" && BeforeRenewalValidation is not null)
                        {
                            await BeforeRenewalValidation(context.Options.IdentityStore!);
                        }

                    };
                });
                if (clock is not null)
                {
                    services.Configure<BlueskyAuthenticationOptions>(BlueskyAuthenticationDefaults.AuthenticationScheme, options =>
                    {
                        options.TimeProvider = clock;
                        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
                        options.SlidingExpiration = sliding;
                        options.Events.OnSigningIn = context =>
                        {
                            context.Properties.AllowRefresh = allowRefresh;
                            return Task.CompletedTask;
                        };
                    });
                }
            });
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
            NotifyLocationChanged(false);
        }
    }

    private sealed class NavigationServices(IServiceProvider services, NavigationManager navigation) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(NavigationManager) ? navigation : services.GetService(serviceType);
    }

    // Browser module behavior is covered by the Node tests; component tests invoke .NET callbacks directly.
    private sealed class TestJavaScript : IJSRuntime, IJSObjectReference, IDisposable
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            ValueTask.FromResult(typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class SessionClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _now;

        internal void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class TestLogin : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path == "/test/login")
                {
                    string scope = "atproto " + BlueskyOAuthPermissionSets.FullApp.ToString();
                    string payload = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
                        $$"""{"sub":"{{Did}}","exp":4102444800,"scope":"{{scope}}"}"""));
                    DPoPAccessCredentials credentials = new(new Uri("https://pds.example"), "eyJhbGciOiJub25lIn0." + payload + ".",
                        "secret-refresh-token", JsonWebKeys.CreateRsaJson(), "secret-nonce");
                    var identity = IIdentityStore.BuildClaimsIdentity(credentials);
                    await context.SignInAsync(BlueskyAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    return;
                }

                if (context.Request.Path == "/test/cookie")
                {
                    await context.Response.WriteAsync(context.Request.Headers.Cookie.ToString());
                    return;
                }

                await nextMiddleware(context);
            });
            next(app);
        };
    }

    private sealed class FakePds : IHttpClientFactory
    {
        internal string? Failure { get; set; }
        internal string? Avatar { get; set; }
        internal string? EmbedJson { get; set; }
        internal bool MediaOnly { get; set; }
        internal int Writes { get; set; }
        internal int TimelineRequests { get; set; }
        internal string? LastWrite { get; set; }
        internal string CurrentProfileCid { get; set; } = ProfileCid;
        internal int RefreshRequests { get; set; }
        internal bool NonceChallenge { get; set; }
        internal Func<Task>? BeforeNonceChallenge { get; set; }
        internal string Authority { get; private set; } = "https://pds.example/";
        internal string RefreshedAccessToken { get; private set; } = AccessToken(DateTimeOffset.UtcNow.AddHours(1));

        public HttpClient CreateClient(string name) => new(new Handler(this));

        internal async Task<WebApplication> StartOAuthServer()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            app.Run(async context =>
            {
                using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method),
                    Authority + context.Request.Path.Value!.TrimStart('/'));
                using var client = CreateClient(string.Empty);
                using var response = await client.SendAsync(request, context.RequestAborted);
                context.Response.StatusCode = (int)response.StatusCode;
                context.Response.ContentType = "application/json";
                await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            });
            await app.StartAsync(TestContext.Current.CancellationToken);
            Authority = app.Urls.Single().TrimEnd('/') + "/";
            RefreshedAccessToken = AccessToken(DateTimeOffset.UtcNow.AddHours(1), Authority);

            return app;
        }

        private sealed class Handler(FakePds pds) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string path = request.RequestUri!.AbsolutePath;
                if (path.StartsWith("/xrpc/", StringComparison.Ordinal) && path != "/xrpc/com.atproto.server.describeServer")
                {
                    Assert.Equal("DPoP", request.Headers.Authorization?.Scheme);
                }

                switch (path)
                {
                    case "/.well-known/oauth-protected-resource":
                        return Json(HttpStatusCode.OK, $$"""{"resource":"{{pds.Authority}}","authorization_servers":["{{pds.Authority}}"]}""");
                    case "/.well-known/oauth-authorization-server":
                        return Json(HttpStatusCode.OK, $$"""
                            {"issuer":"{{pds.Authority}}","authorization_endpoint":"{{pds.Authority}}oauth/authorize",
                            "token_endpoint":"{{pds.Authority}}oauth/token","jwks_uri":"{{pds.Authority}}jwks",
                            "response_types_supported":["code"],"grant_types_supported":["authorization_code","refresh_token"],
                            "code_challenge_methods_supported":["S256"],"token_endpoint_auth_methods_supported":["none"]}
                            """);
                    case "/jwks":
                        return Json(HttpStatusCode.OK, """{"keys":[]}""");
                    case "/xrpc/com.atproto.server.describeServer":
                        return Json(HttpStatusCode.OK, $$"""{"did":"did:web:{{new Uri(pds.Authority).Host}}","availableUserDomains":["pds.example"]}""");
                    case "/oauth/token":
                        pds.RefreshRequests++;
                        return pds.Failure == "refresh"
                            ? Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""")
                            : Json(HttpStatusCode.OK, $$"""
                                {"access_token":"{{pds.RefreshedAccessToken}}","refresh_token":"rotated-refresh-token",
                                "token_type":"DPoP","expires_in":3600}
                                """);
                    case "/xrpc/app.bsky.actor.getProfile":
                        string avatar = pds.Avatar is null ? "null" : JsonSerializer.Serialize(pds.Avatar);
                        return Json(HttpStatusCode.OK, $$"""{"did":"{{Did}}","handle":"test.bsky.social","displayName":"Test user","avatar":{{avatar}}}""");
                    case "/xrpc/app.bsky.actor.getPreferences":
                        return pds.Failure == "preferences" ? Failure() : Json(HttpStatusCode.OK, """{"preferences":[]}""");
                    case "/xrpc/app.bsky.feed.getTimeline":
                        pds.TimelineRequests++;
                        if (pds.NonceChallenge && pds.TimelineRequests == 1)
                        {
                            if (pds.BeforeNonceChallenge is not null)
                            {
                                await pds.BeforeNonceChallenge();
                            }

                            var challenge = Json(HttpStatusCode.Unauthorized, """{"error":"use_dpop_nonce"}""");
                            challenge.Headers.Add("DPoP-Nonce", "rotated-nonce");
                            return challenge;
                        }

                        return pds.Failure == "timeline" ? Failure() : Json(HttpStatusCode.OK, $$$$"""
                            {"feed":[{"post":{"uri":"at://{{{{Did}}}}/app.bsky.feed.post/3kexample","cid":"{{{{ProfileCid}}}}",
                            "author":{"did":"{{{{Did}}}}","handle":"test.bsky.social","displayName":"Test user"},
                            "record":{"$type":"app.bsky.feed.post","text":"Hello from the timeline","createdAt":"2026-10-01T12:00:00Z"},
                            "embed":{"$type":"app.bsky.embed.images#view","images":[
                            {"thumb":"https://images.example/first-thumb.jpg","fullsize":"https://images.example/first.jpg","alt":"First image description"},
                            {"thumb":"https://images.example/second-thumb.jpg","fullsize":"https://images.example/second.jpg","alt":"Second image description"}]},
                            "indexedAt":"2026-10-01T12:00:00Z","replyCount":0,"likeCount":1,"repostCount":0},
                            "reply":{
                            "root":{"$type":"app.bsky.feed.defs#postView","uri":"at://{{{{Did}}}}/app.bsky.feed.post/3kroot","cid":"{{{{ProfileCid}}}}",
                            "author":{"did":"{{{{Did}}}}","handle":"root.bsky.social","displayName":"Root author"},
                            "record":{"$type":"app.bsky.feed.post","text":"Text from the thread root","createdAt":"2026-10-01T10:00:00Z"},
                            "indexedAt":"2026-10-01T10:00:00Z"},
                            "parent":{"$type":"app.bsky.feed.defs#postView","uri":"at://{{{{Did}}}}/app.bsky.feed.post/3kparent","cid":"{{{{ProfileCid}}}}",
                            "author":{"did":"{{{{Did}}}}","handle":"parent.bsky.social","displayName":"Parent author"},
                            "record":{"$type":"app.bsky.feed.post","text":"Text from the reply parent","createdAt":"2026-10-01T11:00:00Z"},
                            "indexedAt":"2026-10-01T11:00:00Z"}
                            }}]}
                            """, pds.MediaOnly, pds.EmbedJson);
                    case "/xrpc/com.atproto.repo.getRecord":
                        string cid = pds.Failure == "conflict" ? ChangedCid : pds.CurrentProfileCid;
                        return pds.Failure == "profile" ? Failure() : Json(HttpStatusCode.OK, $$$$"""
                            {"uri":"at://{{{{Did}}}}/app.bsky.actor.profile/self","cid":"{{{{cid}}}}",
                            "value":{"$type":"app.bsky.actor.profile","displayName":"Original name","description":"Original description","pronouns":"she/her",
                            "labels":{"$type":"com.atproto.label.defs#selfLabels","values":[{"val":"!no-unauthenticated"}]}}}
                            """);
                    case "/xrpc/com.atproto.repo.putRecord":
                        pds.Writes++;
                        pds.LastWrite = await request.Content!.ReadAsStringAsync(cancellationToken);
                        if (pds.Failure != "write")
                        {
                            pds.CurrentProfileCid = ChangedCid;
                        }

                        return pds.Failure == "write" ? Failure() : Json(HttpStatusCode.OK,
                            $$"""{"uri":"at://{{Did}}/app.bsky.actor.profile/self","cid":"{{ChangedCid}}"}""");
                    default:
                        return Json(HttpStatusCode.NotFound, """{"error":"NotFound"}""");
                }
            }

            private static HttpResponseMessage Failure() =>
                Json(HttpStatusCode.BadRequest, """{"error":"InvalidRequest","message":"Test failure"}""");

            private static HttpResponseMessage Json(HttpStatusCode code, string json, bool mediaOnly = false, string? embedJson = null)
            {
                if (mediaOnly)
                {
                    var document = JsonNode.Parse(json)!;
                    var post = document["feed"]![0]!["post"]!;
                    post["record"]!["text"] = "";
                    post["record"]!["embed"] = JsonNode.Parse("""
                        {"$type":"app.bsky.embed.external","external":{"uri":"https://media.example/post","title":"Media","description":""}}
                        """);
                    if (embedJson is not null)
                    {
                        post["embed"] = JsonNode.Parse(embedJson);
                    }

                    json = document.ToJsonString();
                }

                return new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            }
        }
    }
}
