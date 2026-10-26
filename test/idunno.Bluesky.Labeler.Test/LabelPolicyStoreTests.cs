// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.Labeler.Test;

public class LabelPolicyStoreTests
{
    private static readonly Did s_administrator = new("did:plc:administrator");
    private static readonly Cid s_cid = new(TargetResolutionHandler.RecordCid);
    private const string Declaration = """
        {
          "$type":"app.bsky.labeler.service",
          "policies":{"labelValues":["spam","scam"],"labelValueDefinitions":[]},
          "createdAt":"2026-01-01T00:00:00Z",
          "subjectTypes":["account","record"],
          "extension":{"preserve":true}
        }
        """;

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task OptInIsDurableFailClosedAndDoesNotChangeCoreDistribution(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        ILabelPolicyStore store = fixture.Services.GetRequiredService<ILabelPolicyStore>();
        Assert.Same(fixture.Store, store);
        Assert.Null(await store.ReadPolicyStateAsync(token));
        LabelPolicyState initial = await store.InitializePolicyAsync(cancellationToken: token);
        Assert.Equal(LabelPolicyPhase.Unverified, initial.Phase);
        Assert.Null(initial.Policy);
        Assert.Null(initial.PublicationOperationId);
        Assert.Null(initial.VerifiedUntil);
        Assert.Equal(TimeSpan.FromSeconds(60), initial.VerificationFreshness);
        Assert.Equal(initial.Revision, (await ((ILabelPolicyStore)fixture.NewStore()).InitializePolicyAsync(cancellationToken: token)).Revision);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.InitializePolicyAsync(TimeSpan.FromSeconds(30), token));
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        Assert.Equal(0, (await fixture.Store.PublishAsync(cancellationToken: token)).PublishedThrough);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task DraftsAreImmutableAndPublicationRequiresIndependentMatchingObservation(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelPolicyStore store = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState initial = await store.InitializePolicyAsync(cancellationToken: token);
        LabelPolicyDefinition draft = Policy();
        Assert.Equal(draft.DeclarationJson, Success(await store.SavePolicyAsync(administrator, "draft", draft, "Reviewed rules.", token)).DeclarationJson);
        Assert.Equal(initial.Revision, (await store.ReadPolicyStateAsync(token))!.Revision);
        Assert.Equal(LabelServiceErrorCode.IdempotencyConflict,
            (await store.SavePolicyAsync(administrator, "changed", Policy(rules: "Different rules."), "Reviewed rules.", token)).Error!.Code);
        LabelPolicyState publishing = Success(await store.BeginPolicyPublicationAsync(
            caller: administrator, operationId: "publish", version: "v1", expectedRevision: initial.Revision,
            previousCid: null, reason: "Publish reviewed policy.", cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Publishing, publishing.Phase);
        Assert.Equal("publish", publishing.PublicationOperationId);
        Assert.Null(publishing.VerifiedUntil);
        using JsonDocument old = JsonDocument.Parse(Declaration.Replace("\"spam\"", "\"other\"", StringComparison.Ordinal));
        LabelPolicyState pending = Success(await store.ObservePolicyAsync(
            expectedRevision: publishing.Revision, verificationStartedAt: publishing.DatabaseTime,
            observedCid: s_cid, declaration: old.RootElement, cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Publishing, pending.Phase);
        Assert.Equal("publish", pending.PublicationOperationId);
        using JsonDocument declaration = JsonDocument.Parse(Declaration);
        LabelPolicyState verified = Success(await store.ObservePolicyAsync(
            expectedRevision: pending.Revision, verificationStartedAt: pending.DatabaseTime,
            observedCid: s_cid, declaration: declaration.RootElement, cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.VerifiedActive, verified.Phase);
        Assert.Equal("publish", verified.PublicationOperationId);
        Assert.Equal(s_cid, verified.VerifiedCid);
        Assert.Equal(pending.DatabaseTime.AddSeconds(60), verified.VerifiedUntil);
        Assert.Contains("\"extension\":{\"preserve\":true}", verified.Policy!.DeclarationJson, StringComparison.Ordinal);
        LabelPolicyState recovered = Success(await store.BeginPolicyPublicationAsync(
            caller: administrator, operationId: "publish", version: "v1", expectedRevision: initial.Revision,
            previousCid: null, reason: "Publish reviewed policy.", cancellationToken: token));
        Assert.Equal(publishing.Revision, recovered.Revision);
        Assert.Equal("publish", recovered.PublicationOperationId);
        Assert.Equal(LabelPolicyPhase.Publishing, recovered.Phase);
        Assert.Equal(verified.Revision, (await store.ReadPolicyStateAsync(token))!.Revision);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task DetectedDriftIsStickyUntilExplicitAdministratorReconciliation(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelPolicyStore store = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await ActivateAsync(store, administrator, token);
        using JsonDocument absent = JsonDocument.Parse("null");
        LabelPolicyState drifted = Success(await store.ObservePolicyAsync(
            expectedRevision: active.Revision, verificationStartedAt: active.DatabaseTime,
            observedCid: null, declaration: absent.RootElement, cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Conflict, drifted.Phase);
        Assert.Null(drifted.VerifiedUntil);
        using JsonDocument restored = JsonDocument.Parse(Declaration);
        LabelPolicyState stillClosed = Success(await store.ObservePolicyAsync(
            expectedRevision: drifted.Revision, verificationStartedAt: drifted.DatabaseTime,
            observedCid: s_cid, declaration: restored.RootElement, cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Conflict, stillClosed.Phase);
        LabelPolicyState intent = Success(await store.BeginPolicyPublicationAsync(
            caller: administrator, operationId: "reconcile", version: "v1", expectedRevision: stillClosed.Revision,
            previousCid: s_cid, reason: "Reconcile explicit external restoration.", cancellationToken: token));
        LabelPolicyState reopened = Success(await store.ObservePolicyAsync(
            expectedRevision: intent.Revision, verificationStartedAt: intent.DatabaseTime,
            observedCid: s_cid, declaration: restored.RootElement, cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.VerifiedActive, reopened.Phase);
        Assert.Equal(s_cid, reopened.PreviousCid);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task CachedVerificationRejectsRollbackWithoutNetworkAndRecoversAtObservationStart(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        LabelPolicyState active = await ActivateAsync((ILabelPolicyStore)fixture.Store, administrator, token);
        Microsoft.Extensions.Time.Testing.FakeTimeProvider clock = new(active.DatabaseTime.AddMilliseconds(-430));
        ILabelPolicyStore restarted = (ILabelPolicyStore)fixture.NewStore(clock);
        TargetResolutionHandler handler = new() { Offline = true };
        using HttpClient client = new(handler);
        LabelPolicyService verifier = new(issuer: StoreFixture.Issuer, store: restarted, httpClient: client);
        foreach (bool force in new[] { false, true })
        {
            Assert.Equal(LabelServiceErrorCode.PolicyUnavailable,
                (await verifier.GetVerifiedStateAsync(force, token)).Error!.Code);
        }

        Assert.Equal(0, handler.Requests);
        Assert.Equal(active.Revision, (await restarted.ReadPolicyStateAsync(token))!.Revision);
        clock.Advance(TimeSpan.FromMilliseconds(430));
        Assert.True((await verifier.GetVerifiedStateAsync(cancellationToken: token)).Succeeded);
        Assert.Equal(0, handler.Requests);
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task InvalidObservationsAreBoundedStickyAndRevisionFencedAcrossRestart(string engine, bool deep)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelPolicyStore store = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await ActivateAsync(store, administrator, token);
        string invalid = Declaration.TrimEnd()[..^1] + ",\"invalid\":" +
            (deep ? new string('[', 33) + "0" + new string(']', 33) : "\"" + new string('x', 65536) + "\"") + "}";
        using JsonDocument declaration = JsonDocument.Parse(invalid);
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await store.ObservePolicyAsync(
            expectedRevision: active.Revision - 1,
            verificationStartedAt: active.DatabaseTime,
            observedCid: s_cid,
            declaration: declaration.RootElement,
            cancellationToken: token)).Error!.Code);
        Assert.Equal(active.Revision, (await store.ReadPolicyStateAsync(token))!.Revision);
        LabelPolicyState conflicted = Success(await store.ObservePolicyAsync(
            expectedRevision: active.Revision,
            verificationStartedAt: active.DatabaseTime,
            observedCid: s_cid,
            declaration: declaration.RootElement,
            cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Conflict, conflicted.Phase);
        Assert.Null(conflicted.VerifiedUntil);
        ILabelPolicyStore restarted = (ILabelPolicyStore)fixture.NewStore();
        LabelPolicyHistoryEntry history = Success(await restarted.GetPolicyHistoryAsync(administrator, cancellationToken: token))[^1];
        Assert.Equal(s_cid, history.ObservedCid);
        Assert.Equal("null", history.ObservedDeclarationJson);
        using JsonDocument restored = JsonDocument.Parse(Declaration);
        Assert.Equal(LabelPolicyPhase.Conflict, Success(await restarted.ObservePolicyAsync(
            expectedRevision: conflicted.Revision,
            verificationStartedAt: conflicted.DatabaseTime,
            observedCid: s_cid,
            declaration: restored.RootElement,
            cancellationToken: token)).Phase);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ObservationsRejectStaleRevisionsExpiredLookupsAndFutureDatabaseTimes(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelPolicyStore store = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await ActivateAsync(store, administrator, token);
        using JsonDocument declaration = JsonDocument.Parse(Declaration);
        foreach ((long revision, DateTimeOffset started, string diagnostic) in new[]
        {
            (active.Revision - 1, active.DatabaseTime, "policy changed"),
            (active.Revision, active.DatabaseTime.Subtract(active.VerificationFreshness), "window expired"),
            (active.Revision, active.DatabaseTime.AddMilliseconds(430), "ahead of the database clock"),
            (active.Revision, active.DatabaseTime.AddDays(1), "ahead of the database clock")
        })
        {
            LabelServiceResult<LabelPolicyState> result = await store.ObservePolicyAsync(
                expectedRevision: revision,
                verificationStartedAt: started,
                observedCid: s_cid,
                declaration: declaration.RootElement,
                cancellationToken: token);
            Assert.Equal(LabelServiceErrorCode.StaleDecision, result.Error!.Code);
            Assert.Contains(diagnostic, result.Error.Message, StringComparison.Ordinal);
        }

        LabelPolicyState unchanged = (await store.ReadPolicyStateAsync(token))!;
        Assert.Equal(active.Revision, unchanged.Revision);
        Assert.Equal(active.VerifiedUntil, unchanged.VerifiedUntil);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task DefaultProviderClockExecutesWithoutTheTestReplacement(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        Assert.Null(fixture.DatabaseClock);
        LabelPolicyState state = await ((ILabelPolicyStore)fixture.Store).InitializePolicyAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.InRange(state.DatabaseTime.Year, 2000, 2100);
        Assert.Equal(TimeSpan.Zero, state.DatabaseTime.Offset);
    }

    [Theory]
    [InlineData("sqlite", -1, true)]
    [InlineData("sqlite", 0, false)]
    [InlineData("sqlite", 1, false)]
    [InlineData("postgresql", -1, true)]
    [InlineData("postgresql", 0, false)]
    [InlineData("postgresql", 1, false)]
    [InlineData("mysql", -1, true)]
    [InlineData("mysql", 0, false)]
    [InlineData("mysql", 1, false)]
    public async Task VerificationExpiryUsesTheSameControlledClockAfterRestart(
        string engine,
        int millisecondsFromExpiry,
        bool succeeds)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelPolicyStore store = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await ActivateAsync(store, administrator, token);
        Assert.Equal(fixture.DatabaseClock!.GetUtcNow(), active.DatabaseTime);
        fixture.DatabaseClock.Advance(active.VerificationFreshness.Add(TimeSpan.FromMilliseconds(millisecondsFromExpiry)));
        ILabelPolicyStore restarted = (ILabelPolicyStore)fixture.NewStore();
        LabelPolicyState current = Assert.IsType<LabelPolicyState>(await restarted.ReadPolicyStateAsync(token));
        Assert.Equal(fixture.DatabaseClock.GetUtcNow(), current.DatabaseTime);
        Assert.Equal(active.VerifiedUntil, current.VerifiedUntil);
        using JsonDocument declaration = JsonDocument.Parse(Declaration);
        LabelServiceResult<LabelPolicyState> observation = await restarted.ObservePolicyAsync(
            expectedRevision: current.Revision,
            verificationStartedAt: active.DatabaseTime,
            observedCid: s_cid,
            declaration: declaration.RootElement,
            cancellationToken: token);
        Assert.Equal(succeeds, observation.Succeeded);
        if (!succeeds)
        {
            Assert.Equal(LabelServiceErrorCode.StaleDecision, observation.Error!.Code);
            Assert.Contains("window expired", observation.Error.Message, StringComparison.Ordinal);
            Assert.Equal(current.Revision, (await restarted.ReadPolicyStateAsync(token))!.Revision);
        }
    }

    [Theory]
    [InlineData("sqlite", LabelTeamRole.Viewer)]
    [InlineData("sqlite", LabelTeamRole.Moderator)]
    [InlineData("postgresql", LabelTeamRole.Viewer)]
    [InlineData("postgresql", LabelTeamRole.Moderator)]
    [InlineData("mysql", LabelTeamRole.Viewer)]
    [InlineData("mysql", LabelTeamRole.Moderator)]
    public async Task PolicyWritesRequireLiveAdministratorAndReceiptRecoveryDoesNotBypassRevocation(string engine, LabelTeamRole role)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelPolicyStore store = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await ActivateAsync(store, administrator, token);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Did member = new("did:plc:member");
        Success(await team.ChangeTeamAsync(
            administrator, new(operationId: "enroll", action: LabelTeamAction.Enroll, target: member, reason: "Enroll.", role: role), token));
        Success(await team.ChangeTeamAsync(
            administrator, new(operationId: "bind", action: LabelTeamAction.BindLocal, target: member, reason: "Bind.", credential: "uid:1"), token));
        LabelAdministrationCaller caller = Success(await team.AuthenticateLocalAsync("uid:1", token));
        Assert.True((await store.GetPolicyStateAsync(caller, token)).Succeeded);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await store.SavePolicyAsync(caller, "draft", Policy(), "Draft.", token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await store.BeginPolicyPublicationAsync(
            caller: caller, operationId: "publish-denied", version: "v1", expectedRevision: active.Revision,
            previousCid: s_cid, reason: "Not authorized.", cancellationToken: token)).Error!.Code);
        Success(await team.ChangeTeamAsync(
            administrator, new(operationId: "suspend", action: LabelTeamAction.Suspend, target: member, reason: "Suspend."), token));
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await store.GetPolicyStateAsync(caller, token)).Error!.Code);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task LostPublicationAcknowledgementRecoversOriginalIntentWithoutReopeningGate(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelPolicyStore store = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState initial = await store.InitializePolicyAsync(cancellationToken: token);
        Success(await store.SavePolicyAsync(administrator, "draft", Policy(), "Draft.", token));
        fixture.SetCommitHooks(afterCommit: (_, _) => throw new SqliteException("Lost acknowledgement.", 10));
        LabelServiceResult<LabelPolicyState> uncertain = await store.BeginPolicyPublicationAsync(
            caller: administrator, operationId: "publish", version: "v1", expectedRevision: initial.Revision,
            previousCid: s_cid, reason: "Publish.", cancellationToken: token);
        Assert.Equal(LabelServiceErrorCode.OutcomeUnknown, uncertain.Error!.Code);
        fixture.SetCommitHooks();
        ILabelPolicyStore restarted = (ILabelPolicyStore)fixture.NewStore();
        LabelPolicyState recovered = Success(await restarted.BeginPolicyPublicationAsync(
            caller: administrator, operationId: "publish", version: "v1", expectedRevision: initial.Revision,
            previousCid: s_cid, reason: "Publish.", cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Publishing, recovered.Phase);
        Assert.Null(recovered.VerifiedUntil);
        Assert.Equal(LabelServiceErrorCode.IdempotencyConflict, (await restarted.BeginPolicyPublicationAsync(
            caller: administrator, operationId: "publish", version: "v1", expectedRevision: initial.Revision,
            previousCid: null, reason: "Publish.", cancellationToken: token)).Error!.Code);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task AbandonedPublicationRemainsClosedAndRecoversWithoutAssumingRemoteRollback(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelPolicyStore store = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState initial = await store.InitializePolicyAsync(cancellationToken: token);
        Success(await store.SavePolicyAsync(administrator, "draft", Policy(), "Draft.", token));
        LabelPolicyState publishing = Success(await store.BeginPolicyPublicationAsync(
            caller: administrator,
            operationId: "publish",
            version: "v1",
            expectedRevision: initial.Revision,
            previousCid: s_cid,
            reason: "Publish.",
            cancellationToken: token));
        LabelStoreState stream = await fixture.Store.GetStateAsync(token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.BeginRetirementAsync(
            expectedRevision: stream.Revision,
            caller: administrator,
            cancellationToken: token));
        fixture.SetCommitHooks(afterCommit: (_, _) => throw new SqliteException("Lost acknowledgement.", 10));
        Assert.Equal(LabelServiceErrorCode.OutcomeUnknown, (await store.AbandonPolicyPublicationAsync(
            caller: administrator,
            operationId: "abandon",
            expectedRevision: publishing.Revision,
            reason: "The conditional remote write must be reconciled.",
            cancellationToken: token)).Error!.Code);
        fixture.SetCommitHooks();
        ILabelPolicyStore restarted = (ILabelPolicyStore)fixture.NewStore();
        LabelPolicyState abandoned = Success(await restarted.AbandonPolicyPublicationAsync(
            caller: administrator,
            operationId: "abandon",
            expectedRevision: publishing.Revision,
            reason: "The conditional remote write must be reconciled.",
            cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Conflict, abandoned.Phase);
        Assert.Equal(StoreFixture.Issuer, abandoned.Issuer);
        Assert.Equal(s_cid, abandoned.PreviousCid);
        Assert.Equal("v1", abandoned.Policy!.Issuance.Version);
        Assert.Null(abandoned.VerifiedUntil);
        using JsonDocument declaration = JsonDocument.Parse(Declaration);
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await restarted.ObservePolicyAsync(
            expectedRevision: publishing.Revision,
            verificationStartedAt: publishing.DatabaseTime,
            observedCid: s_cid,
            declaration: declaration.RootElement,
            cancellationToken: token)).Error!.Code);
        LabelPolicyState lateWrite = Success(await restarted.ObservePolicyAsync(
            expectedRevision: abandoned.Revision,
            verificationStartedAt: abandoned.DatabaseTime,
            observedCid: s_cid,
            declaration: declaration.RootElement,
            cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Conflict, lateWrite.Phase);
        Assert.Equal(LabelServiceErrorCode.IdempotencyConflict, (await restarted.AbandonPolicyPublicationAsync(
            caller: administrator,
            operationId: "abandon",
            expectedRevision: publishing.Revision,
            reason: "Changed recovery intent.",
            cancellationToken: token)).Error!.Code);
        LabelPolicyState intent = Success(await restarted.BeginPolicyPublicationAsync(
            caller: administrator,
            operationId: "reconcile",
            version: "v1",
            expectedRevision: lateWrite.Revision,
            previousCid: s_cid,
            reason: "Reconcile the retained remote result.",
            cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.VerifiedActive, Success(await restarted.ObservePolicyAsync(
            expectedRevision: intent.Revision,
            verificationStartedAt: intent.DatabaseTime,
            observedCid: s_cid,
            declaration: declaration.RootElement,
            cancellationToken: token)).Phase);
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task ManualIssuanceRechecksPolicyAtCommitAndSafeWithdrawalSurvivesInvalidClock(string engine, bool rollback)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelPolicyStore store = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await ActivateAsync(store, administrator, token);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        LabelTarget target = LabelTarget.Account(new Did("did:plc:subject"));
        LabelOperationContext context = new(actor: s_administrator, reason: "Review.");
        LabelStoreState initial = await fixture.Store.GetStateAsync(token);
        DateTimeOffset created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        LabelDecisionCommit prepared = new(
            request: LabelOperationRequest.Issue(operationId: "issue", target: target, value: "spam", context: context),
            resolvedTarget: target,
            expectedRevision: initial.Revision,
            signedLabel: new(key.CreateLabel(source: StoreFixture.Issuer, uri: target.Subject, value: "spam", creationTimestamp: created)),
            audit: new(evaluatedAt: created, authorizationVersion: "test", policyVersion: "v1", signingKeyMultibase: key.ExportPublicKeyMultibase()))
        {
            AdministrationCaller = administrator
        };
        LabelOperationReceipt issued = Success(await fixture.Store.CommitDecisionAsync(prepared, token));
        Assert.True(issued.Changed);
        ILabelStore commitStore;
        if (rollback)
        {
            commitStore = fixture.NewStore(new(active.DatabaseTime.AddMilliseconds(-430)));
        }
        else
        {
            fixture.DatabaseClock!.Advance(active.VerificationFreshness);
            commitStore = fixture.Store;
        }
        LabelDecisionCommit stale = new(
            request: LabelOperationRequest.Issue(operationId: "stale", target: target, value: "scam", context: context),
            resolvedTarget: target,
            expectedRevision: issued.StateAtCommit.Revision,
            signedLabel: new(key.CreateLabel(source: StoreFixture.Issuer, uri: target.Subject, value: "scam", creationTimestamp: created)),
            audit: prepared.Audit)
        {
            AdministrationCaller = administrator
        };
        Assert.Equal(LabelServiceErrorCode.PolicyUnavailable, (await commitStore.CommitDecisionAsync(stale, token)).Error!.Code);
        Assert.Equal(issued.StateAtCommit, await fixture.Store.GetStateAsync(token));
        Assert.Contains(Success(await ((ILabelAdministrationStore)fixture.Store).GetAdministrationAuditAsync(
            administrator, cancellationToken: token)), receipt =>
            receipt.OperationId == "stale" && receipt.Action == "policy-issuance-denied" && !receipt.Succeeded);
        LabelDecisionCommit negate = new(
            request: LabelOperationRequest.Negate(operationId: "negate", target: target, value: "spam", context: context),
            resolvedTarget: target,
            expectedRevision: issued.StateAtCommit.Revision,
            signedLabel: new(key.CreateNegation(source: StoreFixture.Issuer, uri: target.Subject, value: "spam", creationTimestamp: created.AddTicks(1))),
            audit: prepared.Audit)
        {
            AdministrationCaller = administrator
        };
        Assert.True(Success(await commitStore.CommitDecisionAsync(negate, token)).Changed);
        Assert.Equal(active.Revision, (await store.ReadPolicyStateAsync(token))!.Revision);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RetainedVersionsAndRecoveryHistoryPreserveIntentAcrossDriftAbandonmentAndLateWrites(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await BootstrapAsync(fixture);
        ILabelPolicyStore store = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await ActivateAsync(store, caller, token);
        using JsonDocument declaration = JsonDocument.Parse(Declaration);
        LabelPolicyDefinition next = new(
            version: "v2", declaration: declaration.RootElement, allowAccounts: true, allowRecords: true, rules: "Updated review rules.");
        Success(await store.SavePolicyAsync(caller, "draft-v2", next, "Reviewed draft.", token));
        Assert.Equal(active.Revision, Success(await store.GetPolicyStateAsync(caller, token)).Revision);
        Assert.Equal(new[] { "v1", "v2" },
            Success(await store.GetPolicyVersionsAsync(caller, cancellationToken: token)).Select(policy => policy.Issuance.Version));
        Assert.Equal("v2", Assert.Single(Success(await store.GetPolicyVersionsAsync(
            caller: caller, afterVersion: "v1", limit: 1, cancellationToken: token))).Issuance.Version);
        Assert.Equal(next.DeclarationJson, Success(await store.GetPolicyVersionAsync(caller, "v2", token)).DeclarationJson);
        LabelPolicyState publication = Success(await store.BeginPolicyPublicationAsync(
            caller: caller, operationId: "publish-v2", version: "v2", expectedRevision: active.Revision,
            previousCid: active.VerifiedCid, reason: "Conditional publication.", cancellationToken: token));
        using JsonDocument mismatched = JsonDocument.Parse(Declaration.Replace("\"spam\"", "\"changed\"", StringComparison.Ordinal));
        LabelPolicyState mismatch = Success(await store.ObservePolicyAsync(
            expectedRevision: publication.Revision, verificationStartedAt: publication.DatabaseTime,
            observedCid: s_cid, declaration: mismatched.RootElement, cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Publishing, mismatch.Phase);
        LabelPolicyState abandoned = Success(await store.AbandonPolicyPublicationAsync(
            caller: caller, operationId: "abandon-v2", expectedRevision: mismatch.Revision, reason: "Recover uncertainty.", cancellationToken: token));
        LabelPolicyState late = Success(await store.ObservePolicyAsync(
            expectedRevision: abandoned.Revision, verificationStartedAt: abandoned.DatabaseTime,
            observedCid: s_cid, declaration: declaration.RootElement, cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Conflict, late.Phase);
        ILabelPolicyStore restarted = (ILabelPolicyStore)fixture.NewStore();
        IReadOnlyList<LabelPolicyHistoryEntry> history = Success(await restarted.GetPolicyHistoryAsync(caller, cancellationToken: token));
        Assert.Equal(8, history.Count);
        Assert.Equal(LabelPolicyHistoryAction.Draft, history[0].Action);
        Assert.Equal(LabelPolicyHistoryAction.Publication, history[1].Action);
        Assert.Equal(LabelPolicyHistoryAction.Observation, history[2].Action);
        Assert.Equal(LabelPolicyHistoryAction.Observation, history[5].Action);
        Assert.NotEqual(history[5].Policy!.DeclarationJson, history[5].ObservedDeclarationJson);
        Assert.Equal(LabelPolicyPhase.Publishing, history[5].State!.Phase);
        Assert.Equal(LabelPolicyHistoryAction.Abandonment, history[6].Action);
        Assert.Equal(LabelPolicyPhase.Conflict, history[7].State!.Phase);
        Assert.Equal(next.DeclarationJson, history[7].ObservedDeclarationJson);
        Assert.Null(history[7].Actor);
        Assert.Null(history[7].OperationId);
        LabelPolicyHistoryEntry recovered = Success(await restarted.GetPolicyOperationAsync(caller, "publish-v2", token));
        Assert.Equal(publication.Revision, recovered.State!.Revision);
        Assert.Equal(LabelPolicyPhase.Publishing, recovered.State.Phase);
        Assert.Equal(active.VerifiedCid, recovered.State.PreviousCid);
        Assert.Equal("v2", recovered.Policy!.Issuance.Version);
        Assert.Equal(caller.Actor, recovered.Actor);
        Assert.Equal(history[4].Sequence, recovered.Sequence);
        Assert.Equal(history.Skip(4).Select(entry => entry.Sequence), Success(await restarted.GetPolicyHistoryAsync(
            caller: caller, afterSequence: history[3].Sequence, limit: 100, cancellationToken: token)).Select(entry => entry.Sequence));
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task PolicyHistoryDraftReceiptAndAuditCommitTogetherAndRecoverWithoutDuplicateHistory(string engine, bool afterCommit)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await BootstrapAsync(fixture);
        ILabelPolicyStore store = (ILabelPolicyStore)fixture.Store;
        await ActivateAsync(store, caller, token);
        using JsonDocument declaration = JsonDocument.Parse(Declaration);
        LabelPolicyDefinition next = new(
            version: "v2", declaration: declaration.RootElement, allowAccounts: true, allowRecords: true, rules: "Updated rules.");
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        int auditCount = Success(await team.GetAdministrationAuditAsync(caller, cancellationToken: token)).Count;
        if (afterCommit)
        {
            fixture.SetCommitHooks(afterCommit: (_, _) => throw new SqliteException("Lost acknowledgement.", 10));
        }
        else
        {
            fixture.SetCommitHooks(beforeCommit: (_, _, _) => throw new SqliteException("Before commit.", 10));
        }

        Assert.Equal(afterCommit ? LabelServiceErrorCode.OutcomeUnknown : LabelServiceErrorCode.StorageUnavailable,
            (await store.SavePolicyAsync(caller, "draft-v2", next, "Draft.", token)).Error!.Code);
        fixture.SetCommitHooks();
        ILabelPolicyStore restarted = (ILabelPolicyStore)fixture.NewStore();
        Assert.Equal(afterCommit ? 4 : 3, Success(await restarted.GetPolicyHistoryAsync(caller, cancellationToken: token)).Count);
        Assert.Equal(afterCommit ? 2 : 1, Success(await restarted.GetPolicyVersionsAsync(caller, cancellationToken: token)).Count);
        Assert.Equal(auditCount + (afterCommit ? 1 : 0), Success(await team.GetAdministrationAuditAsync(caller, cancellationToken: token)).Count);
        if (afterCommit)
        {
            LabelPolicyHistoryEntry recovered = Success(await restarted.GetPolicyOperationAsync(caller, "draft-v2", token));
            Assert.Equal(next.DeclarationJson, recovered.Policy!.DeclarationJson);
            Success(await restarted.SavePolicyAsync(caller, "draft-v2", next, "Draft.", token));
            Assert.Equal(4, Success(await restarted.GetPolicyHistoryAsync(caller, cancellationToken: token)).Count);
        }
        else
        {
            Assert.Equal(LabelServiceErrorCode.OperationNotFound, (await restarted.GetPolicyOperationAsync(caller, "draft-v2", token)).Error!.Code);
        }
    }

    [Theory]
    [InlineData("""{"$type":"app.bsky.labeler.service","policies":{"labelValues":[]},"createdAt":"2026-01-01T00:00:00Z"}""")]
    [InlineData("""{"$type":"app.bsky.labeler.service","policies":{"labelValues":[]},"createdAt":"2026-01-01T00:00:00Z","subjectTypes":["chat"]}""")]
    [InlineData("""{"$type":"app.bsky.labeler.service","policies":{"labelValues":[]},"createdAt":"2026-01-01T00:00:00Z","subjectTypes":[],"reasonTypes":null}""")]
    [InlineData("""{"$type":"app.bsky.labeler.service","policies":{"labelValues":[]},"createdAt":"2026-01-01T00:00:00Z","subjectTypes":[],"extra":{"a":1,"a":2}}""")]
    public void DraftValidationRejectsImplicitUnsupportedNullOrAmbiguousScope(string json)
    {
        Assert.ThrowsAny<ArgumentException>(() => Policy(json: json));
    }

    [Theory]
    [InlineData("""{"\uD800":true}""")]
    [InlineData("""{"value":"\uD800"}""")]
    [InlineData("""{"value":0.0}""")]
    [InlineData("""{"value":1e0}""")]
    [InlineData("""{"value":1.00000000000000000000000000001}""")]
    [InlineData("""{"value":9223372036854775808}""")]
    [InlineData("""{"value":-9223372036854775809}""")]
    public void DraftsRejectMalformedUnicodeAndNonRepositoryNumbers(string extension)
    {
        Assert.ThrowsAny<ArgumentException>(() => Policy(json:
            Declaration.Replace("""{"preserve":true}""", extension, StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task CanonicalRepositoryNumbersSurvivePublicationAndObservation(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelPolicyStore store = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState initial = await store.InitializePolicyAsync(cancellationToken: token);
        LabelPolicyDefinition policy = Policy(json: Declaration.Replace(
            """{"preserve":true}""", """{"zero":-0,"min":-9223372036854775808,"max":9223372036854775807}""", StringComparison.Ordinal));
        Success(await store.SavePolicyAsync(administrator, "draft", policy, "Integer extensions.", token));
        Assert.Contains("\"zero\":0", policy.DeclarationJson, StringComparison.Ordinal);
        LabelPolicyState publication = Success(await store.BeginPolicyPublicationAsync(
            caller: administrator, operationId: "publish", version: "v1", expectedRevision: initial.Revision,
            previousCid: null, reason: "Publish.", cancellationToken: token));
        using JsonDocument remote = JsonDocument.Parse(policy.DeclarationJson);
        Assert.Equal(LabelPolicyPhase.VerifiedActive, Success(await store.ObservePolicyAsync(
            expectedRevision: publication.Revision,
            verificationStartedAt: publication.DatabaseTime,
            observedCid: s_cid,
            declaration: remote.RootElement,
            cancellationToken: token)).Phase);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{}]")]
    [InlineData("""[{"identifier":"spam","severity":"inform","blurs":"none","defaultSetting":null,"locales":[{"lang":"en","name":"Spam","description":"Spam."}]}]""")]
    [InlineData("""[{"identifier":"spam","severity":"unsupported","blurs":"none","locales":[{"lang":"en","name":"Spam","description":"Spam."}]}]""")]
    [InlineData("""[{"identifier":"!hide","severity":"inform","blurs":"none","locales":[{"lang":"en","name":"Hide","description":"Hide."}]}]""")]
    [InlineData("""[{"identifier":"spam","severity":"inform","blurs":"none","locales":[]}]""")]
    [InlineData("""[{"identifier":"spam","severity":"inform","blurs":"none","locales":[null]}]""")]
    [InlineData("""[{"identifier":"spam","severity":"inform","blurs":"none","locales":[{"lang":"en","name":"Spam","description":"Spam."},{"lang":"EN","name":"Spam","description":"Spam."}]}]""")]
    public void DraftsUseSharedSdkCustomDefinitionValidation(string definitions)
    {
        Assert.ThrowsAny<ArgumentException>(() => Policy(json: Declaration.Replace(
            "\"labelValueDefinitions\":[]", "\"labelValueDefinitions\":" + definitions, StringComparison.Ordinal)));
    }

    [Fact]
    public void ValidDefinitionsRetainExtensionsAndCanonicalEncodingCannotExceedTheStorageBound()
    {
        const string definition = """
            [{"identifier":"spam","severity":"inform","blurs":"content","locales":[{"lang":"en","name":"Spam","description":"Spam."}],
              "extension":{"retained":true}}]
            """;
        LabelPolicyDefinition policy = Policy(json: Declaration.Replace(
            "\"labelValueDefinitions\":[]", "\"labelValueDefinitions\":" + definition, StringComparison.Ordinal));
        using JsonDocument retained = JsonDocument.Parse(policy.DeclarationJson);
        Assert.True(retained.RootElement.GetProperty("policies").GetProperty("labelValueDefinitions")[0]
            .GetProperty("extension").GetProperty("retained").GetBoolean());
        string expansion = Declaration.TrimEnd()[..^1] + ",\"unicode\":\"" + new string('\u00e9', 20000) + "\"}";
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(expansion) < 65536);
        Assert.Throws<ArgumentOutOfRangeException>(() => Policy(json: expansion));
    }

    /// <summary>Creates a complete immutable policy retaining declaration extension data.</summary>
    private static LabelPolicyDefinition Policy(string rules = "Review evidence before labeling.", string json = Declaration)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return new(version: "v1", declaration: document.RootElement, allowAccounts: true, allowRecords: true, rules: rules);
    }

    /// <summary>Requires a successful classified result without losing failure diagnostics.</summary>
    private static T Success<T>(LabelServiceResult<T> result) where T : class
    {
        Assert.True(result.Succeeded, result.Error?.Message);
        return result.Result;
    }

    /// <summary>Bootstraps explicit local administrator authority using the existing team store.</summary>
    internal static async Task<LabelAdministrationCaller> BootstrapAsync(StoreFixture fixture)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Success(await team.BootstrapAsync(s_administrator, "uid:0", "bootstrap", token));
        return Success(await team.AuthenticateLocalAsync("uid:0", token));
    }

    /// <summary>Exercises draft, publication and independent provider observation before returning an active gate.</summary>
    internal static async Task<LabelPolicyState> ActivateAsync(
        ILabelPolicyStore store, LabelAdministrationCaller administrator, CancellationToken token)
    {
        LabelPolicyState initial = await store.InitializePolicyAsync(cancellationToken: token);
        Success(await store.SavePolicyAsync(administrator, "draft", Policy(), "Draft.", token));
        LabelPolicyState publishing = Success(await store.BeginPolicyPublicationAsync(
            caller: administrator, operationId: "publish", version: "v1", expectedRevision: initial.Revision,
            previousCid: null, reason: "Publish.", cancellationToken: token));
        using JsonDocument declaration = JsonDocument.Parse(Declaration);
        return Success(await store.ObservePolicyAsync(
            expectedRevision: publishing.Revision,
            verificationStartedAt: publishing.DatabaseTime,
            observedCid: s_cid,
            declaration: declaration.RootElement,
            cancellationToken: token));
    }
}
