// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Net;
using System.Security.Cryptography;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.Time.Testing;

namespace idunno.Bluesky.Labeler.Test;

public class LabelAdministrationTests
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task AuditTimesUseDatabaseClockSurviveRetriesAndRetainUnknownLegacyTimes(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine, databaseClock: clock);
        ILabelAdministrationStore store = (ILabelAdministrationStore)fixture.Store;
        LabelAdministrationReceipt original = (await store.BootstrapAsync(s_administrator, "uid:0", "timestamp-bootstrap", token)).Result!;
        Assert.Equal(clock.GetUtcNow(), original.CreatedAt);
        clock.Advance(TimeSpan.FromHours(1));
        ILabelAdministrationStore restarted = (ILabelAdministrationStore)fixture.NewStore();
        LabelAdministrationReceipt retry = (await restarted.BootstrapAsync(s_administrator, "uid:0", "timestamp-bootstrap", token)).Result!;
        Assert.Equal(original, retry);
        LabelAdministrationCaller caller = (await restarted.AuthenticateLocalAsync("uid:0", token)).Result!;
        LabelAdministrationReceipt denial = (await restarted.RecordAdministrationDenialAsync("Test denied event timestamp.", caller, token)).Result!;
        Assert.Equal(clock.GetUtcNow(), denial.CreatedAt);
        IReadOnlyList<LabelAdministrationReceipt> audit = (await restarted.GetAdministrationAuditAsync(caller, cancellationToken: token)).Result!;
        Assert.Equal(original.CreatedAt, Assert.Single(audit, item => item.Sequence == original.Sequence).CreatedAt);
        Assert.Equal(denial.CreatedAt, Assert.Single(audit, item => item.Sequence == denial.Sequence).CreatedAt);

        string legacy = Convert.ToHexString(Encoding.UTF8.GetBytes(
            """{"Sequence":1,"OperationId":"legacy","Actor":null,"LocalIdentity":null,"Action":"legacy","Reason":"Historical event without timestamp.","Succeeded":true}"""));
        string payload = engine == "postgresql" ? $"decode('{legacy}','hex')" : $"X'{legacy}'";
        await fixture.ExecuteAsync($"UPDATE label_admin_audit SET payload={payload} WHERE seq=1");
        audit = (await restarted.GetAdministrationAuditAsync(caller, cancellationToken: token)).Result!;
        Assert.Null(Assert.Single(audit, item => item.Sequence == 1).CreatedAt);
        LabelTableQuery dated = new(
            Kind: LabelTableKind.Audit,
            Sort: LabelTableSort.Received,
            AuditFrom: original.CreatedAt,
            AuditTo: denial.CreatedAt);
        LabelServiceResult<LabelTablePage> legacyFiltered = await ((ILabelTableStore)fixture.NewStore()).ReadTableAsync(caller, dated, token);
        Assert.True(legacyFiltered.Succeeded, legacyFiltered.Error?.Message + legacyFiltered.Error?.Exception);
        Assert.Equal(1, legacyFiltered.Result.Total);
        Assert.Equal(denial.Sequence, Assert.Single(legacyFiltered.Result.Audit!).Sequence);
    }

    private static readonly Did s_administrator = new("did:plc:administrator");
    private static readonly Did s_member = new("did:plc:member");

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task AuditFiltersCountAndPageTheSameInclusiveDatabaseTimeRange(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        DateTimeOffset start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        FakeTimeProvider clock = new(start.AddMilliseconds(123));
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine, databaseClock: clock);
        ILabelAdministrationStore store = (ILabelAdministrationStore)fixture.Store;
        LabelAdministrationCaller administrator = await BootstrapAsync(store);
        List<LabelAdministrationReceipt> denials = [];
        for (int index = 0; index < 35; index++)
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            LabelServiceResult<LabelAdministrationReceipt> result = await store.RecordAdministrationDenialAsync(
                reason: "Test audit filter event.",
                caller: index % 2 == 0 ? administrator : null,
                cancellationToken: token);
            Assert.True(result.Succeeded, result.Error?.Message);
            denials.Add(result.Result);
        }

        DateTimeOffset first = denials[0].CreatedAt!.Value;
        DateTimeOffset last = denials[^1].CreatedAt!.Value;
        LabelTableQuery defaults = new(LabelTableKind.Audit, Sort: LabelTableSort.Received, Ascending: true);
        foreach ((LabelTableQuery query, long total) in new (LabelTableQuery, long)[]
        {
            (defaults, 36),
            (defaults with { AuditSucceeded = true }, 1),
            (defaults with { AuditSucceeded = false }, 35),
            (defaults with { AuditActor = s_administrator.Value }, 19),
            (defaults with { AuditActor = "uid:0" }, 0),
            (defaults with { AuditActor = "missing' OR 1=1 --" }, 0),
            (defaults with { AuditFrom = first, AuditTo = last }, 35),
            (defaults with { AuditFrom = first.AddTicks(1), AuditTo = last.AddTicks(-1) }, 33),
            (defaults with { AuditFrom = first, AuditTo = first }, 1),
            (defaults with { AuditFrom = first.ToOffset(TimeSpan.FromHours(-8)), AuditTo = first }, 1),
            (defaults with { AuditFrom = last }, 1),
            (defaults with { AuditTo = first }, 2),
            (defaults with { AuditFrom = last.AddMilliseconds(1) }, 0)
        })
        {
            LabelServiceResult<LabelTablePage> result = await ((ILabelTableStore)fixture.NewStore()).ReadTableAsync(administrator, query, token);
            Assert.True(result.Succeeded, result.Error?.Message + result.Error?.Exception);
            Assert.Equal(total, result.Result.Total);
            Assert.Equal(Math.Min(total, LabelTableQuery.PageSize), result.Result.Audit!.Count);
        }

        LabelTableQuery combined = defaults with
        {
            AuditActor = s_administrator.Value,
            AuditSucceeded = false,
            AuditFrom = first,
            AuditTo = last
        };
        ILabelTableStore tables = (ILabelTableStore)fixture.Store;
        LabelTablePage pageOne = (await tables.ReadTableAsync(administrator, combined, token)).Result!;
        LabelTablePage pageTwo = (await tables.ReadTableAsync(administrator, combined with { Page = 99 }, token)).Result!;
        Assert.Equal(18, pageOne.Total);
        Assert.Equal(18, pageTwo.Total);
        Assert.Equal(2, pageTwo.Page);
        Assert.Equal(denials.Where((_, index) => index % 2 == 0).Select(item => item.Sequence),
            pageOne.Audit!.Concat(pageTwo.Audit!).Select(item => item.Sequence));
        Assert.Throws<ArgumentException>(() => (combined with { AuditTo = first.AddDays(-1) }).Validate());

        LabelServiceResult<LabelAdministrationReceipt> started = await tables.BeginAuditExportAsync(administrator, defaults, token);
        Assert.True(started.Succeeded, started.Error?.Message + started.Error?.Exception);
        Assert.Equal("audit-export-started", started.Result.Action);
        Assert.Equal(administrator.Actor, started.Result.Actor);
        Assert.Equal(clock.GetUtcNow(), started.Result.CreatedAt);
        long cutoff = started.Result.Sequence - 1;
        await store.RecordAdministrationDenialAsync("Concurrent event after export cutoff.", administrator, token);
        List<long> exported = [];
        long cursor = 0;
        while (true)
        {
            LabelServiceResult<IReadOnlyList<LabelAdministrationReceipt>> result = await tables.ReadAuditExportPageAsync(
                caller: administrator,
                query: defaults with { Sort = LabelTableSort.Number, AuditAfterSequence = cursor, AuditThroughSequence = cutoff },
                cancellationToken: token);
            Assert.True(result.Succeeded, result.Error?.Message + result.Error?.Exception);
            IReadOnlyList<LabelAdministrationReceipt> entries = result.Result;
            if (entries.Count == 0)
            {
                break;
            }

            exported.AddRange(entries.Select(entry => entry.Sequence));
            cursor = entries[^1].Sequence;
        }

        Assert.Equal(Enumerable.Range(1, 36).Select(value => (long)value), exported);
        await EnrollAndBindAsync(store, administrator, LabelTeamRole.Viewer);
        LabelAdministrationCaller member = (await store.AuthenticateLocalAsync("uid:1", token)).Result!;
        Assert.True((await tables.BeginAuditExportAsync(member, defaults, token)).Succeeded);
        Assert.True((await store.ChangeTeamAsync(administrator, Change(LabelTeamAction.Suspend, "suspend-exporter"), token)).Succeeded);
        Assert.False((await tables.ReadTableAsync(member, defaults, token)).Succeeded);
        Assert.False((await tables.ReadAuditExportPageAsync(member, defaults with { AuditThroughSequence = cutoff }, token)).Succeeded);
        Assert.False((await tables.BeginAuditExportAsync(member, defaults, token)).Succeeded);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RevocationFencesPreviouslyAuthorizedMaintenanceMutations(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore administration = (ILabelAdministrationStore)fixture.Store;
        ILabelMaintenanceStore maintenance = (ILabelMaintenanceStore)fixture.Store;
        LabelAdministrationCaller administrator = await BootstrapAsync(administration);
        await EnrollAndBindAsync(administration, administrator, LabelTeamRole.Administrator);
        LabelAdministrationCaller member = (await administration.AuthenticateLocalAsync("uid:1", token)).Result!;
        Assert.True((await administration.AuthorizeAsync(
            member, LabelAdministrationPermission.Maintain, "Maintenance authorization.", token)).Succeeded);
        await maintenance.RegisterHostAsync(Guid.NewGuid(), token);
        Assert.True((await administration.ChangeTeamAsync(
            administrator, Change(LabelTeamAction.Suspend, "revoke-maintenance"), token)).Succeeded);
        using LabelSigningKey replacement = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.BeginRotationAsync(
            operationId: "revoked-rotation",
            previousKeyId: "active",
            replacementKeyId: "replacement",
            replacementPublicKey: replacement.ExportPublicKeyMultibase(),
            caller: member,
            cancellationToken: token));
        LabelStoreState state = await fixture.Store.GetStateAsync(token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.BeginRetirementAsync(
            state.Revision, member, token));
        Assert.False((await maintenance.GetMaintenanceStateAsync(token)).IsActive);
        Assert.Equal(state, await fixture.Store.GetStateAsync(token));
    }

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
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task BrowserCallerEvidenceUsesDatabaseTimeAndExpiresAfterTenMinutes(string engine)
    {
        FakeTimeProvider databaseClock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine, databaseClock: databaseClock);
        ILabelAdministrationStore store = (ILabelAdministrationStore)fixture.Store;
        await BootstrapAsync(store);

        LabelServiceResult<LabelAdministrationCaller> authenticated = await store.AuthenticateBrowserAsync(
            s_administrator,
            TestContext.Current.CancellationToken);
        Assert.True(authenticated.Succeeded, authenticated.Error?.Message);
        Assert.Equal(databaseClock.GetUtcNow().AddMinutes(10), authenticated.Result!.ExpiresAt);
        Assert.True((await store.GetTeamAsync(authenticated.Result, TestContext.Current.CancellationToken)).Succeeded);

        databaseClock.Advance(TimeSpan.FromMinutes(10));

        Assert.False((await store.GetTeamAsync(authenticated.Result, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task BrowserCallerEvidenceIsRejectedAfterMemberSuspension(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore store = (ILabelAdministrationStore)fixture.Store;
        LabelAdministrationCaller administrator = await BootstrapAsync(store);
        await EnrollAndBindAsync(store, administrator, LabelTeamRole.Viewer);
        LabelAdministrationCaller browser = (await store.AuthenticateBrowserAsync(
            s_member,
            TestContext.Current.CancellationToken)).Result!;
        Assert.True((await store.GetTeamAsync(browser, TestContext.Current.CancellationToken)).Succeeded);

        Assert.True((await store.ChangeTeamAsync(
            administrator,
            Change(LabelTeamAction.Suspend, "suspend-browser-member"),
            TestContext.Current.CancellationToken)).Succeeded);

        Assert.False((await store.GetTeamAsync(browser, TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await store.AuthenticateBrowserAsync(
            s_member,
            TestContext.Current.CancellationToken)).Succeeded);
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
    public async Task ServiceReplayAndExpiryUseDurableProviderState(string engine)
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

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task TokenExpiryIndexIsInstalledAndRepairedOnReinitialization(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        await AssertTokenExpiryIndexAsync(fixture);
        await fixture.ExecuteAsync(engine switch
        {
            "mysql" => "DROP INDEX label_admin_token_expiry ON label_admin_tokens",
            _ => "DROP INDEX label_admin_token_expiry"
        });
        ILabelStore reopened = fixture.NewStore();
        await reopened.InitializeAsync(StoreFixture.Issuer, TestContext.Current.CancellationToken);
        await AssertTokenExpiryIndexAsync(fixture);
        await reopened.InitializeAsync(StoreFixture.Issuer, TestContext.Current.CancellationToken);
        await AssertTokenExpiryIndexAsync(fixture);
    }

    [Theory]
    [InlineData("sqlite", 1)]
    [InlineData("postgresql", 1)]
    [InlineData("mysql", 1)]
    [InlineData("sqlite", 2)]
    [InlineData("postgresql", 2)]
    [InlineData("mysql", 2)]
    public async Task AuditTraversalTerminatesWithoutGeneratingEntriesAndStillAuditsDenials(string engine, int limit)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelAdministrationStore store = (ILabelAdministrationStore)fixture.Store;
        LabelAdministrationCaller administrator = await BootstrapAsync(store);
        await EnrollAndBindAsync(store, administrator, LabelTeamRole.Viewer);
        LabelAdministrationCaller member = (await store.AuthenticateLocalAsync("uid:1", TestContext.Current.CancellationToken)).Result!;
        Assert.True((await store.ChangeTeamAsync(
            administrator, Change(LabelTeamAction.Suspend, "suspend"), TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await store.GetAdministrationAuditAsync(
            member, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        IReadOnlyList<LabelAdministrationReceipt> initial = (await store.GetAdministrationAuditAsync(
            administrator, cancellationToken: TestContext.Current.CancellationToken)).Result!;
        Assert.Contains(initial, receipt => !receipt.Succeeded && receipt.Action == "audit-read" && receipt.Actor == s_member);
        List<LabelAdministrationReceipt> traversal = [];
        long after = 0;
        bool reachedEnd = false;
        for (int pageNumber = 0; pageNumber <= initial.Count; pageNumber++)
        {
            LabelServiceResult<IReadOnlyList<LabelAdministrationReceipt>> page = await store.GetAdministrationAuditAsync(
                caller: administrator, afterSequence: after, limit: limit, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(page.Succeeded);
            if (page.Result.Count == 0)
            {
                reachedEnd = true;
                break;
            }

            traversal.AddRange(page.Result);
            after = page.Result[^1].Sequence;
        }

        Assert.True(reachedEnd);
        Assert.Equal(initial, traversal);
        Assert.Empty((await store.GetAdministrationAuditAsync(
            caller: administrator, afterSequence: after, limit: limit, cancellationToken: TestContext.Current.CancellationToken)).Result!);
        Assert.Equal(initial, (await store.GetAdministrationAuditAsync(
            administrator, cancellationToken: TestContext.Current.CancellationToken)).Result!);
    }

    /// <summary>Checks provider metadata for an expiry-leading cleanup index, including the indexed column.</summary>
    private static async Task AssertTokenExpiryIndexAsync(StoreFixture fixture)
    {
        await using DbConnection connection = fixture.Connection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = fixture.Engine switch
        {
            "mysql" => "SELECT column_name FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='label_admin_tokens' AND index_name='label_admin_token_expiry' AND seq_in_index=1",
            "postgresql" => "SELECT pg_get_indexdef(indexrelid,1,true) FROM pg_index WHERE indexrelid='label_admin_token_expiry'::regclass",
            _ => "SELECT name FROM pragma_index_info('label_admin_token_expiry') WHERE seqno=0"
        };
        Assert.Equal("expires_ms", await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
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
