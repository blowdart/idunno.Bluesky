// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public partial class ModerationTests
{
    private const string ResolutionRecordUri = "at://did:plc:reporter/app.bsky.feed.post/3loqo3h6jbb2g";
    private const string ResolutionReport = """
        {"reasonType":"com.atproto.moderation.defs#reasonOther",
         "subject":{"$type":"com.atproto.admin.defs#repoRef","did":"at://did:plc:reporter/app.bsky.feed.post/3loqo3h6jbb2g"}}
        """;

    [Theory]
    [InlineData("resolved")]
    [InlineData("record-not-found")]
    [InlineData("record-unavailable")]
    [InlineData("inconsistent-record")]
    [InlineData("pds-unavailable")]
    [InlineData("unsupported-did")]
    public async Task UriOnlyIntakeRetainsOriginalEvidenceAndIndependentObservation(string outcome)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase())
        {
            MissingSubjectRecord = outcome == "record-not-found",
            SubjectRecordStatus = outcome == "record-unavailable" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK,
            InconsistentSubjectRecord = outcome == "inconsistent-record",
            MissingSubjectPds = outcome == "pds-unavailable"
        };
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        await fixture.Clock.PolicyVerificationRegistrations.Reader.ReadAsync(token);
        await VerifyPolicyAsync(fixture);
        await fixture.Clock.DeletedPostReviewRegistrations.Reader.ReadAsync(token);
        string reportJson = outcome == "unsupported-did"
            ? ResolutionReport.Replace("did:plc:reporter", "did:key:unsupported", StringComparison.Ordinal)
            : ResolutionReport;
        string recordUri = outcome == "unsupported-did"
            ? ResolutionRecordUri.Replace("did:plc:reporter", "did:key:unsupported", StringComparison.Ordinal)
            : ResolutionRecordUri;
        using HttpRequestMessage request = ReportRequest(key, fixture.Clock.GetUtcNow(), reportJson);
        using HttpResponseMessage accepted = await fixture.Client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using JsonDocument receipt = JsonDocument.Parse(await accepted.Content.ReadAsByteArrayAsync(token));
        long id = receipt.RootElement.GetProperty("id").GetInt64();
        LabelAdministrationCaller caller = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .AuthenticateLocalAsync("uid:0", token)).Result!;
        LabelModerationReport report = (await fixture.Services.GetRequiredService<ILabelReportStore>()
            .GetReportAsync(caller, id, token)).Result!;
        Assert.Equal(recordUri, report.Submission.Subject.Subject);
        Assert.Null(report.Submission.Subject.Cid);
        LabelModerationCase moderationCase = (await fixture.Services.GetRequiredService<ILabelModerationStore>()
            .GetCaseAsync(caller, report.CaseId, token)).Result!;
        Assert.Equal(report.Submission.Subject.Subject, moderationCase.Subject.Subject);
        Assert.Null(moderationCase.Subject.Cid);
        using HttpResponseMessage view = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.report.getReport",
            query: "?id=" + id);
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        using JsonDocument details = JsonDocument.Parse(await view.Content.ReadAsByteArrayAsync(token));
        JsonElement observation = details.RootElement.GetProperty("recordResolution");
        Assert.Equal(outcome == "unsupported-did" ? "pds-unavailable" : outcome, observation.GetProperty("status").GetString());
        Assert.Equal(outcome == "resolved", observation.TryGetProperty("cid", out JsonElement resolved));
        if (outcome == "resolved")
        {
            Assert.Equal(Cid, resolved.GetString());
        }
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingAndFailedUriOnlyCasesRecoverWithoutChangingNativeIdentityAndPinFirstSuccess(bool legacy)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase()) { MissingSubjectRecord = true };
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        await fixture.Clock.PolicyVerificationRegistrations.Reader.ReadAsync(token);
        await VerifyPolicyAsync(fixture);
        await fixture.Clock.DeletedPostReviewRegistrations.Reader.ReadAsync(token);
        LabelAdministrationCaller caller = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .AuthenticateLocalAsync("uid:0", token)).Result!;
        ILabelModerationStore cases = fixture.Services.GetRequiredService<ILabelModerationStore>();
        long caseId;
        if (legacy)
        {
            caseId = (await cases.ChangeCaseAsync(
                caller: caller,
                change: new(
                    operationId: "legacy-uri-case",
                    subject: LabelReportSubject.Record(new(ResolutionRecordUri)),
                    action: LabelCaseAction.Open,
                    reason: "Existing URI-only case."),
                cancellationToken: token)).Result!.Case.Id;
        }
        else
        {
            using HttpRequestMessage request = ReportRequest(key, fixture.Clock.GetUtcNow(), ResolutionReport);
            using HttpResponseMessage accepted = await fixture.Client.SendAsync(request, token);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            caseId = Assert.Single((await cases.GetCasesAsync(caller, cancellationToken: token)).Result!).Id;
        }
        using HttpRequestMessage accountReport = ReportRequest(key, fixture.Clock.GetUtcNow());
        using HttpResponseMessage accountAccepted = await fixture.Client.SendAsync(accountReport, token);
        Assert.Equal(HttpStatusCode.OK, accountAccepted.StatusCode);
        using HttpResponseMessage unresolved = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryStatuses",
            query: "?includeMuted=true&limit=100");
        Assert.Equal(HttpStatusCode.OK, unresolved.StatusCode);
        using JsonDocument unresolvedBody = JsonDocument.Parse(await unresolved.Content.ReadAsByteArrayAsync(token));
        Assert.Single(unresolvedBody.RootElement.GetProperty("subjectStatuses").EnumerateArray());
        Assert.Equal(1, unresolvedBody.RootElement.GetProperty("unresolvedRecordCount").GetInt32());
        Assert.False(unresolved.Headers.Contains("Warning"));
        handler.MissingSubjectRecord = false;
        using HttpResponseMessage recovered = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryStatuses",
            query: "?subject=" + Uri.EscapeDataString(ResolutionRecordUri));
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        using JsonDocument recoveredBody = JsonDocument.Parse(await recovered.Content.ReadAsByteArrayAsync(token));
        JsonElement status = Assert.Single(recoveredBody.RootElement.GetProperty("subjectStatuses").EnumerateArray());
        Assert.Equal(caseId, status.GetProperty("id").GetInt64());
        Assert.Equal(Cid, status.GetProperty("subject").GetProperty("cid").GetString());
        Assert.Equal(ResolutionRecordUri, status.GetProperty("originalSubject").GetProperty("uri").GetString());
        Assert.False(recoveredBody.RootElement.TryGetProperty("unresolvedRecordCount", out _));
        using HttpResponseMessage recordView = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.getRecord",
            query: "?uri=" + Uri.EscapeDataString(ResolutionRecordUri) + "&cid=" + Cid);
        Assert.Equal(HttpStatusCode.OK, recordView.StatusCode);
        using JsonDocument recordBody = JsonDocument.Parse(await recordView.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(caseId, recordBody.RootElement.GetProperty("moderation").GetProperty("subjectStatus").GetProperty("id").GetInt64());
        int reads = handler.SubjectRecordReads;
        handler.MissingSubjectRecord = true;
        LabelModerationCase current = (await cases.GetCaseAsync(caller, caseId, token)).Result!;
        Assert.Null(current.Subject.Cid);
        Assert.True((await cases.ChangeCaseAsync(
            caller: caller,
            change: new(
                operationId: "preserve-pinned-observation",
                subject: current.Subject,
                action: LabelCaseAction.Note,
                reason: "Keep native scope.",
                caseId: caseId,
                expectedRevision: current.Revision),
            cancellationToken: token)).Succeeded);
        using HttpResponseMessage pinned = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryStatuses",
            query: "?subject=" + Uri.EscapeDataString(ResolutionRecordUri));
        Assert.Equal(HttpStatusCode.OK, pinned.StatusCode);
        using JsonDocument pinnedBody = JsonDocument.Parse(await pinned.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(Cid, Assert.Single(pinnedBody.RootElement.GetProperty("subjectStatuses").EnumerateArray())
            .GetProperty("subject").GetProperty("cid").GetString());
        Assert.Equal(reads, handler.SubjectRecordReads);
    }

    [Fact]
    public async Task ExplicitCidIntakeNeverReadsOrRewritesTheReportedRecord()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase()) { MissingSubjectRecord = true };
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        await fixture.Clock.PolicyVerificationRegistrations.Reader.ReadAsync(token);
        await VerifyPolicyAsync(fixture);
        await fixture.Clock.DeletedPostReviewRegistrations.Reader.ReadAsync(token);
        string body = $$$"""
            {"reasonType":"com.atproto.moderation.defs#reasonOther",
             "subject":{"$type":"com.atproto.repo.strongRef","uri":"{{{ResolutionRecordUri}}}","cid":"{{{Cid}}}"}}
            """;
        using HttpRequestMessage request = ReportRequest(key, fixture.Clock.GetUtcNow(), body);
        using HttpResponseMessage accepted = await fixture.Client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using HttpResponseMessage statuses = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryStatuses");
        Assert.Equal(HttpStatusCode.OK, statuses.StatusCode);
        using JsonDocument result = JsonDocument.Parse(await statuses.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(Cid, Assert.Single(result.RootElement.GetProperty("subjectStatuses").EnumerateArray())
            .GetProperty("subject").GetProperty("cid").GetString());
        Assert.Equal(0, handler.SubjectRecordReads);
    }

    [Fact]
    public async Task PendingPromotionRetainsIntakeObservationRatherThanLookingUpANewerVersion()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase()) { DeclarationStatus = HttpStatusCode.ServiceUnavailable };
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        await fixture.Clock.PolicyVerificationRegistrations.Reader.ReadAsync(token);
        await fixture.Clock.DeletedPostReviewRegistrations.Reader.ReadAsync(token);
        using HttpRequestMessage request = ReportRequest(key, fixture.Clock.GetUtcNow(), ResolutionReport);
        using HttpResponseMessage accepted = await fixture.Client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        LabelAdministrationCaller caller = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .AuthenticateLocalAsync("uid:0", token)).Result!;
        ILabelReportStore reports = fixture.Services.GetRequiredService<ILabelReportStore>();
        Assert.Empty((await reports.GetReportsAsync(caller, cancellationToken: token)).Result!);
        handler.DeclarationStatus = HttpStatusCode.OK;
        handler.MissingSubjectRecord = true;
        await VerifyPolicyAsync(fixture);
        LabelPolicyState state = (await fixture.Services.GetRequiredService<ILabelPolicyStore>().ReadPolicyStateAsync(token))!;
        Assert.True((await reports.ProcessPendingReportsAsync(state.Revision, 100, token)).Succeeded);
        LabelModerationReport promoted = Assert.Single((await reports.GetReportsAsync(caller, cancellationToken: token)).Result!);
        Assert.Null(promoted.Submission.Subject.Cid);
        using HttpResponseMessage view = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.report.getReport",
            query: "?id=" + promoted.Id);
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        using JsonDocument result = JsonDocument.Parse(await view.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(Cid, result.RootElement.GetProperty("recordResolution").GetProperty("cid").GetString());
        Assert.Equal(Cid, result.RootElement.GetProperty("subjectStatus").GetProperty("subject").GetProperty("cid").GetString());
        Assert.Equal(1, handler.SubjectRecordReads);
    }

    [Fact]
    public async Task CidScopedOzoneActionsKeepImplicitVersionScopeButPermitExplicitCrossVersionReportSelection()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase());
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        await fixture.Clock.PolicyVerificationRegistrations.Reader.ReadAsync(token);
        await VerifyPolicyAsync(fixture);
        await fixture.Clock.DeletedPostReviewRegistrations.Reader.ReadAsync(token);
        using HttpRequestMessage firstRequest = ReportRequest(key, fixture.Clock.GetUtcNow(), ResolutionReport);
        using HttpResponseMessage first = await fixture.Client.SendAsync(firstRequest, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using JsonDocument firstReceipt = JsonDocument.Parse(await first.Content.ReadAsByteArrayAsync(token));
        long firstId = firstReceipt.RootElement.GetProperty("id").GetInt64();
        const string otherCid = "bafyreif2pall7dybz7vecqka3zo24irdwabwdi4wc5l5cbqwp2eeg33uu4";
        handler.SubjectRecordCid = otherCid;
        using HttpRequestMessage secondRequest = ReportRequest(key, fixture.Clock.GetUtcNow(), ResolutionReport);
        using HttpResponseMessage second = await fixture.Client.SendAsync(secondRequest, token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using JsonDocument secondReceipt = JsonDocument.Parse(await second.Content.ReadAsByteArrayAsync(token));
        long secondId = secondReceipt.RootElement.GetProperty("id").GetInt64();
        LabelAdministrationCaller caller = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .AuthenticateLocalAsync("uid:0", token)).Result!;
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.ChangeTeamAsync(
            caller: caller,
            change: new(
                operationId: "cross-version-moderator",
                action: LabelTeamAction.Enroll,
                target: new("did:plc:second-moderator"),
                reason: "Permit explicit release.",
                role: LabelTeamRole.Moderator),
            cancellationToken: token)).Succeeded);
        ILabelModerationStore cases = fixture.Services.GetRequiredService<ILabelModerationStore>();
        LabelModerationCase current = Assert.Single((await cases.GetCasesAsync(caller, cancellationToken: token)).Result!);
        Assert.True((await cases.ChangeCaseAsync(
            caller: caller,
            change: new(
                operationId: "release-cross-version",
                subject: current.Subject,
                action: LabelCaseAction.Assign,
                reason: "Release for implicit version-scoped handling.",
                caseId: current.Id,
                expectedRevision: current.Revision),
            cancellationToken: token)).Succeeded);
        foreach (bool explicitlySelected in new[] { false, true })
        {
            string selection = explicitlySelected ? $$"""{"ids":[{{secondId}}]}""" : """{"all":true}""";
            string body = $$$"""
                {"createdBy":"{{{s_administrator}}}",
                 "subject":{"$type":"com.atproto.repo.strongRef","uri":"{{{ResolutionRecordUri}}}","cid":"{{{Cid}}}"},
                 "event":{"$type":"tools.ozone.moderation.defs#modEventAcknowledge"},
                 "reportAction":{{{selection}}}}
                """;
            using HttpResponseMessage handled = await SendOzoneAsync(
                fixture: fixture,
                key: key,
                method: "tools.ozone.moderation.emitEvent",
                body: body);
            Assert.Equal(HttpStatusCode.OK, handled.StatusCode);
            ILabelReportStore retained = fixture.Services.GetRequiredService<ILabelReportStore>();
            Assert.NotNull((await retained.GetReportAsync(caller, firstId, token)).Result!.HandledAt);
            Assert.Equal(explicitlySelected, (await retained.GetReportAsync(caller, secondId, token)).Result!.HandledAt is not null);
        }
        ILabelReportStore reports = fixture.Services.GetRequiredService<ILabelReportStore>();
        LabelModerationReport firstReport = (await reports.GetReportAsync(caller, firstId, token)).Result!;
        LabelModerationReport secondReport = (await reports.GetReportAsync(caller, secondId, token)).Result!;
        Assert.NotNull(firstReport.HandledAt);
        Assert.NotNull(secondReport.HandledAt);
        Assert.Equal(firstReport.CaseId, secondReport.CaseId);
        LabelModerationCase moderationCase = (await fixture.Services.GetRequiredService<ILabelModerationStore>()
            .GetCaseAsync(caller, firstReport.CaseId, token)).Result!;
        Assert.Null(moderationCase.Subject.Cid);
        Assert.Equal(LabelCaseStatus.Resolved, moderationCase.Status);
    }
}
