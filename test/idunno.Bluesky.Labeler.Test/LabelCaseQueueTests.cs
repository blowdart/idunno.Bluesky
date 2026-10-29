// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Test;

public class LabelCaseQueueTests
{
    private static readonly LabelReportSubject s_subject = LabelReportSubject.Account(new("did:plc:subject"));
    private const string Reason = "tools.ozone.report.defs#reasonMisleadingSpam";

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task FixedViewsCombineExactSubjectReasonAndManualAssignmentFilters(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        Success(await ((ILabelAdministrationStore)fixture.Store).ChangeTeamAsync(
            caller,
            new(
                operationId: "second-moderator",
                action: LabelTeamAction.Enroll,
                target: new("did:plc:another-moderator"),
                reason: "Exercise manual routing with multiple eligible moderators.",
                role: LabelTeamRole.Moderator),
            token));
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        ILabelReportStore reports = (ILabelReportStore)fixture.Store;
        ModerationReportAuthenticationResult owner = await ReportTestAuthentication.CreateAsync(s_subject.Repository, policy.DatabaseTime, "appeal");
        LabelModerationReport appeal = Success(await reports.SubmitReportAsync(
            owner, new(s_subject, "com.atproto.moderation.defs#reasonAppeal"), policy.Revision, token));
        LabelReportSubject other = LabelReportSubject.Account(new("did:plc:other"));
        ModerationReportAuthenticationResult reporter = await ReportTestAuthentication.CreateAsync(new("did:plc:reporter"), policy.DatabaseTime, "report");
        LabelModerationReport ordinary = Success(await reports.SubmitReportAsync(reporter, new(other, Reason), policy.Revision, token));
        AtUri uri = new($"at://{s_subject.Repository}/app.bsky.actor.profile/self");
        LabelReportSubject versionOne = LabelReportSubject.Record(uri, new(TargetResolutionHandler.RecordCid));
        LabelReportSubject versionTwo = LabelReportSubject.Record(uri, new("bafyreif2pall7yry6b3sh5rttgeczzmhnnemy6rtbwwsvxeceebzxtpfwu"));
        LabelCaseReceipt assigned = Success(await cases.ChangeCaseAsync(
            caller, new(operationId: "open-one", subject: versionOne, action: LabelCaseAction.Open, reason: "Review."), token));
        assigned = Success(await cases.ChangeCaseAsync(
            caller,
            new(
                operationId: "assign", subject: versionOne, action: LabelCaseAction.Assign, reason: "Manual assignment.",
                caseId: assigned.Case.Id, expectedRevision: assigned.Case.Revision, assignee: caller.Actor),
            token));
        assigned = Success(await cases.ChangeCaseAsync(
            caller,
            new(
                operationId: "review", subject: versionOne, action: LabelCaseAction.StartReview, reason: "Review.",
                caseId: assigned.Case.Id, expectedRevision: assigned.Case.Revision),
            token));
        LabelCaseReceipt resolved = Success(await cases.ChangeCaseAsync(
            caller, new(operationId: "open-two", subject: versionTwo, action: LabelCaseAction.Open, reason: "Review."), token));
        Success(await cases.ChangeCaseAsync(
            caller,
            new(
                operationId: "resolve", subject: versionTwo, action: LabelCaseAction.Resolve, reason: "No action.",
                caseId: resolved.Case.Id, expectedRevision: resolved.Case.Revision),
            token));
        LabelCaseReceipt independent = Success(await cases.ChangeCaseAsync(
            caller,
            new(operationId: "independent", subject: LabelReportSubject.Account(new("did:plc:independent")), action: LabelCaseAction.Open, reason: "Review."),
            token));
        (LabelCaseWorkflow Workflow, long[] Ids)[] expected =
        [
            (LabelCaseWorkflow.Unresolved, [appeal.CaseId, ordinary.CaseId, assigned.Case.Id, independent.Case.Id]),
            (LabelCaseWorkflow.Open, [appeal.CaseId, ordinary.CaseId, independent.Case.Id]),
            (LabelCaseWorkflow.InReview, [assigned.Case.Id]),
            (LabelCaseWorkflow.Unassigned, [appeal.CaseId, ordinary.CaseId, independent.Case.Id]),
            (LabelCaseWorkflow.AssignedToMe, [assigned.Case.Id]),
            (LabelCaseWorkflow.PendingAppeals, [appeal.CaseId]),
            (LabelCaseWorkflow.Resolved, [resolved.Case.Id]),
            (LabelCaseWorkflow.All, [appeal.CaseId, ordinary.CaseId, assigned.Case.Id, resolved.Case.Id, independent.Case.Id])
        ];
        foreach ((LabelCaseWorkflow workflow, long[] ids) in expected)
        {
            Assert.Equal(ids, Success(await cases.GetCaseQueueAsync(caller, new(workflow), cancellationToken: token)).Cases.Select(item => item.Id));
        }

        Assert.Equal(ordinary.CaseId, Assert.Single(Success(await cases.GetCaseQueueAsync(
            caller, new(reasonType: Reason), cancellationToken: token)).Cases).Id);
        Assert.Empty(Success(await cases.GetCaseQueueAsync(
            caller, new(reasonType: Reason.ToUpperInvariant()), cancellationToken: token)).Cases);
        Assert.Equal(assigned.Case.Id, Assert.Single(Success(await cases.GetCaseQueueAsync(
            caller, new(queue: LabelCaseWorkflow.All, subject: versionOne, assignee: caller.Actor), cancellationToken: token)).Cases).Id);
        Assert.Equal(resolved.Case.Id, Assert.Single(Success(await cases.GetCaseQueueAsync(
            caller, new(queue: LabelCaseWorkflow.Resolved, subject: versionTwo), cancellationToken: token)).Cases).Id);
        Assert.Empty(Success(await cases.GetCaseQueueAsync(
            caller, new(queue: LabelCaseWorkflow.Unresolved, subject: versionTwo), cancellationToken: token)).Cases);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ContinuationsSurviveRestartButRejectChangedFiltersAndCallers(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        for (int index = 0; index < 3; index++)
        {
            string suffix = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Success(await cases.ChangeCaseAsync(
                caller,
                new(operationId: "open-" + suffix, subject: LabelReportSubject.Account(new("did:plc:subject" + suffix)),
                    action: LabelCaseAction.Open, reason: "Review."),
                token));
        }

        LabelCaseQuery query = new(LabelCaseWorkflow.All);
        LabelCasePage first = Success(await cases.GetCaseQueueAsync(caller: caller, query: query, limit: 1, cancellationToken: token));
        Assert.NotNull(first.Cursor);
        ILabelModerationStore restarted = (ILabelModerationStore)fixture.NewStore();
        LabelCasePage rest = Success(await restarted.GetCaseQueueAsync(
            caller: caller, query: query, cursor: first.Cursor, limit: 100, cancellationToken: token));
        Assert.Equal<long>([2, 3], rest.Cases.Select(item => item.Id));
        Assert.Null(rest.Cursor);
        LabelCaseQuery[] changes =
        [
            new(LabelCaseWorkflow.Unresolved),
            new(queue: LabelCaseWorkflow.All, subject: s_subject),
            new(queue: LabelCaseWorkflow.All, reasonType: Reason),
            new(queue: LabelCaseWorkflow.All, assignee: caller.Actor)
        ];
        foreach (LabelCaseQuery change in changes)
        {
            Assert.Equal(LabelServiceErrorCode.InvalidCursor, (await restarted.GetCaseQueueAsync(
                caller: caller, query: change, cursor: first.Cursor, cancellationToken: token)).Error!.Code);
        }

        foreach (string cursor in new[] { "", "xenon1.invalid.1", first.Cursor + ".extra", first.Cursor[..^1] + "-1" })
        {
            Assert.Equal(LabelServiceErrorCode.InvalidCursor, (await restarted.GetCaseQueueAsync(
                caller: caller, query: query, cursor: cursor, cancellationToken: token)).Error!.Code);
        }

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
        Assert.Equal(LabelServiceErrorCode.InvalidCursor, (await restarted.GetCaseQueueAsync(
            caller: viewer, query: query, cursor: first.Cursor, cancellationToken: token)).Error!.Code);
        LabelCasePage viewerPage = Success(await restarted.GetCaseQueueAsync(
            caller: viewer, query: query, limit: 1, cancellationToken: token));
        Success(await team.ChangeTeamAsync(
            caller,
            new(operationId: "suspend", action: LabelTeamAction.Suspend, target: viewerDid, reason: "Revoke inspection."),
            token));
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await restarted.GetCaseQueueAsync(
            caller: viewer, query: query, cursor: viewerPage.Cursor, cancellationToken: token)).Error!.Code);
    }

    /// <summary>Retains classified provider failure detail in test diagnostics.</summary>
    private static T Success<T>(LabelServiceResult<T> result) where T : class
    {
        Assert.True(result.Succeeded, result.Error?.Message);
        return result.Result;
    }
}
