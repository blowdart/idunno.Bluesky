// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net;
using System.Net.Http.Headers;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public class AdministrationTests
{
    private static readonly Did s_actor = new("did:plc:administration-actor");
    private static readonly AtProtoServiceReference s_audience = new(TestServerBuilder.s_issuer, "labeler_admin");

    [Theory]
    [InlineData("missing", HttpStatusCode.Unauthorized)]
    [InlineData("oauth", HttpStatusCode.Unauthorized)]
    [InlineData("bare-audience", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-service", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-method", HttpStatusCode.Unauthorized)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData("fifteen-minute", HttpStatusCode.OK)]
    [InlineData("over-limit", HttpStatusCode.Unauthorized)]
    [InlineData("outsider", HttpStatusCode.Forbidden)]
    [InlineData("member", HttpStatusCode.OK)]
    [InlineData("untrusted-proxy", HttpStatusCode.Forbidden)]
    [InlineData("trusted-proxy", HttpStatusCode.OK)]
    [InlineData("trusted-proxy-chain", HttpStatusCode.OK)]
    [InlineData("spoofed-proxy-chain", HttpStatusCode.Forbidden)]
    public async Task RemoteAuthenticationRequiresExactMethodAudienceAndLiveMembership(string kind, HttpStatusCode expected)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(s_actor, "uid:0", "bootstrap", TestContext.Current.CancellationToken)).Succeeded);
        DateTimeOffset now = await DatabaseNowAsync(fixture);
        fixture.Clock.SetUtcNow(now);
        string? token = kind switch
        {
            "missing" => null,
            "oauth" => "pds-oauth-access-token",
            "bare-audience" => Token(key, now, audience: TestServerBuilder.s_issuer.Value),
            "wrong-service" => Token(key, now, audience: TestServerBuilder.s_issuer.Value + "#other"),
            "wrong-method" => Token(key, now, method: "dev.idunno.labeler.admin.getAudit"),
            "expired" => Token(key, now.AddMinutes(-2)),
            "fifteen-minute" => Token(key, now, lifetime: TimeSpan.FromMinutes(15)),
            "over-limit" => Token(key, now, lifetime: TimeSpan.FromMinutes(15).Add(TimeSpan.FromSeconds(1))),
            "outsider" => Token(key, now, actor: "did:plc:outsider"),
            _ => Token(key, now)
        };
        using HttpRequestMessage request = new(HttpMethod.Get, "http://localhost/xrpc/dev.idunno.labeler.admin.getTeam");
        if (kind is not ("untrusted-proxy" or "trusted-proxy" or "trusted-proxy-chain" or "spoofed-proxy-chain"))
        {
            AddTrustedProxyHeaders(request);
        }

        if (kind is "untrusted-proxy" or "trusted-proxy" or "trusted-proxy-chain" or "spoofed-proxy-chain")
        {
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
            request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
            request.Headers.TryAddWithoutValidation(
                "X-Test-Remote-IP",
                kind switch
                {
                    "trusted-proxy" => "192.0.2.1",
                    "trusted-proxy-chain" => "192.0.2.2",
                    _ => "192.0.2.99"
                });
            if (kind is "trusted-proxy-chain" or "spoofed-proxy-chain")
            {
                request.Headers.Remove("X-Forwarded-For");
                request.Headers.TryAddWithoutValidation(
                    "X-Forwarded-For",
                    kind == "trusted-proxy-chain" ? "198.51.100.10, 192.0.2.1" : "192.0.2.1");
                request.Headers.Remove("X-Forwarded-Proto");
                request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https,http");
            }
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        if (kind is "untrusted-proxy" or "spoofed-proxy-chain")
        {
            Assert.Empty(response.Headers.WwwAuthenticate);
        }

        if (kind == "untrusted-proxy")
        {
            using HttpResponseMessage secure = await GetTeamAsync(fixture, token!);
            Assert.Equal(HttpStatusCode.OK, secure.StatusCode);
        }

        using HttpResponseMessage publicResponse = await fixture.Client.GetAsync(
            "/xrpc/com.atproto.label.queryLabels?uriPatterns=did:plc:subject", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
    }

    [Fact]
    public async Task ServiceTokenReplayIsDurableAndCoversEquivalentSignatures()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(s_actor, "uid:0", "bootstrap", TestContext.Current.CancellationToken)).Succeeded);
        DateTimeOffset now = await DatabaseNowAsync(fixture);
        fixture.Clock.SetUtcNow(now);
        string token = Token(key, now);
        string equivalentSignature = MalleateSignature(token);
        using HttpResponseMessage first = await GetTeamAsync(fixture, equivalentSignature);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using HttpResponseMessage replay = await GetTeamAsync(fixture, token);
        Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
        using HttpResponseMessage rejectedEquivalentReplay = await GetTeamAsync(fixture, equivalentSignature);
        Assert.Equal(HttpStatusCode.Forbidden, rejectedEquivalentReplay.StatusCode);
        ServiceAuthenticationResult evidence = await ServiceAuthentication.VerifyAsync(
            token: token, audience: s_audience, method: "dev.idunno.labeler.admin.getTeam",
            httpClient: identity, timeProvider: fixture.Clock, cancellationToken: TestContext.Current.CancellationToken);
        using ServiceProvider restarted = fixture.OtherProvider();
        ILabelAdministrationStore other = restarted.GetRequiredService<ILabelAdministrationStore>();
        Assert.False((await other.AuthenticateServiceAsync(evidence, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Theory]
    [InlineData("::ffff:192.0.2.1", HttpStatusCode.OK)]
    [InlineData("203.0.113.10", HttpStatusCode.OK)]
    [InlineData("::ffff:203.0.113.10", HttpStatusCode.OK)]
    [InlineData("::1", HttpStatusCode.OK)]
    [InlineData("::ffff:192.0.2.99", HttpStatusCode.Forbidden)]
    [InlineData("2001:db8::1", HttpStatusCode.Forbidden)]
    public async Task ProxyTrustPreservesIpv6AndRecognizesMappedIpv4(string peer, HttpStatusCode expected)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(s_actor, "uid:0", "bootstrap", TestContext.Current.CancellationToken)).Succeeded);
        DateTimeOffset now = await DatabaseNowAsync(fixture);
        fixture.Clock.SetUtcNow(now);
        using HttpRequestMessage request = new(HttpMethod.Get, "http://localhost/xrpc/dev.idunno.labeler.admin.getTeam");
        request.Headers.TryAddWithoutValidation("X-Test-Remote-IP", peer);
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(key, now));

        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("issueLabel", "valid", HttpStatusCode.OK, null)]
    [InlineData("enroll", "valid", HttpStatusCode.OK, null)]
    [InlineData("changeTeam", "valid", HttpStatusCode.OK, null)]
    [InlineData("issueLabel", "viewer", HttpStatusCode.Forbidden, "Unauthorized")]
    [InlineData("issueLabel", "malformed", HttpStatusCode.BadRequest, "InvalidRequest")]
    [InlineData("issueLabel", "oversized", HttpStatusCode.BadRequest, "InvalidRequest")]
    [InlineData("issueLabel", "mismatched", HttpStatusCode.BadRequest, "InvalidRequest")]
    [InlineData("issueLabel", "null", HttpStatusCode.BadRequest, "InvalidRequest")]
    [InlineData("issueLabel", "no-reason", HttpStatusCode.BadRequest, "InvalidRequest")]
    public async Task RemotePostsEnforceAuthorizationAndAuditRejectedBodies(
        string method,
        string kind,
        HttpStatusCode expected,
        string? expectedError)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(s_actor, "uid:0", "bootstrap", TestContext.Current.CancellationToken)).Succeeded);
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", TestContext.Current.CancellationToken)).Result!;
        Did viewer = new("did:plc:viewer");
        Assert.True((await new LabelTeamService(team, identity).EnrollAsync(
            caller: administrator, member: viewer, role: LabelTeamRole.Viewer,
            operationId: "setup-viewer", reason: "Set up viewer.", cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        DateTimeOffset now = await DatabaseNowAsync(fixture);
        fixture.Clock.SetUtcNow(now);
        string body = method switch
        {
            "enroll" => """{"operationId":"post-operation","target":"did:plc:new-member","role":0,"reason":"Enroll member."}""",
            "changeTeam" => """{"operationId":"post-operation","target":"did:plc:viewer","action":1,"role":1,"reason":"Promote viewer."}""",
            _ => """{"operationId":"post-operation","target":"did:plc:subject","value":"spam","reason":"Issue a label."}"""
        };
        body = kind switch
        {
            "malformed" => "{",
            "oversized" => new string(' ', AdministrationWire.MaximumRequestBytes + 1),
            "mismatched" => """{"method":"enroll","operationId":"post-operation","target":"did:plc:new-member","reason":"Must not dispatch."}""",
            "null" => "null",
            "no-reason" => """{"operationId":"post-operation","target":"did:plc:subject","value":"spam"}""",
            _ => body
        };
        using HttpRequestMessage request = new(HttpMethod.Post, "http://localhost/xrpc/dev.idunno.labeler.admin." + method);
        AddTrustedProxyHeaders(request);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(
            key: key, now: now, method: "dev.idunno.labeler.admin." + method,
            actor: kind == "viewer" ? viewer.Value : s_actor.Value));
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        using JsonDocument result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(expected == HttpStatusCode.OK, result.RootElement.GetProperty("succeeded").GetBoolean());
        if (expectedError is not null)
        {
            Assert.Equal(expectedError, result.RootElement.GetProperty("error").GetString());
        }

        ILabelStore labels = fixture.Services.GetRequiredService<ILabelStore>();
        LabelStoreState state = await labels.GetStateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected == HttpStatusCode.OK && method == "issueLabel" ? 1 : 0, state.IssuedThrough);
        IReadOnlyList<LabelTeamMember> members = (await team.GetTeamAsync(administrator, TestContext.Current.CancellationToken)).Result!;
        Assert.Equal(expected == HttpStatusCode.OK && method == "enroll" ? 3 : 2, members.Count);
        Assert.Equal(expected == HttpStatusCode.OK && method == "changeTeam" ? LabelTeamRole.Moderator : LabelTeamRole.Viewer,
            Assert.Single(members, member => member.Did == viewer).Role);
        IReadOnlyList<LabelAdministrationReceipt> audit = (await team.GetAdministrationAuditAsync(
            administrator, cancellationToken: TestContext.Current.CancellationToken)).Result!;
        if (expected != HttpStatusCode.OK)
        {
            Assert.Contains(audit, receipt => !receipt.Succeeded && receipt.Actor == (kind == "viewer" ? viewer : s_actor));
            Assert.DoesNotContain(audit, receipt => receipt.Succeeded && receipt.OperationId == "post-operation");
        }
        else
        {
            Assert.Contains(audit, receipt => receipt.Succeeded && receipt.Actor == s_actor && receipt.OperationId == "post-operation");
        }
    }

    [Fact]
    public async Task RemoteAdministrationCanBeDisabledWithoutDisablingLocalAdministration()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        string endpoint = LocalEndpoint();
        await using TestServerBuilder fixture = await CreateAsync(
            key, identity, endpoint, remoteAdministrationEnabled: false);
        LabelServiceResult<LabelAdministrationReceipt> bootstrap = await fixture.Services.BootstrapLocalAdministratorAsync(
            s_actor, "bootstrap", TestContext.Current.CancellationToken);
        Assert.True(bootstrap.Succeeded, bootstrap.Error?.Message);

        using HttpResponseMessage remote = await GetTeamAsync(fixture, Token(key, await DatabaseNowAsync(fixture)));
        Assert.Equal(HttpStatusCode.NotFound, remote.StatusCode);

        using JsonDocument local = JsonDocument.Parse(await LabelerLocalAdministration.SendLocalCommandAsync(
            endpoint, """{"method":"getTeam"}""", TestContext.Current.CancellationToken));
        Assert.True(local.RootElement.GetProperty("succeeded").GetBoolean());
    }

    [Fact]
    public async Task ServiceTokenRoutesBypassHostFallbackPolicyButStillRequireServiceAuthentication()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, Policy>();
                services.AddLabelerAdministration(s_audience, key, identity);
            },
            fallbackAuthorizationRequired: true);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(s_actor, "uid:0", "bootstrap", TestContext.Current.CancellationToken)).Succeeded);
        DateTimeOffset now = await DatabaseNowAsync(fixture);
        fixture.Clock.SetUtcNow(now);

        using HttpResponseMessage fallback = await fixture.Client.GetAsync("/test/fallback", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, fallback.StatusCode);

        using HttpRequestMessage missingRequest = new(HttpMethod.Get, "http://localhost/xrpc/dev.idunno.labeler.admin.getTeam");
        AddTrustedProxyHeaders(missingRequest);
        using HttpResponseMessage missingCredentials = await fixture.Client.SendAsync(
            missingRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, missingCredentials.StatusCode);

        using HttpResponseMessage authorized = await GetTeamAsync(fixture, Token(key, now));
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(AdministrationWire.MaximumRequestBytes + 1)]
    public async Task InvalidWindowsPipeFrameIsAuditedWithoutStoppingTheListener(int length)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        string endpoint = LocalEndpoint();
        await using TestServerBuilder fixture = await CreateAsync(key, identity, endpoint);
        Assert.True((await fixture.Services.BootstrapLocalAdministratorAsync(
            s_actor, "bootstrap", TestContext.Current.CancellationToken)).Succeeded);

        await using (NamedPipeClientStream malformed = new(
            serverName: ".", pipeName: endpoint, direction: PipeDirection.Out, options: PipeOptions.Asynchronous))
        {
            await malformed.ConnectAsync(TestContext.Current.CancellationToken);
            byte[] invalidLength = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(invalidLength, length);
            await malformed.WriteAsync(invalidLength, TestContext.Current.CancellationToken);
            await malformed.FlushAsync(TestContext.Current.CancellationToken);
        }

        using JsonDocument response = JsonDocument.Parse(await LabelerLocalAdministration.SendLocalCommandAsync(
            endpoint, """{"method":"getTeam"}""", TestContext.Current.CancellationToken));
        Assert.True(response.RootElement.GetProperty("succeeded").GetBoolean());
        ILabelAdministrationStore store = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller caller = (await store.AuthenticateLocalAsync(
            LabelerLocalAdministration.CurrentIdentity(), TestContext.Current.CancellationToken)).Result!;
        IReadOnlyList<LabelAdministrationReceipt> audit = (await store.GetAdministrationAuditAsync(
            caller, cancellationToken: TestContext.Current.CancellationToken)).Result!;
        Assert.Contains(audit, receipt => !receipt.Succeeded && receipt.Reason == "Malformed local administrative frame.");
    }

    [Theory]
    [InlineData("getPublicationState", null)]
    [InlineData("getPublicationState", "")]
    [InlineData("getPublicationState", "   ")]
    [InlineData("getOperation", null)]
    [InlineData("getOperation", "")]
    [InlineData("getOperation", "   ")]
    public async Task LocalInspectionSuppliesDefaultReasonWithoutChangingMutationRequirements(string method, string? reason)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        string endpoint = LocalEndpoint();
        await using TestServerBuilder fixture = await CreateAsync(key, identity, endpoint);
        Assert.True((await fixture.Services.BootstrapLocalAdministratorAsync(
            s_actor, "bootstrap", TestContext.Current.CancellationToken)).Succeeded);
        using JsonDocument issued = JsonDocument.Parse(await LabelerLocalAdministration.SendLocalCommandAsync(
            endpoint, """{"method":"issueLabel","operationId":"inspection-operation","target":"did:plc:subject","value":"spam","reason":"Issue for inspection."}""",
            TestContext.Current.CancellationToken));
        Assert.True(issued.RootElement.GetProperty("succeeded").GetBoolean());
        string reasonProperty = reason is null ? string.Empty : ",\"reason\":\"" + reason + "\"";
        using JsonDocument response = JsonDocument.Parse(await LabelerLocalAdministration.SendLocalCommandAsync(
            endpoint, $$"""{"method":"{{method}}","operationId":"inspection-operation"{{reasonProperty}}}""", TestContext.Current.CancellationToken));
        Assert.True(response.RootElement.GetProperty("succeeded").GetBoolean());
        using JsonDocument mutation = JsonDocument.Parse(await LabelerLocalAdministration.SendLocalCommandAsync(
            endpoint, """{"method":"issueLabel","operationId":"no-reason","target":"did:plc:subject","value":"spam"}""",
            TestContext.Current.CancellationToken));
        Assert.Equal("InvalidRequest", mutation.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task RemoteAuthenticationHonorsConfiguredServiceTokenLifetime()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key, identity, maximumServiceTokenLifetime: TimeSpan.FromSeconds(30));
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(s_actor, "uid:0", "bootstrap", TestContext.Current.CancellationToken)).Succeeded);
        DateTimeOffset now = await DatabaseNowAsync(fixture);
        fixture.Clock.SetUtcNow(now);
        using HttpResponseMessage response = await GetTeamAsync(
            fixture, Token(key, now, lifetime: TimeSpan.FromSeconds(31)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("""{"method":"changeTeam","reason":null}""", "InvalidRequest")]
    [InlineData("""{"method":"changeTeam","actor":"did:plc:spoofed"}""", "InvalidRequest")]
    [InlineData("""{"method":"getTeam","target":null}""", "InvalidRequest")]
    [InlineData("""{"method":"unknownMethod"}""", "InvalidRequest")]
    [InlineData("""{"method":"getAudit","limit":17}""", "InvalidRequest")]
    [InlineData("""{"method":"issueLabel","operationId":"invalid-uri","target":"at://did:plc:subject/app.bsky.feed.post/key?x=y","value":"spam","reason":"Reject malformed record URI."}""", "InvalidRequest")]
    [InlineData("""{"method":"issueLabel","operationId":"rejected-policy","target":"did:plc:administration-actor","value":"unavailable","reason":"Reject an undeclared value."}""", "ValueNotDeclared")]
    public async Task LocalChannelAuditsRejectedInvocationsWithoutStoppingTheHost(string command, string expectedError)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        string endpoint = LocalEndpoint();
        await using TestServerBuilder fixture = await CreateAsync(key, identity, endpoint);
        LabelServiceResult<LabelAdministrationReceipt> bootstrap = await fixture.Services.BootstrapLocalAdministratorAsync(
            s_actor, "bootstrap", TestContext.Current.CancellationToken);
        Assert.True(bootstrap.Succeeded);
        using JsonDocument invalid = JsonDocument.Parse(await LabelerLocalAdministration.SendLocalCommandAsync(
            endpoint, command, TestContext.Current.CancellationToken));
        Assert.False(invalid.RootElement.GetProperty("succeeded").GetBoolean());
        Assert.Equal(expectedError, invalid.RootElement.GetProperty("error").GetString());
        using JsonDocument valid = JsonDocument.Parse(await LabelerLocalAdministration.SendLocalCommandAsync(
            endpoint, """{"method":"getTeam"}""", TestContext.Current.CancellationToken));
        Assert.True(valid.RootElement.GetProperty("succeeded").GetBoolean());
        ILabelAdministrationStore store = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller caller = (await store.AuthenticateLocalAsync(
            bootstrap.Result!.LocalIdentity!, TestContext.Current.CancellationToken)).Result!;
        IReadOnlyList<LabelAdministrationReceipt> audit = (await store.GetAdministrationAuditAsync(
            caller, cancellationToken: TestContext.Current.CancellationToken)).Result!;
        Assert.Contains(audit, receipt => !receipt.Succeeded && receipt.Actor == s_actor &&
            receipt.LocalIdentity == bootstrap.Result.LocalIdentity);
    }

    [Fact]
    public async Task LocalChannelRequiresExplicitOsDelegationAndHonorsRevocation()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        string endpoint = LocalEndpoint();
        await using TestServerBuilder fixture = await CreateAsync(key, identity, endpoint);
        using JsonDocument denied = JsonDocument.Parse(await LabelerLocalAdministration.SendLocalCommandAsync(
            endpoint, """{"method":"getTeam"}""", TestContext.Current.CancellationToken));
        Assert.False(denied.RootElement.GetProperty("succeeded").GetBoolean());
        LabelServiceResult<LabelAdministrationReceipt> bootstrap = await fixture.Services.BootstrapLocalAdministratorAsync(
            s_actor, "bootstrap", TestContext.Current.CancellationToken);
        Assert.True(bootstrap.Succeeded, bootstrap.Error?.Message);
        string osIdentity = bootstrap.Result!.LocalIdentity!;
        Assert.StartsWith(OperatingSystem.IsWindows() ? "sid:" : "uid:", osIdentity, StringComparison.Ordinal);
        using JsonDocument allowed = JsonDocument.Parse(await LabelerLocalAdministration.SendLocalCommandAsync(
            endpoint, """{"method":"getTeam"}""", TestContext.Current.CancellationToken));
        Assert.True(allowed.RootElement.GetProperty("succeeded").GetBoolean());
        if (OperatingSystem.IsWindows())
        {
            using NamedPipeClientStream pipe = new(
                serverName: ".", pipeName: endpoint, direction: PipeDirection.InOut,
                options: PipeOptions.Asynchronous, impersonationLevel: TokenImpersonationLevel.Identification);
            await pipe.ConnectAsync(TestContext.Current.CancellationToken);
            PipeSecurity security = pipe.GetAccessControl();
            Assert.True(security.AreAccessRulesProtected);
            using WindowsIdentity current = WindowsIdentity.GetCurrent();
            Assert.Equal(current.User, security.GetOwner(typeof(SecurityIdentifier)));
            PipeAccessRule[] rules = [.. security.GetAccessRules(
                includeExplicit: true, includeInherited: true, targetType: typeof(SecurityIdentifier)).Cast<PipeAccessRule>()];
            Assert.Contains(rules, rule => OperatingSystem.IsWindows() && rule.AccessControlType == AccessControlType.Deny &&
                rule.IdentityReference.Equals(new SecurityIdentifier(WellKnownSidType.NetworkSid, null)));
            Assert.Single(rules, rule => OperatingSystem.IsWindows() && rule.AccessControlType == AccessControlType.Allow && rule.IdentityReference.Equals(current.User));
        }

        ILabelAdministrationStore store = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller caller = (await store.AuthenticateLocalAsync(osIdentity, TestContext.Current.CancellationToken)).Result!;
        IReadOnlyList<LabelAdministrationReceipt> audit = (await store.GetAdministrationAuditAsync(
            caller, cancellationToken: TestContext.Current.CancellationToken)).Result!;
        Assert.Contains(audit, receipt => !receipt.Succeeded && receipt.Actor is null && receipt.LocalIdentity == osIdentity);
        Assert.True((await store.ChangeTeamAsync(caller, new(
            operationId: "revoke-local", action: LabelTeamAction.RevokeLocal, target: s_actor,
            reason: "Remove OS delegation.", credential: osIdentity), TestContext.Current.CancellationToken)).Succeeded);
        using JsonDocument revoked = JsonDocument.Parse(await LabelerLocalAdministration.SendLocalCommandAsync(
            endpoint, """{"method":"getTeam"}""", TestContext.Current.CancellationToken));
        Assert.False(revoked.RootElement.GetProperty("succeeded").GetBoolean());
        if (OperatingSystem.IsLinux())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(endpoint));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(Path.GetDirectoryName(endpoint)!));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedLocalPeerConsumesFrameBeforeReplyingWithoutParsingIt(bool revoked)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        ILabelAdministrationStore store = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await store.BootstrapAsync(s_actor, "uid:0", "bootstrap", TestContext.Current.CancellationToken)).Succeeded);
        LabelAdministrationCaller administrator = (await store.AuthenticateLocalAsync("uid:0", TestContext.Current.CancellationToken)).Result!;
        if (revoked)
        {
            foreach (LabelTeamAction action in new[] { LabelTeamAction.BindLocal, LabelTeamAction.RevokeLocal })
            {
                Assert.True((await store.ChangeTeamAsync(
                    caller: administrator,
                    change: new(
                        operationId: action.ToString(), action: action, target: s_actor,
                        reason: "Configure revoked peer.", credential: "uid:42"),
                    cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
            }
        }

        using MemoryStream frame = new();
        await LabelerLocalAdministration.WriteFrameAsync(frame, "{"u8.ToArray(), TestContext.Current.CancellationToken);
        await using ReadBeforeReplyStream stream = new(frame.ToArray());
        using LocalAdministrationHost host = new(
            endpoint: "unused",
            dispatcher: fixture.Services.GetRequiredService<AdministrationDispatcher>(),
            store: store,
            logger: NullLogger<LocalAdministrationHost>.Instance);
        await host.ProcessAsync(stream, "uid:42", TestContext.Current.CancellationToken);
        Assert.Equal(stream.Length, stream.Position);
        stream.Response.Position = 0;
        byte[] response = await LabelerLocalAdministration.ReadFrameAsync(
            stream.Response, AdministrationWire.MaximumResponseBytes, TestContext.Current.CancellationToken);
        using JsonDocument denied = JsonDocument.Parse(response);
        Assert.Equal("Unauthorized", denied.RootElement.GetProperty("error").GetString());
        IReadOnlyList<LabelAdministrationReceipt> audit = (await store.GetAdministrationAuditAsync(
            administrator, cancellationToken: TestContext.Current.CancellationToken)).Result!;
        Assert.Contains(audit, receipt => !receipt.Succeeded && receipt.LocalIdentity == "uid:42");
        Assert.DoesNotContain(audit, receipt => receipt.Reason == "Malformed local administrative invocation.");
    }

    [Fact]
    public async Task AuditPageWithMaximumEscapedReasonsFitsLocalFrameLimit()
    {
        const int maximumAuditPageSize = 16;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        string endpoint = LocalEndpoint();
        await using TestServerBuilder fixture = await CreateAsync(key, identity, endpoint);
        LabelServiceResult<LabelAdministrationReceipt> bootstrap = await fixture.Services.BootstrapLocalAdministratorAsync(
            s_actor, "bootstrap", TestContext.Current.CancellationToken);
        Assert.True(bootstrap.Succeeded, bootstrap.Error?.Message);

        ILabelAdministrationStore store = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        string reason = new('\u0001', 4096);
        for (int index = 0; index < maximumAuditPageSize; index++)
        {
            Assert.True((await store.RecordAdministrationDenialAsync(
                reason, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        }

        string command = $$"""{"method":"getAudit","afterSequence":{{bootstrap.Result!.Sequence}},"limit":16}""";
        string wireResponse = await LabelerLocalAdministration.SendLocalCommandAsync(
            endpoint, command, TestContext.Current.CancellationToken);
        Assert.True(Encoding.UTF8.GetByteCount(wireResponse) < 1024 * 1024);
        using JsonDocument response = JsonDocument.Parse(wireResponse);
        JsonElement audit = response.RootElement.GetProperty("audit");
        Assert.Equal(maximumAuditPageSize, audit.GetArrayLength());
        Assert.All(audit.EnumerateArray(), entry =>
            Assert.Equal(4096, entry.GetProperty("reason").GetString()!.Length));
    }

    [Fact]
    public void LinuxSocketDirectoriesRejectSymlinksPermissiveModesAndReplaceableAncestors()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string parent = Path.Combine(Path.GetTempPath(), "labeler-permissions-" + Guid.NewGuid().ToString("N"));
        string directory = Path.Combine(parent, "private");
        string link = Path.Combine(parent, "link");
        UnixFileMode privateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        Directory.CreateDirectory(directory, privateMode);
        File.SetUnixFileMode(parent, privateMode);
        try
        {
            LocalPeer.VerifyDirectory(directory);
            File.SetUnixFileMode(directory, privateMode | UnixFileMode.OtherRead);
            Assert.Throws<UnauthorizedAccessException>(() => LocalPeer.VerifyDirectory(directory));
            File.SetUnixFileMode(directory, privateMode);
            Directory.CreateSymbolicLink(link, directory);
            Assert.Throws<UnauthorizedAccessException>(() => LocalPeer.VerifyDirectory(link));
            File.SetUnixFileMode(parent, privateMode | UnixFileMode.GroupWrite);
            Assert.Throws<UnauthorizedAccessException>(() => LocalPeer.VerifyDirectory(directory));
        }
        finally
        {
            File.SetUnixFileMode(parent, privateMode);
            File.Delete(link);
            Directory.Delete(directory);
            Directory.Delete(parent);
        }
    }

    /// <summary>Creates an opt-in administrative host without changing public-distribution defaults.</summary>
    private static async Task<TestServerBuilder> CreateAsync(
        LabelSigningKey key,
        HttpClient identity,
        string? endpoint = null,
        TimeSpan? maximumServiceTokenLifetime = null,
        bool remoteAdministrationEnabled = true)
    {
        TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, Policy>();
                services.AddLabelerAdministration(s_audience, key, identity, maximumServiceTokenLifetime);
                if (endpoint is not null)
                {
                    services.AddLabelerLocalAdministration(endpoint);
                }
            },
            remoteAdministrationEnabled: remoteAdministrationEnabled);
        fixture.OwnedSocketDirectory = endpoint is not null && OperatingSystem.IsLinux() ? Path.GetDirectoryName(endpoint) : null;
        return fixture;
    }

    /// <summary>Uses the database's own clock to control persisted service-token deadlines.</summary>
    private static async Task<DateTimeOffset> DatabaseNowAsync(TestServerBuilder fixture)
    {
        using SqliteConnection connection = new($"Data Source={fixture.DatabasePath};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT CAST((julianday('now') - 2440587.5)*86400000 AS INTEGER)";
        return DateTimeOffset.FromUnixTimeMilliseconds((long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!);
    }

    /// <summary>Returns a task-owned OS endpoint, with Linux directory cleanup after host disposal.</summary>
    private static string LocalEndpoint() => OperatingSystem.IsWindows()
        ? "labeler-admin-" + Guid.NewGuid().ToString("N")
        : Path.Combine(Path.GetTempPath(), "labeler-admin-" + Guid.NewGuid().ToString("N"), "admin.sock");

    /// <summary>Sends a method-bound service JWT to the HTTPS adapter.</summary>
    private static async Task<HttpResponseMessage> GetTeamAsync(TestServerBuilder fixture, string token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "http://localhost/xrpc/dev.idunno.labeler.admin.getTeam");
        AddTrustedProxyHeaders(request);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Marks a test request as arriving through the fixture's configured trusted proxy.</summary>
    private static void AddTrustedProxyHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("X-Test-Remote-IP", "192.0.2.1");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.10");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
    }

    /// <summary>Signs short-lived account service assertions using the test account's repository key.</summary>
    private static string Token(
        LabelSigningKey key,
        DateTimeOffset now,
        string? audience = null,
        string method = "dev.idunno.labeler.admin.getTeam",
        string? actor = null,
        TimeSpan? lifetime = null)
    {
        string header = Base64Url(Encoding.UTF8.GetBytes("""{"typ":"JWT","alg":"ES256"}"""));
        string payload = $$"""
            {"iss":"{{actor ?? s_actor.Value}}","aud":"{{audience ?? s_audience.ToString()}}",
             "lxm":"{{method}}","iat":{{now.ToUnixTimeSeconds()}},"exp":{{(now + (lifetime ?? TimeSpan.FromSeconds(60))).ToUnixTimeSeconds()}},
             "jti":"{{Guid.NewGuid():N}}"}
            """;
        string signingInput = header + "." + Base64Url(Encoding.UTF8.GetBytes(payload));
        using ECDsa signer = ECDsa.Create();
        signer.ImportPkcs8PrivateKey(key.ExportPkcs8PrivateKey(), out _);
        return signingInput + "." + Base64Url(signer.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256));
    }

    /// <summary>Encodes JWT segments without padding.</summary>
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Changes the ECDSA S value to its equivalent curve-order complement.</summary>
    private static string MalleateSignature(string token)
    {
        string[] parts = token.Split('.');
        byte[] signature = Convert.FromBase64String(parts[2].Replace('-', '+').Replace('_', '/').PadRight((parts[2].Length + 3) / 4 * 4, '='));
        byte[] order = Convert.FromHexString("FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551");
        byte[] s = signature.AsSpan(32, 32).ToArray();
        byte[] malleatedS = new byte[s.Length];
        int borrow = 0;
        for (int index = s.Length - 1; index >= 0; index--)
        {
            int digit = order[index] - s[index] - borrow;
            borrow = digit < 0 ? 1 : 0;
            malleatedS[index] = (byte)(digit < 0 ? digit + 256 : digit);
        }

        if (borrow != 0)
        {
            throw new InvalidOperationException("The test signature is outside the P-256 curve order.");
        }

        byte[] malleated = [.. signature.AsSpan(0, 32), .. malleatedS];
        return string.Join('.', parts[0], parts[1], Base64Url(malleated));
    }

    /// <summary>Serves matching account DID documents without external identity services.</summary>
    private sealed class IdentityHandler(string multikey) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string actor = request.RequestUri!.AbsolutePath.Trim('/');
            string document = $$"""
                {"id":"{{actor}}","verificationMethod":[{"id":"{{actor}}#atproto","type":"Multikey",
                "controller":"{{actor}}","publicKeyMultibase":"{{multikey}}"}]}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(document, Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>Rejects responses written before the entire client frame has been consumed, without scheduling races.</summary>
    private sealed class ReadBeforeReplyStream(byte[] frame) : MemoryStream(frame, writable: false)
    {
        internal MemoryStream Response { get; } = new();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Assert.Equal(Length, Position);
            return Response.WriteAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Response.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>Provides the deliberately small policy used by adapter boundary tests.</summary>
    private sealed class Policy : ILabelIssuancePolicyProvider
    {
        public Task<LabelServiceResult<LabelIssuancePolicy>> GetPolicyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(LabelServiceResult<LabelIssuancePolicy>.Success(new("policy", ["spam"], true, true)));
    }
}
