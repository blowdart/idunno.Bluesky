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
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task OzoneLabelPreservesEscalatedReviewUntilExplicitAcknowledgment(bool acknowledge, bool withReports, bool record)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, remoteEnabled: true, ozone: true);
        await fixture.Clock.PolicyVerificationRegistrations.Reader.ReadAsync(token);
        await VerifyPolicyAsync(fixture);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller caller = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        ILabelModerationStore cases = fixture.Services.GetRequiredService<ILabelModerationStore>();
        ILabelReportStore reports = fixture.Services.GetRequiredService<ILabelReportStore>();
        const string uri = "at://did:plc:reporter/app.bsky.feed.post/3loqo3h6jbb2g";
        LabelReportSubject subject = record
            ? LabelReportSubject.Record(new(uri), new(Cid))
            : LabelReportSubject.Account(new("did:plc:subject"));
        string wireSubject = record
            ? $$$"""{"$type":"com.atproto.repo.strongRef","uri":"{{{uri}}}","cid":"{{{Cid}}}"}"""
            : """{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"}""";
        LabelModerationCase opened = (await cases.ChangeCaseAsync(
            caller: caller,
            change: new(
                operationId: "checkbox-case",
                subject: subject,
                action: LabelCaseAction.Open,
                reason: "Verify escalation checkbox behavior."),
            cancellationToken: token)).Result!.Case;
        if (withReports)
        {
            for (int index = 0; index < 2; index++)
            {
                string reportBody = """
                    {"reasonType":"com.atproto.moderation.defs#reasonOther","reason":"Review this subject.","subject":
                    """ + wireSubject + "}";
                using HttpRequestMessage request = ReportRequest(key, fixture.Clock.GetUtcNow(), reportBody);
                using HttpResponseMessage accepted = await fixture.Client.SendAsync(request, token);
                Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            }
        }
        string prefix = """{"createdBy":"did:plc:administration-actor","subject":""" + wireSubject + ""","event":""";
        using HttpResponseMessage escalation = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: prefix + """{"$type":"tools.ozone.moderation.defs#modEventEscalate"}}""");
        Assert.Equal(HttpStatusCode.OK, escalation.StatusCode);
        foreach (bool negate in new[] { false, true })
        {
            string labelEvent = negate
                ? """{"$type":"tools.ozone.moderation.defs#modEventLabel","createLabelVals":[],"negateLabelVals":["spam"]}}"""
                : """{"$type":"tools.ozone.moderation.defs#modEventLabel","createLabelVals":["spam"],"negateLabelVals":[]}}""";
            using HttpResponseMessage labeled = await SendOzoneAsync(
                fixture: fixture, key: key, method: "tools.ozone.moderation.emitEvent", body: prefix + labelEvent);
            Assert.True(labeled.StatusCode == HttpStatusCode.OK, await labeled.Content.ReadAsStringAsync(token));
            LabelModerationCase current = (await cases.GetCaseAsync(caller, opened.Id, token)).Result!;
            Assert.Equal(LabelCaseStatus.Escalated, current.Status);
            Assert.Equal(opened.Assignee, current.Assignee);
            IReadOnlyList<LabelModerationReport> submissions = (await reports.GetReportsAsync(
                caller: caller, caseId: opened.Id, cancellationToken: token)).Result!;
            Assert.Equal(withReports ? 2 : 0, submissions.Count);
            Assert.All(submissions, report => Assert.Null(report.HandledAt));
        }
        Assert.Equal(2, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        if (acknowledge)
        {
            using HttpResponseMessage acknowledged = await SendOzoneAsync(
                fixture: fixture,
                key: key,
                method: "tools.ozone.moderation.emitEvent",
                body: prefix + """
                    {"$type":"tools.ozone.moderation.defs#modEventAcknowledge","comment":"[DEFINITIVE_PREVIOUS_ACTION]"}}
                    """);
            Assert.True(acknowledged.StatusCode == HttpStatusCode.OK, await acknowledged.Content.ReadAsStringAsync(token));
        }
        Assert.Equal(acknowledge ? LabelCaseStatus.Resolved : LabelCaseStatus.Escalated,
            (await cases.GetCaseAsync(caller, opened.Id, token)).Result!.Status);
        Assert.All((await reports.GetReportsAsync(caller: caller, caseId: opened.Id, cancellationToken: token)).Result!,
            report => Assert.Equal(acknowledge, report.HandledAt is not null));
    }

    [Fact]
    public async Task OzoneEscalationPreservesExactRecordVersionsAndProjectsNativeEscalation()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, remoteEnabled: true, ozone: true);
        await VerifyPolicyAsync(fixture);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller caller = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        ILabelModerationStore cases = fixture.Services.GetRequiredService<ILabelModerationStore>();
        const string uri = "at://did:plc:subject/app.bsky.feed.post/one";
        const string secondCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
        List<LabelModerationCase> opened = [];
        foreach (string cid in new[] { Cid, secondCid })
        {
            LabelServiceResult<LabelCaseReceipt> result = await cases.ChangeCaseAsync(
                caller: caller,
                change: new(
                    operationId: "record-escalation-" + cid,
                    subject: LabelReportSubject.Record(new(uri), new(cid)),
                    action: LabelCaseAction.Open,
                    reason: "Review this record version."),
                cancellationToken: token);
            Assert.True(result.Succeeded, result.Error?.Message);
            opened.Add(result.Result!.Case);
        }
        Assert.Equal(HttpStatusCode.BadRequest, await EscalateAsync(uri + "missing", Cid));
        Assert.Equal(HttpStatusCode.OK, await EscalateAsync(uri, Cid));
        LabelModerationCase first = (await cases.GetCaseAsync(caller, opened[0].Id, token)).Result!;
        LabelModerationCase second = (await cases.GetCaseAsync(caller, opened[1].Id, token)).Result!;
        Assert.Equal(LabelCaseStatus.Escalated, first.Status);
        Assert.Equal(LabelCaseStatus.Open, second.Status);
        Assert.Equal(opened[0].Assignee, first.Assignee);
        Assert.True((await cases.ChangeCaseAsync(
            caller: caller,
            change: new(
                operationId: "native-record-escalation",
                subject: second.Subject,
                action: LabelCaseAction.Escalate,
                reason: "Native moderator escalation.",
                caseId: second.Id,
                expectedRevision: second.Revision),
            cancellationToken: token)).Succeeded);
        using HttpResponseMessage events = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryEvents",
            query: "?types=tools.ozone.moderation.defs%23modEventEscalate");
        using JsonDocument history = JsonDocument.Parse(await events.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(2, history.RootElement.GetProperty("events").GetArrayLength());
        Assert.True((await cases.ChangeCaseAsync(
            caller: caller,
            change: new(
                operationId: "resolve-record-escalation",
                subject: first.Subject,
                action: LabelCaseAction.Resolve,
                reason: "Review complete.",
                caseId: first.Id,
                expectedRevision: first.Revision),
            cancellationToken: token)).Succeeded);
        Assert.Equal(HttpStatusCode.BadRequest, await EscalateAsync(uri, Cid));
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);

        async Task<HttpStatusCode> EscalateAsync(string recordUri, string cid)
        {
            string body = $$$"""
                {"createdBy":"{{{s_administrator}}}",
                 "subject":{"$type":"com.atproto.repo.strongRef","uri":"{{{recordUri}}}","cid":"{{{cid}}}"},
                 "event":{"$type":"tools.ozone.moderation.defs#modEventEscalate"}}
                """;
            using HttpResponseMessage response = await SendOzoneAsync(
                fixture: fixture, key: key, method: "tools.ozone.moderation.emitEvent", body: body);
            return response.StatusCode;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OzoneEscalationPreservesReportsAssignmentAndLabelsAndProjectsItsQueue(bool remarks)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, remoteEnabled: true, ozone: true);
        await VerifyPolicyAsync(fixture);
        List<long> reportIds = [];
        foreach (string did in new[] { "did:plc:subject", "did:plc:subject", "did:plc:another" })
        {
            string report = $$$"""
                {"reasonType":"com.atproto.moderation.defs#reasonOther","reason":"Review this account.",
                "subject":{"$type":"com.atproto.admin.defs#repoRef","did":"{{{did}}}"}}
                """;
            using HttpRequestMessage request = ReportRequest(key, fixture.Clock.GetUtcNow(), report);
            using HttpResponseMessage accepted = await fixture.Client.SendAsync(request, token);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            using JsonDocument reportResponse = JsonDocument.Parse(await accepted.Content.ReadAsByteArrayAsync(token));
            reportIds.Add(reportResponse.RootElement.GetProperty("id").GetInt64());
        }
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller caller = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        ILabelReportStore reports = fixture.Services.GetRequiredService<ILabelReportStore>();
        ILabelModerationStore cases = fixture.Services.GetRequiredService<ILabelModerationStore>();
        LabelModerationReport first = (await reports.GetReportAsync(caller, reportIds[0], token)).Result!;
        LabelModerationCase before = (await cases.GetCaseAsync(caller, first.CaseId, token)).Result!;
        string body = $$$"""
            {"createdBy":"{{{s_administrator}}}","externalId":"escalation-test",
             "subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},
             "event":{"$type":"tools.ozone.moderation.defs#modEventEscalate"
             {{{(remarks ? ",\"comment\":\"Needs a second opinion.\"" : "")}}}}}
            """;
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture, key: key, method: "tools.ozone.moderation.emitEvent", body: body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(token));
        LabelModerationCase escalated = (await cases.GetCaseAsync(caller, first.CaseId, token)).Result!;
        Assert.Equal(LabelCaseStatus.Escalated, escalated.Status);
        Assert.Equal(before.Assignee, escalated.Assignee);
        foreach (long id in reportIds)
        {
            Assert.Null((await reports.GetReportAsync(caller, id, token)).Result!.HandledAt);
        }
        Assert.Equal(LabelCaseStatus.Open, (await cases.GetCaseAsync(caller,
            (await reports.GetReportAsync(caller, reportIds[2], token)).Result!.CaseId, token)).Result!.Status);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        using HttpResponseMessage duplicate = await SendOzoneAsync(
            fixture: fixture, key: key, method: "tools.ozone.moderation.emitEvent", body: body);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        using HttpResponseMessage statuses = await SendOzoneAsync(
            fixture: fixture, key: key, method: "tools.ozone.moderation.queryStatuses",
            query: "?reviewState=tools.ozone.moderation.defs%23reviewEscalated");
        using JsonDocument statusPage = JsonDocument.Parse(await statuses.Content.ReadAsByteArrayAsync(token));
        Assert.Equal("tools.ozone.moderation.defs#reviewEscalated",
            Assert.Single(statusPage.RootElement.GetProperty("subjectStatuses").EnumerateArray()).GetProperty("reviewState").GetString());
        using HttpResponseMessage queued = await SendOzoneAsync(
            fixture: fixture, key: key, method: "tools.ozone.report.queryReports", query: "?status=escalated");
        using JsonDocument queue = JsonDocument.Parse(await queued.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(2, queue.RootElement.GetProperty("reports").GetArrayLength());
        string invalid = body.Replace("\"externalId\":\"escalation-test\",", "\"reportAction\":{\"all\":true},", StringComparison.Ordinal);
        using HttpResponseMessage rejected = await SendOzoneAsync(
            fixture: fixture, key: key, method: "tools.ozone.moderation.emitEvent", body: invalid);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(escalated.Revision, (await cases.GetCaseAsync(caller, first.CaseId, token)).Result!.Revision);
        string acknowledge = body.Replace("\"externalId\":\"escalation-test\",", "", StringComparison.Ordinal)
            .Replace("#modEventEscalate", "#modEventAcknowledge", StringComparison.Ordinal);
        using HttpResponseMessage resolved = await SendOzoneAsync(
            fixture: fixture, key: key, method: "tools.ozone.moderation.emitEvent", body: acknowledge);
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Equal(LabelCaseStatus.Resolved, (await cases.GetCaseAsync(caller, first.CaseId, token)).Result!.Status);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OzoneAcknowledgmentHandlesReportsAndResolvesOnlyTheSubmittedSubject(bool remarks, bool selectedOnly)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, remoteEnabled: true, ozone: true);
        await VerifyPolicyAsync(fixture);
        List<long> reportIds = [];
        foreach (string subject in new[] { "did:plc:subject", "did:plc:subject", "did:plc:another" })
        {
            string report = $$$"""
                {"reasonType":"com.atproto.moderation.defs#reasonOther","reason":"Review this account.",
                 "subject":{"$type":"com.atproto.admin.defs#repoRef","did":"{{{subject}}}"}}
                """;
            using HttpRequestMessage request = ReportRequest(key, fixture.Clock.GetUtcNow(), report);
            using HttpResponseMessage accepted = await fixture.Client.SendAsync(request, token);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            using JsonDocument response = JsonDocument.Parse(await accepted.Content.ReadAsByteArrayAsync(token));
            reportIds.Add(response.RootElement.GetProperty("id").GetInt64());
        }
        string body = $$"""
            {"createdBy":"{{s_administrator}}","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},
             "event":{"$type":"tools.ozone.moderation.defs#modEventAcknowledge","acknowledgeAccountSubjects":false
             {{(remarks ? ",\"comment\":\"Reviewed; no label change needed.\"" : "")}}}
             {{(selectedOnly ? ",\"reportAction\":{\"ids\":[" + reportIds[0] + "],\"note\":\"Selected report reviewed.\"}" : "")}}}
            """;
        using HttpResponseMessage acknowledged = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: body);
        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller caller = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        ILabelReportStore reports = fixture.Services.GetRequiredService<ILabelReportStore>();
        LabelModerationReport first = (await reports.GetReportAsync(caller, reportIds[0], token)).Result!;
        Assert.NotNull(first.HandledAt);
        Assert.Equal(
            selectedOnly ? "Selected report reviewed." : remarks ? "Reviewed; no label change needed." : "Ozone moderation event.",
            first.ResolutionReason);
        LabelServiceResult<LabelModerationReport> secondResult = await reports.GetReportAsync(caller, reportIds[1], token);
        Assert.True(secondResult.Succeeded, secondResult.Error?.ToString());
        LabelModerationReport second = secondResult.Result;
        Assert.Equal(!selectedOnly, second.HandledAt is not null);
        Assert.Null((await reports.GetReportAsync(caller, reportIds[2], token)).Result!.HandledAt);
        LabelModerationCase moderationCase = (await fixture.Services.GetRequiredService<ILabelModerationStore>()
            .GetCaseAsync(caller, first.CaseId, token)).Result!;
        Assert.Equal(!selectedOnly, moderationCase.Status == LabelCaseStatus.Resolved);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        using HttpResponseMessage queued = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.report.queryReports",
            query: "?status=closed");
        Assert.Equal(HttpStatusCode.OK, queued.StatusCode);
        using JsonDocument queue = JsonDocument.Parse(await queued.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(selectedOnly ? 1 : 2, queue.RootElement.GetProperty("reports").GetArrayLength());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task OzonePersonalMediaPreferencesRoundTripThroughXenon(int filters)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            xenonUi: true,
            ozone: true);
        using HttpResponseMessage initial = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.setting.listOptions",
            query: "?scope=personal");
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        using JsonDocument initialBody = JsonDocument.Parse(await initial.Content.ReadAsByteArrayAsync(token));
        JsonElement option = Assert.Single(initialBody.RootElement.GetProperty("options").EnumerateArray());
        Assert.Equal(s_administrator.Value, option.GetProperty("did").GetString());
        Assert.Equal(s_administrator.Value, option.GetProperty("createdBy").GetString());
        Assert.Equal(s_administrator.Value, option.GetProperty("lastUpdatedBy").GetString());
        Assert.True(option.GetProperty("value").GetProperty("graphicMediaPrefs").GetProperty("graphic-pref-blur-porn").GetBoolean());
        string body = $$$"""
            {"key":"dev.idunno.labeler.mediaPreferences","scope":"personal","value":{"graphicMediaPrefs":{
            "graphic-pref-blur-porn":{{{((filters & 1) != 0 ? "true" : "false")}}},
            "graphic-pref-grayscale-porn":{{{((filters & 2) != 0 ? "true" : "false")}}},
            "graphic-pref-translucent-porn":{{{((filters & 4) != 0 ? "true" : "false")}}}
            }
            }
            }
            """;
        using HttpResponseMessage saved = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.setting.upsertOption",
            body: body);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using JsonDocument savedBody = JsonDocument.Parse(await saved.Content.ReadAsByteArrayAsync(token));
        JsonElement preferences = savedBody.RootElement.GetProperty("option").GetProperty("value").GetProperty("graphicMediaPrefs");
        Assert.Equal(15, preferences.EnumerateObject().Count());
        Assert.Equal((filters & 1) != 0, preferences.GetProperty("graphic-pref-blur-porn").GetBoolean());
        Assert.False(preferences.GetProperty("graphic-pref-blur-nudity").GetBoolean());
        using HttpRequestMessage pageRequest = new(HttpMethod.Get, "/Xenon/Preferences");
        pageRequest.Headers.Add("X-Test-Did", s_administrator.Value);
        using HttpResponseMessage page = await fixture.Client.SendAsync(pageRequest, token);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        string html = await page.Content.ReadAsStringAsync(token);
        foreach ((string name, int bit) in new[] { ("blur", 1), ("grayscale", 2), ("translucent", 4) })
        {
            Assert.Equal((filters & bit) != 0, html.Contains($"name=\"{name}\" value=\"porn\" checked=\"checked\"", StringComparison.Ordinal));
        }
        string csrf = System.Net.WebUtility.HtmlDecode(
            PreferencesAntiforgeryRegex().Match(html).Groups[1].Value);
        Assert.NotEmpty(csrf);
        using HttpRequestMessage xenonSave = new(HttpMethod.Post, "/Xenon/Preferences")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = csrf,
                ["blur"] = "nudity"
            })
        };
        xenonSave.Headers.Add("X-Test-Did", s_administrator.Value);
        xenonSave.Headers.Add("Cookie", string.Join("; ", page.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';')[0])));
        using HttpResponseMessage xenonSaved = await fixture.Client.SendAsync(xenonSave, token);
        Assert.Equal(HttpStatusCode.Redirect, xenonSaved.StatusCode);
        using HttpResponseMessage reloaded = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.setting.listOptions",
            query: "?scope=personal&keys=dev.idunno.labeler.mediaPreferences");
        Assert.Equal(HttpStatusCode.OK, reloaded.StatusCode);
        using JsonDocument reloadedBody = JsonDocument.Parse(await reloaded.Content.ReadAsByteArrayAsync(token));
        JsonElement reloadedFilters = Assert.Single(reloadedBody.RootElement.GetProperty("options").EnumerateArray())
            .GetProperty("value").GetProperty("graphicMediaPrefs");
        Assert.True(reloadedFilters.GetProperty("graphic-pref-blur-nudity").GetBoolean());
        Assert.False(reloadedFilters.GetProperty("graphic-pref-blur-porn").GetBoolean());
    }

    [Theory]
    [InlineData("", 0, HttpStatusCode.OK)]
    [InlineData("?scope=instance", 0, HttpStatusCode.OK)]
    [InlineData("?scope=personal", 1, HttpStatusCode.OK)]
    [InlineData("?scope=personal&prefix=dev.idunno", 1, HttpStatusCode.OK)]
    [InlineData("?scope=personal&prefix=other", 0, HttpStatusCode.OK)]
    [InlineData("?scope=personal&keys=dev.idunno.other", 0, HttpStatusCode.OK)]
    [InlineData("?scope=personal&prefix=dev.idunno&keys=invalid", 1, HttpStatusCode.OK)]
    [InlineData("?scope=personal&keys=invalid", 0, HttpStatusCode.BadRequest)]
    [InlineData("?scope=wrong", 0, HttpStatusCode.BadRequest)]
    [InlineData("?scope=personal&cursor=invalid", 0, HttpStatusCode.BadRequest)]
    [InlineData("?scope=personal&limit=0", 0, HttpStatusCode.BadRequest)]
    [InlineData("?scope=personal&limit=101", 0, HttpStatusCode.BadRequest)]
    [InlineData("?scope=personal&did=did:plc:another", 0, HttpStatusCode.BadRequest)]
    public async Task OzonePersonalMediaPreferencesListIsNarrowlyFiltered(string query, int count, HttpStatusCode expected)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, remoteEnabled: true, ozone: true);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.setting.listOptions",
            query: query);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            Assert.Equal(count, document.RootElement.GetProperty("options").GetArrayLength());
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
    }

    [Theory]
    [InlineData("""{"key":"dev.idunno.other","scope":"personal","value":{}}""")]
    [InlineData("""{"key":"dev.idunno.labeler.mediaPreferences","scope":"instance","value":{}}""")]
    [InlineData("""{"key":"dev.idunno.labeler.mediaPreferences","scope":"personal","value":{"graphicMediaPrefs":{"graphic-pref-blur-porn":"true"}}}""")]
    [InlineData("""{"key":"dev.idunno.labeler.mediaPreferences","scope":"personal","value":{"graphicMediaPrefs":{"unknown":true}}}""")]
    [InlineData("""{"key":"dev.idunno.labeler.mediaPreferences","scope":"personal","value":{"graphicMediaPrefs":{"graphic-pref-blur-porn":true,"graphic-pref-blur-porn":false}}}""")]
    [InlineData("""{"key":"dev.idunno.labeler.mediaPreferences","scope":"personal","value":{"graphicMediaPrefs":{}},"did":"did:plc:another"}""")]
    [InlineData("""{"key":"dev.idunno.labeler.mediaPreferences","scope":"personal","value":{}}""")]
    [InlineData("""{"key":"dev.idunno.labeler.mediaPreferences","scope":"personal","value":{"graphicMediaPrefs":{}},"managerRole":"tools.ozone.team.defs#roleAdmin"}""")]
    public async Task OzonePersonalMediaPreferencesRejectUnsupportedWritesWithoutChangingPreferences(string body)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, remoteEnabled: true, ozone: true);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.setting.upsertOption",
            body: body);
        Assert.False(response.IsSuccessStatusCode);
        using HttpResponseMessage reloaded = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.setting.listOptions",
            query: "?scope=personal");
        Assert.Equal(HttpStatusCode.OK, reloaded.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await reloaded.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.True(Assert.Single(document.RootElement.GetProperty("options").EnumerateArray())
            .GetProperty("value").GetProperty("graphicMediaPrefs").GetProperty("graphic-pref-blur-porn").GetBoolean());
    }

    [Theory]
    [InlineData(false, "true")]
    [InlineData(false, "false")]
    [InlineData(true, "true")]
    [InlineData(true, "false")]
    public async Task OzoneSubjectStatusesAcceptIncludeMutedWithoutChangingNativeCaseResults(bool populated, string includeMuted)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        if (populated)
        {
            await VerifyPolicyAsync(fixture);
            using HttpRequestMessage report = ReportRequest(key, fixture.Clock.GetUtcNow());
            using HttpResponseMessage accepted = await fixture.Client.SendAsync(report, token);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        const string query = "?limit=100&sortField=lastReportedAt&sortDirection=desc";
        using HttpResponseMessage baseline = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryStatuses",
            query: query);
        Assert.Equal(HttpStatusCode.OK, baseline.StatusCode);
        string expected = await baseline.Content.ReadAsStringAsync(token);
        using JsonDocument body = JsonDocument.Parse(expected);
        Assert.Equal(populated ? 1 : 0, body.RootElement.GetProperty("subjectStatuses").GetArrayLength());

        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryStatuses",
            query: query + "&includeMuted=" + includeMuted);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadAsStringAsync(token));

        using HttpResponseMessage subject = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryStatuses",
            query: "?subject=did:plc:subject&includeMuted=" + includeMuted + "&limit=1");
        Assert.Equal(HttpStatusCode.OK, subject.StatusCode);
        using JsonDocument subjectBody = JsonDocument.Parse(await subject.Content.ReadAsStringAsync(token));
        Assert.Equal(populated ? 1 : 0, subjectBody.RootElement.GetProperty("subjectStatuses").GetArrayLength());
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory]
    [InlineData("?includeMuted=")]
    [InlineData("?includeMuted=invalid")]
    [InlineData("?includeMuted=1")]
    [InlineData("?includeMuted=true&includeMuted=false")]
    [InlineData("?onlyMuted=invalid")]
    public async Task OzoneSubjectStatusesRejectMalformedMuteFlagsAndUnsupportedFiltersEvenWithoutCases(string query)
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
            method: "tools.ozone.moderation.queryStatuses",
            query: query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("InvalidRequest", error.RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task OzoneLabelsAndSelectedReportResolutionCommitTogether(bool selectedOnly, bool temporary)
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
        await fixture.Clock.PolicyVerificationRegistrations.Reader.ReadAsync(token);
        await VerifyPolicyAsync(fixture);
        DateTimeOffset requestedAt = fixture.Clock.GetUtcNow();
        using HttpRequestMessage reportRequest = ReportRequest(key, fixture.Clock.GetUtcNow());
        using HttpResponseMessage accepted = await fixture.Client.SendAsync(reportRequest, token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using JsonDocument reportBody = JsonDocument.Parse(await accepted.Content.ReadAsByteArrayAsync(token));
        long reportId = reportBody.RootElement.GetProperty("id").GetInt64();
        using HttpRequestMessage anotherReport = ReportRequest(key, fixture.Clock.GetUtcNow());
        using HttpResponseMessage anotherAccepted = await fixture.Client.SendAsync(anotherReport, token);
        Assert.Equal(HttpStatusCode.OK, anotherAccepted.StatusCode);
        using JsonDocument anotherBody = JsonDocument.Parse(await anotherAccepted.Content.ReadAsByteArrayAsync(token));
        long anotherId = anotherBody.RootElement.GetProperty("id").GetInt64();
        string body = $$"""
            {"createdBy":"{{s_administrator}}","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},
             "event":{"$type":"tools.ozone.moderation.defs#modEventLabel","comment":"Reviewed and labeled.","createLabelVals":["spam","porn"],"negateLabelVals":[]
             {{(temporary ? ",\"durationInHours\":24" : "")}}}
             {{(selectedOnly ? ",\"reportAction\":{\"ids\":[" + reportId + "],\"note\":\"Selected report handled.\"}" : "")}}}
            """;
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
        using JsonDocument result = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        Assert.True(result.RootElement.GetProperty("id").GetInt64() > 0);
        Assert.Equal("did:plc:subject", result.RootElement.GetProperty("subject").GetProperty("did").GetString());
        Assert.Equal(2, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        using HttpResponseMessage query = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.report.queryReports",
            query: "?status=closed");
        Assert.Equal(HttpStatusCode.OK, query.StatusCode);
        using JsonDocument reports = JsonDocument.Parse(await query.Content.ReadAsByteArrayAsync(token));
        Assert.Contains(reports.RootElement.GetProperty("reports").EnumerateArray(), item => item.GetProperty("id").GetInt64() == reportId);
        Assert.Equal(selectedOnly ? 1 : 2, reports.RootElement.GetProperty("reports").GetArrayLength());
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller caller = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        ILabelReportStore reportStore = fixture.Services.GetRequiredService<ILabelReportStore>();
        LabelModerationReport handled = (await reportStore.GetReportAsync(caller, reportId, token)).Result!;
        Assert.NotNull(handled.HandledAt);
        Assert.Equal(selectedOnly ? "Selected report handled." : "Reviewed and labeled.", handled.ResolutionReason);
        Assert.Equal(!selectedOnly, (await reportStore.GetReportAsync(caller, anotherId, token)).Result!.HandledAt is not null);
        LabelModerationCase moderationCase = (await fixture.Services.GetRequiredService<ILabelModerationStore>()
            .GetCaseAsync(caller, handled.CaseId, token)).Result!;
        Assert.Equal(!selectedOnly, moderationCase.Status == LabelCaseStatus.Resolved);
        await fixture.PublishAllAsync();
        idunno.Bluesky.Labeler.Storage.LabelStoreState state = await fixture.Store.GetStateAsync(token);
        idunno.Bluesky.Labeler.Storage.LabelPage labels = await fixture.Store.ReplayAsync(
            streamId: state.StreamId,
            after: 0,
            through: state.PublishedThrough,
            cancellationToken: token);
        Assert.Equal(2, labels.Labels.Count);
        Assert.All(labels.Labels, label => Assert.Equal(
            temporary ? requestedAt.AddDays(1) : (DateTimeOffset?)null,
            label.SignedLabel.Label.ExpiresAt));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OzoneEmptyOrInvalidLabelDecisionsDoNotHandleReports(bool invalid)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, remoteEnabled: true, ozone: true);
        await VerifyPolicyAsync(fixture);
        using HttpRequestMessage reportRequest = ReportRequest(key, fixture.Clock.GetUtcNow());
        using HttpResponseMessage accepted = await fixture.Client.SendAsync(reportRequest, token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using JsonDocument reportBody = JsonDocument.Parse(await accepted.Content.ReadAsByteArrayAsync(token));
        long reportId = reportBody.RootElement.GetProperty("id").GetInt64();
        string body = $$"""
            {"createdBy":"{{s_administrator}}","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},
             "event":{"$type":"tools.ozone.moderation.defs#modEventLabel",
             "createLabelVals":{{(invalid ? "[\"spam\",\"unlisted\"]" : "[]")}},"negateLabelVals":[]}
            }
            """;
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: body);
        Assert.Equal(!invalid, response.IsSuccessStatusCode);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller caller = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        LabelModerationReport retained = (await fixture.Services.GetRequiredService<ILabelReportStore>()
            .GetReportAsync(caller, reportId, token)).Result!;
        Assert.Null(retained.HandledAt);
        Assert.NotEqual(LabelCaseStatus.Resolved, (await fixture.Services.GetRequiredService<ILabelModerationStore>()
            .GetCaseAsync(caller, retained.CaseId, token)).Result!.Status);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
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
        Assert.True(reportJson.RootElement.TryGetProperty("subjectStatus", out _));
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
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OzoneReportResolutionPreservesAnUnreviewedOwnerAppeal(bool acknowledge)
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
        Assert.True(issued.StatusCode == HttpStatusCode.OK, await issued.Content.ReadAsStringAsync(token));
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
        Assert.True(accepted.StatusCode == HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync(token));
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelServiceResult<LabelAdministrationCaller> authentication = await team.AuthenticateLocalAsync("uid:0", token);
        Assert.True(authentication.Succeeded);
        ILabelReportStore reports = fixture.Services.GetRequiredService<ILabelReportStore>();
        LabelServiceResult<IReadOnlyList<LabelModerationReport>> reportPage = await reports.GetReportsAsync(
            caller: authentication.Result,
            cancellationToken: token);
        Assert.True(reportPage.Succeeded);
        LabelModerationReport report = Assert.Single(reportPage.Result);
        string close = acknowledge ? """
            {"createdBy":"did:plc:administration-actor","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:reporter"},
             "event":{"$type":"tools.ozone.moderation.defs#modEventAcknowledge","comment":"Report acknowledged, appeal awaits review."}}
            """ : $$"""
            {"reportId":{{report.Id}},"activity":{"$type":"tools.ozone.report.defs#closeActivity"},"internalNote":"Report acknowledged, appeal awaits review."}
            """;
        using HttpResponseMessage closed = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: acknowledge ? "tools.ozone.moderation.emitEvent" : "tools.ozone.report.createActivity",
            body: close);
        Assert.True(closed.StatusCode == HttpStatusCode.OK, await closed.Content.ReadAsStringAsync(token));
        LabelModerationReport handled = (await reports.GetReportAsync(authentication.Result, report.Id, token)).Result!;
        Assert.NotNull(handled.HandledAt);
        Assert.Equal("Report acknowledged, appeal awaits review.", handled.ResolutionReason);
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
            Assert.True(acknowledged.StatusCode == HttpStatusCode.OK, await acknowledged.Content.ReadAsStringAsync(token));
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

    /// <summary>Extracts the unchanged antiforgery field from the preferences form.</summary>
    [System.Text.RegularExpressions.GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial System.Text.RegularExpressions.Regex PreferencesAntiforgeryRegex();
}
