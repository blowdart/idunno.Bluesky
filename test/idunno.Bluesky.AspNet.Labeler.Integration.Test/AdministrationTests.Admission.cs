// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public partial class AdministrationTests
{
    [Fact]
    public async Task DenialsFromManyPeersBeyondTheGlobalAuditBudgetAreRejectedButNotAudited()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAdmissionAsync(key, identity, denialAudits: 10);
        int baseline = await AdmissionAuditCountAsync(fixture);
        for (int index = 0; index < 40; index++)
        {
            using HttpRequestMessage request = AdmissionRequest(
                path: "/xrpc/dev.idunno.labeler.admin.getTeam",
                assertion: "invalid",
                peer: $"198.18.0.{index + 1}");
            using HttpResponseMessage response = await fixture.Client.SendAsync(request, token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        Assert.Equal(baseline + 10, await AdmissionAuditCountAsync(fixture));
        using HttpRequestMessage valid = AdmissionRequest(
            path: "/xrpc/dev.idunno.labeler.admin.getTeam",
            assertion: Token(key, fixture.Clock.GetUtcNow()),
            peer: "198.19.0.1");
        using HttpResponseMessage authorized = await fixture.Client.SendAsync(valid, token);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundaryDenialBurstsAcrossPeersRetainBoundedAuditAndCorrectResponses(bool ozone)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAdmissionAsync(key, identity);
        string method = ozone ? "tools.ozone.server.getConfig" : "dev.idunno.labeler.admin.getTeam";
        for (int index = 0; index < 280; index++)
        {
            int kind = index % 3;
            string peer = $"198.18.{index / 256}.{index % 256}";
            using HttpRequestMessage request = AdmissionRequest(
                path: "/xrpc/" + method + (kind == 1 ? "?unknown=true" : string.Empty),
                assertion: kind == 1 ? Token(
                    key: key,
                    now: fixture.Clock.GetUtcNow(),
                    audience: ozone ? TestServerBuilder.s_issuer.Value : s_audience.ToString(),
                    method: method) : "invalid",
                peer: peer,
                https: kind != 2);
            using HttpResponseMessage response = await fixture.Client.SendAsync(request, token);
            Assert.Equal(kind switch
            {
                0 => HttpStatusCode.Unauthorized,
                1 => HttpStatusCode.BadRequest,
                _ => HttpStatusCode.Forbidden
            }, response.StatusCode);
        }

        Assert.Equal(257, await AdmissionAuditCountAsync(fixture));
        using HttpRequestMessage valid = AdmissionRequest(
            path: "/xrpc/dev.idunno.labeler.admin.getTeam",
            assertion: Token(key, fixture.Clock.GetUtcNow()),
            peer: "198.19.0.1");
        using HttpResponseMessage authorized = await fixture.Client.SendAsync(valid, token);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        Assert.Equal(257, await AdmissionAuditCountAsync(fixture));
    }

    [Theory]
    [InlineData(0, 16, 1024)]
    [InlineData(100001, 16, 1024)]
    [InlineData(120, 0, 1024)]
    [InlineData(120, 1025, 1024)]
    [InlineData(120, 16, 0)]
    [InlineData(120, 16, 65537)]
    public void RemoteAdmissionRejectsUnboundedConfiguration(int requestsPerMinute, int concurrentRequests, int peers)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        ServiceCollection services = new();
        services.AddSqliteLabelStorage("Data Source=admission-options.db");
        services.AddSingleton<ILabelIssuancePolicyProvider, Policy>();
        services.AddLabelerAdministration(s_audience, key, identity);
        services.Configure<LabelerAdministrationOptions>(options =>
        {
            options.RequestsPerMinutePerPeer = requestsPerMinute;
            options.MaximumConcurrentRequestsPerPeer = concurrentRequests;
            options.MaximumTrackedPeers = peers;
        });
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<LabelerAdministrationOptions>>().Value);
    }

    [Theory]
    [InlineData(false, "invalid", HttpStatusCode.Unauthorized)]
    [InlineData(true, "invalid", HttpStatusCode.Unauthorized)]
    [InlineData(false, "missing", HttpStatusCode.Unauthorized)]
    [InlineData(true, "missing", HttpStatusCode.Unauthorized)]
    [InlineData(false, "insecure", HttpStatusCode.Forbidden)]
    [InlineData(true, "insecure", HttpStatusCode.Forbidden)]
    [InlineData(false, "malformed", HttpStatusCode.BadRequest)]
    [InlineData(true, "malformed", HttpStatusCode.BadRequest)]
    public async Task RemoteAdmissionPrecedesAuthenticationAndAuditAcrossNativeAndOzoneRoutes(
        bool ozone,
        string rejection,
        HttpStatusCode expected)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using AdmissionIdentityHandler handler = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAdmissionAsync(key, identity);
        int initialAudit = await AdmissionAuditCountAsync(fixture);
        string method = ozone ? "tools.ozone.server.getConfig" : "dev.idunno.labeler.admin.getTeam";
        string path = "/xrpc/" + method + (rejection == "malformed" ? "?unknown=true" : "");
        string? assertion = rejection switch
        {
            "missing" => null,
            "malformed" => Token(
                key: key,
                now: fixture.Clock.GetUtcNow(),
                audience: ozone ? TestServerBuilder.s_issuer.Value : s_audience.ToString(),
                method: method),
            _ => "invalid"
        };
        using HttpRequestMessage rejected = AdmissionRequest(
            path: path,
            assertion: assertion,
            peer: "198.51.100.10",
            https: rejection != "insecure");
        using HttpResponseMessage first = await fixture.Client.SendAsync(rejected, token);
        Assert.Equal(expected, first.StatusCode);
        int retainedAudit = await AdmissionAuditCountAsync(fixture);
        Assert.Equal(initialAudit + 1, retainedAudit);
        int identityReads = handler.Requests;

        string legitimateAssertion = Token(key, fixture.Clock.GetUtcNow());
        using HttpRequestMessage throttled = AdmissionRequest("/xrpc/dev.idunno.labeler.admin.getTeam", legitimateAssertion);
        using HttpResponseMessage limited = await fixture.Client.SendAsync(throttled, token);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(TimeSpan.FromMinutes(1), limited.Headers.RetryAfter?.Delta);
        Assert.Equal("no-store", limited.Headers.CacheControl?.ToString());
        Assert.Equal(identityReads, handler.Requests);
        Assert.Equal(retainedAudit, await AdmissionAuditCountAsync(fixture));

        // The rejected request must not consume this token's durable replay identity.
        using HttpRequestMessage legitimate = AdmissionRequest(
            path: "/xrpc/dev.idunno.labeler.admin.getTeam",
            assertion: legitimateAssertion,
            peer: "198.51.100.11");
        using HttpResponseMessage admitted = await fixture.Client.SendAsync(legitimate, token);
        Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);
        using HttpResponseMessage publicRead = await fixture.Client.GetAsync(
            "/xrpc/com.atproto.label.queryLabels?uriPatterns=did:plc:subject", token);
        Assert.Equal(HttpStatusCode.OK, publicRead.StatusCode);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Forbidden)]
    [InlineData(true, HttpStatusCode.Unauthorized)]
    public async Task UntrustedForwardedHeadersCannotChangeRemoteAdmissionIdentity(bool https, HttpStatusCode expected)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAdmissionAsync(key, identity);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                $"{(https ? "https" : "http")}://localhost/xrpc/dev.idunno.labeler.admin.getTeam");
            request.Headers.Add("X-Test-Remote-IP", "192.0.2.99");
            request.Headers.Add("X-Forwarded-For", $"198.51.100.{attempt + 10}");
            request.Headers.Add("X-Forwarded-Proto", "https");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
            using HttpResponseMessage response = await fixture.Client.SendAsync(request, token);
            Assert.Equal(attempt == 0 ? expected : HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public async Task MappedAddressesShareAdmissionAndPeerCapacityDoesNotEvictLiveAllowances()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAdmissionAsync(
            key: key,
            identity: identity,
            requestsPerMinute: 1,
            concurrentRequests: 16,
            peers: 2);
        foreach ((string peer, HttpStatusCode status) in new[]
        {
            ("198.51.100.10", HttpStatusCode.Unauthorized),
            ("::ffff:198.51.100.10", HttpStatusCode.TooManyRequests),
            ("198.51.100.11", HttpStatusCode.Unauthorized),
            ("198.51.100.12", HttpStatusCode.TooManyRequests),
            ("198.51.100.10", HttpStatusCode.TooManyRequests)
        })
        {
            using HttpRequestMessage request = AdmissionRequest("/xrpc/dev.idunno.labeler.admin.getTeam", "invalid", peer);
            using HttpResponseMessage response = await fixture.Client.SendAsync(request, token);
            Assert.Equal(status, response.StatusCode);
        }

        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        using HttpRequestMessage afterWindow = AdmissionRequest("/xrpc/dev.idunno.labeler.admin.getTeam", "invalid", "198.51.100.12");
        using HttpResponseMessage recovered = await fixture.Client.SendAsync(afterWindow, token);
        Assert.Equal(HttpStatusCode.Unauthorized, recovered.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdmittedDenialAuditFailuresRemainFailClosed(bool https)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAdmissionAsync(key, identity);
        SqliteLabelStore store = (SqliteLabelStore)fixture.Store;
        store.BeforeCommitAsync = (_, _, _) => throw new SqliteException("Injected denial persistence failure.", 10);
        try
        {
            using HttpRequestMessage request = AdmissionRequest(
                path: "/xrpc/dev.idunno.labeler.admin.getTeam",
                assertion: "invalid",
                peer: "198.51.100.10",
                https: https);
            using HttpResponseMessage response = await fixture.Client.SendAsync(request, token);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            using HttpRequestMessage next = AdmissionRequest("/xrpc/dev.idunno.labeler.admin.getTeam", "invalid");
            using HttpResponseMessage limited = await fixture.Client.SendAsync(next, token);
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        }
        finally
        {
            store.BeforeCommitAsync = null;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoteConcurrencyAdmissionIsReleasedAfterDenialPersistence(bool advanceWindow)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new IdentityHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAdmissionAsync(
            key: key,
            identity: identity,
            requestsPerMinute: 10,
            concurrentRequests: 1);
        SqliteLabelStore store = (SqliteLabelStore)fixture.Store;
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.BeforeCommitAsync = async (_, _, cancellationToken) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        };
        using HttpRequestMessage firstRequest = AdmissionRequest("/xrpc/dev.idunno.labeler.admin.getTeam", "invalid");
        Task<HttpResponseMessage> pending = fixture.Client.SendAsync(firstRequest, token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
            if (advanceWindow)
            {
                fixture.Clock.Advance(TimeSpan.FromMinutes(1));
            }
            using HttpRequestMessage concurrent = AdmissionRequest("/xrpc/dev.idunno.labeler.admin.getTeam", "invalid");
            using HttpResponseMessage limited = await fixture.Client.SendAsync(concurrent, token);
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        }
        finally
        {
            store.BeforeCommitAsync = null;
            release.TrySetResult();
        }
        using HttpResponseMessage first = await pending;
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        using HttpRequestMessage next = AdmissionRequest(
            path: "/xrpc/dev.idunno.labeler.admin.getTeam",
            assertion: Token(key, fixture.Clock.GetUtcNow()));
        using HttpResponseMessage admitted = await fixture.Client.SendAsync(next, token);
        Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);
    }

    /// <summary>Starts both adapters with bounded admission and one database/application clock for valid assertions.</summary>
    private static async Task<TestServerBuilder> CreateAdmissionAsync(
        LabelSigningKey key,
        HttpClient identity,
        int requestsPerMinute = 1,
        int concurrentRequests = 16,
        int peers = 1024,
        int denialAudits = 600)
    {
        TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, Policy>();
                services.AddLabelerAdministration(s_audience, key, identity);
                services.Configure<LabelerAdministrationOptions>(options =>
                {
                    options.RemoteMode = RemoteAdministrationMode.Enabled;
                    options.RequestsPerMinutePerPeer = requestsPerMinute;
                    options.MaximumConcurrentRequestsPerPeer = concurrentRequests;
                    options.MaximumTrackedPeers = peers;
                    options.MaximumDenialAuditsPerMinute = denialAudits;
                });
            },
            controlDatabaseTime: true,
            ozoneAdministration: true);
        await fixture.Services.GetRequiredService<RetirementWorker>().InitialIteration.WaitAsync(TestContext.Current.CancellationToken);
        await fixture.Clock.WithdrawalRegistrations.Reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.True((await fixture.Services.GetRequiredService<ILabelAdministrationStore>().BootstrapAsync(
            administrator: s_actor,
            localIdentity: "uid:0",
            operationId: "admission-bootstrap",
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        return fixture;
    }

    /// <summary>Builds a request through a trusted proxy without letting production code read test-only headers.</summary>
    private static HttpRequestMessage AdmissionRequest(
        string path,
        string? assertion = null,
        string peer = "198.51.100.10",
        bool https = true)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Test-Remote-IP", "192.0.2.1");
        request.Headers.Add("X-Forwarded-For", peer);
        request.Headers.Add("X-Forwarded-Proto", https ? "https" : "http");
        if (assertion is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertion);
        }
        return request;
    }

    /// <summary>Inspects durable audit without consuming the remote admission allowance.</summary>
    private static async Task<int> AdmissionAuditCountAsync(TestServerBuilder fixture)
    {
        ILabelAdministrationStore store = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller caller = (await store.AuthenticateLocalAsync("uid:0", TestContext.Current.CancellationToken)).Result!;
        return (await store.GetAdministrationAuditAsync(
            caller: caller,
            limit: 1000,
            cancellationToken: TestContext.Current.CancellationToken)).Result!.Count;
    }

    /// <summary>Counts identity lookups to detect authentication work before admission.</summary>
    private sealed class AdmissionIdentityHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private int _requests;
        internal int Requests => Volatile.Read(ref _requests);

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
