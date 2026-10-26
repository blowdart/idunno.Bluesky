// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;

namespace idunno.Bluesky.Labeler.Test;

public class LabelAppealStoreTests
{
    private static readonly Did s_owner = new(TargetResolutionHandler.SubjectDid);
    private static readonly LabelReportSubject s_account = LabelReportSubject.Account(s_owner);
    private const string AppealReason = "com.atproto.moderation.defs#reasonAppeal";
    private const string LegacyAppealReason = "tools.ozone.report.defs#reasonAppeal";

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task CaptureUsesCurrentSignedTimeAndRetainsSeparateReportsAcrossRestart(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller caller, LabelPolicyState policy) = await ActivateAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DateTimeOffset time = policy.DatabaseTime.AddHours(-4);
        SignedLabelSnapshot original = new(key.CreateLabel(
            source: StoreFixture.Issuer, uri: s_account.Subject, value: "spam", creationTimestamp: time));
        await fixture.Store.AppendAsync(
            operationId: "manual-history",
            labels:
            [
                original,
                new(key.CreateLabel(source: StoreFixture.Issuer, uri: s_account.Subject, value: "scam", creationTimestamp: time)),
                new(key.CreateNegation(source: StoreFixture.Issuer, uri: s_account.Subject, value: "scam", creationTimestamp: time.AddHours(1))),
                new(key.CreateLabel(
                    source: StoreFixture.Issuer, uri: s_account.Subject, value: "expired",
                    creationTimestamp: time, expiresAt: time.AddHours(1))),
                new(key.CreateLabel(source: StoreFixture.Issuer, uri: s_account.Subject, value: "spam", creationTimestamp: time.AddHours(-1))),
                new(key.CreateLabel(source: StoreFixture.Issuer, uri: "did:plc:other", value: "spam", creationTimestamp: time))
            ],
            cancellationToken: token);
        LabelModerationReport first = await SubmitAsync(fixture.Store, policy, "first", s_account);
        LabelModerationReport second = await SubmitAsync(fixture.Store, policy, "second", s_account, LegacyAppealReason);
        ILabelAppealStore restarted = (ILabelAppealStore)fixture.NewStore();
        LabelAppeal appeal = Assert.Single(Success(await restarted.GetAppealsAsync(caller, cancellationToken: token)));
        StoredLabel challenge = Assert.Single(appeal.Challenges);
        Assert.Equal(1, challenge.Sequence);
        Assert.Equal(original.GetJsonBytes(), challenge.SignedLabel.GetJsonBytes());
        Assert.Equal(LabelAppealStatus.Pending, appeal.Status);
        Assert.Equal(first.CaseId, appeal.CaseId);
        Assert.Equal(second.CaseId, appeal.CaseId);
        Assert.Equal(new[] { first.Id, second.Id },
            Success(await restarted.GetAppealReportsAsync(caller, appeal.Id, cancellationToken: token)));
        Assert.Single(Success(await restarted.GetAppealReportsAsync(
            caller: caller, appealId: appeal.Id, afterReportId: first.Id, limit: 1, cancellationToken: token)));
        Assert.Equal(6, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RecordAppealsCaptureOnlyTheExactCurrentCidAndAccountRemainsSeparate(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller caller, LabelPolicyState policy) = await ActivateAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        AtUri uri = new($"at://{s_owner}/app.bsky.actor.profile/self");
        Cid oldCid = new(TargetResolutionHandler.RecordCid);
        Cid newCid = new("bafyreif2pall7yry6b3sh5rttgeczzmhnnemy6rtbwwsvxeceebzxtpfwu");
        DateTimeOffset created = policy.DatabaseTime.AddHours(-1);
        await fixture.Store.AppendAsync(
            operationId: "manual-history",
            labels:
            [
                new(key.CreateLabel(source: StoreFixture.Issuer, uri: uri.ToString(), value: "spam", creationTimestamp: created, cid: oldCid)),
                new(key.CreateLabel(source: StoreFixture.Issuer, uri: uri.ToString(), value: "spam", creationTimestamp: created.AddMinutes(1), cid: newCid)),
                new(key.CreateLabel(source: StoreFixture.Issuer, uri: s_account.Subject, value: "spam", creationTimestamp: created))
            ],
            cancellationToken: token);
        await SubmitAsync(fixture.Store, policy, "old", LabelReportSubject.Record(uri, oldCid));
        await SubmitAsync(fixture.Store, policy, "new", LabelReportSubject.Record(uri, newCid));
        await SubmitAsync(fixture.Store, policy, "account", s_account);
        IReadOnlyList<LabelAppeal> appeals = Success(await ((ILabelAppealStore)fixture.Store).GetAppealsAsync(caller, cancellationToken: token));
        Assert.Equal(3, appeals.Count);
        Assert.Empty(appeals[0].Challenges);
        Assert.Equal(2, Assert.Single(appeals[1].Challenges).Sequence);
        Assert.Equal(3, Assert.Single(appeals[2].Challenges).Sequence);
        Assert.Equal(3, appeals.Select(appeal => appeal.CaseId).Distinct().Count());
    }

    [Theory]
    [InlineData("sqlite", LabelAppealStatus.Upheld)]
    [InlineData("sqlite", LabelAppealStatus.Withdrawn)]
    [InlineData("sqlite", LabelAppealStatus.Replaced)]
    [InlineData("postgresql", LabelAppealStatus.Upheld)]
    [InlineData("postgresql", LabelAppealStatus.Withdrawn)]
    [InlineData("postgresql", LabelAppealStatus.Replaced)]
    [InlineData("mysql", LabelAppealStatus.Upheld)]
    [InlineData("mysql", LabelAppealStatus.Withdrawn)]
    [InlineData("mysql", LabelAppealStatus.Replaced)]
    public async Task ExplicitOutcomesUseSharedAtomicIssuanceAndRecoverOriginalProvenance(string engine, LabelAppealStatus outcome)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller caller, LabelPolicyState policy) = await ActivateAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelIssuanceService service = Service(fixture, caller, policy, key, http);
        LabelOperationReceipt manual = Success(await service.ExecuteAsync(LabelOperationRequest.Issue(
            operationId: "manual", target: LabelTarget.Account(s_owner), value: "spam",
            context: new(caller.Actor, "Case-independent labeling.")), token));
        LabelModerationReport report = await SubmitAsync(fixture.Store, policy, "appeal", s_account);
        ILabelAppealStore appeals = (ILabelAppealStore)fixture.Store;
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        LabelAppeal appeal = Assert.Single(Success(await appeals.GetAppealsAsync(caller, cancellationToken: token)));
        LabelModerationCase moderationCase = Success(await cases.GetCaseAsync(caller, report.CaseId, token));
        Assert.Equal(LabelServiceErrorCode.CaseConflict, (await cases.ChangeCaseAsync(
            caller,
            new(
                operationId: "close-without-review", subject: s_account, action: LabelCaseAction.Resolve,
                reason: "Cannot skip appeal review.", caseId: moderationCase.Id, expectedRevision: moderationCase.Revision),
            token)).Error!.Code);
        LabelCaseDecision implicitClosure = Decision(moderationCase, [], "implicit", null, null);
        Assert.Equal(LabelServiceErrorCode.CaseConflict,
            (await service.ExecuteCaseDecisionAsync(caller, implicitClosure, token)).Error!.Code);
        LabelCaseLabelAction[] actions = outcome switch
        {
            LabelAppealStatus.Withdrawn => [new(LabelOperationKind.Negate, "spam")],
            LabelAppealStatus.Replaced => [new(LabelOperationKind.Negate, "spam"), new(LabelOperationKind.Issue, "scam")],
            _ => []
        };
        LabelCaseDecision decision = Decision(moderationCase, actions, "review", [appeal.Id], outcome);
        LabelCaseDecisionReceipt receipt = Success(await service.ExecuteCaseDecisionAsync(caller, decision, token));
        LabelAppeal resolved = Success(await ((ILabelAppealStore)fixture.NewStore()).GetAppealAsync(caller, appeal.Id, token));
        Assert.Equal(outcome, resolved.Status);
        Assert.Equal("review", resolved.DecisionOperationId);
        Assert.Equal(manual.Label!.SignedLabel.GetJsonBytes(), Assert.Single(resolved.Challenges).SignedLabel.GetJsonBytes());
        Assert.Equal(LabelCaseStatus.Resolved, receipt.CaseEvent.Case.Status);
        Assert.Equal(actions.Length, receipt.Labels.Count);
        LabelCaseDecisionReceipt replay = Success(await service.ExecuteCaseDecisionAsync(caller, decision, token));
        Assert.Equal(receipt.CaseEvent.Sequence, replay.CaseEvent.Sequence);
        Assert.Equal(outcome, replay.Request.AppealOutcome);
        Assert.Equal(new[] { appeal.Id }, replay.Request.AppealIds);
        Assert.Equal(receipt.Labels.Select(label => label.Label!.SignedLabel.GetJsonBytes()),
            replay.Labels.Select(label => label.Label!.SignedLabel.GetJsonBytes()));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ChangedOutcomesRequireSupersededReviewAndOtherPendingAppealsKeepCaseOpen(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller caller, LabelPolicyState policy) = await ActivateAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelIssuanceService service = Service(fixture, caller, policy, key, http);
        LabelOperationContext context = new(caller.Actor, "Independent manual correction.");
        Success(await service.ExecuteAsync(LabelOperationRequest.Issue(
            operationId: "manual", target: LabelTarget.Account(s_owner), value: "spam", context: context), token));
        LabelModerationReport first = await SubmitAsync(fixture.Store, policy, "first", s_account);
        Success(await service.ExecuteAsync(LabelOperationRequest.Issue(
            operationId: "replacement", target: LabelTarget.Account(s_owner), value: "spam", context: context,
            expiresAt: policy.DatabaseTime.AddDays(1)), token));
        await SubmitAsync(fixture.Store, policy, "second", s_account);
        ILabelAppealStore appeals = (ILabelAppealStore)fixture.Store;
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        IReadOnlyList<LabelAppeal> pending = Success(await appeals.GetAppealsAsync(caller, cancellationToken: token));
        Assert.Equal(2, pending.Count);
        LabelModerationCase moderationCase = Success(await cases.GetCaseAsync(caller, first.CaseId, token));
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await service.ExecuteCaseDecisionAsync(
            caller, Decision(moderationCase, [], "stale", [pending[0].Id], LabelAppealStatus.Upheld), token)).Error!.Code);
        LabelCaseDecisionReceipt superseded = Success(await service.ExecuteCaseDecisionAsync(
            caller, Decision(moderationCase, [], "superseded", [pending[0].Id], LabelAppealStatus.Superseded), token));
        Assert.Equal(LabelCaseStatus.InReview, superseded.CaseEvent.Case.Status);
        Assert.Equal(LabelAppealStatus.Pending, Success(await appeals.GetAppealAsync(caller, pending[1].Id, token)).Status);
        LabelCaseDecisionReceipt upheld = Success(await service.ExecuteCaseDecisionAsync(
            caller, Decision(superseded.CaseEvent.Case, [], "upheld", [pending[1].Id], LabelAppealStatus.Upheld), token));
        Assert.Equal(LabelCaseStatus.Resolved, upheld.CaseEvent.Case.Status);
        Assert.Equal(2, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task IntakeFaultsRollbackReportAppealAndReplayTogetherAndRecoverLostAcknowledgement(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller caller, LabelPolicyState policy) = await ActivateAsync(fixture);
        ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(s_owner, policy.DatabaseTime, "appeal");
        ILabelReportStore reports = (ILabelReportStore)fixture.Store;
        ILabelAppealStore appeals = (ILabelAppealStore)fixture.Store;
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        int auditCount = Success(await team.GetAdministrationAuditAsync(caller, cancellationToken: token)).Count;
        fixture.SetCommitHooks(beforeCommit: (_, _, _) => throw new SqliteException("Before commit.", 10));
        Assert.Equal(LabelServiceErrorCode.StorageUnavailable,
            (await reports.SubmitReportAsync(authentication, new(s_account, AppealReason), policy.Revision, token)).Error!.Code);
        fixture.SetCommitHooks();
        Assert.Empty(Success(await reports.GetReportsAsync(caller, cancellationToken: token)));
        Assert.Empty(Success(await appeals.GetAppealsAsync(caller, cancellationToken: token)));
        Assert.Equal(auditCount, Success(await team.GetAdministrationAuditAsync(caller, cancellationToken: token)).Count);
        fixture.SetCommitHooks(afterCommit: (_, _) => throw new SqliteException("Lost acknowledgement.", 10));
        Assert.Equal(LabelServiceErrorCode.OutcomeUnknown,
            (await reports.SubmitReportAsync(authentication, new(s_account, AppealReason), policy.Revision, token)).Error!.Code);
        fixture.SetCommitHooks();
        LabelAppeal appeal = Assert.Single(Success(await ((ILabelAppealStore)fixture.NewStore()).GetAppealsAsync(caller, cancellationToken: token)));
        Assert.Empty(appeal.Challenges);
        Assert.Single(Success(await appeals.GetAppealReportsAsync(caller, appeal.Id, cancellationToken: token)));
        Assert.Equal(LabelServiceErrorCode.Unauthorized,
            (await reports.SubmitReportAsync(authentication, new(s_account, AppealReason), policy.Revision, token)).Error!.Code);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ViewerReadsAreLiveAuthorizedAndSuccessfulInspectionDoesNotAppendAudit(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller caller, LabelPolicyState policy) = await ActivateAsync(fixture);
        await SubmitAsync(fixture.Store, policy, "appeal", s_account);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Did viewerDid = new("did:plc:viewer");
        Success(await team.ChangeTeamAsync(
            caller,
            new(operationId: "enroll", action: LabelTeamAction.Enroll, target: viewerDid, reason: "Enroll viewer.", role: LabelTeamRole.Viewer),
            token));
        Success(await team.ChangeTeamAsync(
            caller,
            new(operationId: "bind", action: LabelTeamAction.BindLocal, target: viewerDid, reason: "Bind viewer.", credential: "uid:1"),
            token));
        LabelAdministrationCaller viewer = Success(await team.AuthenticateLocalAsync("uid:1", token));
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await ((ILabelModerationStore)fixture.Store).ChangeCaseAsync(
            viewer,
            new(operationId: "viewer-review", subject: s_account, action: LabelCaseAction.OpenAppeal, reason: "Viewer cannot open reviews."),
            token)).Error!.Code);
        int auditCount = Success(await team.GetAdministrationAuditAsync(caller, cancellationToken: token)).Count;
        ILabelAppealStore appeals = (ILabelAppealStore)fixture.Store;
        LabelAppeal appeal = Assert.Single(Success(await appeals.GetAppealsAsync(viewer, cancellationToken: token)));
        Success(await appeals.GetAppealAsync(viewer, appeal.Id, token));
        Assert.Single(Success(await appeals.GetAppealReportsAsync(viewer, appeal.Id, cancellationToken: token)));
        Assert.Empty(Success(await appeals.GetAppealsAsync(
            caller: viewer, caseId: appeal.CaseId, status: LabelAppealStatus.Upheld, cancellationToken: token)));
        ILabelPolicyStore policies = (ILabelPolicyStore)fixture.Store;
        Success(await policies.GetPolicyVersionAsync(viewer, "v1", token));
        Assert.Single(Success(await policies.GetPolicyVersionsAsync(viewer, cancellationToken: token)));
        Assert.Equal(3, Success(await policies.GetPolicyHistoryAsync(viewer, cancellationToken: token)).Count);
        Success(await policies.GetPolicyOperationAsync(viewer, "publish", token));
        Assert.Equal(auditCount, Success(await team.GetAdministrationAuditAsync(caller, cancellationToken: token)).Count);
        Success(await team.ChangeTeamAsync(
            caller,
            new(operationId: "suspend", action: LabelTeamAction.Suspend, target: viewerDid, reason: "Revoke inspection."),
            token));
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await appeals.GetAppealAsync(viewer, appeal.Id, token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await appeals.GetAppealsAsync(viewer, cancellationToken: token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await appeals.GetAppealReportsAsync(viewer, appeal.Id, cancellationToken: token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await policies.GetPolicyVersionAsync(viewer, "v1", token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await policies.GetPolicyVersionsAsync(viewer, cancellationToken: token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await policies.GetPolicyHistoryAsync(viewer, cancellationToken: token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await policies.GetPolicyOperationAsync(viewer, "publish", token)).Error!.Code);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task InternalReviewsNeedNoExternalReportAndStableRetryCannotRecaptureNewerLabels(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller caller, LabelPolicyState policy) = await ActivateAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelIssuanceService service = Service(fixture, caller, policy, key, http);
        LabelOperationContext context = new(caller.Actor, "Manual labeling.");
        LabelOperationReceipt original = Success(await service.ExecuteAsync(LabelOperationRequest.Issue(
            operationId: "manual", target: LabelTarget.Account(s_owner), value: "spam", context: context), token));
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        LabelCaseChange request = new(
            operationId: "internal", subject: s_account, action: LabelCaseAction.OpenAppeal, reason: "Internal quality review.");
        LabelCaseReceipt opened = Success(await cases.ChangeCaseAsync(caller, request, token));
        await SubmitAsync(fixture.Store, policy, "external", s_account);
        ILabelAppealStore appeals = (ILabelAppealStore)fixture.Store;
        LabelAppeal appeal = Assert.Single(Success(await appeals.GetAppealsAsync(caller, cancellationToken: token)));
        Assert.Equal(caller.Actor, appeal.Initiator);
        Assert.Single(Success(await appeals.GetAppealReportsAsync(caller, appeal.Id, cancellationToken: token)));
        Success(await service.ExecuteAsync(LabelOperationRequest.Issue(
            operationId: "later", target: LabelTarget.Account(s_owner), value: "spam", context: context,
            expiresAt: policy.DatabaseTime.AddDays(1)), token));
        Assert.Equal(opened.Sequence, Success(await cases.ChangeCaseAsync(caller, request, token)).Sequence);
        LabelAppeal retained = Assert.Single(Success(await appeals.GetAppealsAsync(caller, cancellationToken: token)));
        Assert.Equal(original.Label!.Sequence, Assert.Single(retained.Challenges).Sequence);
        LabelCaseReceipt another = Success(await cases.ChangeCaseAsync(
            caller,
            new(operationId: "another-review", subject: s_account, action: LabelCaseAction.OpenAppeal, reason: "Review the newer outcome."),
            token));
        Assert.Equal(opened.Case.Id, another.Case.Id);
        Assert.Equal(2, Success(await appeals.GetAppealsAsync(caller, cancellationToken: token)).Count);
        Assert.Single(Success(await ((ILabelReportStore)fixture.Store).GetReportsAsync(caller, cancellationToken: token)));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task NewerRecordCidCannotBeUpheldOrWithdrawnThroughAnOlderCapturedAppeal(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller caller, LabelPolicyState policy) = await ActivateAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        AtUri uri = new($"at://{s_owner}/app.bsky.actor.profile/self");
        Cid originalCid = new(TargetResolutionHandler.RecordCid);
        LabelReportSubject subject = LabelReportSubject.Record(uri, originalCid);
        DateTimeOffset created = policy.DatabaseTime.AddHours(-1);
        await fixture.Store.AppendAsync(
            "original",
            [new(key.CreateLabel(source: StoreFixture.Issuer, uri: uri.ToString(), value: "spam", creationTimestamp: created, cid: originalCid))],
            cancellationToken: token);
        LabelModerationReport report = await SubmitAsync(fixture.Store, policy, "appeal", subject);
        LabelAppeal appeal = Assert.Single(Success(await ((ILabelAppealStore)fixture.Store).GetAppealsAsync(caller, cancellationToken: token)));
        await fixture.Store.AppendAsync(
            "later-version",
            [new(key.CreateLabel(
                source: StoreFixture.Issuer, uri: uri.ToString(), value: "spam", creationTimestamp: created.AddMinutes(1),
                cid: new("bafyreif2pall7yry6b3sh5rttgeczzmhnnemy6rtbwwsvxeceebzxtpfwu")))],
            cancellationToken: token);
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        LabelModerationCase moderationCase = Success(await cases.GetCaseAsync(caller, report.CaseId, token));
        LabelCaseDecision uphold = Decision(moderationCase, [], "uphold", [appeal.Id], LabelAppealStatus.Upheld);
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await cases.CommitCaseDecisionAsync(
            new(caller, uphold, policy.Revision, []), token)).Error!.Code);
        LabelCaseDecision supersede = Decision(moderationCase, [], "supersede", [appeal.Id], LabelAppealStatus.Superseded);
        Success(await cases.CommitCaseDecisionAsync(new(caller, supersede, policy.Revision, []), token));
        Assert.Equal(LabelAppealStatus.Superseded,
            Success(await ((ILabelAppealStore)fixture.Store).GetAppealAsync(caller, appeal.Id, token)).Status);
        Assert.Equal(2, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task ResolutionFaultsKeepLabelAppealCaseReceiptAndAuditAtomic(string engine, bool afterCommit)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller caller, LabelPolicyState policy) = await ActivateAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelIssuanceService service = Service(fixture, caller, policy, key, http);
        Success(await service.ExecuteAsync(LabelOperationRequest.Issue(
            operationId: "manual", target: LabelTarget.Account(s_owner), value: "spam", context: new(caller.Actor, "Manual labeling.")), token));
        LabelModerationReport report = await SubmitAsync(fixture.Store, policy, "appeal", s_account);
        LabelAppeal appeal = Assert.Single(Success(await ((ILabelAppealStore)fixture.Store).GetAppealsAsync(caller, cancellationToken: token)));
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        LabelModerationCase moderationCase = Success(await cases.GetCaseAsync(caller, report.CaseId, token));
        LabelCaseDecision request = Decision(
            moderationCase, [new(LabelOperationKind.Negate, "spam")], "withdraw", [appeal.Id], LabelAppealStatus.Withdrawn);
        LabelDecisionCommit prepared = Success(await service.PrepareDecisionAsync(LabelOperationRequest.Negate(
            operationId: request.ActionOperationId(0), target: LabelTarget.Account(s_owner), value: "spam",
            context: new(caller.Actor, request.Reason)), token));
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
            (await cases.CommitCaseDecisionAsync(new(caller, request, policy.Revision, [prepared]), token)).Error!.Code);
        fixture.SetCommitHooks();
        ILabelStore restarted = fixture.NewStore();
        LabelAppeal retained = Success(await ((ILabelAppealStore)restarted).GetAppealAsync(caller, appeal.Id, token));
        Assert.Equal(afterCommit ? LabelAppealStatus.Withdrawn : LabelAppealStatus.Pending, retained.Status);
        Assert.Equal(afterCommit ? 2 : 1, (await restarted.GetStateAsync(token)).IssuedThrough);
        LabelModerationCase retainedCase = Success(await ((ILabelModerationStore)restarted).GetCaseAsync(caller, moderationCase.Id, token));
        Assert.Equal(afterCommit ? LabelCaseStatus.Resolved : LabelCaseStatus.Open, retainedCase.Status);
        if (afterCommit)
        {
            LabelCaseDecisionReceipt receipt = Success(await ((ILabelModerationStore)restarted).GetCaseDecisionAsync(caller, request.OperationId, token));
            Assert.Equal(prepared.SignedLabel!.GetJsonBytes(), Assert.Single(receipt.Labels).Label!.SignedLabel.GetJsonBytes());
            Assert.Equal(auditCount + 1, Success(await team.GetAdministrationAuditAsync(caller, cancellationToken: token)).Count);
        }
        else
        {
            Assert.Equal(auditCount, Success(await team.GetAdministrationAuditAsync(caller, cancellationToken: token)).Count);
            Assert.Equal(LabelServiceErrorCode.OperationNotFound,
                (await ((ILabelModerationStore)restarted).GetCaseDecisionAsync(caller, request.OperationId, token)).Error!.Code);
        }
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task CaptureBoundRejectsTheWholeReportInsteadOfTruncatingChallenges(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller caller, LabelPolicyState policy) = await ActivateAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot[] history = Enumerable.Range(0, 1001).Select(index => new SignedLabelSnapshot(
            key.CreateLabel(
                source: StoreFixture.Issuer,
                uri: s_account.Subject,
                value: "historical-" + index,
                creationTimestamp: policy.DatabaseTime.AddHours(-1)))).ToArray();
        await fixture.Store.AppendAsync(
            operationId: "history-first",
            labels: history.Take(1000).ToArray(),
            cancellationToken: token);
        LabelModerationReport accepted = await SubmitAsync(fixture.Store, policy, "at-capture-limit", s_account);
        LabelAppeal original = Assert.Single(Success(await ((ILabelAppealStore)fixture.Store).GetAppealsAsync(caller, cancellationToken: token)));
        Assert.Equal(1000, original.Challenges.Count);
        await fixture.Store.AppendAsync(
            operationId: "history-last",
            labels: [history[1000]],
            cancellationToken: token);
        ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(s_owner, policy.DatabaseTime, "bounded-appeal");
        LabelServiceResult<LabelModerationReport> result = await ((ILabelReportStore)fixture.Store).SubmitReportAsync(
            authentication, new(s_account, AppealReason), policy.Revision, token);
        Assert.False(result.Succeeded);
        Assert.Equal(LabelServiceErrorCode.PolicyUnavailable, result.Error!.Code);
        Assert.Contains("bounded capture limit", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(accepted.Id, Assert.Single(Success(await ((ILabelReportStore)fixture.Store).GetReportsAsync(caller, cancellationToken: token))).Id);
        Assert.Equal(original.Id, Assert.Single(Success(await ((ILabelAppealStore)fixture.Store).GetAppealsAsync(caller, cancellationToken: token))).Id);
        Assert.Equal(accepted.CaseId, Assert.Single(Success(await ((ILabelModerationStore)fixture.Store).GetCasesAsync(caller, cancellationToken: token))).Id);
        LabelServiceResult<LabelModerationReport> retry = await ((ILabelReportStore)fixture.Store).SubmitReportAsync(
            authentication, new(s_account, AppealReason), policy.Revision, token);
        Assert.Equal(LabelServiceErrorCode.PolicyUnavailable, retry.Error!.Code);
        Assert.Contains("bounded capture limit", retry.Error.Message, StringComparison.Ordinal);
        Assert.Equal(1001, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    /// <summary>Initializes the real provider's shared administration and independently verified policy gate.</summary>
    private static async Task<(LabelAdministrationCaller Caller, LabelPolicyState Policy)> ActivateAsync(StoreFixture fixture)
    {
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync(
            (ILabelPolicyStore)fixture.Store, caller, TestContext.Current.CancellationToken);
        return (caller, policy);
    }

    /// <summary>Creates independent owner authentication for each distinct standard report submission.</summary>
    private static async Task<LabelModerationReport> SubmitAsync(
        ILabelStore store,
        LabelPolicyState policy,
        string tokenId,
        LabelReportSubject subject,
        string reason = AppealReason)
    {
        ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(s_owner, policy.DatabaseTime, tokenId);
        return Success(await ((ILabelReportStore)store).SubmitReportAsync(
            authentication, new(subject, reason), policy.Revision, TestContext.Current.CancellationToken));
    }

    /// <summary>Creates production shared issuance with a fixed clock and provider-backed moderator authority.</summary>
    private static LabelIssuanceService Service(
        StoreFixture fixture,
        LabelAdministrationCaller caller,
        LabelPolicyState policy,
        LabelSigningKey key,
        HttpClient http) => new(
            issuer: StoreFixture.Issuer,
            signingKey: key,
            store: fixture.Store,
            authorizer: new LabelTeamAuthorizer((ILabelAdministrationStore)fixture.Store, caller),
            policyProvider: new TestLabelIssuancePolicyProvider(policy.Policy!.Issuance),
            httpClient: http,
            timeProvider: fixture.DatabaseClock!);

    /// <summary>Specifies appeal resolution independently of the current provider observations.</summary>
    private static LabelCaseDecision Decision(
        LabelModerationCase moderationCase,
        IReadOnlyCollection<LabelCaseLabelAction> actions,
        string operation,
        IReadOnlyCollection<long>? appeals,
        LabelAppealStatus? outcome) => new(
            operationId: operation,
            caseId: moderationCase.Id,
            expectedRevision: moderationCase.Revision,
            policyVersion: "v1",
            reason: "Explicit moderator review.",
            actions: actions,
            appealIds: appeals,
            appealOutcome: outcome);

    /// <summary>Preserves classified provider diagnostics when an expected operation fails.</summary>
    private static T Success<T>(LabelServiceResult<T> result) where T : class
    {
        Assert.True(result.Succeeded, result.Error?.Message);
        return result.Result;
    }
}
