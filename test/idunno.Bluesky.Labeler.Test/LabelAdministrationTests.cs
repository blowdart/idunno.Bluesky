// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Net;
using System.Security.Cryptography;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Labels;

using Microsoft.Extensions.Time.Testing;

namespace idunno.Bluesky.Labeler.Test;

public class LabelAdministrationTests
{
    private static readonly Did s_administrator = new("did:plc:administrator");
    private static readonly Did s_member = new("did:plc:member");

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task BootstrapIsPermanentIdempotentAndSurvivesRestart(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore store = (ILabelAdministrationStore)fixture.Store;
        LabelServiceResult<LabelAdministrationReceipt> first = await store.BootstrapAsync(s_administrator, "uid:0", "bootstrap", TestContext.Current.CancellationToken);
        Assert.True(first.Succeeded, first.Error?.Message);
        Assert.Equal(first.Result, (await store.BootstrapAsync(s_administrator, "uid:0", "bootstrap", TestContext.Current.CancellationToken)).Result);
        Assert.False((await store.BootstrapAsync(s_member, "uid:1", "bootstrap", TestContext.Current.CancellationToken)).Succeeded);
        ILabelAdministrationStore restarted = (ILabelAdministrationStore)fixture.NewStore();
        Assert.False((await restarted.BootstrapAsync(s_member, "uid:1", "other-bootstrap", TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await restarted.AuthenticateLocalAsync("uid:1", TestContext.Current.CancellationToken)).Succeeded);
        LabelAdministrationCaller caller = (await restarted.AuthenticateLocalAsync("uid:0", TestContext.Current.CancellationToken)).Result!;
        Assert.Equal(s_administrator, caller.Actor);
        Assert.Equal("uid:0", caller.LocalIdentity);
        LabelTeamMember member = Assert.Single((await restarted.GetTeamAsync(caller, TestContext.Current.CancellationToken)).Result!);
        Assert.Equal(LabelTeamRole.Administrator, member.Role);
    }

    [Theory]
    [InlineData("sqlite", LabelTeamRole.Viewer, LabelAdministrationPermission.Inspect, true)]
    [InlineData("sqlite", LabelTeamRole.Viewer, LabelAdministrationPermission.Moderate, false)]
    [InlineData("sqlite", LabelTeamRole.Moderator, LabelAdministrationPermission.Moderate, true)]
    [InlineData("sqlite", LabelTeamRole.Moderator, LabelAdministrationPermission.ManageTeam, false)]
    [InlineData("sqlite", LabelTeamRole.Administrator, LabelAdministrationPermission.Maintain, true)]
    [InlineData("postgresql", LabelTeamRole.Viewer, LabelAdministrationPermission.Moderate, false)]
    [InlineData("postgresql", LabelTeamRole.Moderator, LabelAdministrationPermission.Moderate, true)]
    [InlineData("mysql", LabelTeamRole.Viewer, LabelAdministrationPermission.Moderate, false)]
    [InlineData("mysql", LabelTeamRole.Administrator, LabelAdministrationPermission.Maintain, true)]
    public async Task RolesAreLiveAndDenialsAreAudited(
        string engine,
        LabelTeamRole role,
        LabelAdministrationPermission permission,
        bool allowed)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore store = (ILabelAdministrationStore)fixture.Store;
        LabelAdministrationCaller administrator = await BootstrapAsync(store);
        await EnrollAndBindAsync(store, administrator, role);
        LabelAdministrationCaller member = (await store.AuthenticateLocalAsync("uid:1", TestContext.Current.CancellationToken)).Result!;
        Assert.Equal(allowed, (await store.AuthorizeAsync(member, permission, "Test permission.", TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await store.ChangeTeamAsync(administrator, Change(LabelTeamAction.Suspend, "suspend"), TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await store.AuthorizeAsync(member, LabelAdministrationPermission.Inspect, "Revoked invocation.", TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await store.AuthenticateLocalAsync("uid:1", TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await store.ChangeTeamAsync(administrator, Change(LabelTeamAction.Reinstate, "reinstate"), TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await store.AuthenticateLocalAsync("uid:1", TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await store.AuthorizeAsync(member, LabelAdministrationPermission.Inspect, "Old invocation.", TestContext.Current.CancellationToken)).Succeeded);
        IReadOnlyList<LabelAdministrationReceipt> audit = (await store.GetAdministrationAuditAsync(administrator, cancellationToken: TestContext.Current.CancellationToken)).Result!;
        Assert.Contains(audit, receipt => !receipt.Succeeded && receipt.Actor == s_member && receipt.LocalIdentity == "uid:1");
    }

    [Theory]
    [InlineData("sqlite", LabelTeamAction.Remove)]
    [InlineData("sqlite", LabelTeamAction.Suspend)]
    [InlineData("sqlite", LabelTeamAction.SetRole)]
    [InlineData("postgresql", LabelTeamAction.Remove)]
    [InlineData("postgresql", LabelTeamAction.Suspend)]
    [InlineData("postgresql", LabelTeamAction.SetRole)]
    [InlineData("mysql", LabelTeamAction.Remove)]
    [InlineData("mysql", LabelTeamAction.Suspend)]
    [InlineData("mysql", LabelTeamAction.SetRole)]
    public async Task LastAdministratorIsProtectedAcrossConcurrentConnections(string engine, LabelTeamAction action)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore first = (ILabelAdministrationStore)fixture.Store;
        LabelAdministrationCaller administrator = await BootstrapAsync(first);
        Assert.False((await first.ChangeTeamAsync(administrator, new(
            operationId: "last-admin", action: action, target: s_administrator, reason: "Cannot remove final administrator."), TestContext.Current.CancellationToken)).Succeeded);
        await EnrollAndBindAsync(first, administrator, LabelTeamRole.Administrator);
        ILabelAdministrationStore second = (ILabelAdministrationStore)fixture.NewStore();
        LabelAdministrationCaller member = (await second.AuthenticateLocalAsync("uid:1", TestContext.Current.CancellationToken)).Result!;
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<LabelServiceResult<LabelAdministrationReceipt>> LoseFirstAsync()
        {
            return RunAsync(first, administrator, s_member, "remove-second");
        }

        Task<LabelServiceResult<LabelAdministrationReceipt>> LoseSecondAsync()
        {
            return RunAsync(second, member, s_administrator, "remove-first");
        }

        async Task<LabelServiceResult<LabelAdministrationReceipt>> RunAsync(
            ILabelAdministrationStore store,
            LabelAdministrationCaller actor,
            Did target,
            string operation)
        {
            await release.Task;
            return await store.ChangeTeamAsync(actor, new(
                operationId: operation, action: action, target: target, reason: "Concurrent administrator change."));
        }

        Task<LabelServiceResult<LabelAdministrationReceipt>> firstChange = LoseFirstAsync();
        Task<LabelServiceResult<LabelAdministrationReceipt>> secondChange = LoseSecondAsync();
        release.SetResult();
        LabelServiceResult<LabelAdministrationReceipt>[] results = await Task.WhenAll(firstChange, secondChange);
        Assert.Single(results, result => result.Succeeded);
        LabelAdministrationCaller survivor = (await first.AuthenticateLocalAsync("uid:0", TestContext.Current.CancellationToken)).Result
            ?? (await first.AuthenticateLocalAsync("uid:1", TestContext.Current.CancellationToken)).Result!;
        IReadOnlyList<LabelTeamMember> team = (await first.GetTeamAsync(survivor, TestContext.Current.CancellationToken)).Result!;
        Assert.Single(team, entry => entry is { Active: true, Removed: false, Role: LabelTeamRole.Administrator });
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RevocationWinsAgainstAnAlreadyAuthorizedInFlightDecision(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore store = (ILabelAdministrationStore)fixture.Store;
        LabelAdministrationCaller administrator = await BootstrapAsync(store);
        await EnrollAndBindAsync(store, administrator, LabelTeamRole.Moderator);
        LabelAdministrationCaller member = (await store.AuthenticateLocalAsync("uid:1", TestContext.Current.CancellationToken)).Result!;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        BlockingPolicy policy = new();
        LabelIssuanceService service = new(
            issuer: StoreFixture.Issuer, signingKey: key, store: fixture.Store,
            authorizer: new LabelTeamAuthorizer(store, member), policyProvider: policy, httpClient: http);
        Task<LabelServiceResult<LabelOperationReceipt>> pending = service.ExecuteAsync(LabelOperationRequest.Issue(
            operationId: "revoked-label", target: LabelTarget.Account(new Did(TargetResolutionHandler.SubjectDid)),
            value: "spam", context: new(member.Actor, "Authorized before suspension.")), TestContext.Current.CancellationToken);
        await policy.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True((await store.ChangeTeamAsync(administrator, Change(LabelTeamAction.Suspend, "suspend"), TestContext.Current.CancellationToken)).Succeeded);
        policy.Release.SetResult();
        LabelServiceResult<LabelOperationReceipt> result = await pending;
        Assert.False(result.Succeeded);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, result.Error!.Code);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).IssuedThrough);
        Assert.False((await fixture.Store.GetOperationAsync("revoked-label", TestContext.Current.CancellationToken)).Succeeded);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RevokedAndReplacedLocalBindingsCannotReviveOldEvidence(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore store = (ILabelAdministrationStore)fixture.Store;
        LabelAdministrationCaller administrator = await BootstrapAsync(store);
        await EnrollAndBindAsync(store, administrator, LabelTeamRole.Moderator);
        LabelAdministrationCaller old = (await store.AuthenticateLocalAsync("uid:1", TestContext.Current.CancellationToken)).Result!;
        Assert.True((await store.ChangeTeamAsync(administrator, Change(LabelTeamAction.RevokeLocal, "revoke", credential: "uid:1"), TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await store.AuthorizeAsync(old, LabelAdministrationPermission.Moderate, "Revoked binding.", TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await store.ChangeTeamAsync(administrator, Change(LabelTeamAction.BindLocal, "rebind", credential: "uid:1"), TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await store.AuthorizeAsync(old, LabelAdministrationPermission.Moderate, "Replaced binding.", TestContext.Current.CancellationToken)).Succeeded);
        LabelAdministrationCaller current = (await store.AuthenticateLocalAsync("uid:1", TestContext.Current.CancellationToken)).Result!;
        Assert.True((await store.AuthorizeAsync(current, LabelAdministrationPermission.Moderate, "New binding.", TestContext.Current.CancellationToken)).Succeeded);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ServiceReplayRevocationAndExpiryUseDurableProviderState(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore store = (ILabelAdministrationStore)fixture.Store;
        LabelAdministrationCaller administrator = await BootstrapAsync(store);
        await EnrollAndBindAsync(store, administrator, LabelTeamRole.Moderator);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new ServiceIdentityHandler(key.ExportPublicKeyMultibase()));
        FakeTimeProvider clock = new(await DatabaseNowAsync(fixture));
        AtProtoServiceReference audience = new(StoreFixture.Issuer, "labeler_admin");
        const string method = "dev.idunno.labeler.admin.getTeam";
        ServiceAuthenticationResult first = await ServiceAuthentication.VerifyAsync(
            token: ServiceToken(key, clock.GetUtcNow(), audience), audience: audience, method: method,
            httpClient: identity, timeProvider: clock, cancellationToken: TestContext.Current.CancellationToken);
        LabelAdministrationCaller caller = (await store.AuthenticateServiceAsync(first, TestContext.Current.CancellationToken)).Result!;
        Assert.True((await store.AuthorizeAsync(caller, LabelAdministrationPermission.Moderate, "Current token.", TestContext.Current.CancellationToken)).Succeeded);
        ILabelAdministrationStore restarted = (ILabelAdministrationStore)fixture.NewStore();
        Assert.False((await restarted.AuthenticateServiceAsync(first, TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await store.ChangeTeamAsync(administrator, Change(
            LabelTeamAction.RevokeToken, "revoke-token", credential: first.TokenHash), TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await restarted.AuthorizeAsync(caller, LabelAdministrationPermission.Moderate, "Revoked token.", TestContext.Current.CancellationToken)).Succeeded);
        ServiceAuthenticationResult second = await ServiceAuthentication.VerifyAsync(
            token: ServiceToken(key, clock.GetUtcNow(), audience), audience: audience, method: method,
            httpClient: identity, timeProvider: clock, cancellationToken: TestContext.Current.CancellationToken);
        LabelAdministrationCaller next = (await restarted.AuthenticateServiceAsync(second, TestContext.Current.CancellationToken)).Result!;
        await fixture.ExecuteAsync("UPDATE label_admin_tokens SET expires_ms=0");
        Assert.False((await store.AuthorizeAsync(next, LabelAdministrationPermission.Inspect, "Database-expired token.", TestContext.Current.CancellationToken)).Succeeded);
        AtProtoServiceReference wrong = new(new Did("did:plc:other-issuer"), "labeler_admin");
        ServiceAuthenticationResult otherIssuer = await ServiceAuthentication.VerifyAsync(
            token: ServiceToken(key, clock.GetUtcNow(), wrong), audience: wrong, method: method,
            httpClient: identity, timeProvider: clock, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False((await store.AuthenticateServiceAsync(otherIssuer, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task EnrollmentRequiresTheResolvedDidDocumentToMatch(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore store = (ILabelAdministrationStore)fixture.Store;
        LabelAdministrationCaller administrator = await BootstrapAsync(store);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new ServiceIdentityHandler(key.ExportPublicKeyMultibase()));
        LabelTeamService service = new(store, identity);
        LabelServiceResult<LabelAdministrationReceipt> result = await service.EnrollAsync(
            caller: administrator, member: s_member, role: LabelTeamRole.Moderator,
            operationId: "verified-enrollment", reason: "Verify the resolved account.", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded, result.Error?.Message);
        LabelServiceResult<LabelAdministrationReceipt> mismatch = await service.EnrollAsync(
            caller: administrator, member: new Did("did:plc:mismatched"), role: LabelTeamRole.Administrator,
            operationId: "mismatch", reason: "Reject a mismatched DID document.", cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(mismatch.Succeeded);
        Assert.Equal(2, (await store.GetTeamAsync(administrator, TestContext.Current.CancellationToken)).Result!.Count);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ConcurrentBootstrapHasExactlyOneWinner(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore first = (ILabelAdministrationStore)fixture.Store;
        ILabelAdministrationStore second = (ILabelAdministrationStore)fixture.NewStore();
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<LabelServiceResult<LabelAdministrationReceipt>> BootstrapAsync(
            ILabelAdministrationStore store,
            Did actor,
            string localIdentity,
            string operationId)
        {
            await release.Task;
            return await store.BootstrapAsync(
                administrator: actor, localIdentity: localIdentity, operationId: operationId,
                cancellationToken: TestContext.Current.CancellationToken);
        }

        Task<LabelServiceResult<LabelAdministrationReceipt>> one = BootstrapAsync(first, s_administrator, "uid:0", "first-bootstrap");
        Task<LabelServiceResult<LabelAdministrationReceipt>> two = BootstrapAsync(second, s_member, "uid:1", "second-bootstrap");
        release.SetResult();
        LabelServiceResult<LabelAdministrationReceipt>[] results = await Task.WhenAll(one, two);
        Assert.Single(results, result => result.Succeeded);
        LabelAdministrationReceipt winner = Assert.Single(results, result => result.Succeeded).Result!;
        LabelAdministrationCaller caller = (await first.AuthenticateLocalAsync(
            winner.LocalIdentity!, TestContext.Current.CancellationToken)).Result!;
        Assert.Single((await first.GetTeamAsync(caller, TestContext.Current.CancellationToken)).Result!);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task SuccessfulAndNoOpDecisionsCarryOsAndOperationAuditAndRecoveryRequiresLiveAuthority(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore store = (ILabelAdministrationStore)fixture.Store;
        LabelAdministrationCaller administrator = await BootstrapAsync(store);
        await EnrollAndBindAsync(store, administrator, LabelTeamRole.Moderator);
        LabelAdministrationCaller member = (await store.AuthenticateLocalAsync("uid:1", TestContext.Current.CancellationToken)).Result!;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelIssuanceService service = new(
            issuer: StoreFixture.Issuer,
            signingKey: key,
            store: fixture.Store,
            authorizer: new LabelTeamAuthorizer(store, member),
            policyProvider: new TestLabelIssuancePolicyProvider(new("policy", ["spam"], true, true)),
            httpClient: http);
        foreach (string operation in new[] { "first-label", "no-op-label" })
        {
            LabelServiceResult<LabelOperationReceipt> result = await service.ExecuteAsync(
                LabelOperationRequest.Issue(
                    operationId: operation, target: LabelTarget.Account(new Did(TargetResolutionHandler.SubjectDid)),
                    value: "spam", context: new(member.Actor, "Audited decision.")), TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded, result.Error?.Message);
            Assert.Equal(operation == "first-label", result.Result!.Changed);
        }

        IReadOnlyList<LabelAdministrationReceipt> audit = (await store.GetAdministrationAuditAsync(
            administrator, cancellationToken: TestContext.Current.CancellationToken)).Result!;
        foreach (string operation in new[] { "first-label", "no-op-label" })
        {
            LabelAdministrationReceipt receipt = Assert.Single(audit, entry => entry.OperationId == operation);
            Assert.True(receipt.Succeeded);
            Assert.Equal(s_member, receipt.Actor);
            Assert.Equal("uid:1", receipt.LocalIdentity);
        }

        Assert.True((await store.ChangeTeamAsync(administrator, Change(LabelTeamAction.Suspend, "suspend"), TestContext.Current.CancellationToken)).Succeeded);
        LabelServiceResult<LabelOperationReceipt> recovered = await service.GetOperationAsync(
            operationId: "first-label", context: new(member.Actor, "Recovery after suspension."),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(recovered.Succeeded);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, recovered.Error!.Code);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task MaintenanceAuditDistinguishesIntentAndResultWithoutGrantingExecution(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore store = (ILabelAdministrationStore)fixture.Store;
        LabelAdministrationCaller administrator = await BootstrapAsync(store);
        await EnrollAndBindAsync(store, administrator, LabelTeamRole.Moderator);
        LabelAdministrationCaller member = (await store.AuthenticateLocalAsync("uid:1", TestContext.Current.CancellationToken)).Result!;
        Assert.False((await store.AuditMaintenanceAsync(
            caller: member, operationId: "maintenance", reason: "Moderator cannot perform maintenance.",
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        foreach (LabelMaintenanceAuditPhase phase in Enum.GetValues<LabelMaintenanceAuditPhase>())
        {
            LabelServiceResult<LabelAdministrationReceipt> result = await store.AuditMaintenanceAsync(
                caller: administrator, operationId: "maintenance", reason: "Coordinator observation.", phase: phase,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded);
            Assert.Equal("maintenance-" + phase, result.Result!.Action);
            Assert.Equal(phase != LabelMaintenanceAuditPhase.Failed, result.Result.Succeeded);
        }

        Assert.Equal(0, (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).IssuedThrough);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task UpgradesVersionThreeWithoutChangingSignedHistory(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot signed = new(key.CreateLabel(
            source: StoreFixture.Issuer, uri: "did:plc:subject", value: "spam",
            creationTimestamp: new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));
        await fixture.Store.AppendAsync("before-upgrade", [signed], cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        LabelStoreState before = await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken);
        await fixture.ExecuteAsync("""
            DROP TABLE label_admin_tokens;
            DROP TABLE label_admin_receipts;
            DROP TABLE label_admin_audit;
            DROP TABLE label_team;
            UPDATE label_schema SET version=3;
            """);
        ILabelStore reopened = fixture.NewStore();
        await reopened.InitializeAsync(StoreFixture.Issuer, TestContext.Current.CancellationToken);
        Assert.Equal(before, await reopened.GetStateAsync(TestContext.Current.CancellationToken));
        StoredLabel restored = Assert.Single((await reopened.ReplayAsync(
            streamId: before.StreamId, after: 0, through: before.IssuedThrough,
            cancellationToken: TestContext.Current.CancellationToken)).Labels);
        Assert.Equal(signed.GetJsonBytes(), restored.SignedLabel.GetJsonBytes());
        Assert.True((await ((ILabelAdministrationStore)reopened).BootstrapAsync(
            administrator: s_administrator, localIdentity: "uid:0", operationId: "after-upgrade",
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
    }

    /// <summary>Reads the same authoritative clock as persisted credential fencing.</summary>
    private static async Task<DateTimeOffset> DatabaseNowAsync(StoreFixture fixture)
    {
        await using DbConnection connection = fixture.Connection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = fixture.Engine switch
        {
            "mysql" => "SELECT CAST(UNIX_TIMESTAMP(CURRENT_TIMESTAMP(3))*1000 AS SIGNED)",
            "postgresql" => "SELECT CAST(EXTRACT(EPOCH FROM clock_timestamp())*1000 AS BIGINT)",
            _ => "SELECT CAST((julianday('now') - 2440587.5)*86400000 AS INTEGER)"
        };
        return DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)));
    }

    /// <summary>Creates a fresh short-lived assertion for one method and issuer.</summary>
    private static string ServiceToken(LabelSigningKey key, DateTimeOffset now, AtProtoServiceReference audience)
    {
        string header = Encode(Encoding.UTF8.GetBytes("""{"typ":"JWT","alg":"ES256"}"""));
        string payload = $$"""
            {"iss":"{{s_member}}","aud":"{{audience}}","lxm":"dev.idunno.labeler.admin.getTeam",
            "iat":{{now.ToUnixTimeSeconds()}},"exp":{{now.AddSeconds(60).ToUnixTimeSeconds()}},"jti":"{{Guid.NewGuid():N}}"}
            """;
        string input = header + "." + Encode(Encoding.UTF8.GetBytes(payload));
        using ECDsa signer = ECDsa.Create();
        signer.ImportPkcs8PrivateKey(key.ExportPkcs8PrivateKey(), out _);
        return input + "." + Encode(signer.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256));
    }

    /// <summary>Produces a JWT segment.</summary>
    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Serves a verified test member, with an intentional mismatch for enrollment regression.</summary>
    private sealed class ServiceIdentityHandler(string multikey) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string json = $$"""
                {"id":"{{s_member}}","verificationMethod":[{"id":"{{s_member}}#atproto","type":"Multikey",
                "controller":"{{s_member}}","publicKeyMultibase":"{{multikey}}"}]}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>Establishes the explicit root delegation used by provider conformance cases.</summary>
    private static async Task<LabelAdministrationCaller> BootstrapAsync(ILabelAdministrationStore store)
    {
        Assert.True((await store.BootstrapAsync(s_administrator, "uid:0", "bootstrap")).Succeeded);
        return (await store.AuthenticateLocalAsync("uid:0")).Result!;
    }

    /// <summary>Enters a member through the trusted provider seam and explicitly delegates a second OS identity.</summary>
    private static async Task EnrollAndBindAsync(
        ILabelAdministrationStore store,
        LabelAdministrationCaller administrator,
        LabelTeamRole role)
    {
        Assert.True((await store.ChangeTeamAsync(administrator, Change(LabelTeamAction.Enroll, "enroll", role))).Succeeded);
        Assert.True((await store.ChangeTeamAsync(administrator, Change(LabelTeamAction.BindLocal, "bind", credential: "uid:1"))).Succeeded);
    }

    /// <summary>Creates a stable member-targeted change for one isolated fixture.</summary>
    private static LabelTeamChange Change(
        LabelTeamAction action,
        string operation,
        LabelTeamRole role = LabelTeamRole.Viewer,
        string? credential = null) =>
        new(operationId: operation, action: action, target: s_member, reason: "Provider conformance.", role: role, credential: credential);

    /// <summary>Signals the phase after authorization but before the provider's commit fence.</summary>
    private sealed class BlockingPolicy : ILabelIssuancePolicyProvider
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<LabelServiceResult<LabelIssuancePolicy>> GetPolicyAsync(CancellationToken cancellationToken = default)
        {
            Entered.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return LabelServiceResult<LabelIssuancePolicy>.Success(new(
                version: "policy", declaredValues: ["spam"], allowAccounts: true, allowRecords: true));
        }
    }
}
