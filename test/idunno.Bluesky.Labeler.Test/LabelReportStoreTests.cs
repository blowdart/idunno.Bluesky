// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.Data.Sqlite;

namespace idunno.Bluesky.Labeler.Test;

public class LabelReportStoreTests
{
    private static readonly Did s_reporter = new("did:plc:reporter");
    private static readonly Did s_administrator = new("did:plc:administrator");
    private static readonly LabelReportSubject s_account = LabelReportSubject.Account(new("did:plc:subject"));
    private const string Reason = "tools.ozone.report.defs#reasonMisleadingSpam";

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task CasePageAuthorSummariesCountAllReportsButDeduplicateRecordUrisAcrossCids(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller administrator, LabelPolicyState policy) = await ActivateAsync(fixture);
        ILabelReportStore reports = (ILabelReportStore)fixture.Store;
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        AtUri uri = new($"at://{s_account.Repository}/app.bsky.actor.profile/self");
        LabelReportSubject[] subjects =
        [
            s_account,
            LabelReportSubject.Record(uri, new(TargetResolutionHandler.RecordCid)),
            LabelReportSubject.Record(uri, new("bafyreif2pall7yry6b3sh5rttgeczzmhnnemy6rtbwwsvxeceebzxtpfwu")),
            LabelReportSubject.Record(uri, new(TargetResolutionHandler.RecordCid)),
            LabelReportSubject.Record(new($"at://{s_account.Repository}/app.bsky.feed.post/example"), new(TargetResolutionHandler.RecordCid)),
            LabelReportSubject.Record(new($"at://{s_account.Repository}/app.bsky.graph.starterpack/example"), new(TargetResolutionHandler.RecordCid)),
            LabelReportSubject.Record(new($"at://{s_account.Repository}/app.bsky.graph.list/example"), new(TargetResolutionHandler.RecordCid)),
            LabelReportSubject.Record(new($"at://{s_account.Repository}/app.bsky.feed.generator/example"), new(TargetResolutionHandler.RecordCid)),
            LabelReportSubject.Record(new($"at://{s_account.Repository}/example.custom.record/example"), new(TargetResolutionHandler.RecordCid)),
            LabelReportSubject.Account(new("did:plc:subject-other"))
        ];
        long closedCaseId = 0;
        for (int index = 0; index < subjects.Length; index++)
        {
            ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(
                s_reporter, policy.DatabaseTime, "summary-" + index);
            LabelModerationReport report = Success(await reports.SubmitReportAsync(
                authentication, new(subjects[index], Reason), policy.Revision, token));
            if (index == 0)
            {
                closedCaseId = report.CaseId;
            }
        }

        LabelModerationCase current = Success(await cases.GetCaseAsync(administrator, closedCaseId, token));
        Success(await cases.CommitCaseDecisionAsync(
            new(
                caller: administrator,
                request: new(
                    operationId: "resolve-account",
                    caseId: current.Id,
                    expectedRevision: current.Revision,
                    policyVersion: policy.Policy!.Issuance.Version,
                    reason: "Retain handled reports in all-time summary.",
                    actions: [],
                    handleOtherReports: true),
                expectedPolicyRevision: policy.Revision,
                preparedActions: []),
            token));
        ILabelTableStore table = Assert.IsType<ILabelTableStore>(fixture.NewStore(), exactMatch: false);
        LabelTablePage page = Success(await table.ReadTableAsync(
            administrator, new(LabelTableKind.Cases), token));
        LabelAuthorReportSummary summary = Assert.Single(page.AuthorSummaries!, item => item.Did == s_account.Repository.Value);
        Assert.Equal(9, summary.Reports);
        Assert.Equal(6, summary.Records);
        Assert.Equal(3, summary.ReportKinds["profile"]);
        Assert.Equal(1, summary.ReportKinds["post"]);
        Assert.Equal(1, summary.ReportKinds["starter pack"]);
        Assert.Equal(1, summary.ReportKinds["list"]);
        Assert.Equal(1, summary.ReportKinds["feed"]);
        Assert.Equal(1, summary.ReportKinds["other record"]);
        LabelAuthorReportSummary other = Assert.Single(page.AuthorSummaries!, item => item.Did == "did:plc:subject-other");
        Assert.Equal(1, other.Reports);
        Assert.Equal(0, other.Records);
        Assert.Equal(1, other.ReportKinds["profile"]);
        Assert.Equal(0, other.ReportKinds["post"]);
        Assert.DoesNotContain(page.Cases!, item => item.Id == closedCaseId);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Success(await team.ChangeTeamAsync(
            administrator,
            new(
                operationId: "summary-viewer",
                action: LabelTeamAction.Enroll,
                target: s_reporter,
                reason: "Verify live summary access.",
                role: LabelTeamRole.Viewer),
            token));
        LabelAdministrationCaller viewer = Success(await team.AuthenticateBrowserAsync(s_reporter, token));
        Success(await team.ChangeTeamAsync(
            administrator,
            new(operationId: "summary-revoke", action: LabelTeamAction.Suspend, target: s_reporter, reason: "Revoke summary inspection."),
            token));
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await table.ReadTableAsync(viewer, new(LabelTableKind.Cases), token)).Error!.Code);
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task IndependentReporterNeedsNoTeamMembershipAndReplayRemainsRejectedAfterRestart(string engine, bool legacy)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller administrator, LabelPolicyState policy) = await ActivateAsync(fixture);
        ILabelReportStore store = (ILabelReportStore)fixture.Store;
        ModerationReportAuthenticationResult reporter = await ReportTestAuthentication.CreateAsync(
            reporter: s_reporter, databaseTime: policy.DatabaseTime, tokenId: "one", legacy: legacy);
        using JsonDocument metadata = JsonDocument.Parse("""{"platform":"test","nested":{"retained":true}}""");
        LabelReportSubmission submission = new(
            subject: s_account, reasonType: Reason, reason: "Review confidential evidence.",
            modToolName: "test/tool", modToolMetadata: metadata.RootElement);
        LabelModerationReport accepted = Success(await store.SubmitReportAsync(reporter, submission, policy.Revision, token));
        Assert.Equal(s_reporter, accepted.ReportedBy);
        Assert.Equal(legacy ? StoreFixture.s_issuer.Value : StoreFixture.s_issuer.Value + "#atproto_labeler", accepted.Audience);
        Assert.Equal("v1", accepted.PolicyVersion);
        Assert.Equal(Reason, accepted.Submission.ReasonType);
        Assert.Equal(submission.Reason, accepted.Submission.Reason);
        Assert.Equal(metadata.RootElement.GetRawText(), accepted.Submission.ModToolMetadata!.Value.GetRawText());
        ILabelReportStore restarted = (ILabelReportStore)fixture.NewStore();
        Assert.Equal(LabelServiceErrorCode.Unauthorized,
            (await restarted.SubmitReportAsync(reporter, submission, policy.Revision, token)).Error!.Code);
        Assert.Single(Success(await restarted.GetReportsAsync(administrator, cancellationToken: token)));
        LabelModerationReport reread = Success(await restarted.GetReportAsync(administrator, accepted.Id, token));
        Assert.Equal(accepted.CreatedAt, reread.CreatedAt);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ConcurrentFreshTokensRetainEveryReportButShareOneExactSubjectCase(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller administrator, LabelPolicyState policy) = await ActivateAsync(fixture);
        ILabelReportStore first = (ILabelReportStore)fixture.Store;
        ILabelReportStore second = (ILabelReportStore)fixture.NewStore();
        ModerationReportAuthenticationResult one = await ReportTestAuthentication.CreateAsync(s_reporter, policy.DatabaseTime, "one");
        ModerationReportAuthenticationResult two = await ReportTestAuthentication.CreateAsync(s_reporter, policy.DatabaseTime, "two");
        LabelReportSubmission submission = new(s_account, Reason);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<LabelModerationReport> SubmitAsync(ILabelReportStore store, ModerationReportAuthenticationResult authentication)
        {
            await start.Task.WaitAsync(token);
            return Success(await store.SubmitReportAsync(authentication, submission, policy.Revision, token));
        }

        Task<LabelModerationReport> firstTask = SubmitAsync(first, one);
        Task<LabelModerationReport> secondTask = SubmitAsync(second, two);
        start.SetResult();
        LabelModerationReport[] reports = await Task.WhenAll(firstTask, secondTask);
        Assert.NotEqual(reports[0].Id, reports[1].Id);
        Assert.Equal(reports[0].CaseId, reports[1].CaseId);
        Assert.Equal(2, Success(await first.GetReportsAsync(administrator, cancellationToken: token)).Count);
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        Assert.Equal(2, Success(await cases.GetCaseAsync(administrator, reports[0].CaseId, token)).Revision);
        Assert.Equal(2, Success(await cases.GetCaseHistoryAsync(administrator, reports[0].CaseId, cancellationToken: token)).Count);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task AcceptanceNeverFetchesSubjectsAndPreservesDistinctRecordVersions(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller administrator, LabelPolicyState policy) = await ActivateAsync(fixture);
        ILabelReportStore store = (ILabelReportStore)fixture.Store;
        AtUri uri = new($"at://{s_account.Repository}/app.bsky.actor.profile/self");
        LabelReportSubject[] subjects =
        [
            s_account,
            LabelReportSubject.Record(uri, new(TargetResolutionHandler.RecordCid)),
            LabelReportSubject.Record(uri, new("bafyreif2pall7yry6b3sh5rttgeczzmhnnemy6rtbwwsvxeceebzxtpfwu"))
        ];
        List<long> ids = [];
        for (int index = 0; index < subjects.Length; index++)
        {
            ModerationReportAuthenticationResult reporter = await ReportTestAuthentication.CreateAsync(
                s_reporter, policy.DatabaseTime, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            LabelModerationReport report = Success(await store.SubmitReportAsync(reporter, new(subjects[index], Reason), policy.Revision, token));
            ids.Add(report.CaseId);
            Assert.Equal(subjects[index].Cid, report.Submission.Subject.Cid);
        }

        Assert.Equal(3, ids.Distinct().Count());
        IReadOnlyList<LabelModerationReport> page = Success(await store.GetReportsAsync(caller: administrator, limit: 1, cancellationToken: token));
        LabelModerationReport item = Assert.Single(page);
        Assert.Equal(2, Success(await store.GetReportsAsync(caller: administrator, afterId: item.Id, cancellationToken: token)).Count);
        Assert.Single(Success(await store.GetReportsAsync(caller: administrator, caseId: ids[1], cancellationToken: token)));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task WrongAudienceStalePolicyAndNonownerAppealsCannotCreateReportsOrCases(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller administrator, LabelPolicyState policy) = await ActivateAsync(fixture);
        ILabelReportStore store = (ILabelReportStore)fixture.Store;
        ModerationReportAuthenticationResult wrong = await ReportTestAuthentication.CreateAsync(
            reporter: s_reporter, databaseTime: policy.DatabaseTime, tokenId: "wrong", labeler: new("did:plc:other"));
        Assert.Equal(LabelServiceErrorCode.Unauthorized,
            (await store.SubmitReportAsync(wrong, new(s_account, Reason), policy.Revision, token)).Error!.Code);
        ModerationReportAuthenticationResult reporter = await ReportTestAuthentication.CreateAsync(s_reporter, policy.DatabaseTime, "valid");
        Assert.Equal(LabelServiceErrorCode.PolicyUnavailable,
            (await store.SubmitReportAsync(reporter, new(s_account, Reason), policy.Revision - 1, token)).Error!.Code);
        ILabelReportStore rolledBack = (ILabelReportStore)fixture.NewStore(new(policy.DatabaseTime.AddMilliseconds(-430)));
        Assert.Equal(LabelServiceErrorCode.PolicyUnavailable,
            (await rolledBack.SubmitReportAsync(reporter, new(s_account, Reason), policy.Revision, token)).Error!.Code);
        foreach (string appeal in new[] { "com.atproto.moderation.defs#reasonAppeal", "tools.ozone.report.defs#reasonAppeal" })
        {
            Assert.Equal(LabelServiceErrorCode.Unauthorized,
                (await store.SubmitReportAsync(reporter, new(s_account, appeal), policy.Revision, token)).Error!.Code);
        }

        Assert.Empty(Success(await store.GetReportsAsync(administrator, cancellationToken: token)));
        Assert.Empty(Success(await ((ILabelModerationStore)fixture.Store).GetCasesAsync(administrator, cancellationToken: token)));
        Assert.True((await store.SubmitReportAsync(reporter, new(s_account, Reason), policy.Revision, token)).Succeeded);
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task BeforeCommitFaultRollsBackReportCaseReplayAndAuditButLostAcknowledgementPreservesAll(string engine, bool disallowed)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        (LabelAdministrationCaller administrator, LabelPolicyState policy) = await ActivateAsync(
            fixture, disallowed ? ""","reasonTypes":["tools.ozone.report.defs#reasonOther"]""" : "");
        ILabelReportStore store = (ILabelReportStore)fixture.Store;
        ModerationReportAuthenticationResult reporter = await ReportTestAuthentication.CreateAsync(s_reporter, policy.DatabaseTime, "one");
        fixture.SetCommitHooks(beforeCommit: (_, _, _) => throw new SqliteException("Before commit.", 10));
        Assert.Equal(LabelServiceErrorCode.StorageUnavailable,
            (await store.SubmitReportAsync(reporter, new(s_account, Reason), policy.Revision, token)).Error!.Code);
        fixture.SetCommitHooks();
        Assert.Empty(Success(await store.GetReportsAsync(administrator, cancellationToken: token)));
        Assert.Empty(Success(await ((ILabelModerationStore)fixture.Store).GetCasesAsync(administrator, cancellationToken: token)));
        fixture.SetCommitHooks(afterCommit: (_, _) => throw new SqliteException("Lost acknowledgement.", 10));
        Assert.Equal(LabelServiceErrorCode.OutcomeUnknown,
            (await store.SubmitReportAsync(reporter, new(s_account, Reason), policy.Revision, token)).Error!.Code);
        fixture.SetCommitHooks();
        Assert.Single(Success(await store.GetReportsAsync(administrator, cancellationToken: token)));
        Assert.Equal(LabelServiceErrorCode.Unauthorized,
            (await store.SubmitReportAsync(reporter, new(s_account, Reason), policy.Revision, token)).Error!.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Context.")]
    public void OptionalReportContextRetainsValidEmptyAndWhitespaceStrings(string text)
    {
        Assert.Equal(text, new LabelReportSubmission(s_account, Reason, text).Reason);
        Assert.Throws<ArgumentOutOfRangeException>(() => new LabelReportSubmission(s_account, Reason, new string('x', 2001)));
        Assert.Throws<ArgumentException>(() => new LabelReportSubmission(s_account, Reason, "\ud800"));
    }

    [Theory]
    [InlineData("sqlite", false, false)]
    [InlineData("sqlite", true, false)]
    [InlineData("sqlite", false, true)]
    [InlineData("postgresql", false, false)]
    [InlineData("postgresql", true, false)]
    [InlineData("postgresql", false, true)]
    [InlineData("mysql", false, false)]
    [InlineData("mysql", true, false)]
    [InlineData("mysql", false, true)]
    public async Task DisallowedReasonsAreHandledWithoutChangingExistingWorkAndSurviveRestart(
        string engine,
        bool existingCase,
        bool acceptNone)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        (LabelAdministrationCaller administrator, LabelPolicyState policy) = await ActivateAsync(
            fixture, acceptNone ? ""","reasonTypes":[]""" : ""","reasonTypes":["tools.ozone.report.defs#reasonOther"]""");
        ILabelReportStore reports = (ILabelReportStore)fixture.Store;
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        LabelModerationReport? allowed = null;
        LabelModerationCase? baseline = null;
        if (existingCase)
        {
            allowed = Success(await reports.SubmitReportAsync(
                await ReportTestAuthentication.CreateAsync(s_reporter, policy.DatabaseTime, "allowed"),
                new(s_account, "tools.ozone.report.defs#reasonOther"), policy.Revision, token));
            Success(await cases.ChangeCaseAsync(
                administrator,
                new(
                    operationId: "pending-review",
                    subject: s_account,
                    action: LabelCaseAction.OpenAppeal,
                    reason: "Keep existing appeal work pending."),
                token));
            baseline = Success(await cases.GetCaseAsync(administrator, allowed.CaseId, token));
        }
        ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(
            s_reporter, policy.DatabaseTime, "disallowed");
        LabelReportSubmission submission = new(s_account, Reason, "Retain the original report.");
        LabelModerationReport report = Success(await reports.SubmitReportAsync(authentication, submission, policy.Revision, token));
        Assert.Equal("Not Applicable", report.ResolutionReason);
        Assert.Equal(report.CreatedAt, report.HandledAt);
        Assert.NotNull(report.DecisionOperationId);
        Assert.Equal(s_reporter, report.ReportedBy);
        Assert.Equal(submission.Reason, report.Submission.Reason);
        LabelModerationCase current = Success(await cases.GetCaseAsync(administrator, report.CaseId, token));
        if (baseline is not null)
        {
            Assert.Equal(baseline.Id, current.Id);
            Assert.Equal(baseline.Revision, current.Revision);
            Assert.Equal(baseline.Status, current.Status);
            Assert.Equal(baseline.Assignee, current.Assignee);
            Assert.Equal(baseline.UpdatedAt, current.UpdatedAt);
            Assert.Null(Success(await reports.GetReportAsync(administrator, allowed!.Id, token)).DecisionOperationId);
        }
        else
        {
            Assert.Equal(LabelCaseStatus.Resolved, current.Status);
        }
        ILabelTableStore tables = Assert.IsType<ILabelTableStore>(fixture.NewStore(), exactMatch: false);
        LabelNavigationCounts counts = Success(await tables.ReadNavigationCountsAsync(administrator, token));
        Assert.Equal(existingCase ? 1 : 0, counts.Reports);
        LabelTablePage openCases = Success(await tables.ReadTableAsync(administrator, new(LabelTableKind.Cases), token));
        Assert.Equal(existingCase, openCases.Cases!.Any(item => item.Id == report.CaseId));
        ILabelReportStore restarted = (ILabelReportStore)fixture.NewStore();
        Assert.Equal("Not Applicable", Success(await restarted.GetReportAsync(administrator, report.Id, token)).ResolutionReason);
        Assert.Equal(LabelServiceErrorCode.Unauthorized,
            (await restarted.SubmitReportAsync(authentication, submission, policy.Revision, token)).Error!.Code);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        LabelAdministrationReceipt automatic = Assert.Single(Success(await ((ILabelAdministrationStore)fixture.Store)
            .GetAdministrationAuditAsync(administrator, cancellationToken: token)),
            entry => entry.Action == "report-not-applicable");
        Assert.Equal(StoreFixture.s_issuer, automatic.Actor);
        Assert.Equal("system:report-reason", automatic.LocalIdentity);
        Assert.Equal("Not Applicable", automatic.Reason);
        IReadOnlyList<LabelAppeal> appeals = Success(await ((ILabelAppealStore)fixture.Store)
            .GetAppealsAsync(administrator, cancellationToken: token));
        if (existingCase)
        {
            Assert.Equal(LabelAppealStatus.Pending, Assert.Single(appeals).Status);
        }
        else
        {
            Assert.Empty(appeals);
            if (!acceptNone)
            {
                LabelModerationReport subsequent = Success(await reports.SubmitReportAsync(
                    await ReportTestAuthentication.CreateAsync(s_reporter, policy.DatabaseTime, "after-automatic"),
                    new(s_account, "tools.ozone.report.defs#reasonOther"), policy.Revision, token));
                Assert.NotEqual(report.CaseId, subsequent.CaseId);
                Assert.Null(subsequent.HandledAt);
                Assert.Equal(LabelCaseStatus.Open, Success(await cases.GetCaseAsync(administrator, subsequent.CaseId, token)).Status);
            }
        }
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task AutomaticReasonHandlingStillRequiresAllowedSubjectsAndOwnerAppeals(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        (LabelAdministrationCaller administrator, LabelPolicyState policy) = await ActivateAsync(fixture,
            ""","reasonTypes":["tools.ozone.report.defs#reasonOther"],"subjectCollections":["app.bsky.feed.post"]""");
        ILabelReportStore reports = (ILabelReportStore)fixture.Store;
        ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(
            s_reporter, policy.DatabaseTime, "invalid-scope");
        LabelReportSubject profile = LabelReportSubject.Record(
            new($"at://{s_account.Repository}/app.bsky.actor.profile/self"), new(TargetResolutionHandler.RecordCid));
        Assert.Equal(LabelServiceErrorCode.TargetNotAllowed,
            (await reports.SubmitReportAsync(authentication, new(profile, Reason), policy.Revision, token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.Unauthorized,
            (await reports.SubmitReportAsync(authentication,
                new(s_account, "tools.ozone.report.defs#reasonAppeal"), policy.Revision, token)).Error!.Code);
        Assert.Empty(Success(await reports.GetReportsAsync(administrator, cancellationToken: token)));
        ModerationReportAuthenticationResult owner = await ReportTestAuthentication.CreateAsync(
            s_account.Repository, policy.DatabaseTime, "owner-appeal");
        LabelModerationReport report = Success(await reports.SubmitReportAsync(
            owner, new(s_account, "tools.ozone.report.defs#reasonAppeal"), policy.Revision, token));
        Assert.Equal("Not Applicable", report.ResolutionReason);
        Assert.Empty(Success(await ((ILabelAppealStore)fixture.Store).GetAppealsAsync(administrator, cancellationToken: token)));
    }

    /// <summary>Requires successful classified outcomes without hiding diagnostic messages.</summary>
    private static T Success<T>(LabelServiceResult<T> result) where T : class
    {
        Assert.True(result.Succeeded, $"{result.Error?.Message}{Environment.NewLine}{result.Error?.Exception}");
        return result.Result;
    }

    /// <summary>Bootstraps protected review and independently verifies a complete report-accepting policy.</summary>
    private static async Task<(LabelAdministrationCaller Administrator, LabelPolicyState Policy)> ActivateAsync(
        StoreFixture fixture,
        string reasonFields = "")
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Success(await team.BootstrapAsync(s_administrator, "uid:0", "bootstrap", token));
        LabelAdministrationCaller administrator = Success(await team.AuthenticateLocalAsync("uid:0", token));
        ILabelPolicyStore store = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState initial = await store.InitializePolicyAsync(cancellationToken: token);
        using JsonDocument declaration = JsonDocument.Parse($$"""
            {"$type":"app.bsky.labeler.service","policies":{"labelValues":["spam"]},
             "createdAt":"2026-01-01T00:00:00Z","subjectTypes":["account","record"]{{reasonFields}}}
            """);
        LabelPolicyDefinition definition = new(
            version: "v1", declaration: declaration.RootElement, allowAccounts: true, allowRecords: true, rules: "Review reports.");
        Success(await store.SavePolicyAsync(administrator, "draft", definition, "Draft.", token));
        LabelPolicyState publishing = Success(await store.BeginPolicyPublicationAsync(
            caller: administrator, operationId: "publish", version: "v1", expectedRevision: initial.Revision,
            previousCid: null, reason: "Publish.", cancellationToken: token));
        LabelPolicyState verified = Success(await store.ObservePolicyAsync(
            expectedRevision: publishing.Revision, verificationStartedAt: publishing.DatabaseTime,
            observedCid: new(TargetResolutionHandler.RecordCid), declaration: declaration.RootElement, cancellationToken: token));
        return (administrator, verified);
    }
}
