// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
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
    [InlineData("", 5)]
    [InlineData("?subjectType=account", 2)]
    [InlineData("?subjectType=record", 3)]
    [InlineData("?collections=app.bsky.feed.post", 2)]
    [InlineData("?collections=app.bsky.feed.post&collections=app.bsky.graph.list", 3)]
    [InlineData("?collections=app.bsky.actor.profile", 0)]
    [InlineData("?subjectType=account&collections=app.bsky.feed.post", 2)]
    [InlineData("?subject=did:plc:reporter", 1)]
    [InlineData("?subject=did:plc:reporter&includeAllUserRecords=false", 1)]
    [InlineData("?subject=did:plc:reporter&includeAllUserRecords=true", 4)]
    [InlineData("?subject=did:plc:reporter&subjectType=record", 1)]
    [InlineData("?subject=did:plc:reporter&includeAllUserRecords=true&subjectType=record", 4)]
    [InlineData("?subject=did:plc:reporter&includeAllUserRecords=true&collections=app.bsky.graph.list", 1)]
    [InlineData("?subject=did:plc:reporter&includeAllUserRecords=true&subjectType=account&collections=app.bsky.feed.post", 4)]
    [InlineData("?subject=at://did:plc:reporter/app.bsky.feed.post/one", 2)]
    [InlineData("?subject=at://did:plc:reporter/app.bsky.feed.post/one&includeAllUserRecords=true", 4)]
    [InlineData("?includeAllUserRecords=true&subjectType=record", 3)]
    [InlineData("?appealed=false", 5)]
    [InlineData("?appealed=true", 0)]
    [InlineData("?lastReviewedBy=did:plc:administration-actor", 0)]
    public async Task OzoneStatusFiltersKeepMixedSubjectsAndEverySubmittedRecordVersion(string query, int expected)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        LabelAdministrationCaller administrator = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .AuthenticateLocalAsync("uid:0", token)).Result!;
        ILabelModerationStore cases = fixture.Services.GetRequiredService<ILabelModerationStore>();
        const string secondCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
        const string uri = "at://did:plc:reporter/app.bsky.feed.post/one";
        LabelReportSubject[] subjects =
        [
            LabelReportSubject.Account(s_reporter),
            LabelReportSubject.Account(new("did:plc:other")),
            LabelReportSubject.Record(new(uri), new(Cid)),
            LabelReportSubject.Record(new(uri), new(secondCid)),
            LabelReportSubject.Record(new("at://did:plc:reporter/app.bsky.graph.list/one"), new(Cid))
        ];
        for (int index = 0; index < subjects.Length; index++)
        {
            LabelServiceResult<LabelCaseReceipt> opened = await cases.ChangeCaseAsync(
                caller: administrator,
                change: new(
                    operationId: "status-subject-" + index,
                    subject: subjects[index],
                    action: LabelCaseAction.Open,
                    reason: "Retain exact submitted subject evidence."),
                cancellationToken: token);
            Assert.True(opened.Succeeded, opened.Error?.Message);
        }
        using JsonDocument result = await QueryStatusFilterAsync(fixture, key, query);
        JsonElement[] statuses = [.. result.RootElement.GetProperty("subjectStatuses").EnumerateArray()];
        Assert.Equal(expected, statuses.Length);
        foreach (JsonElement status in statuses)
        {
            JsonElement reference = status.GetProperty("subject");
            if (reference.TryGetProperty("uri", out JsonElement record))
            {
                Assert.Contains(subjects, subject => subject.Subject == record.GetString() &&
                    subject.Cid?.Value == reference.GetProperty("cid").GetString());
            }
            else
            {
                Assert.Contains(subjects, subject => subject.RecordUri is null && subject.Subject == reference.GetProperty("did").GetString());
            }
        }
        JsonElement[] versions = [.. statuses.Where(status =>
            status.GetProperty("subject").TryGetProperty("uri", out JsonElement record) && record.GetString() == uri)];
        if (versions.Length != 0)
        {
            Assert.Equal(2, versions.Length);
            Assert.Equal(
                new[] { Cid, secondCid }.Order(StringComparer.Ordinal),
                versions.Select(status => status.GetProperty("subject").GetProperty("cid").GetString()).Order(StringComparer.Ordinal));
            Assert.Equal(2, versions.Select(status => status.GetProperty("id").GetInt64()).Distinct().Count());
        }
    }

    [Theory]
    [InlineData("?subject=")]
    [InlineData("?subject=subject.example")]
    [InlineData("?subject=at://subject.example/app.bsky.feed.post/one")]
    [InlineData("?subject=at://did:plc:reporter")]
    [InlineData("?subjectType=")]
    [InlineData("?subjectType=unknown")]
    [InlineData("?subjectType=account&subjectType=record")]
    [InlineData("?collections=")]
    [InlineData("?collections=not-an-nsid")]
    [InlineData("?subjectType=account&collections=not-an-nsid")]
    [InlineData("?collections=app.bsky.feed.post&collections=bad")]
    [InlineData("?includeAllUserRecords=")]
    [InlineData("?includeAllUserRecords=True")]
    [InlineData("?includeAllUserRecords=1")]
    [InlineData("?includeAllUserRecords=true&includeAllUserRecords=false")]
    [InlineData("?appealed=")]
    [InlineData("?appealed=FALSE")]
    [InlineData("?appealed=0")]
    [InlineData("?appealed=%20true")]
    [InlineData("?appealed=true&appealed=false")]
    [InlineData("?lastReviewedBy=")]
    [InlineData("?lastReviewedBy=moderator.example")]
    [InlineData("?lastReviewedBy=did:plc:reporter&lastReviewedBy=did:plc:other")]
    [InlineData("?reviewState=unknown")]
    public async Task OzoneStatusFiltersValidateAllValuesBeforeReadingAnEmptyCasePage(string query)
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
        using JsonDocument error = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("InvalidRequest", error.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task OzoneStatusFiltersBoundRepeatedCollectionsAndRejectUnsupportedConversationStatuses()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        string query = "?" + string.Join('&', Enumerable.Repeat("collections=app.bsky.feed.post", 20));
        using JsonDocument accepted = await QueryStatusFilterAsync(fixture, key, query);
        Assert.Empty(accepted.RootElement.GetProperty("subjectStatuses").EnumerateArray());
        using HttpResponseMessage excessive = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryStatuses",
            query: query + "&collections=app.bsky.graph.list");
        Assert.Equal(HttpStatusCode.BadRequest, excessive.StatusCode);
        using HttpResponseMessage conversation = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryStatuses",
            query: "?subjectType=conversation");
        Assert.Equal(HttpStatusCode.NotImplemented, conversation.StatusCode);
        using JsonDocument error = JsonDocument.Parse(await conversation.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("NotImplemented", error.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task OzoneLastReviewedByUsesLatestReviewNotOpeningAssignmentOrHistoricalMembership()
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
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        Did second = new("did:plc:second-moderator");
        Assert.True((await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "status-second-moderator",
                action: LabelTeamAction.Enroll,
                target: second,
                reason: "Review filter provenance.",
                role: LabelTeamRole.Moderator),
            cancellationToken: token)).Succeeded);
        LabelAdministrationCaller moderator = (await team.AuthenticateBrowserAsync(second, token)).Result!;
        ILabelModerationStore cases = fixture.Services.GetRequiredService<ILabelModerationStore>();
        LabelServiceResult<LabelCaseReceipt> opened = await cases.ChangeCaseAsync(
            caller: administrator,
            change: new(
                operationId: "review-filter-open",
                subject: LabelReportSubject.Account(s_reporter),
                action: LabelCaseAction.Open,
                reason: "Open without reviewing."),
            cancellationToken: token);
        Assert.True(opened.Succeeded, opened.Error?.Message);
        using (JsonDocument noReview = await QueryStatusFilterAsync(fixture, key, "?lastReviewedBy=" + s_administrator.Value))
        {
            Assert.Empty(noReview.RootElement.GetProperty("subjectStatuses").EnumerateArray());
        }
        LabelModerationCase current = opened.Result!.Case;
        for (int index = 0; index < 103; index++)
        {
            LabelCaseAction action = index == 102 ? LabelCaseAction.Assign : LabelCaseAction.Note;
            LabelAdministrationCaller actor = index == 101 ? moderator : administrator;
            LabelServiceResult<LabelCaseReceipt> changed = await cases.ChangeCaseAsync(
                caller: actor,
                change: new(
                    operationId: "review-filter-" + current.Revision,
                    subject: current.Subject,
                    action: action,
                    reason: "Review filter ordering.",
                    caseId: current.Id,
                    expectedRevision: current.Revision,
                    assignee: action == LabelCaseAction.Assign ? s_administrator : null),
                cancellationToken: token);
            Assert.True(changed.Succeeded, changed.Error?.Message);
            current = changed.Result!.Case;
        }
        using JsonDocument latest = await QueryStatusFilterAsync(fixture, key, "?lastReviewedBy=" + second.Value);
        Assert.Equal(second.Value, Assert.Single(latest.RootElement.GetProperty("subjectStatuses").EnumerateArray())
            .GetProperty("lastReviewedBy").GetString());
        using JsonDocument historical = await QueryStatusFilterAsync(fixture, key, "?lastReviewedBy=" + s_administrator.Value);
        Assert.Empty(historical.RootElement.GetProperty("subjectStatuses").EnumerateArray());
    }

    [Theory]
    [InlineData("?subjectType=account", true)]
    [InlineData("?subject=did:plc:other", true)]
    [InlineData("?collections=app.bsky.graph.list", true)]
    [InlineData("?subjectType=record", false)]
    public async Task OzoneStatusFiltersCountUnresolvedUriOnlyEvidenceOnlyWhenItIsSelected(string query, bool succeeds)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()) { MissingSubjectRecord = true });
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        LabelAdministrationCaller administrator = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .AuthenticateLocalAsync("uid:0", token)).Result!;
        LabelServiceResult<LabelCaseReceipt> opened = await fixture.Services.GetRequiredService<ILabelModerationStore>().ChangeCaseAsync(
            caller: administrator,
            change: new(
                operationId: "status-uri-only",
                subject: LabelReportSubject.Record(new("at://did:plc:reporter/app.bsky.feed.post/3loqo3h6jbb2g")),
                action: LabelCaseAction.Open,
                reason: "Retain the absence of a submitted record version."),
            cancellationToken: token);
        Assert.True(opened.Succeeded, opened.Error?.Message);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryStatuses",
            query: query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument result = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        if (succeeds)
        {
            Assert.Empty(result.RootElement.GetProperty("subjectStatuses").EnumerateArray());
        }
        else
        {
            Assert.Empty(result.RootElement.GetProperty("subjectStatuses").EnumerateArray());
            Assert.Equal(1, result.RootElement.GetProperty("unresolvedRecordCount").GetInt32());
            Assert.True(response.Headers.Contains("Warning"));
        }
    }

    [Fact]
    public async Task OzoneLabelOnlyEventsDoNotClaimAReviewButAcknowledgmentsDo()
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
        LabelAdministrationCaller administrator = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .AuthenticateLocalAsync("uid:0", token)).Result!;
        LabelServiceResult<LabelCaseReceipt> opened = await fixture.Services.GetRequiredService<ILabelModerationStore>().ChangeCaseAsync(
            caller: administrator,
            change: new(
                operationId: "label-only-status-case",
                subject: LabelReportSubject.Account(s_reporter),
                action: LabelCaseAction.Open,
                reason: "Retain a case for independent Ozone review provenance."),
            cancellationToken: token);
        Assert.True(opened.Succeeded, opened.Error?.Message);
        const string label = """
            {"createdBy":"did:plc:administration-actor","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:reporter"},
             "event":{"$type":"tools.ozone.moderation.defs#modEventLabel","createLabelVals":["spam"],"negateLabelVals":[]}}
            """;
        using HttpResponseMessage issued = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: label);
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        using (JsonDocument unreviewed = await QueryStatusFilterAsync(fixture, key, "?lastReviewedBy=" + s_administrator.Value))
        {
            Assert.Empty(unreviewed.RootElement.GetProperty("subjectStatuses").EnumerateArray());
        }
        const string acknowledge = """
            {"createdBy":"did:plc:administration-actor","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:reporter"},
             "event":{"$type":"tools.ozone.moderation.defs#modEventAcknowledge"}}
            """;
        using HttpResponseMessage acknowledged = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: acknowledge);
        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        using JsonDocument reviewed = await QueryStatusFilterAsync(fixture, key, "?lastReviewedBy=" + s_administrator.Value);
        Assert.Equal(s_administrator.Value, Assert.Single(reviewed.RootElement.GetProperty("subjectStatuses").EnumerateArray())
            .GetProperty("lastReviewedBy").GetString());
    }

    [Fact]
    public async Task OzoneAppealedFilterReflectsPendingAppealsUntilAnExplicitAppealDecision()
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
        const string label = """
            {"createdBy":"did:plc:administration-actor","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:reporter"},
             "event":{"$type":"tools.ozone.moderation.defs#modEventLabel","createLabelVals":["spam"],"negateLabelVals":[]}}
            """;
        using HttpResponseMessage issued = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: label);
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        await fixture.PublishAllAsync();
        const string appeal = """
            {"subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:reporter"},
             "action":{"$type":"tools.ozone.inbox.appealActionedSubject#labelRef","val":"spam"},"reason":"Please reconsider."}
            """;
        using HttpResponseMessage appealed = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.inbox.appealActionedSubject",
            body: appeal,
            actor: s_reporter.Value);
        Assert.Equal(HttpStatusCode.OK, appealed.StatusCode);
        using (JsonDocument pending = await QueryStatusFilterAsync(fixture, key, "?appealed=true"))
        {
            Assert.True(Assert.Single(pending.RootElement.GetProperty("subjectStatuses").EnumerateArray()).GetProperty("appealed").GetBoolean());
        }
        using (JsonDocument notPending = await QueryStatusFilterAsync(fixture, key, "?appealed=false"))
        {
            Assert.Empty(notPending.RootElement.GetProperty("subjectStatuses").EnumerateArray());
        }
        LabelAdministrationCaller administrator = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .AuthenticateLocalAsync("uid:0", token)).Result!;
        LabelAppeal retained = Assert.Single((await fixture.Services.GetRequiredService<ILabelAppealStore>()
            .GetAppealsAsync(caller: administrator, cancellationToken: token)).Result!);
        LabelModerationCase current = (await fixture.Services.GetRequiredService<ILabelModerationStore>()
            .GetCaseAsync(administrator, retained.CaseId, token)).Result!;
        AdministrationResponse decision = await fixture.Services.GetRequiredService<AdministrationDispatcher>().DispatchAsync(
            caller: administrator,
            command: new(
                Method: "decideCase",
                OperationId: "resolve-status-filter-appeal",
                Reason: "Review this exact pending appeal.",
                Moderation: new(
                    CaseId: current.Id,
                    ExpectedRevision: current.Revision,
                    PolicyVersion: "v1",
                    Actions: [],
                    AppealIds: [retained.Id],
                    AppealOutcome: LabelAppealStatus.Upheld)),
            token: token);
        Assert.True(decision.Succeeded, decision.Message);
        using JsonDocument resolved = await QueryStatusFilterAsync(fixture, key, "?appealed=false");
        Assert.False(Assert.Single(resolved.RootElement.GetProperty("subjectStatuses").EnumerateArray()).GetProperty("appealed").GetBoolean());
        using JsonDocument stillPending = await QueryStatusFilterAsync(fixture, key, "?appealed=true");
        Assert.Empty(stillPending.RootElement.GetProperty("subjectStatuses").EnumerateArray());
    }

    [Theory]
    [InlineData("subjectType=record")]
    [InlineData("collections=app.bsky.graph.list")]
    [InlineData("includeAllUserRecords=true")]
    [InlineData("lastReviewedBy=did:plc:reporter")]
    [InlineData("appealed=false")]
    public async Task OzoneStatusContinuationsBindEveryAddedFilter(string changedFilter)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        LabelAdministrationCaller administrator = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .AuthenticateLocalAsync("uid:0", token)).Result!;
        ILabelModerationStore cases = fixture.Services.GetRequiredService<ILabelModerationStore>();
        for (int index = 0; index < 3; index++)
        {
            Assert.True((await cases.ChangeCaseAsync(
                caller: administrator,
                change: new(
                    operationId: "status-page-" + index,
                    subject: LabelReportSubject.Account(new("did:plc:page-" + index)),
                    action: LabelCaseAction.Open,
                    reason: "Exercise filter-bound chronological continuation."),
                cancellationToken: token)).Succeeded);
        }
        using JsonDocument first = await QueryStatusFilterAsync(fixture, key, "?limit=1");
        long firstId = Assert.Single(first.RootElement.GetProperty("subjectStatuses").EnumerateArray()).GetProperty("id").GetInt64();
        string continuation = "&cursor=" + Uri.EscapeDataString(first.RootElement.GetProperty("cursor").GetString()!);
        using JsonDocument next = await QueryStatusFilterAsync(fixture, key, "?limit=1" + continuation);
        Assert.NotEqual(firstId, Assert.Single(next.RootElement.GetProperty("subjectStatuses").EnumerateArray()).GetProperty("id").GetInt64());
        using HttpResponseMessage changed = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryStatuses",
            query: "?limit=1&" + changedFilter + continuation);
        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
    }

    /// <summary>Reads the real authenticated status endpoint and retains its exact wire document for assertions.</summary>
    private static async Task<JsonDocument> QueryStatusFilterAsync(TestServerBuilder fixture, LabelSigningKey key, string query)
    {
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryStatuses",
            query: query);
        byte[] body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, System.Text.Encoding.UTF8.GetString(body));
        return JsonDocument.Parse(body);
    }
}
