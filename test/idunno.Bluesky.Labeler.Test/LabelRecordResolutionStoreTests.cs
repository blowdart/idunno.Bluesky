// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Test;

public class LabelRecordResolutionStoreTests
{
    private const string Cid = "bafyreihdwdcefgh4dqkjv67uzcmw7ojee6xedzdetojuzjevtenxquvyku";

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task CaseResolutionPinsFirstSuccessAcrossRestartAndNativeMutationsWithoutChangingEvidenceOrRevision(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        ILabelReportStore reports = (ILabelReportStore)fixture.Store;
        ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(
            new Did("did:plc:reporter"), policy.DatabaseTime, "uri-resolution");
        LabelModerationReport report = Success(await reports.SubmitReportAsync(
            authentication: authentication,
            submission: new(
                LabelReportSubject.Record(new("at://did:plc:reporter/app.bsky.feed.post/one")),
                "com.atproto.moderation.defs#reasonOther"),
            expectedPolicyRevision: policy.Revision,
            cancellationToken: token));
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        LabelModerationCase before = Success(await cases.GetCaseAsync(caller, report.CaseId, token));
        ILabelOzoneStore ozone = (ILabelOzoneStore)fixture.Store;
        LabelRecordResolution failed = new(null, "record-not-found", "https://pds.example/", policy.DatabaseTime);
        LabelModerationCase unresolved = Success(await ozone.ResolveRecordCaseAsync(caller, before.Id, failed, token));
        Assert.Equal(failed, unresolved.RecordResolution);
        LabelRecordResolution resolved = new(Cid, "resolved", "https://pds.example/", policy.DatabaseTime);
        LabelModerationCase pinned = Success(await ozone.ResolveRecordCaseAsync(caller, before.Id, resolved, token));
        Assert.Equal(before.Revision, pinned.Revision);
        Assert.Equal(before.UpdatedAt, pinned.UpdatedAt);
        Assert.Equal(before.Subject.Subject, pinned.Subject.Subject);
        Assert.Null(pinned.Subject.Cid);
        ILabelOzoneStore restarted = (ILabelOzoneStore)fixture.NewStore();
        LabelModerationCase retained = Success(await restarted.ResolveRecordCaseAsync(caller, before.Id, failed, token));
        Assert.Equal(resolved, retained.RecordResolution);
        LabelCaseReceipt note = Success(await cases.ChangeCaseAsync(
            caller: caller,
            change: new(
                operationId: "keep-resolution",
                subject: before.Subject,
                action: LabelCaseAction.Note,
                reason: "Preserve native scope.",
                caseId: before.Id,
                expectedRevision: before.Revision),
            cancellationToken: token));
        Assert.Equal(resolved, note.Case.RecordResolution);
        LabelModerationReport original = Success(await ((ILabelReportStore)fixture.NewStore()).GetReportAsync(caller, report.Id, token));
        Assert.Null(original.Submission.Subject.Cid);
        Assert.Null(original.Submission.RecordResolution);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    /// <summary>Requires a successful provider operation before inspecting its result.</summary>
    private static T Success<T>(LabelServiceResult<T> result) where T : class
    {
        Assert.True(result.Succeeded, result.Error?.Message);
        return result.Result!;
    }
}
