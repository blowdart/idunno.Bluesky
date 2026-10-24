// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

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
    [InlineData("outsider", HttpStatusCode.Forbidden)]
    [InlineData("member", HttpStatusCode.OK)]
    [InlineData("http", HttpStatusCode.Unauthorized)]
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
            "outsider" => Token(key, now, actor: "did:plc:outsider"),
            _ => Token(key, now)
        };
        using HttpRequestMessage request = new(HttpMethod.Get,
            (kind == "http" ? "http" : "https") + "://localhost/xrpc/dev.idunno.labeler.admin.getTeam");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        using HttpResponseMessage publicResponse = await fixture.Client.GetAsync(
            "/xrpc/com.atproto.label.queryLabels?uriPatterns=did:plc:subject", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
    }

    [Fact]
    public async Task ServiceTokenReplayAndHashRevocationAreDurable()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(s_actor, "uid:0", "bootstrap", TestContext.Current.CancellationToken)).Succeeded);
        DateTimeOffset now = await DatabaseNowAsync(fixture);
        fixture.Clock.SetUtcNow(now);
        string token = Token(key, now);
        using HttpResponseMessage first = await GetTeamAsync(fixture, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using HttpResponseMessage replay = await GetTeamAsync(fixture, token);
        Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", TestContext.Current.CancellationToken)).Result!;
        string revoked = Token(key, now);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(revoked)));
        Assert.True((await team.ChangeTeamAsync(administrator, new(
            operationId: "revoke-token", action: LabelTeamAction.RevokeToken, target: s_actor,
            reason: "Revoke leaked token.", credential: hash), TestContext.Current.CancellationToken)).Succeeded);
        using HttpResponseMessage rejected = await GetTeamAsync(fixture, revoked);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        using ServiceProvider restarted = fixture.OtherProvider();
        ILabelAdministrationStore other = restarted.GetRequiredService<ILabelAdministrationStore>();
        ServiceAuthenticationResult evidence = await ServiceAuthentication.VerifyAsync(
            token: revoked, audience: s_audience, method: "dev.idunno.labeler.admin.getTeam",
            httpClient: identity, timeProvider: fixture.Clock, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False((await other.AuthenticateServiceAsync(evidence, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Theory]
    [InlineData("""{"method":"changeTeam","reason":null}""", "InvalidRequest")]
    [InlineData("""{"method":"changeTeam","actor":"did:plc:spoofed"}""", "InvalidRequest")]
    [InlineData("""{"method":"getTeam","target":null}""", "InvalidRequest")]
    [InlineData("""{"method":"unknownMethod"}""", "InvalidRequest")]
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
    private static async Task<TestServerBuilder> CreateAsync(LabelSigningKey key, HttpClient identity, string? endpoint = null)
    {
        TestServerBuilder fixture = await TestServerBuilder.CreateAsync(configureAdministration: services =>
        {
            services.AddSingleton<ILabelIssuancePolicyProvider, Policy>();
            services.AddLabelerAdministration(s_audience, key, identity);
            if (endpoint is not null)
            {
                services.AddLabelerLocalAdministration(endpoint);
            }
        });
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
        using HttpRequestMessage request = new(HttpMethod.Get, "https://localhost/xrpc/dev.idunno.labeler.admin.getTeam");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Signs short-lived account service assertions using the test account's repository key.</summary>
    private static string Token(
        LabelSigningKey key,
        DateTimeOffset now,
        string? audience = null,
        string method = "dev.idunno.labeler.admin.getTeam",
        string? actor = null)
    {
        string header = Base64Url(Encoding.UTF8.GetBytes("""{"typ":"JWT","alg":"ES256"}"""));
        string payload = $$"""
            {"iss":"{{actor ?? s_actor.Value}}","aud":"{{audience ?? s_audience.ToString()}}",
             "lxm":"{{method}}","iat":{{now.ToUnixTimeSeconds()}},"exp":{{now.AddSeconds(60).ToUnixTimeSeconds()}},
             "jti":"{{Guid.NewGuid():N}}"}
            """;
        string signingInput = header + "." + Base64Url(Encoding.UTF8.GetBytes(payload));
        using ECDsa signer = ECDsa.Create();
        signer.ImportPkcs8PrivateKey(key.ExportPkcs8PrivateKey(), out _);
        return signingInput + "." + Base64Url(signer.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256));
    }

    /// <summary>Encodes JWT segments without padding.</summary>
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

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

    /// <summary>Provides the deliberately small policy used by adapter boundary tests.</summary>
    private sealed class Policy : ILabelIssuancePolicyProvider
    {
        public Task<LabelServiceResult<LabelIssuancePolicy>> GetPolicyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(LabelServiceResult<LabelIssuancePolicy>.Success(new("policy", ["spam"], true, true)));
    }
}
