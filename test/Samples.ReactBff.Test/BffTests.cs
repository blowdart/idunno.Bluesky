// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using ClaimTypes = System.Security.Claims.ClaimTypes;

namespace Samples.ReactBff.Test;

public class BffTests
{
    [Theory]
    [InlineData(5254)]
    [InlineData(5173)]
    public void LocalhostClientDeclaresScopesAndUsesTheBrowserCallbackPort(int port)
    {
        using var factory = new BffFactory();
        using IServiceScope scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<BlueskyAgentOptions>>().Value.OAuthOptions!;
        var clientId = new Uri(options.ClientId);
        Assert.Equal("http", clientId.Scheme);
        Assert.Equal("localhost", clientId.Host);
        Assert.True(clientId.IsDefaultPort);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(clientId.Query);
        Assert.Equal("http://127.0.0.1/oauth/callback", query["redirect_uri"].ToString());
        Assert.Equal(string.Join(' ', options.GetRequestedScopes()), query["scope"].ToString());
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext();
        accessor.HttpContext.Request.Scheme = "http";
        accessor.HttpContext.Request.Host = new HostString("127.0.0.1", port);
        var manager = scope.ServiceProvider.GetRequiredService<BlueskySignInManager>();
        Assert.Equal(new Uri($"http://127.0.0.1:{port}/oauth/callback"), manager.CreateReturnUri());
    }

    [Theory]
    [InlineData("http", false)]
    [InlineData("https", true)]
    public async Task AntiforgeryCookieFollowsTheRequestScheme(string scheme, bool secure)
    {
        await using var factory = new BffFactory();
        using var browser = new Browser(factory);
        using var response = await browser.Send(HttpMethod.Get, $"{scheme}://127.0.0.1/api/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Equal(secure, cookie.Contains("; secure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("__Host-", cookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RootServesReactAndAcceptsLoopbackHttp()
    {
        await using var factory = new BffFactory();
        using var browser = new Browser(factory);
        using var root = await browser.Send(HttpMethod.Get, "/");
        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Contains("/lib/app/assets/", await root.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal("no-store", root.Headers.CacheControl?.ToString());
        Assert.Equal("no-referrer", Assert.Single(root.Headers.GetValues("Referrer-Policy")));
        Assert.Contains("frame-ancestors 'none'", Assert.Single(root.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
        using var http = await browser.Send(HttpMethod.Get, "http://127.0.0.1/api/session");
        Assert.Equal(HttpStatusCode.OK, http.StatusCode);
    }

    [Fact]
    public async Task AnonymousRequestsAndCallbackAreSafe()
    {
        await using var factory = new BffFactory();
        using var browser = new Browser(factory);
        using var status = await browser.Send(HttpMethod.Get, "/api/session");
        Assert.Contains("\"authenticated\":false", await status.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        using var timeline = await browser.Send(HttpMethod.Get, "/api/timeline");
        Assert.Equal(HttpStatusCode.Unauthorized, timeline.StatusCode);
        Assert.False(timeline.Headers.Contains("Location"));
        using var callback = await browser.Send(HttpMethod.Get, "/oauth/callback?state=untrusted&code=secret");
        Assert.Equal("/?loginError=1", callback.Headers.Location?.ToString());
        using var metadata = await browser.Send(HttpMethod.Get, "/oauth-client-metadata.json");
        Assert.Equal(HttpStatusCode.NotFound, metadata.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/login")]
    [InlineData("POST", "/api/posts")]
    [InlineData("DELETE", "/api/posts/created")]
    [InlineData("POST", "/api/logout")]
    public async Task MutationsRequireCsrf(string method, string path)
    {
        await using var factory = new BffFactory();
        var client = new FakeClient(factory.Clock);
        using var browser = new Browser(factory, client);
        using var result = await browser.Send(new HttpMethod(method), path, new { text = "Hello", handle = "test.bsky.social" }, csrf: false);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(0, client.CreateCount);
        Assert.Equal(0, client.LogoutCount);
        Assert.False(client.Disposed);
    }

    [Fact]
    public async Task CsrfTokenCannotBeUsedByAnotherBrowser()
    {
        await using var factory = new BffFactory();
        using var first = new Browser(factory);
        using var second = new Browser(factory);
        using var tokenResponse = await first.Send(HttpMethod.Get, "/api/csrf");
        using var token = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/logout");
        message.Headers.Add("X-CSRF-TOKEN", token.RootElement.GetProperty("token").GetString());
        using var response = await second.Client.SendAsync(message, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string cookie = Assert.Single(tokenResponse.Headers.GetValues("Set-Cookie"));
        Assert.DoesNotContain("; secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateThenDeleteUsesOnlyTheStoredReference()
    {
        await using var factory = new BffFactory();
        var client = new FakeClient(factory.Clock);
        using var browser = new Browser(factory, client);
        using var create = await browser.Send(HttpMethod.Post, "/api/posts", new { text = "Hello from React" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using var duplicate = await browser.Send(HttpMethod.Post, "/api/posts", new { text = "Duplicate" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var state = await browser.Send(HttpMethod.Get, "/api/session");
        string body = await state.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(client.Reference.Uri.ToString(), body, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessJwt", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RefreshToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DPoP", body, StringComparison.OrdinalIgnoreCase);
        using var delete = await browser.Send(HttpMethod.Delete, "/api/posts/created?uri=at://other/app.bsky.feed.post/other");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Same(client.Reference, client.DeletedReference);
        using var again = await browser.Send(HttpMethod.Delete, "/api/posts/created");
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal(1, client.CreateCount);
    }

    [Fact]
    public async Task UnexpectedCreateReferenceCannotBeUsedForDeletion()
    {
        await using var factory = new BffFactory();
        var client = new FakeClient(factory.Clock)
        {
            Reference = new StrongReference(
                new AtUri("at://did:plc:other/app.bsky.feed.post/3test"),
                new Cid("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"))
        };
        using var browser = new Browser(factory, client);
        using var create = await browser.Send(HttpMethod.Post, "/api/posts", new { text = "Test" });
        Assert.Equal(HttpStatusCode.BadGateway, create.StatusCode);
        using var deletion = await browser.Send(HttpMethod.Delete, "/api/posts/created");
        Assert.Equal(HttpStatusCode.NotFound, deletion.StatusCode);
        Assert.Null(client.DeletedReference);
    }

    [Fact]
    public async Task SameDidBrowsersHaveIndependentSessionsAndPosts()
    {
        await using var factory = new BffFactory();
        var firstClient = new FakeClient(factory.Clock);
        var secondClient = new FakeClient(factory.Clock);
        using var first = new Browser(factory, firstClient);
        using var second = new Browser(factory, secondClient);
        using var created = await first.Send(HttpMethod.Post, "/api/posts", new { text = "First browser" });
        using var deletion = await second.Send(HttpMethod.Delete, "/api/posts/created");
        Assert.Equal(HttpStatusCode.NotFound, deletion.StatusCode);
        using var logout = await second.Send(HttpMethod.Post, "/api/logout");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.True(secondClient.Disposed);
        Assert.False(firstClient.Disposed);
        using var firstState = await first.Send(HttpMethod.Get, "/api/session");
        Assert.Contains("\"authenticated\":true", await firstState.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.NotEqual(first.SessionId, second.SessionId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RefreshPersistsAcrossRequestsOrEndsTheSession(bool refreshSucceeds)
    {
        await using var factory = new BffFactory();
        var client = new FakeClient(factory.Clock)
        {
            TokenExpiresAt = factory.Clock.GetUtcNow(),
            RefreshSucceeds = refreshSucceeds
        };
        using var browser = new Browser(factory, client);
        using var first = await browser.Send(HttpMethod.Get, "/api/timeline");
        using var second = await browser.Send(HttpMethod.Get, "/api/timeline");
        Assert.Equal(refreshSucceeds ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(1, client.RefreshCount);
        Assert.Equal(!refreshSucceeds, client.Disposed);
    }

    [Fact]
    public async Task ConcurrentRequestsCannotExchangeTheSameRefreshToken()
    {
        await using var factory = new BffFactory();
        var client = new FakeClient(factory.Clock) { TokenExpiresAt = factory.Clock.GetUtcNow() };
        using var browser = new Browser(factory, client);
        HttpResponseMessage[] responses = await Task.WhenAll(
            browser.Send(HttpMethod.Get, "/api/timeline"),
            browser.Send(HttpMethod.Get, "/api/timeline"));
        foreach (HttpResponseMessage response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            response.Dispose();
        }

        Assert.Equal(1, client.RefreshCount);
    }

    [Fact]
    public async Task ExpiryIsAbsoluteAndReleasesCredentials()
    {
        await using var factory = new BffFactory();
        var client = new FakeClient(factory.Clock);
        using var browser = new Browser(factory, client);
        factory.Clock.Advance(TimeSpan.FromMinutes(40));
        using var status = await browser.Send(HttpMethod.Get, "/api/session");
        Assert.Contains("\"authenticated\":true", await status.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        factory.Clock.Advance(TimeSpan.FromMinutes(21));
        using var expired = await browser.Send(HttpMethod.Get, "/api/timeline");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        await factory.Services.GetRequiredService<BffSessions>().SweepAsync(TestContext.Current.CancellationToken);
        Assert.True(client.Disposed);
        Assert.Null(factory.Services.GetRequiredService<BffSessions>().Find(browser.SessionId));
    }

    [Fact]
    public async Task LogoutRemovesSessionEvenWhenRevocationThrows()
    {
        await using var factory = new BffFactory();
        var client = new FakeClient(factory.Clock) { FailLogout = true };
        using var browser = new Browser(factory, client);
        using var logout = await browser.Send(HttpMethod.Post, "/api/logout");
        Assert.Equal(HttpStatusCode.BadGateway, logout.StatusCode);
        Assert.DoesNotContain("secret-token", await logout.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.True(client.Disposed);
        using var replay = await browser.Send(HttpMethod.Get, "/api/timeline");
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task PreviousSessionRevocationFailureDoesNotBlockReplacementLogin()
    {
        await using var factory = new BffFactory();
        var previousClient = new FakeClient(factory.Clock) { FailLogout = true };
        using var browser = new Browser(factory, previousClient);
        var sessions = factory.Services.GetRequiredService<BffSessions>();
        string previousId = browser.SessionId!;
        BffSession previousSession = sessions.Find(previousId)!;
        await previousSession.Gate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            await BffEndpoints.RevokeAndRemovePreviousSessionAsync(
                sessions,
                previousId,
                previousSession,
                NullLogger<BffSession>.Instance);
        }
        finally
        {
            previousSession.Gate.Release();
        }

        Assert.True(previousClient.Disposed);
        Assert.Null(sessions.Find(previousId));

        var replacementClient = new FakeClient(factory.Clock);
        string replacementId = sessions.Add(replacementClient);
        Assert.NotEqual(previousId, replacementId);
        Assert.Same(replacementClient, sessions.Find(replacementId)?.Client);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidPostDoesNotWrite(string text)
    {
        await using var factory = new BffFactory();
        var client = new FakeClient(factory.Clock);
        using var browser = new Browser(factory, client);
        using var result = await browser.Send(HttpMethod.Post, "/api/posts", new { text });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(0, client.CreateCount);
    }

    [Fact]
    public async Task GraphemeAndByteLimitsAreEnforced()
    {
        await using var factory = new BffFactory();
        var client = new FakeClient(factory.Clock);
        using var browser = new Browser(factory, client);
        using var tooLong = await browser.Send(HttpMethod.Post, "/api/posts", new { text = new string('a', 301) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        using var tooManyBytes = await browser.Send(HttpMethod.Post, "/api/posts", new { text = "a" + new string('\u0301', 1500) });
        Assert.Equal(HttpStatusCode.BadRequest, tooManyBytes.StatusCode);
        using var boundary = await browser.Send(HttpMethod.Post, "/api/posts", new { text = new string('a', 300) });
        Assert.Equal(HttpStatusCode.OK, boundary.StatusCode);
        Assert.Equal(1, client.CreateCount);
    }

    [Fact]
    public async Task UpstreamFailuresAreExplicitAndDeletionCanBeRetried()
    {
        await using var factory = new BffFactory();
        var client = new FakeClient(factory.Clock) { FailDelete = true };
        using var browser = new Browser(factory, client);
        using var created = await browser.Send(HttpMethod.Post, "/api/posts", new { text = "Test" });
        using var failed = await browser.Send(HttpMethod.Delete, "/api/posts/created");
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        using var state = await browser.Send(HttpMethod.Get, "/api/session");
        Assert.Contains("\"createdPost\":{", await state.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        client.FailDelete = false;
        using var retry = await browser.Send(HttpMethod.Delete, "/api/posts/created");
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
    }

    [Fact]
    public async Task CookieSettingsAndSessionCapacityAreBounded()
    {
        await using var factory = new BffFactory();
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(BffEndpoints.CookieScheme);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, options.Cookie.SecurePolicy);
        Assert.Equal("ReactBff", options.Cookie.Name);
        Assert.False(options.SlidingExpiration);
        var blueskyOptions = factory.Services.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>()
            .Get(BlueskyAuthenticationDefaults.AuthenticationScheme);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, blueskyOptions.CorrelationCookie.SecurePolicy);
        Assert.True(blueskyOptions.CorrelationCookie.HttpOnly);
        var sessions = factory.Services.GetRequiredService<BffSessions>();
        for (int i = 0; i < BffSessions.Capacity; i++)
        {
            sessions.Add(new FakeClient(factory.Clock));
        }

        using var extra = new FakeClient(factory.Clock);
        Assert.Equal(503, Assert.Throws<BffException>(() => sessions.Add(extra)).StatusCode);
    }
}

internal sealed class BffFactory : WebApplicationFactory<Program>
{
    internal FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.Configure<CookieAuthenticationOptions>(BffEndpoints.CookieScheme, options => options.TimeProvider = Clock);
        });
    }
}

internal sealed class Browser : IDisposable
{
    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);
    internal HttpClient Client { get; }
    internal string? SessionId { get; }

    internal Browser(BffFactory factory, FakeClient? sessionClient = null)
    {
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://127.0.0.1:5254"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        if (sessionClient is not null)
        {
            var sessions = factory.Services.GetRequiredService<BffSessions>();
            SessionId = sessions.Add(sessionClient);
            var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(BffEndpoints.CookieScheme);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, sessionClient.Did), new Claim(BffEndpoints.SessionClaim, SessionId)], BffEndpoints.CookieScheme));
            var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
            {
                IssuedUtc = factory.Clock.GetUtcNow(),
                ExpiresUtc = sessions.Find(SessionId)!.ExpiresAt
            }, BffEndpoints.CookieScheme);
            _cookies["ReactBff"] = options.TicketDataFormat.Protect(ticket);
        }
    }

    internal async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null, bool csrf = true)
    {
        using var message = new HttpRequestMessage(method, path);
        if (method != HttpMethod.Get && csrf)
        {
            using var tokenResponse = await Send(HttpMethod.Get, "/api/csrf");
            using var token = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
            message.Headers.Add("X-CSRF-TOKEN", token.RootElement.GetProperty("token").GetString());
        }

        if (_cookies.Count != 0)
        {
            message.Headers.Add("Cookie", string.Join("; ", _cookies.Select(cookie => $"{cookie.Key}={cookie.Value}")));
        }

        if (body is not null)
        {
            message.Content = JsonContent.Create(body);
        }

        HttpResponseMessage response = await Client.SendAsync(message);
        if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies))
        {
            foreach (string cookie in cookies)
            {
                string[] pair = cookie.Split(';')[0].Split('=', 2);
                // Retain a removed session cookie to exercise replay resistance against the server-side store.
                if (pair[1].Length != 0)
                {
                    _cookies[pair[0]] = pair[1];
                }
            }
        }

        return response;
    }

    public void Dispose() => Client.Dispose();
}

internal sealed class FakeClient(FakeTimeProvider clock) : ISessionClient
{
    public string Did => "did:plc:testuser";
    public DateTimeOffset TokenExpiresAt { get; set; } = clock.GetUtcNow().AddMinutes(10);
    internal bool RefreshSucceeds { get; set; } = true;
    internal bool Disposed { get; private set; }
    internal bool FailLogout { get; set; }
    internal bool FailDelete { get; set; }
    internal int RefreshCount { get; private set; }
    internal int CreateCount { get; private set; }
    internal int LogoutCount { get; private set; }
    internal StrongReference? DeletedReference { get; private set; }
    internal StrongReference Reference { get; init; } = new(
        new AtUri("at://did:plc:testuser/app.bsky.feed.post/3test"),
        new Cid("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"));

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        RefreshCount++;
        await Task.Delay(20, cancellationToken);
        TokenExpiresAt = clock.GetUtcNow().AddMinutes(10);
        return RefreshSucceeds;
    }

    public Task<TimelineResponse> GetTimelineAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new TimelineResponse([new TimelinePost(Reference.Uri.ToString(), "test.bsky.social", "Hello")]));

    public Task<StrongReference> CreatePostAsync(string text, CancellationToken cancellationToken)
    {
        CreateCount++;
        return Task.FromResult(Reference);
    }

    public Task DeletePostAsync(StrongReference post, CancellationToken cancellationToken)
    {
        if (FailDelete)
        {
            throw new BffException(502, "Post deletion failed (upstream HTTP 503).");
        }

        DeletedReference = post;
        return Task.CompletedTask;
    }

    public Task LogoutAsync(CancellationToken cancellationToken)
    {
        LogoutCount++;
        if (FailLogout)
        {
            throw new HttpRequestException("secret-token");
        }

        return Task.CompletedTask;
    }

    public void Dispose() => Disposed = true;
}
