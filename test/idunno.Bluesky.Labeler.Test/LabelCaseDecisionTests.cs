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

namespace idunno.Bluesky.Labeler.Test;

public partial class LabelCaseDecisionTests
{
    private static readonly LabelReportSubject s_subject = LabelReportSubject.Account(new(TargetResolutionHandler.SubjectDid));

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task AcknowledgmentPreservesLabelsAndPendingAppealsWhileEndingEscalation(string engine, bool withAppeal)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelIssuanceService service = Service(fixture, caller, active, key, http);
        LabelTarget target = LabelTarget.Account(s_subject.Repository);
        LabelOperationReceipt issued = Success(await service.ExecuteAsync(LabelOperationRequest.Issue(
            operationId: "acknowledge-existing-label",
            target: target,
            value: "spam",
            context: new(caller.Actor, "Retain this label during report acknowledgment.")), token));
        ILabelReportStore reports = (ILabelReportStore)fixture.Store;
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        ILabelAppealStore appeals = (ILabelAppealStore)fixture.Store;
        ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(
            s_subject.Repository, active.DatabaseTime, "acknowledge-report");
        LabelModerationReport report = Success(await reports.SubmitReportAsync(
            authentication,
            new(s_subject, withAppeal ? "com.atproto.moderation.defs#reasonAppeal" : "tools.ozone.report.defs#reasonMisleadingSpam"),
            active.Revision,
            token));
        LabelModerationCase current = Success(await cases.GetCaseAsync(caller, report.CaseId, token));
        current = Success(await cases.ChangeCaseAsync(
            caller: caller,
            change: new(
                operationId: "acknowledge-escalate",
                subject: s_subject,
                action: LabelCaseAction.Escalate,
                reason: "Request a second review.",
                caseId: current.Id,
                expectedRevision: current.Revision),
            cancellationToken: token)).Case;
        LabelCaseDecision decision = new(
            operationId: "acknowledge-without-labels",
            caseId: current.Id,
            expectedRevision: current.Revision,
            policyVersion: "v1",
            reason: "Reviewed; no label changes needed.",
            actions: [],
            handleOtherReports: true);
        LabelCaseDecisionReceipt receipt = Success(await service.ExecuteCaseDecisionAsync(caller, decision, token));
        Assert.Equal(withAppeal ? LabelCaseStatus.InReview : LabelCaseStatus.Resolved, receipt.CaseEvent.Case.Status);
        Assert.Empty(receipt.Labels);
        Assert.Equal(decision.OperationId, Success(await reports.GetReportAsync(caller, report.Id, token)).DecisionOperationId);
        IReadOnlyList<LabelAppeal> retained = Success(await appeals.GetAppealsAsync(caller, caseId: current.Id, cancellationToken: token));
        if (withAppeal)
        {
            Assert.Equal(LabelAppealStatus.Pending, Assert.Single(retained).Status);
        }
        else
        {
            Assert.Empty(retained);
        }
        Assert.Equal(issued.Label!.Sequence, Success(await fixture.Store.ReadDecisionAsync(target, "spam", token)).CurrentLabel!.Sequence);
        Success(await service.ExecuteCaseDecisionAsync(caller, decision, token));
        Assert.Equal(receipt.CaseEvent.Case.Revision, Success(await cases.GetCaseAsync(caller, current.Id, token)).Revision);
    }

    [Theory]
    [InlineData("sqlite", false, false)]
    [InlineData("sqlite", true, false)]
    [InlineData("sqlite", false, true)]
    [InlineData("sqlite", true, true)]
    [InlineData("postgresql", false, false)]
    [InlineData("postgresql", true, false)]
    [InlineData("postgresql", false, true)]
    [InlineData("postgresql", true, true)]
    [InlineData("mysql", false, false)]
    [InlineData("mysql", true, false)]
    [InlineData("mysql", false, true)]
    [InlineData("mysql", true, true)]
    public async Task DecisionsHandleOnlyTheExplicitReportScopeAndRecoverAfterRestart(string engine, bool handleOthers, bool acrossVersions)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        Success(await ((ILabelAdministrationStore)fixture.Store).ChangeTeamAsync(
            caller: caller,
            change: new(
                operationId: "report-second-moderator",
                action: LabelTeamAction.Enroll,
                target: new("did:plc:report-second-moderator"),
                reason: "Keep report cases unassigned before review.",
                role: LabelTeamRole.Moderator),
            cancellationToken: token));
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelIssuanceService service = Service(fixture, caller, active, key, http);
        ILabelReportStore reports = (ILabelReportStore)fixture.Store;
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        AtUri uri = new($"at://{s_subject.Repository}/app.bsky.feed.post/example");
        LabelReportSubject target = LabelReportSubject.Record(uri, new(TargetResolutionHandler.RecordCid));
        LabelReportSubject otherVersion = LabelReportSubject.Record(uri, new("bafyreif2pall7yry6b3sh5rttgeczzmhnnemy6rtbwwsvxeceebzxtpfwu"));
        List<LabelModerationReport> accepted = [];
        for (int index = 0; index < 130; index++)
        {
            accepted.Add(await SubmitAsync(target, "matching-" + index));
        }

        LabelModerationReport differentCid = await SubmitAsync(otherVersion, "other-cid");
        LabelModerationReport uriOnly = await SubmitAsync(LabelReportSubject.Record(uri), "no-cid");
        LabelModerationReport account = await SubmitAsync(s_subject, "account");
        LabelModerationReport otherRecord = await SubmitAsync(
            LabelReportSubject.Record(new(uri + "-different"), target.Cid!), "other-record");
        LabelModerationCase otherCase = Success(await cases.GetCaseAsync(caller, differentCid.CaseId, token));
        LabelModerationCase current = Success(await cases.GetCaseAsync(caller, accepted[0].CaseId, token));
        Assert.Null(current.Assignee);
        Assert.Null(otherCase.Assignee);
        LabelCaseDecision decision = new(
            operationId: "handle-reports",
            caseId: current.Id,
            expectedRevision: current.Revision,
            policyVersion: "v1",
            reason: "Review a selected submission rather than the oldest report.",
            actions: [],
            reportId: accepted[1].Id,
            handleOtherReports: handleOthers,
            handleReportsAcrossRecordVersions: acrossVersions);
        LabelCaseDecisionReceipt receipt = Success(await service.ExecuteCaseDecisionAsync(caller, decision, token));
        Assert.Equal(caller.Actor, receipt.CaseEvent.Case.Assignee);
        bool handleMatching = handleOthers || acrossVersions;
        Assert.Equal(handleMatching ? LabelCaseStatus.Resolved : LabelCaseStatus.InReview, receipt.CaseEvent.Case.Status);
        ILabelReportStore restarted = (ILabelReportStore)fixture.NewStore();
        foreach (LabelModerationReport original in accepted)
        {
            LabelModerationReport reread = Success(await restarted.GetReportAsync(caller, original.Id, token));
            bool handled = handleMatching || original.Id == accepted[1].Id;
            Assert.Equal(handled ? decision.OperationId : null, reread.DecisionOperationId);
            Assert.Equal(handled ? receipt.CaseEvent.CreatedAt : (DateTimeOffset?)null, reread.HandledAt);
            Assert.Equal(original.CreatedAt, reread.CreatedAt);
            Assert.Equal(original.ReportedBy, reread.ReportedBy);
            Assert.Equal(original.Submission.Reason, reread.Submission.Reason);
        }

        Assert.Equal(acrossVersions ? decision.OperationId : null,
            Success(await restarted.GetReportAsync(caller, differentCid.Id, token)).DecisionOperationId);
        LabelModerationReport uriOnlyAfter = Success(await restarted.GetReportAsync(caller, uriOnly.Id, token));
        Assert.Null(uriOnlyAfter.Submission.Subject.Cid);
        Assert.Equal(uri, uriOnlyAfter.Submission.Subject.RecordUri);
        Assert.Equal(acrossVersions ? decision.OperationId : null, uriOnlyAfter.DecisionOperationId);
        LabelModerationCase otherAfter = Success(await cases.GetCaseAsync(caller, differentCid.CaseId, token));
        Assert.Equal(acrossVersions ? caller.Actor : null, otherAfter.Assignee);
        Assert.Equal(otherCase.Revision + (acrossVersions ? 1 : 0), otherAfter.Revision);
        Assert.Equal(acrossVersions ? LabelCaseStatus.Resolved : otherCase.Status, otherAfter.Status);
        foreach (long id in new[] { account.Id, otherRecord.Id })
        {
            Assert.Null(Success(await restarted.GetReportAsync(caller, id, token)).DecisionOperationId);
        }

        ILabelTableStore tables = (ILabelTableStore)fixture.NewStore();
        foreach ((LabelCaseWorkflow workflow, int expected) in new[]
        {
            (LabelCaseWorkflow.Unresolved, acrossVersions ? 2 : handleOthers ? 4 : 133),
            (LabelCaseWorkflow.InReview, handleMatching ? 0 : 129),
            (LabelCaseWorkflow.Resolved, acrossVersions ? 132 : handleOthers ? 130 : 1),
            (LabelCaseWorkflow.All, 134)
        })
        {
            LabelTablePage workflowPage = Success(await tables.ReadTableAsync(
                caller: caller,
                query: new(Kind: LabelTableKind.Reports, Workflow: workflow, Page: 999),
                cancellationToken: token));
            Assert.Equal(expected, workflowPage.Total);
            Assert.Equal(Math.Max(1, (expected + 15) / 16), workflowPage.Page);
        }
        LabelNavigationCounts counts = Success(await tables.ReadNavigationCountsAsync(caller, token));
        int remaining = acrossVersions ? 2 : handleOthers ? 4 : 133;
        Assert.Equal(remaining, counts.Reports);
        Assert.Equal(acrossVersions ? 2 : handleOthers ? 4 : 5, counts.Cases);
        LabelTablePage sorted = Success(await tables.ReadTableAsync(
            caller, new(Kind: LabelTableKind.Reports, Sort: LabelTableSort.Status, Ascending: true), token));
        Assert.All(sorted.Reports!.Take(Math.Min(remaining, 16)), report => Assert.Null(report.DecisionOperationId));
        Assert.All(sorted.Reports!.Skip(Math.Min(remaining, 16)), report => Assert.Equal(decision.OperationId, report.DecisionOperationId));
        LabelCaseDecisionReceipt recovered = Success(await service.ExecuteCaseDecisionAsync(caller, decision, token));
        Assert.Equal(receipt.CaseEvent.Sequence, recovered.CaseEvent.Sequence);
        Assert.Equal(counts, Success(await tables.ReadNavigationCountsAsync(caller, token)));
        LabelModerationReport later = await SubmitAsync(target, "arrived-after-decision");
        Success(await service.ExecuteCaseDecisionAsync(caller, decision, token));
        Assert.Null(Success(await reports.GetReportAsync(caller, later.Id, token)).DecisionOperationId);
        Assert.Equal(LabelServiceErrorCode.IdempotencyConflict, (await service.ExecuteCaseDecisionAsync(
            caller,
            new(
                operationId: decision.OperationId,
                caseId: current.Id,
                expectedRevision: current.Revision,
                policyVersion: "v1",
                reason: decision.Reason,
                actions: [],
                reportId: accepted[1].Id,
                handleOtherReports: handleOthers,
                handleReportsAcrossRecordVersions: !acrossVersions),
            token)).Error!.Code);

        async Task<LabelModerationReport> SubmitAsync(LabelReportSubject subject, string identity)
        {
            ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(
                new("did:plc:reporter"), active.DatabaseTime, identity);
            return Success(await reports.SubmitReportAsync(
                authentication,
                new(subject, "tools.ozone.report.defs#reasonMisleadingSpam", identity),
                active.Revision,
                token));
        }
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task UriOnlyCasesHandleTheSameSubmittedTargetUnlessAllRecordReportsAreExplicitlySelected(string engine, bool allRecords)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelIssuanceService service = Service(fixture, caller, active, key, http);
        ILabelReportStore reports = (ILabelReportStore)fixture.Store;
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        LabelReportSubject subject = LabelReportSubject.Record(new($"at://{s_subject.Repository}/app.bsky.feed.post/example"));
        LabelModerationReport first = await SubmitAsync(subject, "uri-first");
        LabelModerationReport second = await SubmitAsync(subject, "uri-second");
        LabelModerationReport pinned = await SubmitAsync(LabelReportSubject.Record(subject.RecordUri!, new(TargetResolutionHandler.RecordCid)), "pinned");
        LabelModerationReport account = await SubmitAsync(s_subject, "account");
        Assert.Equal(first.CaseId, second.CaseId);
        Assert.NotEqual(first.CaseId, pinned.CaseId);
        Assert.NotEqual(first.CaseId, account.CaseId);
        LabelModerationCase current = Success(await cases.GetCaseAsync(caller, first.CaseId, token));
        LabelCaseDecision decision = new(
            operationId: "uri-only-review",
            caseId: current.Id,
            expectedRevision: current.Revision,
            policyVersion: "v1",
            reason: "Review reports exactly as submitted.",
            actions: [],
            reportId: first.Id,
            handleOtherReports: !allRecords,
            handleReportsAcrossRecordVersions: allRecords);
        Success(await service.ExecuteCaseDecisionAsync(caller, decision, token));
        ILabelModerationStore restartedCases = (ILabelModerationStore)fixture.NewStore();
        ILabelReportStore restartedReports = (ILabelReportStore)fixture.NewStore();
        Assert.Null(Success(await restartedCases.GetCaseAsync(caller, current.Id, token)).Subject.Cid);
        Assert.Equal(subject.RecordUri, Success(await restartedCases.GetCaseAsync(caller, current.Id, token)).Subject.RecordUri);
        foreach (long id in new[] { first.Id, second.Id })
        {
            LabelModerationReport saved = Success(await restartedReports.GetReportAsync(caller, id, token));
            Assert.Null(saved.Submission.Subject.Cid);
            Assert.Equal(subject.RecordUri, saved.Submission.Subject.RecordUri);
            Assert.Equal(decision.OperationId, saved.DecisionOperationId);
        }

        Assert.Equal(allRecords ? decision.OperationId : null, Success(await restartedReports.GetReportAsync(caller, pinned.Id, token)).DecisionOperationId);
        Assert.Null(Success(await restartedReports.GetReportAsync(caller, account.Id, token)).DecisionOperationId);
        LabelCasePage uriQueue = Success(await restartedCases.GetCaseQueueAsync(
            caller: caller,
            query: new(queue: LabelCaseWorkflow.All, subject: subject),
            cancellationToken: token));
        Assert.Equal(current.Id, Assert.Single(uriQueue.Cases).Id);
        LabelSearchPage search = Success(await ((ILabelSearchStore)fixture.NewStore()).SearchAsync(caller, "uri-second", 1, token));
        Assert.Contains(search.Hits, hit => hit.Report?.Id == second.Id && hit.Report.Submission.Subject.Cid is null);
        Assert.All(Success(await restartedCases.GetCaseHistoryAsync(caller, current.Id, cancellationToken: token)),
            entry => Assert.Null(entry.Case.Subject.Cid));

        async Task<LabelModerationReport> SubmitAsync(LabelReportSubject target, string identity)
        {
            ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(s_subject.Repository, active.DatabaseTime, identity);
            return Success(await reports.SubmitReportAsync(
                authentication, new(target, "tools.ozone.report.defs#reasonMisleadingSpam", identity), active.Revision, token));
        }
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task AcrossVersionReportHandlingPreservesOtherPendingAppealsAndRejectsAccounts(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelIssuanceService service = Service(fixture, caller, active, key, http);
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        ILabelReportStore reports = (ILabelReportStore)fixture.Store;
        LabelModerationCase account = await OpenAsync(fixture.Store, caller);
        LabelCaseDecision invalid = new(
            operationId: "invalid-all-versions",
            caseId: account.Id,
            expectedRevision: account.Revision,
            policyVersion: "v1",
            reason: "An account has no record versions.",
            actions: [],
            handleReportsAcrossRecordVersions: true);
        Assert.Equal(LabelServiceErrorCode.InvalidTarget, (await service.ExecuteCaseDecisionAsync(caller, invalid, token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.InvalidTarget, (await cases.CommitCaseDecisionAsync(new(caller, invalid, active.Revision, []), token)).Error!.Code);
        AtUri uri = new($"at://{s_subject.Repository}/app.bsky.feed.post/example");
        LabelReportSubject first = LabelReportSubject.Record(uri, new(TargetResolutionHandler.RecordCid));
        LabelReportSubject second = LabelReportSubject.Record(uri, new("bafyreif2pall7yry6b3sh5rttgeczzmhnnemy6rtbwwsvxeceebzxtpfwu"));
        LabelModerationReport report = await SubmitAsync(first, "first", "tools.ozone.report.defs#reasonMisleadingSpam");
        LabelModerationReport appealReport = await SubmitAsync(second, "appeal", "com.atproto.moderation.defs#reasonAppeal");
        ILabelAppealStore appeals = (ILabelAppealStore)fixture.Store;
        LabelAppeal appeal = Assert.Single(Success(await appeals.GetAppealsAsync(caller, cancellationToken: token)));
        LabelModerationCase current = Success(await cases.GetCaseAsync(caller, report.CaseId, token));
        LabelCaseDecision decision = new(
            operationId: "handle-all-versions",
            caseId: current.Id,
            expectedRevision: current.Revision,
            policyVersion: "v1",
            reason: "Handle reports without resolving appeals on another version.",
            actions: [],
            handleReportsAcrossRecordVersions: true);
        Success(await service.ExecuteCaseDecisionAsync(caller, decision, token));
        Assert.Equal(decision.OperationId, Success(await reports.GetReportAsync(caller, appealReport.Id, token)).DecisionOperationId);
        Assert.Equal(LabelAppealStatus.Pending, Success(await appeals.GetAppealAsync(caller, appeal.Id, token)).Status);
        Assert.Equal(LabelCaseStatus.InReview, Success(await cases.GetCaseAsync(caller, appeal.CaseId, token)).Status);
        Assert.Contains(Success(await cases.GetCaseHistoryAsync(caller, appeal.CaseId, cancellationToken: token)),
            entry => entry.OperationId == decision.OperationId && entry.Reason.Contains(decision.Reason, StringComparison.Ordinal));

        async Task<LabelModerationReport> SubmitAsync(LabelReportSubject subject, string identity, string reason)
        {
            ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(
                subject.Repository, active.DatabaseTime, identity);
            return Success(await reports.SubmitReportAsync(authentication, new(subject, reason), active.Revision, token));
        }
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task ZeroSequenceRequiresAbsentLabelHistoryRatherThanOverwritingAManualDecision(string engine, bool manualFirst)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelIssuanceService service = Service(fixture, caller, active, key, http);
        LabelModerationCase current = await OpenAsync(fixture.Store, caller);
        if (manualFirst)
        {
            Success(await service.ExecuteAsync(LabelOperationRequest.Issue(
                operationId: "concurrent-manual",
                target: LabelTarget.Account(s_subject.Repository),
                value: "spam",
                context: new(caller.Actor, "Concurrent account label.")), token));
        }

        LabelServiceResult<LabelCaseDecisionReceipt> result = await service.ExecuteCaseDecisionAsync(
            caller, Decision(current, [new(LabelOperationKind.Issue, "spam", expectedLabelSequence: 0)]), token);
        if (manualFirst)
        {
            Assert.Equal(LabelServiceErrorCode.StaleDecision, result.Error!.Code);
            Assert.Equal(current.Revision, Success(await ((ILabelModerationStore)fixture.Store).GetCaseAsync(caller, current.Id, token)).Revision);
        }
        else
        {
            Assert.Single(Success(result).Labels);
        }

        Assert.Single(Success(await ((ILabelTableStore)fixture.Store).ReadSubjectLabelsAsync(caller, s_subject, token)));
    }

    [Theory]
    [InlineData("sqlite", 0)]
    [InlineData("sqlite", 2)]
    [InlineData("postgresql", 0)]
    [InlineData("postgresql", 2)]
    [InlineData("mysql", 0)]
    [InlineData("mysql", 2)]
    public async Task AtomicDecisionsResolveCasesAndRecoverExactOutcomesBeforeNetworkAndPolicy(string engine, int count)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        TargetResolutionHandler handler = new();
        using HttpClient http = new(handler);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        TestLabelIssuancePolicyProvider policy = new(active.Policy!.Issuance);
        LabelIssuanceService service = Service(fixture, caller, active, key, http, policy);
        LabelModerationCase moderationCase = await OpenAsync(fixture.Store, caller);
        LabelCaseDecision request = Decision(moderationCase, count == 0 ? [] : [new(LabelOperationKind.Issue, "spam"), new(LabelOperationKind.Issue, "scam")]);
        LabelCaseDecisionReceipt receipt = Success(await service.ExecuteCaseDecisionAsync(caller, request, token));
        Assert.Equal(count, receipt.Labels.Count);
        Assert.Equal(count, receipt.StateAtCommit.IssuedThrough);
        Assert.Equal(LabelCaseStatus.Resolved, receipt.CaseEvent.Case.Status);
        Assert.Equal(moderationCase.Revision + 1, receipt.CaseEvent.Case.Revision);
        Assert.Equal(LabelCaseAction.Decide, receipt.CaseEvent.Action);
        Assert.All(receipt.Labels, label => Assert.True(label.Changed));
        policy.Unavailable = true;
        handler.Offline = true;
        LabelCaseDecisionReceipt recovered = Success(await service.ExecuteCaseDecisionAsync(caller, request, token));
        Assert.Equal(receipt.StateAtCommit, recovered.StateAtCommit);
        Assert.Equal(receipt.CaseEvent.Sequence, recovered.CaseEvent.Sequence);
        Assert.Equal(receipt.Labels.Select(label => label.Label!.SignedLabel.GetJsonBytes()),
            recovered.Labels.Select(label => label.Label!.SignedLabel.GetJsonBytes()));
        LabelCaseDecisionReceipt restarted = Success(await ((ILabelModerationStore)fixture.NewStore()).GetCaseDecisionAsync(
            caller, request.OperationId, token));
        Assert.Equal(receipt.Labels.Select(label => label.Label!.SignedLabel.GetJsonBytes()),
            restarted.Labels.Select(label => label.Label!.SignedLabel.GetJsonBytes()));
        Assert.Equal(LabelServiceErrorCode.IdempotencyConflict, (await service.ExecuteCaseDecisionAsync(
            caller, Decision(moderationCase, request.Actions, reason: "Changed reason."), token)).Error!.Code);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task LastActionFailureCannotCommitEarlierLabelsOrResolveTheCase(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelIssuanceService service = Service(fixture, caller, active, key, http);
        LabelModerationCase moderationCase = await OpenAsync(fixture.Store, caller);
        LabelCaseDecision request = Decision(moderationCase, [new(LabelOperationKind.Issue, "spam"), new(LabelOperationKind.Issue, "scam", expectedLabelSequence: 42)]);
        LabelCaseDecisionCommit prepared = await PrepareAsync(service, caller, request, active.Revision);
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await ((ILabelModerationStore)fixture.Store).CommitCaseDecisionAsync(prepared, token)).Error!.Code);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        Assert.Equal(moderationCase.Revision, Success(await ((ILabelModerationStore)fixture.Store).GetCaseAsync(caller, moderationCase.Id, token)).Revision);
        Assert.Equal(LabelServiceErrorCode.OperationNotFound, (await fixture.Store.GetOperationAsync(request.ActionOperationId(0), token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.OperationNotFound, (await ((ILabelModerationStore)fixture.Store).GetCaseDecisionAsync(
            caller, request.OperationId, token)).Error!.Code);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ConcurrentProvidersCommitOnlyOneCompleteDecisionForTheCaseRevision(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelIssuanceService service = Service(fixture, caller, active, key, http);
        LabelModerationCase moderationCase = await OpenAsync(fixture.Store, caller);
        LabelCaseLabelAction[] actions = [new(LabelOperationKind.Issue, "spam"), new(LabelOperationKind.Issue, "scam")];
        LabelCaseDecisionCommit first = await PrepareAsync(service, caller, Decision(moderationCase, actions), active.Revision);
        LabelCaseDecisionCommit second = await PrepareAsync(service, caller, Decision(moderationCase, actions, operation: "other"), active.Revision);
        LabelServiceResult<LabelCaseDecisionReceipt>[] outcomes = await Task.WhenAll(
            ((ILabelModerationStore)fixture.Store).CommitCaseDecisionAsync(first, token),
            ((ILabelModerationStore)fixture.NewStore()).CommitCaseDecisionAsync(second, token));
        LabelCaseDecisionReceipt winner = Assert.Single(outcomes, result => result.Succeeded).Result!;
        Assert.Equal(LabelServiceErrorCode.CaseConflict, Assert.Single(outcomes, result => !result.Succeeded).Error!.Code);
        Assert.Equal(2, winner.Labels.Count);
        Assert.Equal(2, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task CommitFaultsPreserveAllOrNothingAndRecoverTheOriginalSignatures(string engine, bool afterCommit)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelIssuanceService service = Service(fixture, caller, active, key, http);
        LabelModerationCase moderationCase = await OpenAsync(fixture.Store, caller);
        LabelCaseDecision request = Decision(moderationCase, [new(LabelOperationKind.Issue, "spam"), new(LabelOperationKind.Issue, "scam")]);
        LabelCaseDecisionCommit prepared = await PrepareAsync(service, caller, request, active.Revision);
        if (afterCommit)
        {
            fixture.SetCommitHooks(afterCommit: (_, _) => throw new SqliteException("Lost acknowledgement.", 10));
        }
        else
        {
            fixture.SetCommitHooks(beforeCommit: (_, _, _) => throw new SqliteException("Not submitted.", 10));
        }

        LabelServiceResult<LabelCaseDecisionReceipt> result = await ((ILabelModerationStore)fixture.Store).CommitCaseDecisionAsync(prepared, token);
        Assert.Equal(afterCommit ? LabelServiceErrorCode.OutcomeUnknown : LabelServiceErrorCode.StorageUnavailable, result.Error!.Code);
        fixture.SetCommitHooks();
        ILabelStore restarted = fixture.NewStore();
        Assert.Equal(afterCommit ? 2 : 0, (await restarted.GetStateAsync(token)).IssuedThrough);
        LabelServiceResult<LabelCaseDecisionReceipt> recovered = await ((ILabelModerationStore)restarted).GetCaseDecisionAsync(caller, request.OperationId, token);
        if (afterCommit)
        {
            Assert.Equal(prepared.PreparedActions.Select(action => action.SignedLabel!.GetJsonBytes()),
                Success(recovered).Labels.Select(label => label.Label!.SignedLabel.GetJsonBytes()));
        }
        else
        {
            Assert.Equal(LabelServiceErrorCode.OperationNotFound, recovered.Error!.Code);
            Assert.Equal(LabelCaseStatus.Open, Success(await ((ILabelModerationStore)restarted).GetCaseAsync(caller, moderationCase.Id, token)).Status);
        }
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ExactLabelSequencePreventsWithdrawalOfANewerManualOutcome(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelIssuanceService service = Service(fixture, caller, active, key, http);
        LabelOperationContext context = new(caller.Actor, "Manual case-independent labeling.");
        LabelOperationReceipt original = Success(await service.ExecuteAsync(LabelOperationRequest.Issue(
            operationId: "manual", target: LabelTarget.Account(s_subject.Repository), value: "spam", context: context), token));
        LabelOperationReceipt replacement = Success(await service.ExecuteAsync(LabelOperationRequest.Issue(
            operationId: "replacement", target: LabelTarget.Account(s_subject.Repository), value: "spam", context: context,
            expiresAt: active.DatabaseTime.AddDays(1)), token));
        Assert.True(replacement.Changed);
        LabelModerationCase moderationCase = await OpenAsync(fixture.Store, caller);
        LabelCaseDecision request = Decision(moderationCase, [new(LabelOperationKind.Negate, "spam", expectedLabelSequence: original.Label!.Sequence)]);
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await service.ExecuteCaseDecisionAsync(caller, request, token)).Error!.Code);
        Assert.Equal(replacement.Label!.Sequence, Success(await fixture.Store.ReadDecisionAsync(
            LabelTarget.Account(s_subject.Repository), "spam", token)).CurrentLabel!.Sequence);
        Assert.Equal(LabelCaseStatus.Open, Success(await ((ILabelModerationStore)fixture.Store).GetCaseAsync(caller, moderationCase.Id, token)).Status);
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task CommitRechecksPolicyAndLiveModeratorAuthorityAfterPreparation(string engine, bool revoke)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelAdministrationCaller administrator = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, administrator, token);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Did member = new("did:plc:moderator");
        Success(await team.ChangeTeamAsync(
            administrator,
            new(operationId: "enroll", action: LabelTeamAction.Enroll, target: member, reason: "Enroll moderator.", role: LabelTeamRole.Moderator),
            token));
        Success(await team.ChangeTeamAsync(
            administrator,
            new(operationId: "bind", action: LabelTeamAction.BindLocal, target: member, reason: "Bind moderator.", credential: "uid:1"),
            token));
        LabelAdministrationCaller moderator = Success(await team.AuthenticateLocalAsync("uid:1", token));
        LabelIssuanceService service = Service(fixture, moderator, active, key, http);
        LabelModerationCase moderationCase = await OpenAsync(fixture.Store, moderator);
        LabelCaseDecision request = Decision(moderationCase, [new(LabelOperationKind.Issue, "spam"), new(LabelOperationKind.Issue, "scam")]);
        LabelCaseDecisionCommit prepared = await PrepareAsync(service, moderator, request, active.Revision);
        if (revoke)
        {
            Success(await team.ChangeTeamAsync(
                administrator,
                new(operationId: "suspend", action: LabelTeamAction.Suspend, target: member, reason: "Revoke pending authority."),
                token));
        }
        else
        {
            using JsonDocument absent = JsonDocument.Parse("null");
            Success(await ((ILabelPolicyStore)fixture.Store).ObservePolicyAsync(
                expectedRevision: active.Revision,
                verificationStartedAt: active.DatabaseTime,
                observedCid: null,
                declaration: absent.RootElement,
                cancellationToken: token));
        }

        Assert.Equal(revoke ? LabelServiceErrorCode.Unauthorized : LabelServiceErrorCode.PolicyUnavailable,
            (await ((ILabelModerationStore)fixture.Store).CommitCaseDecisionAsync(prepared, token)).Error!.Code);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        Assert.Equal(LabelCaseStatus.Open, Success(await ((ILabelModerationStore)fixture.Store).GetCaseAsync(
            administrator, moderationCase.Id, token)).Status);
        if (revoke)
        {
            Assert.Equal(LabelServiceErrorCode.Unauthorized, (await ((ILabelModerationStore)fixture.Store).GetCaseDecisionAsync(
                moderator, request.OperationId, token)).Error!.Code);
        }
    }

    [Fact]
    public void DecisionsDefensivelyCopyActionsAndRejectDuplicateLabelSlots()
    {
        LabelCaseLabelAction[] actions = [new(LabelOperationKind.Issue, "spam")];
        LabelCaseDecision decision = new(
            operationId: "decision", caseId: 1, expectedRevision: 1, policyVersion: "v1", reason: "Review.", actions: actions);
        actions[0] = new(LabelOperationKind.Issue, "scam");
        Assert.Equal("spam", Assert.Single(decision.Actions).Value);
        Assert.Throws<ArgumentException>(() => new LabelCaseDecision(
            operationId: "decision", caseId: 1, expectedRevision: 1, policyVersion: "v1", reason: "Review.",
            actions: [new(LabelOperationKind.Issue, "spam"), new(LabelOperationKind.Negate, "spam")]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LabelCaseDecision(
            operationId: "decision", caseId: 1, expectedRevision: 1, policyVersion: "v1", reason: "Review.",
            actions: [.. Enumerable.Range(0, 17).Select(index => new LabelCaseLabelAction(LabelOperationKind.Issue, "value-" + index))]));
    }

    [Fact]
    public void AppealSelectionsAreSortedDefensiveAndBoundedWithExplicitOutcomes()
    {
        long[] ids = [16, 1, 2];
        LabelCaseDecision decision = new(
            operationId: "review", caseId: 1, expectedRevision: 1, policyVersion: "v1",
            reason: "Explicit review.", actions: [], appealIds: ids, appealOutcome: LabelAppealStatus.Upheld);
        ids[0] = 99;
        Assert.Equal([1, 2, 16], decision.AppealIds);
        Assert.Throws<ArgumentOutOfRangeException>(() => new LabelCaseDecision(
            operationId: "review", caseId: 1, expectedRevision: 1, policyVersion: "v1",
            reason: "Explicit review.", actions: [],
            appealIds: [.. Enumerable.Range(1, 17).Select(id => (long)id)], appealOutcome: LabelAppealStatus.Upheld));
    }

    [Theory]
    [InlineData(LabelOperationKind.Negate)]
    [InlineData(LabelOperationKind.Issue)]
    public void ReplacementAppealsAcceptPartialRemovalOrAddition(LabelOperationKind kind)
    {
        LabelCaseDecision decision = new(
            operationId: "replace-selection",
            caseId: 1,
            expectedRevision: 1,
            policyVersion: "v1",
            reason: "Change the current label selection.",
            actions: [new(kind, "spam")],
            appealIds: [1],
            appealOutcome: LabelAppealStatus.Replaced);
        Assert.Equal(kind, Assert.Single(decision.Actions).Kind);
        Assert.Equal(LabelAppealStatus.Replaced, decision.AppealOutcome);
    }

    [Theory]
    [InlineData(new long[] { 0 }, LabelAppealStatus.Upheld)]
    [InlineData(new long[] { -1 }, LabelAppealStatus.Upheld)]
    [InlineData(new long[] { 1, 1 }, LabelAppealStatus.Upheld)]
    [InlineData(new long[] { 1 }, null)]
    [InlineData(new long[] { }, LabelAppealStatus.Upheld)]
    [InlineData(new long[] { 1 }, LabelAppealStatus.Pending)]
    [InlineData(new long[] { 1 }, (LabelAppealStatus)99)]
    [InlineData(new long[] { 1 }, LabelAppealStatus.Withdrawn)]
    [InlineData(new long[] { 1 }, LabelAppealStatus.Replaced)]
    public void InvalidAppealSelectionsAndActionlessCorrectionsAreRejected(long[] ids, LabelAppealStatus? outcome)
    {
        Assert.Throws<ArgumentException>(() => new LabelCaseDecision(
            operationId: "review", caseId: 1, expectedRevision: 1, policyVersion: "v1",
            reason: "Explicit review.", actions: [], appealIds: ids, appealOutcome: outcome));
    }

    /// <summary>Builds the production issuance service with a fixed fake clock and live shared team authority.</summary>
    private static LabelIssuanceService Service(
        StoreFixture fixture,
        LabelAdministrationCaller caller,
        LabelPolicyState active,
        LabelSigningKey key,
        HttpClient http,
        TestLabelIssuancePolicyProvider? policy = null) => new(
            issuer: StoreFixture.s_issuer,
            signingKey: key,
            store: fixture.Store,
            authorizer: new LabelTeamAuthorizer((ILabelAdministrationStore)fixture.Store, caller),
            policyProvider: policy ?? new(active.Policy!.Issuance),
            httpClient: http,
            timeProvider: fixture.DatabaseClock!);

    /// <summary>Opens an independently reviewable account case without requiring a report.</summary>
    private static async Task<LabelModerationCase> OpenAsync(ILabelStore store, LabelAdministrationCaller caller) =>
        Success(await ((ILabelModerationStore)store).ChangeCaseAsync(
            caller,
            new(operationId: "open", subject: s_subject, action: LabelCaseAction.Open, reason: "Review the exact subject."),
            TestContext.Current.CancellationToken)).Case;

    /// <summary>Builds stable case intent independently of signing and policy observations.</summary>
    private static LabelCaseDecision Decision(
        LabelModerationCase moderationCase,
        IReadOnlyCollection<LabelCaseLabelAction> actions,
        string operation = "decision",
        string reason = "Reviewed evidence.") => new(
            operationId: operation,
            caseId: moderationCase.Id,
            expectedRevision: moderationCase.Revision,
            policyVersion: "v1",
            reason: reason,
            actions: actions);

    /// <summary>Prepares ordered actions without committing any of them, enabling precise provider race and fault tests.</summary>
    private static async Task<LabelCaseDecisionCommit> PrepareAsync(
        LabelIssuanceService service,
        LabelAdministrationCaller caller,
        LabelCaseDecision decision,
        long policyRevision)
    {
        List<LabelDecisionCommit> actions = [];
        for (int index = 0; index < decision.Actions.Count; index++)
        {
            LabelCaseLabelAction action = decision.Actions[index];
            LabelOperationContext context = new(caller.Actor, decision.Reason);
            LabelOperationRequest request = action.Kind == LabelOperationKind.Issue
                ? LabelOperationRequest.Issue(
                    operationId: decision.ActionOperationId(index), target: LabelTarget.Account(s_subject.Repository),
                    value: action.Value, context: context, expiresAt: action.ExpiresAt)
                : LabelOperationRequest.Negate(
                    operationId: decision.ActionOperationId(index), target: LabelTarget.Account(s_subject.Repository), value: action.Value, context: context);
            actions.Add(Success(await service.PrepareDecisionAsync(request, TestContext.Current.CancellationToken)));
        }

        return new(caller, decision, policyRevision, actions);
    }

    /// <summary>Requires an explicit successful outcome while retaining the provider failure detail.</summary>
    private static T Success<T>(LabelServiceResult<T> result) where T : class
    {
        Assert.True(result.Succeeded, result.Error?.Message);
        return result.Result;
    }
}
