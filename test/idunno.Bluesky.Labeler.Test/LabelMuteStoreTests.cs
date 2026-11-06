// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Test;

public class LabelMuteStoreTests
{
    private static readonly Did s_reporter = new("did:plc:muted-reporter");
    private static readonly LabelReportSubject s_subject = LabelReportSubject.Account(new("did:plc:mute-subject"));
    private const string Reason = "tools.ozone.report.defs#reasonMisleadingSpam";

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task TemporaryMutesKeepProvenanceAcrossRestartAndExpireExactlyWithoutChangingCases(string engine, bool reporter)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(s_reporter, policy.DatabaseTime, "baseline");
        LabelModerationReport report = Success(await ((ILabelReportStore)fixture.Store).SubmitReportAsync(
            authentication: authentication,
            submission: new(s_subject, Reason),
            expectedPolicyRevision: policy.Revision,
            cancellationToken: token));
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        LabelModerationCase before = Success(await cases.GetCaseAsync(caller, report.CaseId, token));
        fixture.DatabaseClock!.Advance(TimeSpan.FromSeconds(1));
        DateTimeOffset muteTime = fixture.DatabaseClock.GetUtcNow();
        LabelMuteKind kind = reporter ? LabelMuteKind.Reporter : LabelMuteKind.Subject;
        LabelOzoneEvent muted = Success(await ((ILabelOzoneStore)fixture.Store).CommitOzoneEventAsync(
            caller: caller,
            request: Request(s_subject, kind, 6, "mute"),
            policyRevision: policy.Revision,
            labels: [],
            cancellationToken: token));
        ILabelOzoneStore restarted = (ILabelOzoneStore)fixture.NewStore();
        LabelMuteEntry entry = Assert.Single(Success(await restarted.ReadMutesAsync(caller, 0, 100, token)));
        Assert.Equal(muted.Id, entry.EventId);
        Assert.Equal(s_subject.Subject, entry.Subject);
        Assert.Equal(kind, entry.Kind);
        Assert.Equal(caller.Actor.Value, entry.Actor);
        Assert.Equal(muteTime.AddHours(6), entry.Until);
        Assert.Equal(muteTime, entry.CreatedAt);
        LabelMuteState state = Success(await restarted.ReadMuteAsync(caller, s_subject.Subject, token));
        Assert.Equal(entry.Until, reporter ? state.MuteReportingUntil : state.MuteUntil);
        LabelModerationCase after = Success(await cases.GetCaseAsync(caller, report.CaseId, token));
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.Assignee, after.Assignee);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        LabelCaseReceipt muteHistory = Assert.Single(
            Success(await cases.GetCaseHistoryAsync(caller, report.CaseId, cancellationToken: token)),
            receipt => receipt.IsOzoneEvent);
        Assert.Equal(muteTime, muteHistory.CreatedAt);
        Assert.Equal(before.UpdatedAt, muteHistory.Case.UpdatedAt);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        LabelTablePage queue = Success(await ((ILabelTableStore)fixture.Store).ReadTableAsync(
            caller, new(LabelTableKind.Cases), token));
        Assert.Equal(reporter ? 1 : 0, queue.Total);
        Assert.Single(Success(await cases.GetCasesAsync(caller, cancellationToken: token)));
        fixture.DatabaseClock!.Advance(TimeSpan.FromHours(6) - TimeSpan.FromMilliseconds(1));
        Assert.Single(Success(await restarted.ReadMutesAsync(caller, 0, 100, token)));
        fixture.DatabaseClock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Empty(Success(await restarted.ReadMutesAsync(caller, 0, 100, token)));
        state = Success(await restarted.ReadMuteAsync(caller, s_subject.Subject, token));
        Assert.Null(state.MuteUntil);
        Assert.Null(state.MuteReportingUntil);
        Assert.Single(Success(await restarted.ReadOzoneOperationAsync(caller, "mute", token)));
        Assert.Single(Success(await cases.GetCaseQueueAsync(
            caller: caller,
            query: new(LabelCaseWorkflow.Unresolved),
            cancellationToken: token)).Cases);
        if (reporter)
        {
            ModerationReportAuthenticationResult expired = await ReportTestAuthentication.CreateAsync(
                s_subject.Repository, fixture.DatabaseClock.GetUtcNow(), "exact-expiry");
            LabelReportIntakeReceipt accepted = Success(await ((ILabelReportStore)fixture.Store).AcceptReportAsync(
                authentication: expired,
                submission: new(s_subject, Reason),
                expectedPolicyRevision: null,
                maximumPendingReports: 10,
                cancellationToken: token));
            Assert.True(accepted.PendingVerification);
        }
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task MutedReporterSubmissionsAreInspectableWithoutCreatingOrReopeningCasesOrPromotingOnUnmute(string engine, bool existing)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        ILabelReportStore reports = (ILabelReportStore)fixture.Store;
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        LabelModerationCase? baseline = null;
        if (existing)
        {
            ModerationReportAuthenticationResult first = await ReportTestAuthentication.CreateAsync(s_reporter, policy.DatabaseTime, "first");
            LabelModerationReport prior = Success(await reports.SubmitReportAsync(
                authentication: first,
                submission: new(s_subject, Reason),
                expectedPolicyRevision: policy.Revision,
                cancellationToken: token));
            baseline = Success(await cases.GetCaseAsync(caller, prior.CaseId, token));
        }
        ILabelOzoneStore mutes = (ILabelOzoneStore)fixture.Store;
        Success(await mutes.CommitOzoneEventAsync(
            caller: caller,
            request: Request(LabelReportSubject.Account(s_reporter), LabelMuteKind.Reporter, 6, "reporter-mute"),
            policyRevision: policy.Revision,
            labels: [],
            cancellationToken: token));
        foreach (long? revision in new long?[] { null, policy.Revision })
        {
            ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(
                s_reporter, policy.DatabaseTime, "muted-" + revision);
            LabelReportIntakeReceipt receipt = Success(await reports.AcceptReportAsync(
                authentication: authentication,
                submission: new(s_subject, Reason, "Keep this original report."),
                expectedPolicyRevision: revision,
                maximumPendingReports: 1,
                cancellationToken: token));
            Assert.False(receipt.PendingVerification);
            Assert.Equal(LabelServiceErrorCode.Unauthorized, (await reports.AcceptReportAsync(
                authentication: authentication,
                submission: new(s_subject, Reason),
                expectedPolicyRevision: revision,
                maximumPendingReports: 1,
                cancellationToken: token)).Error!.Code);
        }
        LabelTablePage retained = Success(await ((ILabelTableStore)fixture.NewStore()).ReadTableAsync(
            caller, new(LabelTableKind.PendingReports), token));
        Assert.Equal(2, retained.Total);
        Assert.All(retained.PendingReports!, value =>
        {
            Assert.True(value.ReporterMuted);
            Assert.False(value.PendingVerification);
            Assert.Equal(s_reporter, value.ReportedBy);
            Assert.Equal("Keep this original report.", value.Submission.Reason);
        });
        IReadOnlyList<LabelPendingModerationReport> ozoneReports = Success(await mutes.ReadMutedReportsAsync(
            caller: caller, afterId: 0, limit: 100, reportId: null, cancellationToken: token));
        Assert.Equal(2, ozoneReports.Count);
        Assert.Single(Success(await mutes.ReadMutedReportsAsync(
            caller: caller, afterId: 0, limit: 100, reportId: ozoneReports[0].Id, cancellationToken: token)));
        IReadOnlyList<LabelModerationCase> current = Success(await cases.GetCasesAsync(caller, cancellationToken: token));
        if (baseline is not null)
        {
            Assert.Equal(baseline.Revision, Assert.Single(current).Revision);
            Assert.Equal(baseline.UpdatedAt, current[0].UpdatedAt);
        }
        else
        {
            Assert.Empty(current);
        }
        Success(await mutes.CommitOzoneEventAsync(
            caller: caller,
            request: Request(LabelReportSubject.Account(s_reporter), LabelMuteKind.Reporter, null, "reporter-unmute"),
            policyRevision: policy.Revision,
            labels: [],
            cancellationToken: token));
        Assert.Equal(0, Success(await reports.ProcessPendingReportsAsync(policy.Revision, 10, token)).Processed);
        Assert.Equal(existing ? 1 : 0, Success(await reports.GetReportsAsync(caller, cancellationToken: token)).Count);
        Assert.Empty(Success(await mutes.ReadMutesAsync(caller, 0, 100, token)));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task StaleUnmuteCannotRemoveAnExtendedMuteAndDoesNotAppendAnEvent(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        ILabelOzoneStore mutes = (ILabelOzoneStore)fixture.Store;
        LabelOzoneEvent first = Success(await mutes.CommitOzoneEventAsync(
            caller: caller, request: Request(s_subject, LabelMuteKind.Subject, 6, "initial"),
            policyRevision: policy.Revision, labels: [], cancellationToken: token));
        LabelOzoneEvent extended = Success(await mutes.CommitOzoneEventAsync(
            caller: caller, request: Request(s_subject, LabelMuteKind.Subject, 12, "extended"),
            policyRevision: policy.Revision, labels: [], cancellationToken: token));
        LabelOzoneEventRequest stale = Request(s_subject, LabelMuteKind.Subject, null, "stale") with
        {
            Mute = new(LabelMuteKind.Subject, null, first.Id)
        };
        Assert.Equal(LabelServiceErrorCode.CaseConflict,
            (await mutes.CommitOzoneEventAsync(
                caller: caller, request: stale, policyRevision: policy.Revision, labels: [], cancellationToken: token)).Error!.Code);
        Assert.Equal(extended.Id, Assert.Single(Success(await mutes.ReadMutesAsync(caller, 0, 100, token))).EventId);
        Assert.Empty(Success(await mutes.ReadOzoneOperationAsync(caller, "stale", token)));
        Success(await mutes.CommitOzoneEventAsync(
            caller: caller, request: stale with { OperationId = "current", Mute = new(LabelMuteKind.Subject, null, extended.Id) },
            policyRevision: policy.Revision, labels: [], cancellationToken: token));
        Assert.Empty(Success(await mutes.ReadMutesAsync(caller, 0, 100, token)));
    }

    /// <summary>Builds a standalone event that cannot close reports or change review status.</summary>
    private static LabelOzoneEventRequest Request(LabelReportSubject subject, LabelMuteKind kind, int? hours, string operation) => new(
        OperationId: operation,
        ExternalId: null,
        Subject: subject,
        EventJson: $$"""{"$type":"tools.ozone.moderation.defs#modEvent{{(hours is null ? "Unmute" : "Mute")}}{{(kind == LabelMuteKind.Reporter ? "Reporter" : "")}}"}""",
        ModToolJson: null,
        Reason: "Manage a temporary moderation mute.",
        Actions: [],
        ReportIds: [],
        ReportTypes: [],
        AllReports: false,
        ResolveSubject: false,
        Automated: false,
        ReportNote: null,
        Mute: new(kind, hours));

    /// <summary>Requires an explicit successful provider result in all conformance cases.</summary>
    private static T Success<T>(LabelServiceResult<T> result) where T : class
    {
        Assert.True(result.Succeeded, result.Error?.Message);
        return result.Result;
    }
}
