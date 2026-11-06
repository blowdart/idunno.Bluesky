// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public partial class AdministrationTests
{
    [Theory]
    [InlineData("", HttpStatusCode.OK)]
    [InlineData("?limit=100", HttpStatusCode.OK)]
    [InlineData("?limit=0", HttpStatusCode.BadRequest)]
    [InlineData("?limit=101", HttpStatusCode.BadRequest)]
    [InlineData("?limit=wrong", HttpStatusCode.BadRequest)]
    [InlineData("?limit=1&limit=2", HttpStatusCode.BadRequest)]
    [InlineData("?cursor=unknown", HttpStatusCode.BadRequest)]
    public async Task OzoneOnlineMembersValidatesQueriesAndReportsObservedCaller(string query, HttpStatusCode expected)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateOzoneAsync(
            key: key,
            identity: identity,
            allowedOrigins: ["http://127.0.0.1:3000"]);
        using HttpResponseMessage response = await OzoneRequestAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.team.listOnlineMembers",
            query: query);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            JsonElement member = Assert.Single(body.RootElement.GetProperty("members").EnumerateArray());
            Assert.Equal(s_actor.Value, member.GetProperty("did").GetString());
            Assert.Equal(fixture.Clock.GetUtcNow(), member.GetProperty("lastActive").GetDateTimeOffset());
            Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        }
    }

    [Fact]
    public void OzonePresenceExpiresAndRechecksMembershipWithoutInventingActivity()
    {
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        OzoneTeamPresence presence = new(clock);
        Did first = new("did:plc:first");
        Did second = new("did:plc:second");
        LabelTeamMember[] team =
        [
            new(first, LabelTeamRole.Administrator, true, false),
            new(second, LabelTeamRole.Viewer, true, false)
        ];
        Assert.Empty(presence.List(team, 100));
        presence.Observe(first, team);
        clock.Advance(TimeSpan.FromMinutes(1));
        presence.Observe(second, team);
        Assert.Equal(second.Value, Assert.Single(presence.List(team, 1)).Did);
        Assert.Equal([second.Value, first.Value], presence.List(team, 100).Select(member => member.Did));
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal(second.Value, Assert.Single(presence.List(team, 100)).Did);
        team[1] = team[1] with { Active = false };
        Assert.Empty(presence.List(team, 100));
        presence.Observe(second, team);
        Assert.Empty(presence.List(team, 100));
        team[1] = team[1] with { Active = true };
        Assert.Empty(presence.List(team, 100));
        presence.Observe(first, team);
        team[0] = team[0] with { Removed = true };
        Assert.Empty(presence.List(team, 100));
        Assert.Empty(new OzoneTeamPresence(clock).List(team, 100));
    }

    [Theory]
    [InlineData("/.well-known/ozone-metadata.json", "GET")]
    [InlineData("/xrpc/tools.ozone.server.getConfig", "GET")]
    [InlineData("/xrpc/tools.ozone.team.listOnlineMembers", "GET")]
    [InlineData("/xrpc/app.bsky.feed.getPostThread", "GET")]
    [InlineData("/xrpc/tools.ozone.setting.listOptions", "GET")]
    [InlineData("/xrpc/tools.ozone.setting.upsertOption", "POST")]
    [InlineData("/xrpc/tools.ozone.moderation.emitEvent", "POST")]
    public async Task OzoneCorsPreflightAllowsConfiguredOriginsWithoutAuthentication(string path, string method)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateOzoneAsync(
            key: key,
            identity: identity,
            allowedOrigins: ["http://127.0.0.1:3000", "https://ozone.example"],
            fallbackAuthorizationRequired: true);
        using HttpRequestMessage request = new(HttpMethod.Options, path);
        request.Headers.Add("Origin", "http://127.0.0.1:3000");
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("http://127.0.0.1:3000", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal(method, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Methods")));
        string headers = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers"));
        Assert.Contains("Authorization", headers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Content-Type", headers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Origin", response.Headers.Vary);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.Equal(0, (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).IssuedThrough);
    }

    [Theory]
    [InlineData("http://localhost:3000", true, true, "/xrpc/tools.ozone.server.getConfig")]
    [InlineData("http://127.0.0.1:3001", true, true, "/xrpc/tools.ozone.server.getConfig")]
    [InlineData("https://untrusted.example", true, true, "/xrpc/tools.ozone.server.getConfig")]
    [InlineData("null", true, true, "/xrpc/tools.ozone.server.getConfig")]
    [InlineData("http://127.0.0.1:3000", false, true, "/xrpc/tools.ozone.server.getConfig")]
    [InlineData("http://127.0.0.1:3000", true, false, "/xrpc/tools.ozone.server.getConfig")]
    [InlineData("http://127.0.0.1:3000", true, true, "/xrpc/tools.ozone.unknown")]
    [InlineData("http://127.0.0.1:3000", true, true, "/xrpc/dev.idunno.labeler.admin.getTeam")]
    [InlineData("http://127.0.0.1:3000", true, true, "/xrpc/com.atproto.label.queryLabels")]
    [InlineData("http://127.0.0.1:3000", true, true, "/health/labeler/ready")]
    public async Task OzoneCorsDoesNotGrantUnconfiguredOriginsOrOtherRoutes(
        string origin,
        bool configureOrigins,
        bool enabled,
        string path)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateOzoneAsync(
            key: key,
            identity: identity,
            enabled: enabled,
            allowedOrigins: configureOrigins ? ["http://127.0.0.1:3000"] : null);
        foreach (HttpMethod method in new[] { HttpMethod.Options, HttpMethod.Get })
        {
            using HttpRequestMessage request = new(method, path);
            request.Headers.Add("Origin", origin);
            if (method == HttpMethod.Options)
            {
                request.Headers.Add("Access-Control-Request-Method", "GET");
                request.Headers.Add("Access-Control-Request-Headers", "authorization");
            }
            using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
            Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        }
    }

    [Theory]
    [InlineData("success", HttpStatusCode.OK)]
    [InlineData("missing-token", HttpStatusCode.Unauthorized)]
    [InlineData("outsider", HttpStatusCode.Forbidden)]
    [InlineData("insecure", HttpStatusCode.Forbidden)]
    [InlineData("disabled-remote", HttpStatusCode.NotFound)]
    [InlineData("owner-switch-off", HttpStatusCode.NotFound)]
    [InlineData("bad-query", HttpStatusCode.BadRequest)]
    [InlineData("unsupported", HttpStatusCode.NotImplemented)]
    public async Task OzoneCorsHeadersPreserveActualResponseAuthenticationAndErrors(string scenario, HttpStatusCode expected)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateOzoneAsync(
            key: key,
            identity: identity,
            mode: scenario switch
            {
                "disabled-remote" => RemoteAdministrationMode.Disabled,
                "owner-switch-off" => RemoteAdministrationMode.WebUiControlled,
                _ => RemoteAdministrationMode.Enabled
            },
            allowedOrigins: ["http://127.0.0.1:3000", "https://ozone.example"],
            fallbackAuthorizationRequired: true);
        string method = scenario == "unsupported" ? "tools.ozone.team.addMember" : "tools.ozone.server.getConfig";
        using HttpRequestMessage request = new(
            scenario == "unsupported" ? HttpMethod.Post : HttpMethod.Get,
            "/xrpc/" + method + (scenario == "bad-query" ? "?unknown=true" : ""));
        request.Headers.Add("Origin", "https://ozone.example");
        if (scenario != "insecure")
        {
            AddTrustedProxyHeaders(request);
        }
        if (scenario != "missing-token")
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                Token(
                    key: key,
                    now: fixture.Clock.GetUtcNow(),
                    audience: TestServerBuilder.s_issuer.Value,
                    method: method,
                    actor: scenario == "outsider" ? "did:plc:outsider" : null));
        }
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("https://ozone.example", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("Origin", response.Headers.Vary);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example")]
    [InlineData("null")]
    [InlineData("file:///example")]
    [InlineData("https://ozone.example/")]
    [InlineData("https://ozone.example/path")]
    [InlineData("https://ozone.example?query=true")]
    [InlineData("https://ozone.example#fragment")]
    [InlineData("https://user:password@ozone.example")]
    public async Task OzoneCorsRejectsInvalidOriginsBeforeMapping(string origin)
    {
        await using WebApplication app = WebApplication.CreateBuilder().Build();
        ArgumentException exception = Assert.Throws<ArgumentException>(() => app.MapOzoneCompatibility([origin]));
        Assert.Equal("allowedOrigins", exception.ParamName);
    }

    [Fact]
    public async Task OzoneCorsDiscoveryIsReadableOnlyFromAnAllowedOriginWithoutWeakeningHostAuthorization()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateOzoneAsync(
            key: key,
            identity: identity,
            allowedOrigins: ["http://127.0.0.1:3000"],
            fallbackAuthorizationRequired: true);
        using HttpRequestMessage request = new(HttpMethod.Get, "/.well-known/ozone-metadata.json");
        request.Headers.Add("Origin", "http://127.0.0.1:3000");
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http://127.0.0.1:3000", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.True(response.Headers.CacheControl!.NoStore);

        using HttpRequestMessage protectedRequest = new(HttpMethod.Get, "/test/fallback");
        protectedRequest.Headers.Add("Origin", "http://127.0.0.1:3000");
        using HttpResponseMessage protectedResponse = await fixture.Client.SendAsync(protectedRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
        Assert.False(protectedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task OzoneCorsPreflightDoesNotConsumeAnAssertionOrBypassReplayProtection()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateOzoneAsync(
            key: key,
            identity: identity,
            allowedOrigins: ["http://127.0.0.1:3000"]);
        string assertion = Token(
            key: key,
            now: fixture.Clock.GetUtcNow(),
            audience: TestServerBuilder.s_issuer.Value,
            method: "tools.ozone.server.getConfig");
        foreach (HttpMethod method in new[] { HttpMethod.Options, HttpMethod.Get })
        {
            using HttpRequestMessage request = new(method, "/xrpc/tools.ozone.server.getConfig");
            AddTrustedProxyHeaders(request);
            request.Headers.Add("Origin", "http://127.0.0.1:3000");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertion);
            if (method == HttpMethod.Options)
            {
                request.Headers.Add("Access-Control-Request-Method", "GET");
                request.Headers.Add("Access-Control-Request-Headers", "authorization");
            }
            using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(
                method == HttpMethod.Options ? HttpStatusCode.NoContent : HttpStatusCode.OK,
                response.StatusCode);
            Assert.Equal("http://127.0.0.1:3000", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        }

        using HttpRequestMessage replay = new(HttpMethod.Get, "/xrpc/tools.ozone.server.getConfig");
        AddTrustedProxyHeaders(replay);
        replay.Headers.Add("Origin", "http://127.0.0.1:3000");
        replay.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertion);
        using HttpResponseMessage rejected = await fixture.Client.SendAsync(replay, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal("http://127.0.0.1:3000", Assert.Single(rejected.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OzoneDiscoveryUsesConfiguredOriginAndDidKeyOnlyWhenEnabled(bool enabled)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateOzoneAsync(key, identity, enabled);
        using HttpRequestMessage request = new(HttpMethod.Get, "/.well-known/ozone-metadata.json");
        request.Headers.Host = "untrusted.example";
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(enabled ? HttpStatusCode.OK : HttpStatusCode.NotFound, response.StatusCode);
        RouteEndpoint[] routes = [.. fixture.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];
        Assert.Equal(enabled, routes.Any(route => route.RoutePattern.RawText == "/.well-known/ozone-metadata.json"));
        Assert.Equal(enabled, routes.Any(route => route.RoutePattern.RawText == "/xrpc/tools.ozone.server.getConfig"));
        if (enabled)
        {
            Assert.True(response.Headers.CacheControl!.NoStore);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(TestServerBuilder.s_issuer.Value, body.RootElement.GetProperty("did").GetString());
            Assert.Equal("https://labeler.example/", body.RootElement.GetProperty("url").GetString());
            Assert.Equal("did:key:" + key.ExportPublicKeyMultibase(), body.RootElement.GetProperty("publicKey").GetString());
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }
        else
        {
            using HttpResponseMessage native = await GetTeamAsync(fixture, Token(key, fixture.Clock.GetUtcNow()));
            Assert.Equal(HttpStatusCode.OK, native.StatusCode);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OzoneConfigAcceptsExactAndPdsAudiencesWithoutGrantingNativeAccess(bool bareAudience)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateOzoneAsync(key, identity);
        string audience = TestServerBuilder.s_issuer.Value + (bareAudience ? "" : "#atproto_labeler");
        using HttpResponseMessage response = await OzoneRequestAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.server.getConfig",
            audience: audience);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("tools.ozone.team.defs#roleAdmin", body.RootElement.GetProperty("viewer").GetProperty("role").GetString());
        Assert.Single(body.RootElement.EnumerateObject());
        using HttpResponseMessage native = await GetTeamAsync(fixture, Token(key, fixture.Clock.GetUtcNow(), audience: audience));
        Assert.Equal(HttpStatusCode.Unauthorized, native.StatusCode);
    }

    [Theory]
    [InlineData("wrong-audience", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-method", HttpStatusCode.Unauthorized)]
    [InlineData("outsider", HttpStatusCode.Forbidden)]
    [InlineData("insecure", HttpStatusCode.Forbidden)]
    [InlineData("duplicate", HttpStatusCode.BadRequest)]
    [InlineData("unknown-query", HttpStatusCode.BadRequest)]
    [InlineData("disabled-adapter", HttpStatusCode.NotFound)]
    [InlineData("disabled-remote", HttpStatusCode.NotFound)]
    [InlineData("owner-switch-off", HttpStatusCode.NotFound)]
    public async Task OzoneBoundaryRejectsUnauthorizedOrAmbiguousInvocations(string scenario, HttpStatusCode expected)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateOzoneAsync(
            key: key,
            identity: identity,
            enabled: scenario != "disabled-adapter",
            mode: scenario switch
            {
                "disabled-remote" => RemoteAdministrationMode.Disabled,
                "owner-switch-off" => RemoteAdministrationMode.WebUiControlled,
                _ => RemoteAdministrationMode.Enabled
            });
        using HttpResponseMessage response = await OzoneRequestAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.team.listMembers",
            query: scenario switch
            {
                "duplicate" => "?limit=1&limit=2",
                "unknown-query" => "?password=not-a-supported-parameter",
                _ => ""
            },
            audience: scenario == "wrong-audience" ? s_audience.ToString() : null,
            tokenMethod: scenario == "wrong-method" ? "tools.ozone.server.getConfig" : null,
            actor: scenario == "outsider" ? "did:plc:outsider" : null,
            https: scenario != "insecure");
        Assert.Equal(expected, response.StatusCode);
        if (expected != HttpStatusCode.NotFound || scenario == "owner-switch-off")
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.True(body.RootElement.TryGetProperty("error", out _));
            Assert.False(body.RootElement.TryGetProperty("succeeded", out _));
        }
    }

    [Fact]
    public async Task OzoneTeamPagesReflectLiveRolesAndSuspensionWithoutLeakingNotificationEmail()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateOzoneAsync(key, identity);
        ILabelAdministrationStore store = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelServiceResult<LabelAdministrationCaller> authentication = await store.AuthenticateLocalAsync("uid:0", token);
        Assert.True(authentication.Succeeded);
        LabelAdministrationCaller caller = authentication.Result;
        Did moderator = new("did:plc:moderator");
        Did viewer = new("did:plc:viewer");
        foreach ((Did did, LabelTeamRole role) in new[]
        {
            (moderator, LabelTeamRole.Moderator),
            (viewer, LabelTeamRole.Viewer)
        })
        {
            Assert.True((await store.ChangeTeamAsync(
                caller: caller,
                change: new(
                    operationId: "add-" + role,
                    action: LabelTeamAction.Enroll,
                    target: did,
                    reason: "Enroll a test team member.",
                    role: role),
                cancellationToken: token)).Succeeded);
        }
        using HttpResponseMessage first = await OzoneRequestAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.team.listMembers",
            query: "?limit=1");
        using JsonDocument firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync(token));
        JsonElement firstMember = Assert.Single(firstBody.RootElement.GetProperty("members").EnumerateArray());
        Assert.Equal(s_actor.Value, firstMember.GetProperty("did").GetString());
        Assert.Equal(s_actor.Value, firstBody.RootElement.GetProperty("cursor").GetString());
        Assert.False(firstMember.TryGetProperty("notificationEmail", out _));

        using HttpResponseMessage next = await OzoneRequestAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.team.listMembers",
            query: "?cursor=" + Uri.EscapeDataString(s_actor.Value));
        using JsonDocument nextBody = JsonDocument.Parse(await next.Content.ReadAsStringAsync(token));
        Assert.Equal(2, nextBody.RootElement.GetProperty("members").GetArrayLength());
        Assert.False(nextBody.RootElement.TryGetProperty("cursor", out _));

        Assert.True((await store.ChangeTeamAsync(
            caller: caller,
            change: new(
                operationId: "suspend-moderator",
                action: LabelTeamAction.Suspend,
                target: moderator,
                reason: "Suspend the test moderator."),
            cancellationToken: token)).Succeeded);
        using HttpResponseMessage filtered = await OzoneRequestAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.team.listMembers",
            query: "?disabled=true&roles=tools.ozone.team.defs%23roleModerator");
        using JsonDocument filteredBody = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync(token));
        JsonElement suspended = Assert.Single(filteredBody.RootElement.GetProperty("members").EnumerateArray());
        Assert.Equal(moderator.Value, suspended.GetProperty("did").GetString());
        Assert.True(suspended.GetProperty("disabled").GetBoolean());
        using HttpResponseMessage revoked = await OzoneRequestAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.server.getConfig",
            actor: moderator.Value);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        using HttpResponseMessage viewerConfig = await OzoneRequestAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.server.getConfig",
            actor: viewer.Value);
        using JsonDocument viewerBody = JsonDocument.Parse(await viewerConfig.Content.ReadAsStringAsync(token));
        Assert.Equal("tools.ozone.team.defs#roleTriage", viewerBody.RootElement.GetProperty("viewer").GetProperty("role").GetString());
    }

    [Fact]
    public async Task OzoneReplayAndUnsupportedMutationReturnExplicitErrorsWithoutIssuingLabels()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateOzoneAsync(key, identity);
        string assertion = Token(
            key: key,
            now: fixture.Clock.GetUtcNow(),
            audience: TestServerBuilder.s_issuer.Value,
            method: "tools.ozone.team.listMembers");
        using HttpResponseMessage first = await OzoneRequestAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.team.listMembers",
            assertion: assertion);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using HttpResponseMessage replay = await OzoneRequestAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.team.listMembers",
            assertion: assertion);
        Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
        using HttpResponseMessage unsupported = await OzoneRequestAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.team.addMember",
            post: true);
        Assert.Equal(HttpStatusCode.NotImplemented, unsupported.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await unsupported.Content.ReadAsStringAsync(token));
        Assert.Equal("NotImplemented", body.RootElement.GetProperty("error").GetString());
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    /// <summary>Creates an isolated Ozone fixture with one clock for assertion and persisted expiry checks.</summary>
    private static async Task<TestServerBuilder> CreateOzoneAsync(
        LabelSigningKey key,
        HttpClient identity,
        bool enabled = true,
        RemoteAdministrationMode mode = RemoteAdministrationMode.Enabled,
        string[]? allowedOrigins = null,
        bool fallbackAuthorizationRequired = false)
    {
        TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            options: new() { PublicEndpoint = new Uri("https://labeler.example") },
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, Policy>();
                services.AddLabelerAdministration(s_audience, key, identity);
                services.Configure<LabelerAdministrationOptions>(settings =>
                {
                    settings.RemoteMode = mode;
                });
            },
            remoteAdministrationEnabled: null,
            fallbackAuthorizationRequired: fallbackAuthorizationRequired,
            controlDatabaseTime: true,
            ozoneAdministration: enabled,
            ozoneAllowedOrigins: allowedOrigins);
        Assert.True((await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .BootstrapAsync(
                s_actor,
                "uid:0",
                "bootstrap-ozone",
                TestContext.Current.CancellationToken)).Succeeded);
        return fixture;
    }

    /// <summary>Sends a fixed-method assertion through the fixture's trusted proxy.</summary>
    private static async Task<HttpResponseMessage> OzoneRequestAsync(
        TestServerBuilder fixture,
        LabelSigningKey key,
        string method,
        string query = "",
        string? audience = null,
        string? tokenMethod = null,
        string? actor = null,
        bool https = true,
        bool post = false,
        string? assertion = null)
    {
        using HttpRequestMessage request = new(post ? HttpMethod.Post : HttpMethod.Get, "/xrpc/" + method + query);
        if (https)
        {
            AddTrustedProxyHeaders(request);
        }
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            assertion ?? Token(
                key: key,
                now: fixture.Clock.GetUtcNow(),
                audience: audience ?? TestServerBuilder.s_issuer.Value,
                method: tokenMethod ?? method,
                actor: actor));
        return await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
