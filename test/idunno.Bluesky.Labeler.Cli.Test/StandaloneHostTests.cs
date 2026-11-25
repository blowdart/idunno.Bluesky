// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Duende.IdentityModel.OidcClient.DPoP;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Labeler.UI;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Host;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;

using HostProgram = idunno.Bluesky.Labeler.Host.Program;

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>Exercises standalone host authentication, public client metadata and proxy trust boundaries.</summary>
public class StandaloneHostTests
{
    [Theory]
    [InlineData("Bearer", "atproto", false)]
    [InlineData("DPoP", "notatproto", false)]
    [InlineData("DPoP", "atproto", true)]
    public async Task OAuthCallbackHandlesRejectedTokenResponsesWithoutSigningIn(
        string tokenType,
        string scope,
        bool valid)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new(new DateTimeOffset(
            year: 2026,
            month: 1,
            day: 1,
            hour: 0,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero));
        Did did = new($"did:plc:{Guid.NewGuid():N}");
        int tokenRequests = 0;
        using RSA rsa = RSA.Create(2048);
        RSAParameters publicKey = rsa.ExportParameters(false);
        string modulus = Base64UrlEncoder.Encode(publicKey.Modulus!);
        string exponent = Base64UrlEncoder.Encode(publicKey.Exponent!);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        HostProgram.AddWebsiteAuthentication(builder.Services, new Uri("https://labeler.example"));
        builder.Services.AddOptions<BlueskyAuthenticationOptions>(BlueskyAuthenticationDefaults.AuthenticationScheme)
            .Configure(options => options.TimeProvider = clock);
        builder.Services.PostConfigure<BlueskyAgentOptions>(options =>
        {
            options.OAuthOptions = new OAuthOptions("http://localhost", new Uri("http://localhost/Bluesky/Callback"))
            {
                AllowInsecureProtocols = true,
                AllowLoopback = true
            };
        });

        await using WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapRazorPages();
        app.MapGet("/test/oauth/start", async context =>
        {
            string authority = $"{context.Request.Scheme}://{context.Request.Host}";
            OAuthLoginState state = new(
                startUrl: $"{authority}/authorize",
                state: Guid.NewGuid().ToString("N"),
                codeVerifier: "test-code-verifier",
                redirectUri: $"{authority}/Bluesky/Callback",
                error: string.Empty,
                errorDescription: string.Empty,
                expectedAuthority: authority,
                expectedService: authority,
                proofKey: JsonWebKeys.CreateRsaJson(),
                correlationId: Guid.NewGuid())
            {
                OAuthClientId = "http://localhost",
                RequestedScope = "atproto"
            };
            BlueskySignInManager manager = context.RequestServices.GetRequiredService<BlueskySignInManager>();
            await manager.SaveStateAndCreateCorrelationCookie(state, markCookieAsSecure: false);
            context.Response.Redirect(
                $"/Bluesky/Callback?code=test-code&state={state.State}&iss={Uri.EscapeDataString(authority)}");
        });
        app.MapGet("/.well-known/oauth-authorization-server", (HttpContext context) =>
        {
            string authority = $"{context.Request.Scheme}://{context.Request.Host}";
            return Results.Content(
                $$"""
                {
                  "issuer": "{{authority}}",
                  "authorization_endpoint": "{{authority}}/authorize",
                  "token_endpoint": "{{authority}}/token",
                  "pushed_authorization_request_endpoint": "{{authority}}/par",
                  "end_session_endpoint": "{{authority}}/logout",
                  "jwks_uri": "{{authority}}/jwks",
                  "response_types_supported": ["code"],
                  "grant_types_supported": ["authorization_code", "refresh_token"],
                  "code_challenge_methods_supported": ["S256"]
                }
                """,
                "application/json");
        });
        app.MapGet("/jwks", () => Results.Content(
            $$"""{"keys":[{"kty":"RSA","use":"sig","alg":"RS256","kid":"test","n":"{{modulus}}","e":"{{exponent}}"}]}""",
            "application/json"));
        app.MapPost("/token", async context =>
        {
            Interlocked.Increment(ref tokenRequests);
            string authority = $"{context.Request.Scheme}://{context.Request.Host}";
            string header = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"at+jwt"}"""));
            long expiration = clock.GetUtcNow().AddMinutes(15).ToUnixTimeSeconds();
            string payload = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(
                $$"""{"iss":"{{authority}}","aud":"did:web:localhost","sub":"{{did}}","exp":{{expiration}}}"""));
            string accessToken = $"{header}.{payload}.signature";
            context.Response.Headers["DPoP-Nonce"] = "server-issued-nonce";
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $$"""
                {
                  "access_token": "{{accessToken}}",
                  "token_type": "{{tokenType}}",
                  "expires_in": 900,
                  "refresh_token": "test-refresh-token",
                  "scope": "{{scope}}"
                }
                """,
                context.RequestAborted);
        });
        app.MapGet("/xrpc/com.atproto.server.describeServer", () =>
            Results.Content("""{"did":"did:web:localhost","availableUserDomains":[".localhost"]}""", "application/json"));
        await app.StartAsync(cancellationToken);

        Uri address = new(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using HttpClientHandler handler = new() { AllowAutoRedirect = false, UseCookies = false, UseProxy = false };
        using HttpClient client = new(handler) { BaseAddress = new Uri($"http://localhost:{address.Port}") };
        using MetricCollector<long> failures = new(
            app.Services.GetRequiredService<IMeterFactory>(),
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.signins.total.failure");

        using HttpResponseMessage start = await client.GetAsync("/test/oauth/start", cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        Assert.NotNull(start.Headers.Location);
        string correlationCookie = Assert.Single(start.Headers.GetValues("Set-Cookie")).Split(';', 2)[0];
        using HttpRequestMessage callbackRequest = new(HttpMethod.Get, start.Headers.Location);
        callbackRequest.Headers.Add("Cookie", correlationCookie);
        using HttpResponseMessage callback = await client.SendAsync(callbackRequest, cancellationToken);

        Assert.Equal(1, tokenRequests);
        Assert.Equal(valid ? HttpStatusCode.Redirect : HttpStatusCode.BadRequest, callback.StatusCode);
        BlueskyAuthenticationOptions authenticationOptions = app.Services
            .GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>()
            .Get(BlueskyAuthenticationDefaults.AuthenticationScheme);
        IEnumerable<string> setCookies = callback.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? headers) ? headers : [];
        string? authCookie = setCookies.FirstOrDefault(cookie =>
            cookie.StartsWith($"{authenticationOptions.Cookie.Name}=", StringComparison.Ordinal));
        ClaimsIdentity? storedIdentity = await authenticationOptions.IdentityStore!.GetIdentity(did, cancellationToken);
        if (valid)
        {
            Assert.NotNull(authCookie);
            Assert.NotNull(storedIdentity);
            Assert.Empty(failures.GetMeasurementSnapshot());
        }
        else
        {
            Assert.Null(authCookie);
            Assert.Null(storedIdentity);
            Assert.Equal(
                "OAuth2ResponseRejected",
                Assert.Single(failures.GetMeasurementSnapshot()).Tags[BlueskyAuthenticationMetrics.SignInFailureReasonTagName]);
        }

        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task RootRouteReturnsTheUtf8BannerWithVersionAndCommit()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        await using WebApplication app = builder.Build();
        HostProgram.MapRootRoute(app);
        await app.StartAsync(token);
        Uri address = new(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using HttpClient client = new() { BaseAddress = address };

        using HttpResponseMessage response = await client.GetAsync("/", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType.CharSet);
        Assembly uiAssembly = typeof(LabelerUIExtensions).Assembly;
        string? commit = uiAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "BuildCommitId")?.Value
            ?? uiAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string version = uiAssembly.GetName().Version?.ToString(3) ?? "Unknown";
        Assert.False(string.IsNullOrWhiteSpace(commit));
        Assert.Equal(
            Encoding.UTF8.GetBytes($"🏷️ V{version} Commit {commit}"),
            await response.Content.ReadAsByteArrayAsync(token));

        await app.StopAsync(token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RobotsRouteDisallowsAllCrawlersWithoutAuthentication(bool requireAuthorization)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        if (requireAuthorization)
        {
            HostProgram.AddWebsiteAuthentication(builder.Services, new Uri("https://labeler.example"));
            builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(
                new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        }
        await using WebApplication app = builder.Build();
        if (requireAuthorization)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }
        HostProgram.MapRootRoute(app);
        await app.StartAsync(token);
        Uri address = new(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using HttpClient client = new() { BaseAddress = address };
        using HttpResponseMessage response = await client.GetAsync("/robots.txt", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType.CharSet);
        Assert.Equal("User-agent: *\nDisallow: /\n", await response.Content.ReadAsStringAsync(token));
        await app.StopAsync(token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WebsiteMetadataUsesTheSignInOptionsAlongsideNativeMetadata(bool configured)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        if (configured)
        {
            builder.Configuration["BlueskyAgent:OAuthOptions:ClientId"] = "https://labeler.example/custom-metadata.json";
            builder.Configuration["BlueskyAgent:OAuthOptions:ReturnUri"] = "https://labeler.example/custom-callback";
        }

        HostProgram.AddWebsiteAuthentication(builder.Services, new Uri("https://labeler.example"));
        await using WebApplication app = builder.Build();
        app.UseBlueskyOAuthClientMetadata(
            HostProgram.CreateNativeOAuthOptions(
                new Uri("https://labeler.example"),
                new AtProtoServiceReference(new Did("did:web:labeler.example"), "labeler_admin")),
            new BlueskyOAuthClientMetadataOptions { ApplicationType = BlueskyOAuthClientApplicationType.Native });
        app.UseBlueskyOAuthClientMetadata();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/protected", () => "Protected").RequireAuthorization();
        await app.StartAsync(token);
        Uri address = new(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using HttpClient client = new() { BaseAddress = address };
        string path = configured ? "/custom-metadata.json" : "/oauth-client-metadata.json";
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Host = "untrusted.example";
        using HttpResponseMessage response = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        using JsonDocument metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        OAuthOptions options = app.Services
            .GetRequiredService<IOptionsMonitor<BlueskyAgentOptions>>().CurrentValue.OAuthOptions!;
        BlueskyAuthenticationOptions authentication = app.Services
            .GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>()
            .Get(BlueskyAuthenticationDefaults.AuthenticationScheme);
        Assert.Equal("/Bluesky/Login", authentication.LoginPath.Value);
        Assert.Equal("/Bluesky/Logout", authentication.LogoutPath.Value);
        Assert.Equal("/Bluesky/Error", authentication.AccessDeniedPath.Value);
        Assert.Equal(options.ClientId, metadata.RootElement.GetProperty("client_id").GetString());
        Assert.Equal(options.ReturnUri!.ToString(), metadata.RootElement.GetProperty("redirect_uris")[0].GetString());
        Assert.Equal("web", metadata.RootElement.GetProperty("application_type").GetString());
        Assert.Equal("atproto rpc:app.bsky.actor.getProfile?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview", metadata.RootElement.GetProperty("scope").GetString());
        Assert.Equal("https://labeler.example" + path, options.ClientId);
        Assert.Equal(
            configured ? "https://labeler.example/custom-callback" : "https://labeler.example/Bluesky/Callback",
            options.ReturnUri.AbsoluteUri);

        using HttpResponseMessage nativeResponse = await client.GetAsync("/native-client-metadata.json", token);
        Assert.Equal(HttpStatusCode.OK, nativeResponse.StatusCode);
        using JsonDocument native = JsonDocument.Parse(await nativeResponse.Content.ReadAsStringAsync(token));
        Assert.Equal("native", native.RootElement.GetProperty("application_type").GetString());
        Assert.Equal("http://127.0.0.1/", native.RootElement.GetProperty("redirect_uris")[0].GetString());
        await app.StopAsync(token);
    }

    [Fact]
    public void CanonicalHostSerializesBootstrapReceiptWithGeneratedMetadata()
    {
        Assert.Equal("idunno.Bluesky.Labeler.Host", typeof(HostProgram).Assembly.GetName().Name);
        LabelAdministrationReceipt receipt = new(
            Sequence: 1,
            OperationId: "bootstrap",
            Actor: new Did("did:plc:administrator"),
            LocalIdentity: "uid:1654",
            Action: "bootstrap",
            Reason: "Initial setup",
            Succeeded: true);
        string json = JsonSerializer.Serialize(receipt, HostJsonSerializerContext.Default.LabelAdministrationReceipt);
        LabelAdministrationReceipt? restored = JsonSerializer.Deserialize(json, HostJsonSerializerContext.Default.LabelAdministrationReceipt);
        Assert.Equal(receipt, restored);
    }

    [Theory]
    [InlineData("Production", "https://labeler.example", "labeler.example", true, true, true)]
    [InlineData("Production", "https://labeler.example", "labeler.example", false, true, false)]
    [InlineData("Production", "https://labeler.example", "labeler.example", true, false, false)]
    [InlineData("Development", "https://labeler.example", "labeler.example", true, true, false)]
    [InlineData("Production", "http://labeler.example", "labeler.example", true, true, false)]
    [InlineData("Production", "https://localhost", "localhost", true, true, false)]
    [InlineData("Production", "https://127.0.0.1", "127.0.0.1", true, true, false)]
    [InlineData("Production", "https://[::1]", "[::1]", true, true, false)]
    [InlineData("Production", "https://labeler.example", "localhost", true, true, false)]
    [InlineData("Production", "https://labeler.example", "127.0.0.2", true, true, false)]
    [InlineData("Production", "https://labeler.example", "[::ffff:127.0.0.1]", true, true, false)]
    public async Task HstsRequiresNonDevelopmentPublicHttpsAndTrustedTransport(
        string environment,
        string publicOrigin,
        string requestHost,
        bool trusted,
        bool forwardedHttps,
        bool expected)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        builder.Services.AddHsts(_ => { });
        if (trusted)
        {
            builder.Configuration["Labeler:TrustedProxies:0"] = "127.0.0.1";
            builder.Configuration["Labeler:TrustedProxies:1"] = "::ffff:127.0.0.1";
        }

        Uri origin = new(publicOrigin);
        await using WebApplication app = builder.Build();
        HostProgram.ConfigureTransportSecurity(app, origin);
        app.UseBlueskyOAuthClientMetadata(
            HostProgram.CreateNativeOAuthOptions(
                new Uri("https://labeler.example"),
                new AtProtoServiceReference(new Did("did:web:labeler.example"), "labeler_admin")),
            new BlueskyOAuthClientMetadataOptions { ApplicationType = BlueskyOAuthClientApplicationType.Native });
        app.MapGet("/headers", () => Results.Json(new { status = "ok" }));
        await app.StartAsync(token);
        Uri address = new(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using HttpClient client = new() { BaseAddress = address };
        foreach (string path in new[] { "/headers", "/native-client-metadata.json" })
        {
            using HttpRequestMessage request = new(HttpMethod.Get, path);
            request.Headers.Host = requestHost;
            request.Headers.Add("X-Forwarded-For", "198.51.100.1");
            request.Headers.Add("X-Forwarded-Proto", forwardedHttps ? "https" : "http");
            using HttpResponseMessage response = await client.SendAsync(request, token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(expected, response.Headers.Contains("Strict-Transport-Security"));
            Assert.False(response.Headers.Contains("Content-Security-Policy"));
            if (expected)
            {
                string hsts = Assert.Single(response.Headers.GetValues("Strict-Transport-Security"));
                Assert.StartsWith("max-age=", hsts, StringComparison.Ordinal);
                Assert.DoesNotContain("includeSubDomains", hsts, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("preload", hsts, StringComparison.OrdinalIgnoreCase);
            }
        }
        await app.StopAsync(token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyExplicitlyTrustedProxiesCanForwardHttps(bool trusted)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        if (trusted)
        {
            builder.Configuration["Labeler:TrustedProxies:0"] = "127.0.0.1";
            builder.Configuration["Labeler:TrustedProxies:1"] = "::ffff:127.0.0.1";
        }

        await using WebApplication app = builder.Build();
        HostProgram.ConfigureForwardedHeaders(app);
        app.UseBlueskyOAuthClientMetadata(
            HostProgram.CreateNativeOAuthOptions(
                new Uri("https://labeler.example"),
                new AtProtoServiceReference(new Did("did:web:labeler.example"), "labeler_admin")),
            new BlueskyOAuthClientMetadataOptions { ApplicationType = BlueskyOAuthClientApplicationType.Native });
        app.MapGet("/scheme", (HttpContext context) => context.Request.Scheme);
        await app.StartAsync(token);
        Uri address = new(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using HttpClient client = new() { BaseAddress = address };
        using HttpRequestMessage request = new(HttpMethod.Get, "/scheme");
        request.Headers.Add("X-Forwarded-For", "198.51.100.1");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using HttpResponseMessage response = await client.SendAsync(request, token);
        Assert.Equal(trusted ? "https" : "http", await response.Content.ReadAsStringAsync(token));
        using HttpResponseMessage metadataResponse = await client.GetAsync("/native-client-metadata.json", token);
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        Assert.Equal("application/json", metadataResponse.Content.Headers.ContentType!.MediaType);
        using JsonDocument metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync(token));
        Assert.Equal("https://labeler.example/native-client-metadata.json", metadata.RootElement.GetProperty("client_id").GetString());
        Assert.Equal("native", metadata.RootElement.GetProperty("application_type").GetString());
        Assert.Equal("none", metadata.RootElement.GetProperty("token_endpoint_auth_method").GetString());
        Assert.Equal("http://127.0.0.1/", metadata.RootElement.GetProperty("redirect_uris")[0].GetString());
        string[] scopes = metadata.RootElement.GetProperty("scope").GetString()!.Split(' ');
        Assert.Contains("atproto", scopes);
        Assert.Equal(scopes.Length, scopes.Distinct(StringComparer.Ordinal).Count());
        string[] retirementMethods =
        [
            "getRetirementState", "prepareRetirement", "pauseRetirement", "resumeRetirement",
            "extendGracePeriod", "requestKeyRemoval", "attestKeyRemoval", "confirmRetirementHostStopped"
        ];
        foreach (string method in retirementMethods)
        {
            Assert.Contains($"rpc:dev.idunno.labeler.admin.{method}?aud=did%3Aweb%3Alabeler.example%23labeler_admin", scopes);
        }
        Assert.Contains("identity:*", scopes);
        Assert.Contains("repo:app.bsky.labeler.service?action=create", scopes);
        Assert.Contains("repo:app.bsky.labeler.service?action=update", scopes);
        Assert.Contains("rpc:dev.idunno.labeler.admin.beginPolicyPublication?aud=did%3Aweb%3Alabeler.example%23labeler_admin", scopes);
        Assert.Contains("rpc:dev.idunno.labeler.admin.resumePolicyPublication?aud=did%3Aweb%3Alabeler.example%23labeler_admin", scopes);
        Assert.Contains("rpc:dev.idunno.labeler.admin.getAppealReports?aud=did%3Aweb%3Alabeler.example%23labeler_admin", scopes);
        Assert.Contains("rpc:dev.idunno.labeler.admin.getModerationOverview?aud=did%3Aweb%3Alabeler.example%23labeler_admin", scopes);
        Assert.Subset(scopes.ToHashSet(StringComparer.Ordinal),
            Program.CreatePlcOAuthOptions(new Uri("https://labeler.example/native-client-metadata.json")).Scopes.ToHashSet(StringComparer.Ordinal));
        Assert.Contains("rpc:dev.idunno.labeler.admin.issueLabel?aud=did%3Aweb%3Alabeler.example%23labeler_admin", scopes);
        Assert.Contains("rpc:dev.idunno.labeler.admin.completeRotation?aud=did%3Aweb%3Alabeler.example%23labeler_admin", scopes);
        await app.StopAsync(token);
    }

    [Theory]
    [InlineData("127.0.0.0/8", true, false)]
    [InlineData("192.0.2.0/24", false, false)]
    [InlineData("::1/128", true, true)]
    [InlineData("2001:db8::/32", false, true)]
    public async Task TrustedCidrsControlForwardedHeaders(string network, bool trusted, bool ipv6)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(ipv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback, 0));
        builder.Configuration["Labeler:TrustedNetworks:0"] = network;

        await using WebApplication app = builder.Build();
        HostProgram.ConfigureForwardedHeaders(app);
        app.MapGet("/scheme", (HttpContext context) => context.Request.Scheme);
        await app.StartAsync(token);
        Uri address = new(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using HttpClient client = new() { BaseAddress = address };
        using HttpRequestMessage request = new(HttpMethod.Get, "/scheme");
        request.Headers.Add("X-Forwarded-For", "198.51.100.1");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using HttpResponseMessage response = await client.SendAsync(request, token);
        Assert.Equal(trusted ? "https" : "http", await response.Content.ReadAsStringAsync(token));
        await app.StopAsync(token);
    }

    [Theory]
    [InlineData("/Xenon/ForwardedHeaders", "GET", true)]
    [InlineData("/XENON/FORWARDEDHEADERS/", "GET", true)]
    [InlineData("/prefix/Xenon/ForwardedHeaders", "GET", true)]
    [InlineData("/Xenon/ForwardedHeaders", "POST", false)]
    [InlineData("/Xenon/ForwardedHeaders/other", "GET", false)]
    [InlineData("/Xenon", "GET", false)]
    public async Task ForwardedHeadersCaptureIsLimitedToTheDiagnosticPage(
        string path,
        string method,
        bool expectedCapture)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        await using WebApplication app = builder.Build();
        app.UsePathBase("/prefix");
        app.UseLabelerForwardedHeadersDiagnostics();
        app.Run(context => context.Response.WriteAsync(
            context.Items.ContainsKey(typeof(ForwardedHeadersSnapshot)) ? "captured" : "not captured"));
        await app.StartAsync(token);
        Uri address = new(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using HttpClient client = new() { BaseAddress = address };
        using HttpRequestMessage request = new(new HttpMethod(method), path);
        request.Headers.Add("X-Forwarded-For", "198.51.100.1");
        using HttpResponseMessage response = await client.SendAsync(request, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedCapture ? "captured" : "not captured", await response.Content.ReadAsStringAsync(token));
        await app.StopAsync(token);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DockerProbeUsesStandardReadinessMiddlewareWithoutStartingAHost(bool ready)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        builder.Services.AddHealthChecks().AddCheck("readiness", () =>
            ready ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy());
        await using WebApplication app = builder.Build();
        app.MapHealthChecks("/health/labeler/ready");
        await app.StartAsync(token);
        Uri address = new(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        int exitCode = await HostProgram.Main(
            ["--health-check", new Uri(address, "/health/labeler/ready").AbsoluteUri]);
        Assert.Equal(ready ? 0 : 1, exitCode);
        await app.StopAsync(token);
    }

    [Theory]
    [InlineData(204)]
    [InlineData(302)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task DockerProbeRejectsNonReadyResponsesAndDoesNotFollowRedirects(int status)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        await using WebApplication app = builder.Build();
        bool followedRedirect = false;
        app.MapGet("/health/labeler/ready", (HttpContext context) =>
        {
            context.Response.StatusCode = status;
            context.Response.Headers.Location = "/redirected";
        });
        app.MapGet("/redirected", () =>
        {
            followedRedirect = true;
            return "Healthy";
        });
        await app.StartAsync(token);
        Uri address = new(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        Assert.Equal(1, await HostProgram.CheckReadinessAsync(new Uri(address, "/health/labeler/ready"), token));
        Assert.False(followedRedirect);
        await app.StopAsync(token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DockerProbeFailsWhenTransportAbortsOrRequestIsCancelled(bool cancel)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        await using WebApplication app = builder.Build();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        app.MapGet("/health/labeler/ready", async context =>
        {
            entered.TrySetResult();
            if (cancel)
            {
                await release.Task.WaitAsync(token);
            }
            else
            {
                context.Abort();
            }
        });
        await app.StartAsync(token);
        Uri address = new(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<int> probe = HostProgram.CheckReadinessAsync(new Uri(address, "/health/labeler/ready"), cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
            if (cancel)
            {
                await cancellation.CancelAsync();
            }

            Assert.Equal(1, await probe.WaitAsync(TimeSpan.FromSeconds(30), token));
        }
        finally
        {
            release.TrySetResult();
            await app.StopAsync(token);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-url")]
    [InlineData("file:///tmp/ready")]
    [InlineData("https://user:password@labeler.example/health/labeler/ready")]
    public async Task DockerProbeRejectsInvalidArgumentsWithoutLoadingHostConfiguration(string? endpoint)
    {
        string[] args = endpoint is null ? ["--health-check"] : ["--health-check", endpoint];
        Assert.Equal(2, await HostProgram.Main(args));
    }

    [Fact]
    public async Task IdentityClientRefusesLoopbackDestinations()
    {
        using HttpClient client = HostProgram.CreateIdentityClient();
        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync("https://127.0.0.1/.well-known/did.json", TestContext.Current.CancellationToken));
        Assert.NotNull(exception.InnerException);
        Assert.Contains("Unsafe", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
