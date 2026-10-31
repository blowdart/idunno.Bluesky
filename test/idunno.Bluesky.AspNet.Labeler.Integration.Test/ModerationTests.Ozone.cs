// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public partial class ModerationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OzoneLabelsAndSelectedReportResolutionCommitTogether(bool resolveReports)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        string declaration = Declaration.Replace("[\"spam\"]", "[\"spam\",\"porn\"]", StringComparison.Ordinal);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase()) { Declaration = declaration };
        using HttpClient multipleLabelIdentity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: multipleLabelIdentity,
            remoteEnabled: true,
            declarationJson: declaration,
            ozone: true);
        await VerifyPolicyAsync(fixture);
        using HttpRequestMessage reportRequest = ReportRequest(key, fixture.Clock.GetUtcNow());
        using HttpResponseMessage accepted = await fixture.Client.SendAsync(reportRequest, token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using JsonDocument reportBody = JsonDocument.Parse(await accepted.Content.ReadAsByteArrayAsync(token));
        long reportId = reportBody.RootElement.GetProperty("id").GetInt64();
        string body = $$"""
            {"createdBy":"{{s_administrator}}","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},
             "event":{"$type":"tools.ozone.moderation.defs#modEventLabel","createLabelVals":["spam","porn"],"negateLabelVals":[]}
             {{(resolveReports ? ",\"reportAction\":{\"ids\":[" + reportId + "]}" : "")}}}
            """;
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument result = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        Assert.True(result.RootElement.GetProperty("id").GetInt64() > 0);
        Assert.Equal("did:plc:subject", result.RootElement.GetProperty("subject").GetProperty("did").GetString());
        Assert.Equal(2, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        using HttpResponseMessage query = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.report.queryReports",
            query: "?status=" + (resolveReports ? "closed" : "assigned"));
        Assert.Equal(HttpStatusCode.OK, query.StatusCode);
        using JsonDocument reports = JsonDocument.Parse(await query.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(reportId, Assert.Single(reports.RootElement.GetProperty("reports").EnumerateArray()).GetProperty("id").GetInt64());
    }

    [Theory]
    [InlineData("invalid-label")]
    [InlineData("invalid-report")]
    [InlineData("wrong-created-by")]
    [InlineData("unsupported-option")]
    public async Task OzoneRejectsWholeLabelBatchWithoutPartialLabelsOrReportHandling(string scenario)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        await VerifyPolicyAsync(fixture);
        string body = $$"""
            {"createdBy":"{{(scenario == "wrong-created-by" ? s_reporter : s_administrator)}}",
             "subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},
             "event":{"$type":"tools.ozone.moderation.defs#modEventLabel",
                      "createLabelVals":{{(scenario == "invalid-label" ? "[\"spam\",\"unlisted\"]" : "[\"spam\"]")}},
                      "negateLabelVals":[]{{(scenario == "unsupported-option" ? ",\"takedown\":true" : "")}}}
             {{(scenario == "invalid-report" ? ",\"reportAction\":{\"ids\":[999999]}" : "")}}}
            """;
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: body);
        Assert.False(response.IsSuccessStatusCode);
        using JsonDocument failure = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        Assert.True(failure.RootElement.TryGetProperty("error", out _));
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        using HttpResponseMessage history = await SendOzoneAsync(fixture, key, "tools.ozone.moderation.queryEvents");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        using JsonDocument events = JsonDocument.Parse(await history.Content.ReadAsByteArrayAsync(token));
        Assert.Empty(events.RootElement.GetProperty("events").EnumerateArray());
    }

    [Fact]
    public async Task OzoneExternalIdentifiersRejectDuplicatesButFreshIdenticalActionsAreDistinct()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        await VerifyPolicyAsync(fixture);
        const string eventBody = """
            {"createdBy":"did:plc:administration-actor","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},
             "event":{"$type":"tools.ozone.moderation.defs#modEventAcknowledge"},"externalId":"ticket-123"}
            """;
        using HttpResponseMessage first = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: eventBody);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using HttpResponseMessage duplicate = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: eventBody);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        using JsonDocument error = JsonDocument.Parse(await duplicate.Content.ReadAsByteArrayAsync(token));
        Assert.Equal("DuplicateExternalId", error.RootElement.GetProperty("error").GetString());
        string fresh = eventBody.Replace(",\"externalId\":\"ticket-123\"", "", StringComparison.Ordinal);
        using HttpResponseMessage second = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: fresh);
        using HttpResponseMessage third = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: fresh);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        using JsonDocument secondBody = JsonDocument.Parse(await second.Content.ReadAsByteArrayAsync(token));
        using JsonDocument thirdBody = JsonDocument.Parse(await third.Content.ReadAsByteArrayAsync(token));
        Assert.NotEqual(secondBody.RootElement.GetProperty("id").GetInt64(), thirdBody.RootElement.GetProperty("id").GetInt64());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OzoneCanResolveUriOnlyReportsWithoutReplacingTheirEvidenceCid(bool activity)
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
        await VerifyPolicyAsync(fixture);
        const string uri = "at://did:plc:reporter/app.bsky.feed.post/3loqo3h6jbb2g";
        const string report = $$$"""
            {"reasonType":"tools.ozone.report.defs#reasonMisleadingSpam",
             "subject":{"$type":"com.atproto.admin.defs#repoRef","did":"{{{uri}}}"}}
            """;
        using HttpRequestMessage request = ReportRequest(key, fixture.Clock.GetUtcNow(), report);
        using HttpResponseMessage accepted = await fixture.Client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using JsonDocument receipt = JsonDocument.Parse(await accepted.Content.ReadAsByteArrayAsync(token));
        long id = receipt.RootElement.GetProperty("id").GetInt64();
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelServiceResult<LabelAdministrationCaller> authentication = await team.AuthenticateLocalAsync("uid:0", token);
        Assert.True(authentication.Succeeded);
        ILabelReportStore reportStore = fixture.Services.GetRequiredService<ILabelReportStore>();
        LabelServiceResult<LabelModerationReport> reportResult = await reportStore.GetReportAsync(authentication.Result, id, token);
        Assert.True(reportResult.Succeeded);
        if (!activity)
        {
            Assert.True((await team.ChangeTeamAsync(
                caller: authentication.Result,
                change: new(
                    operationId: "enroll-second-moderator",
                    action: LabelTeamAction.Enroll,
                    target: new("did:plc:second-moderator"),
                    reason: "Allow an unassigned report case.",
                    role: LabelTeamRole.Moderator),
                cancellationToken: token)).Succeeded);
            ILabelModerationStore cases = fixture.Services.GetRequiredService<ILabelModerationStore>();
            LabelServiceResult<LabelModerationCase> current = await cases.GetCaseAsync(authentication.Result, reportResult.Result.CaseId, token);
            Assert.True(current.Succeeded);
            Assert.True((await cases.ChangeCaseAsync(
                caller: authentication.Result,
                change: new(
                    operationId: "release-case",
                    subject: current.Result.Subject,
                    action: LabelCaseAction.Assign,
                    reason: "Release report assignment for bulk closure.",
                    caseId: current.Result.Id,
                    expectedRevision: current.Result.Revision),
                cancellationToken: token)).Succeeded);
        }
        string body = activity
            ? $$"""{"reportId":{{id}},"activity":{"$type":"tools.ozone.report.defs#closeActivity"},"internalNote":"Reviewed."}"""
            : $$"""{"subject":"{{uri}}","internalNote":"Reviewed."}""";
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: activity ? "tools.ozone.report.createActivity" : "tools.ozone.report.closeReports",
            body: body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        reportResult = await reportStore.GetReportAsync(authentication.Result, id, token);
        Assert.True(reportResult.Succeeded);
        LabelModerationReport retained = reportResult.Result;
        Assert.NotNull(retained.HandledAt);
        Assert.Null(retained.Submission.Subject.Cid);
        Assert.Equal(uri, retained.Submission.Subject.Subject);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        using HttpResponseMessage reportView = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.report.getReport",
            query: "?id=" + id);
        Assert.Equal(HttpStatusCode.OK, reportView.StatusCode);
        using JsonDocument reportJson = JsonDocument.Parse(await reportView.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(uri, reportJson.RootElement.GetProperty("subject").GetProperty("subject").GetString());
        Assert.False(reportJson.RootElement.TryGetProperty("subjectStatus", out _));
        handler.MissingSubjectRecord = true;
        using HttpResponseMessage eventView = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.getEvent",
            query: "?id=" + id * 4);
        Assert.Equal(HttpStatusCode.OK, eventView.StatusCode);
        using JsonDocument detail = JsonDocument.Parse(await eventView.Content.ReadAsByteArrayAsync(token));
        Assert.Equal("tools.ozone.moderation.defs#recordViewNotFound", detail.RootElement.GetProperty("subject").GetProperty("$type").GetString());
        Assert.Equal(uri, detail.RootElement.GetProperty("subject").GetProperty("uri").GetString());
        using HttpResponseMessage history = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryEvents",
            query: "?subject=" + Uri.EscapeDataString(uri));
        Assert.Equal(HttpStatusCode.NotImplemented, history.StatusCode);
        using HttpResponseMessage activities = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.report.listActivities",
            query: "?reportId=" + id);
        Assert.Equal(HttpStatusCode.OK, activities.StatusCode);
        using JsonDocument activitiesJson = JsonDocument.Parse(await activities.Content.ReadAsByteArrayAsync(token));
        JsonElement closure = Assert.Single(activitiesJson.RootElement.GetProperty("activities").EnumerateArray());
        Assert.Equal("tools.ozone.report.defs#closeActivity", closure.GetProperty("activity").GetProperty("$type").GetString());
        Assert.Equal(s_administrator.Value, closure.GetProperty("createdBy").GetString());
    }

    [Theory]
    [InlineData(LabelSigningCurve.P256)]
    [InlineData(LabelSigningCurve.Secp256k1)]
    public async Task OzoneIssueAndNegationKeepVerifiablePublicDistributionAndDurableEvents(LabelSigningCurve curve)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(curve);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        await VerifyPolicyAsync(fixture);
        foreach (bool negate in new[] { false, true })
        {
            string body = $$$"""
                {"createdBy":"{{{s_administrator}}}","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},
                 "event":{"$type":"tools.ozone.moderation.defs#modEventLabel",
                          "createLabelVals":{{{(negate ? "[]" : "[\"spam\"]")}}},
                          "negateLabelVals":{{{(negate ? "[\"spam\"]" : "[]")}}}}}
                """;
            using HttpResponseMessage response = await SendOzoneAsync(
                fixture: fixture,
                key: key,
                method: "tools.ozone.moderation.emitEvent",
                body: body);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await fixture.PublishAllAsync();
            AtProtoHttpResult<PagedReadOnlyCollection<Label>> query = await AtProtoServer.QueryLabels(
                uriPatterns: ["did:plc:subject"],
                sources: null,
                limit: 100,
                cursor: null,
                service: fixture.Address,
                accessCredentials: null,
                httpClient: fixture.Client,
                cancellationToken: token);
            Assert.True(query.Succeeded);
            Label label = Assert.Single(query.Result);
            Assert.Equal(negate, label.IsNegationLabel);
            DidDocument signingDocument = JsonSerializer.Deserialize<DidDocument>(
                $$"""
                {"id":"{{TestServerBuilder.s_issuer}}","verificationMethod":[{"id":"{{TestServerBuilder.s_issuer}}#atproto_label",
                 "type":"Multikey","controller":"{{TestServerBuilder.s_issuer}}","publicKeyMultibase":"{{key.ExportPublicKeyMultibase()}}"}]}
                """,
                AtProtoJsonSerializerOptions.Default)!;
            label.Verify(signingDocument);
        }
        await using TestServerBuilder restarted = await TestServerBuilder.CreateAsync(
            database: fixture.DatabasePath,
            configureAdministration: services =>
            {
                services.AddLabelerModeration(issuer: TestServerBuilder.s_issuer, httpClient: identity);
                services.AddLabelerAdministration(new(TestServerBuilder.s_issuer, "labeler_admin"), key, identity);
                services.Configure<LabelerAdministrationOptions>(settings => settings.RemoteMode = RemoteAdministrationMode.Enabled);
            },
            moderation: true,
            ozoneAdministration: true,
            controlDatabaseTime: true);
        using HttpResponseMessage events = await SendOzoneAsync(restarted, key, "tools.ozone.moderation.queryEvents");
        Assert.Equal(HttpStatusCode.OK, events.StatusCode);
        using JsonDocument history = JsonDocument.Parse(await events.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(2, history.RootElement.GetProperty("events").GetArrayLength());
        Assert.Equal(
            2,
            history.RootElement.GetProperty("events").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64()).Distinct().Count());
    }

    [Fact]
    public async Task OzoneReportResolutionPreservesAnUnreviewedOwnerAppeal()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        await VerifyPolicyAsync(fixture);
        const string issue = """
            {"createdBy":"did:plc:administration-actor","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:reporter"},
             "event":{"$type":"tools.ozone.moderation.defs#modEventLabel","createLabelVals":["spam"],"negateLabelVals":[]}}
            """;
        using HttpResponseMessage issued = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: issue);
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        await fixture.PublishAllAsync();
        const string appeal = """
            {"subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:reporter"},
             "action":{"$type":"tools.ozone.inbox.appealActionedSubject#labelRef","val":"spam"},"reason":"Please reconsider."}
            """;
        using HttpResponseMessage accepted = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.inbox.appealActionedSubject",
            body: appeal,
            actor: s_reporter.Value);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelServiceResult<LabelAdministrationCaller> authentication = await team.AuthenticateLocalAsync("uid:0", token);
        Assert.True(authentication.Succeeded);
        ILabelReportStore reports = fixture.Services.GetRequiredService<ILabelReportStore>();
        LabelServiceResult<IReadOnlyList<LabelModerationReport>> reportPage = await reports.GetReportsAsync(
            caller: authentication.Result,
            cancellationToken: token);
        Assert.True(reportPage.Succeeded);
        LabelModerationReport report = Assert.Single(reportPage.Result);
        string close = $$"""
            {"reportId":{{report.Id}},"activity":{"$type":"tools.ozone.report.defs#closeActivity"},"internalNote":"Report acknowledged, appeal awaits review."}
            """;
        using HttpResponseMessage closed = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.report.createActivity",
            body: close);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        LabelServiceResult<IReadOnlyList<LabelAppeal>> appeals = await fixture.Services.GetRequiredService<ILabelAppealStore>()
            .GetAppealsAsync(caller: authentication.Result, cancellationToken: token);
        Assert.True(appeals.Succeeded);
        Assert.Equal(LabelAppealStatus.Pending, Assert.Single(appeals.Result).Status);
        LabelServiceResult<LabelModerationCase> moderationCase = await fixture.Services.GetRequiredService<ILabelModerationStore>()
            .GetCaseAsync(authentication.Result, report.CaseId, token);
        Assert.True(moderationCase.Succeeded);
        Assert.NotEqual(LabelCaseStatus.Resolved, moderationCase.Result.Status);
        Assert.Equal(1, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"createdBy":true}""")]
    [InlineData("""{"createdBy":"did:plc:administration-actor","subject":{},"event":{}}""")]
    [InlineData("""{"createdBy":"did:plc:administration-actor","createdBy":"did:plc:administration-actor"}""")]
    [InlineData("""{"createdBy":"did:plc:administration-actor","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},"event":{"$type":"tools.ozone.moderation.defs#modEventAcknowledge"},"modTool":42}""")]
    [InlineData("""{"createdBy":"did:plc:administration-actor","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},"event":{"$type":"tools.ozone.moderation.defs#modEventAcknowledge"},"modTool":{"meta":{}}}""")]
    public async Task OzoneMalformedEventsReturnXrpcInputErrorsWithoutServerExceptions(string body)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument error = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("InvalidRequest", error.RootElement.GetProperty("error").GetString());
        Assert.Equal(0, (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).IssuedThrough);
    }

    [Fact]
    public async Task OzoneMissingReportsUseTheDeclaredNotFoundError()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.report.getReport",
            query: "?id=999999");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument error = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("NotFound", error.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task OzoneRejectsInvalidContinuationsEvenWhenThereAreNoEvents()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryEvents",
            query: "?cursor=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OzoneHistoryRetainsNativeOperationsWithOzonePrefixedIdentifiers(bool sharedIdentifier)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelServiceResult<LabelAdministrationCaller> caller = await team.AuthenticateLocalAsync("uid:0", token);
        Assert.True(caller.Succeeded);
        ILabelModerationStore cases = fixture.Services.GetRequiredService<ILabelModerationStore>();
        const string reason = "Native operation with an Ozone-prefixed identifier.";
        LabelServiceResult<LabelCaseReceipt> opened = await cases.ChangeCaseAsync(
            caller: caller.Result,
            change: new(
                operationId: "native-case",
                subject: LabelReportSubject.Account(new("did:plc:subject")),
                action: LabelCaseAction.Open,
                reason: "Create a case for native history."),
            cancellationToken: token);
        Assert.True(opened.Succeeded);
        string operationId = "ozone:manual-note";
        if (sharedIdentifier)
        {
            await VerifyPolicyAsync(fixture);
            const string body = """
                {"createdBy":"did:plc:administration-actor","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},
                 "event":{"$type":"tools.ozone.moderation.defs#modEventAcknowledge"}}
                """;
            using HttpResponseMessage acknowledged = await SendOzoneAsync(
                fixture: fixture,
                key: key,
                method: "tools.ozone.moderation.emitEvent",
                body: body);
            Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
            LabelServiceResult<IReadOnlyList<LabelCaseReceipt>> projected = await cases.GetCaseHistoryAsync(
                caller: caller.Result,
                caseId: opened.Result.Case.Id,
                cancellationToken: token);
            Assert.True(projected.Succeeded);
            operationId = Assert.Single(projected.Result, entry => entry.Reason == "Ozone moderation event.").OperationId;
        }
        LabelServiceResult<LabelModerationCase> current = await cases.GetCaseAsync(caller.Result, opened.Result.Case.Id, token);
        Assert.True(current.Succeeded);
        Assert.True((await cases.ChangeCaseAsync(
            caller: caller.Result,
            change: new(
                operationId: operationId,
                subject: current.Result.Subject,
                action: LabelCaseAction.Note,
                reason: reason,
                caseId: current.Result.Id,
                expectedRevision: current.Result.Revision),
            cancellationToken: token)).Succeeded);
        LabelServiceResult<IReadOnlyList<LabelCaseReceipt>> history = await cases.GetCaseHistoryAsync(
            caller: caller.Result,
            caseId: opened.Result.Case.Id,
            cancellationToken: token);
        Assert.True(history.Succeeded);
        Assert.Contains(history.Result, entry => entry.Reason == reason);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture,
            key,
            "tools.ozone.moderation.queryEvents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument events = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        Assert.Contains(
            events.RootElement.GetProperty("events").EnumerateArray(),
            entry => entry.GetProperty("event").TryGetProperty("comment", out JsonElement comment) && comment.GetString() == reason);
        Assert.DoesNotContain(
            events.RootElement.GetProperty("events").EnumerateArray(),
            entry => entry.GetProperty("event").TryGetProperty("comment", out JsonElement comment) && comment.GetString() == "Ozone moderation event.");
    }

    /// <summary>Sends one Ozone method with a fresh exact-method assertion over the isolated test identity.</summary>
    private static async Task<HttpResponseMessage> SendOzoneAsync(
        TestServerBuilder fixture,
        LabelSigningKey key,
        string method,
        string? body = null,
        string query = "",
        string? actor = null)
    {
        using HttpRequestMessage request = new(body is null ? HttpMethod.Get : HttpMethod.Post, "/xrpc/" + method + query);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }
        AdministrationTests.AddTrustedProxyHeaders(request);
        request.Headers.Authorization = new(
            "Bearer",
            AdministrationTests.Token(
                key: key,
                now: fixture.Clock.GetUtcNow(),
                audience: TestServerBuilder.s_issuer.Value,
                method: method,
                actor: actor));
        return await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
