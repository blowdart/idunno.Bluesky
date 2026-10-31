// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Test;

public partial class LabelCaseDecisionTests
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task OzoneTransactionsKeepMultiLabelAndCrossVersionReportEvidenceAcrossRestart(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelIssuanceService service = Service(
            fixture: fixture,
            caller: caller,
            active: active,
            key: key,
            http: http);
        ILabelReportStore reports = (ILabelReportStore)fixture.Store;
        AtUri uri = new($"at://{s_subject.Repository}/app.bsky.feed.post/example");
        LabelReportSubject target = LabelReportSubject.Record(uri, new(TargetResolutionHandler.RecordCid));
        LabelReportSubject[] versions =
        [
            target,
            LabelReportSubject.Record(uri),
            LabelReportSubject.Record(uri, new("bafyreif2pall7yry6b3sh5rttgeczzmhnnemy6rtbwwsvxeceebzxtpfwu"))
        ];
        List<LabelModerationReport> accepted = [];
        foreach (LabelReportSubject subject in versions)
        {
            ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(
                new("did:plc:reporter"),
                active.DatabaseTime,
                Guid.NewGuid().ToString("N"));
            accepted.Add(Success(await reports.SubmitReportAsync(
                authentication: authentication,
                submission: new(subject, "tools.ozone.report.defs#reasonMisleadingSpam", "Review this version."),
                expectedPolicyRevision: active.Revision,
                cancellationToken: token)));
        }
        LabelOzoneEventRequest request = OzoneRequest(target, [accepted[0].Id, accepted[1].Id]);
        LabelOzoneEvent committed = Success(await service.ExecuteOzoneEventAsync(caller, request, token));
        Assert.Equal([accepted[0].Id, accepted[1].Id], committed.ReportIds);
        Assert.Equal(2, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        ILabelOzoneStore reopened = (ILabelOzoneStore)fixture.NewStore();
        LabelOzoneEvent restored = Assert.Single(Success(await reopened.ReadOzoneOperationAsync(caller, request.OperationId, token)));
        Assert.Equal(committed.Id, restored.Id);
        Assert.Equal(request.EventJson, restored.EventJson);
        Assert.Equal(request.ModToolJson, restored.ModToolJson);
        Assert.Equal(request.ReportNote, restored.ReportNote);
        Assert.True(restored.Automated);
        ILabelModerationStore history = (ILabelModerationStore)fixture.NewStore();
        foreach (LabelModerationReport report in accepted.Take(2))
        {
            IReadOnlyList<LabelCaseReceipt> entries = Success(await history.GetCaseHistoryAsync(
                caller: caller,
                caseId: report.CaseId,
                cancellationToken: token));
            Assert.True(Assert.Single(entries, entry => entry.OperationId == request.OperationId).IsOzoneEvent);
        }
        ILabelReportStore retained = (ILabelReportStore)fixture.NewStore();
        LabelModerationReport uriOnly = Success(await retained.GetReportAsync(caller, accepted[1].Id, token));
        Assert.Null(uriOnly.Submission.Subject.Cid);
        Assert.NotNull(uriOnly.HandledAt);
        Assert.Equal("Selected reports reviewed.", uriOnly.ResolutionReason);
        Assert.Null(Success(await retained.GetReportAsync(caller, accepted[2].Id, token)).HandledAt);
        Assert.Equal(
            LabelServiceErrorCode.IdempotencyConflict,
            (await service.ExecuteOzoneEventAsync(caller, request, token)).Error!.Code);
        Assert.Equal(2, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task OzoneProviderRechecksAuthorityAndPolicyBeforeApplyingAnyPreparedLabel(string engine, bool revoke)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient http = new(new TargetResolutionHandler());
        LabelAdministrationCaller administrator = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, administrator, token);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Did member = new("did:plc:ozone-moderator");
        Success(await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "enroll",
                action: LabelTeamAction.Enroll,
                target: member,
                reason: "Enroll moderator.",
                role: LabelTeamRole.Moderator),
            cancellationToken: token));
        Success(await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "bind",
                action: LabelTeamAction.BindLocal,
                target: member,
                reason: "Bind moderator.",
                credential: "uid:1"),
            cancellationToken: token));
        LabelAdministrationCaller moderator = Success(await team.AuthenticateLocalAsync("uid:1", token));
        LabelIssuanceService service = Service(
            fixture: fixture,
            caller: moderator,
            active: active,
            key: key,
            http: http);
        LabelOzoneEventRequest request = OzoneRequest(s_subject, []);
        List<LabelDecisionCommit> prepared = [];
        for (int index = 0; index < request.Actions.Length; index++)
        {
            prepared.Add(Success(await service.PrepareDecisionAsync(
                LabelOperationRequest.Issue(
                    operationId: request.OperationId + ":" + index,
                    target: LabelTarget.Account(s_subject.Repository),
                    value: request.Actions[index].Value,
                    context: new(moderator.Actor, request.Reason)),
                token)));
        }
        if (revoke)
        {
            Success(await team.ChangeTeamAsync(
                caller: administrator,
                change: new(
                    operationId: "suspend",
                    action: LabelTeamAction.Suspend,
                    target: member,
                    reason: "Revoke before commit."),
                cancellationToken: token));
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
        LabelServiceResult<LabelOzoneEvent> result = await ((ILabelOzoneStore)fixture.Store).CommitOzoneEventAsync(
            caller: moderator,
            request: request,
            policyRevision: active.Revision,
            labels: prepared,
            cancellationToken: token);
        Assert.Equal(revoke ? LabelServiceErrorCode.Unauthorized : LabelServiceErrorCode.PolicyUnavailable, result.Error!.Code);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        Assert.Empty(Success(await ((ILabelOzoneStore)fixture.NewStore()).ReadOzoneEventsAsync(
            administrator,
            0,
            100,
            token)));
    }

    /// <summary>Creates a bounded event with distinct label actions and explicit original report IDs.</summary>
    private static LabelOzoneEventRequest OzoneRequest(LabelReportSubject subject, long[] ids) => new(
        OperationId: "ozone:provider-operation",
        ExternalId: "provider-operation",
        Subject: subject,
        EventJson: """{"$type":"tools.ozone.moderation.defs#modEventLabel","createLabelVals":["spam","scam"],"negateLabelVals":[]}""",
        ModToolJson: """{"name":"Ozone","meta":{"source":"conformance"}}""",
        Reason: "Review a complete label batch.",
        Actions: [new(LabelOperationKind.Issue, "spam"), new(LabelOperationKind.Issue, "scam")],
        ReportIds: ids,
        ReportTypes: [],
        AllReports: false,
        ResolveSubject: false,
        Automated: true,
        ReportNote: "Selected reports reviewed.");
}
