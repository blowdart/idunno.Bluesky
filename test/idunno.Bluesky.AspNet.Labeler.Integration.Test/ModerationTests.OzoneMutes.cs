// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public partial class ModerationTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("?includeMuted=false", 0)]
    [InlineData("?includeMuted=true", 1)]
    [InlineData("?onlyMuted=true", 0)]
    [InlineData("?includeMuted=true&onlyMuted=true", 1)]
    [InlineData("?includeMuted=true&onlyMuted=false", 1)]
    public async Task OzoneSubjectMutesFilterStatusesWithoutChangingReviewStateOrLabels(string query, int expected)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key, identity: identity, remoteEnabled: true, ozone: true);
        await VerifyPolicyAsync(fixture);
        using HttpRequestMessage report = ReportRequest(key, fixture.Clock.GetUtcNow());
        using HttpResponseMessage accepted = await fixture.Client.SendAsync(report, token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        string authenticated = $$"""
            {"createdBy":"{{s_administrator}}","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},
             "event":{"$type":"tools.ozone.moderation.defs#modEventMute","durationInHours":6}
            }
            """;
        using HttpResponseMessage muted = await SendOzoneAsync(
            fixture: fixture, key: key, method: "tools.ozone.moderation.emitEvent", body: authenticated);
        Assert.Equal(HttpStatusCode.OK, muted.StatusCode);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture, key: key, method: "tools.ozone.moderation.queryStatuses", query: query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        JsonElement[] statuses = [.. document.RootElement.GetProperty("subjectStatuses").EnumerateArray()];
        Assert.Equal(expected, statuses.Length);
        if (expected != 0)
        {
            JsonElement status = Assert.Single(statuses);
            Assert.Equal(fixture.Clock.GetUtcNow().AddHours(6), status.GetProperty("muteUntil").GetDateTimeOffset());
            Assert.Equal(s_administrator.Value, status.GetProperty("lastReviewedBy").GetString());
            Assert.Equal("tools.ozone.moderation.defs#reviewOpen", status.GetProperty("reviewState").GetString());
        }
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Fact]
    public async Task OzoneMutedReporterReportsRemainInspectableWithoutCreatingCasesAndStayMutedAfterUnmute()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key, identity: identity, remoteEnabled: true, ozone: true);
        await VerifyPolicyAsync(fixture);
        foreach (bool unmute in new[] { false, true })
        {
            string body = $$"""
                {"createdBy":"{{s_administrator}}",
                 "subject":{"$type":"com.atproto.admin.defs#repoRef","did":"{{s_reporter}}"},
                 "event":{"$type":"tools.ozone.moderation.defs#modEvent{{(unmute ? "UnmuteReporter" : "MuteReporter")}}"}
                }
                """;
            using HttpResponseMessage changed = await SendOzoneAsync(
                fixture: fixture, key: key, method: "tools.ozone.moderation.emitEvent", body: body);
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            if (!unmute)
            {
                using HttpRequestMessage report = ReportRequest(key, fixture.Clock.GetUtcNow());
                using HttpResponseMessage accepted = await fixture.Client.SendAsync(report, token);
                Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            }
            using HttpResponseMessage mutedReports = await SendOzoneAsync(
                fixture: fixture, key: key, method: "tools.ozone.report.queryReports", query: "?status=open&isMuted=true");
            Assert.Equal(HttpStatusCode.OK, mutedReports.StatusCode);
            using JsonDocument document = JsonDocument.Parse(await mutedReports.Content.ReadAsByteArrayAsync(token));
            JsonElement retained = Assert.Single(document.RootElement.GetProperty("reports").EnumerateArray());
            Assert.True(retained.GetProperty("isMuted").GetBoolean());
            Assert.False(retained.TryGetProperty("subjectStatus", out _));
            long id = retained.GetProperty("id").GetInt64();
            using HttpResponseMessage detail = await SendOzoneAsync(
                fixture: fixture, key: key, method: "tools.ozone.report.getReport", query: "?id=" + id);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using HttpResponseMessage events = await SendOzoneAsync(
                fixture: fixture, key: key, method: "tools.ozone.moderation.queryEvents",
                query: "?types=tools.ozone.moderation.defs%23modEventReport");
            Assert.Equal(HttpStatusCode.OK, events.StatusCode);
            using JsonDocument eventBody = JsonDocument.Parse(await events.Content.ReadAsByteArrayAsync(token));
            Assert.Equal(retained.GetProperty("eventId").GetInt64(),
                Assert.Single(eventBody.RootElement.GetProperty("events").EnumerateArray()).GetProperty("id").GetInt64());
            using HttpResponseMessage activities = await SendOzoneAsync(
                fixture: fixture, key: key, method: "tools.ozone.report.listActivities", query: "?reportId=" + id);
            Assert.Equal(HttpStatusCode.OK, activities.StatusCode);
            using JsonDocument activityBody = JsonDocument.Parse(await activities.Content.ReadAsByteArrayAsync(token));
            Assert.Empty(activityBody.RootElement.GetProperty("activities").EnumerateArray());
            using HttpResponseMessage ordinary = await SendOzoneAsync(
                fixture: fixture, key: key, method: "tools.ozone.report.queryReports", query: "?status=open&isMuted=false");
            using JsonDocument ordinaryBody = JsonDocument.Parse(await ordinary.Content.ReadAsByteArrayAsync(token));
            Assert.Empty(ordinaryBody.RootElement.GetProperty("reports").EnumerateArray());
        }
        LabelServiceResult<LabelAdministrationCaller> authenticated = await ((ILabelAdministrationStore)fixture.Store).AuthenticateLocalAsync("uid:0", token);
        Assert.True(authenticated.Succeeded);
        LabelAdministrationCaller caller = authenticated.Result;
        LabelServiceResult<IReadOnlyList<LabelModerationCase>> cases = await ((ILabelModerationStore)fixture.Store).GetCasesAsync(
            caller: caller,
            cancellationToken: token);
        Assert.True(cases.Succeeded);
        Assert.Empty(cases.Result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(-1)]
    [InlineData(2161)]
    public async Task OzoneMuteRejectsNonstandardOrPermanentDurationsWithoutRetainingAnEvent(int duration)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key, identity: identity, remoteEnabled: true, ozone: true);
        await VerifyPolicyAsync(fixture);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.emitEvent",
            body: $$"""
                {"createdBy":"{{s_administrator}}","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},
                 "event":{"$type":"tools.ozone.moderation.defs#modEventMute","durationInHours":{{duration}}}
                }
                """);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using HttpResponseMessage history = await SendOzoneAsync(
            fixture: fixture, key: key, method: "tools.ozone.moderation.queryEvents");
        using JsonDocument historyBody = JsonDocument.Parse(await history.Content.ReadAsByteArrayAsync(token));
        Assert.Empty(historyBody.RootElement.GetProperty("events").EnumerateArray());
    }
}
