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
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public partial class ModerationTests
{
    [Theory]
    [InlineData("false", false, 1)]
    [InlineData("true", false, 2)]
    [InlineData("true", true, 0)]
    public async Task OzonePanelHistoryFiltersAccountRecordsAndStrikeEvents(
        string includeAllUserRecords,
        bool withStrike,
        int expected)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key, identity: identity, remoteEnabled: true, ozone: true);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        ILabelModerationStore cases = fixture.Services.GetRequiredService<ILabelModerationStore>();
        LabelReportSubject[] subjects =
        [
            LabelReportSubject.Account(new("did:plc:subject")),
            LabelReportSubject.Record(new("at://did:plc:subject/app.bsky.feed.post/one"), new(Cid)),
            LabelReportSubject.Account(new("did:plc:other"))
        ];
        for (int index = 0; index < subjects.Length; index++)
        {
            LabelServiceResult<LabelCaseReceipt> opened = await cases.ChangeCaseAsync(
                caller: administrator,
                change: new(
                    operationId: "panel-history-" + index,
                    subject: subjects[index],
                    action: LabelCaseAction.Open,
                    reason: "Retain a subject history entry."),
                cancellationToken: token);
            Assert.True(opened.Succeeded, opened.Error?.Message);
        }
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.queryEvents",
            query: "?subject=did:plc:subject&includeAllUserRecords=" + includeAllUserRecords +
                "&withStrike=" + withStrike.ToString().ToLowerInvariant() +
                "&limit=25&createdAfter=2022-11-01T07:00:00.000Z&createdBefore=2099-01-01T00:00:00.000Z");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(expected, body.RootElement.GetProperty("events").GetArrayLength());
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory]
    [InlineData("?includeAllUserRecords=True")]
    [InlineData("?withStrike=1")]
    [InlineData("?includeAllUserRecords=true&includeAllUserRecords=false")]
    [InlineData("?subject=invalid")]
    [InlineData("?createdAfter=invalid")]
    [InlineData("?hasComment=invalid")]
    public async Task OzonePanelHistoryValidatesFiltersEvenWithoutEvents(string query)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key, identity: identity, remoteEnabled: true, ozone: true);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture, key: key, method: "tools.ozone.moderation.queryEvents", query: query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OzonePanelHydratesBatchSubjects(bool records)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key, identity: identity, remoteEnabled: true, ozone: true);
        string subject = records ? "at://did:plc:reporter/app.bsky.feed.post/3loqo3h6jbb2g" : "did:plc:reporter";
        string parameter = records ? "uris" : "dids";
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: records ? "tools.ozone.moderation.getRecords" : "tools.ozone.moderation.getRepos",
            query: "?" + parameter + "=" + Uri.EscapeDataString(subject));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        JsonElement view = Assert.Single(body.RootElement.GetProperty(records ? "records" : "repos").EnumerateArray());
        Assert.Equal(subject, view.GetProperty(records ? "uri" : "did").GetString());
        Assert.Equal(records
            ? "tools.ozone.moderation.defs#recordViewDetail"
            : "tools.ozone.moderation.defs#repoViewDetail", view.GetProperty("$type").GetString());
    }

    [Fact]
    public async Task OzonePanelBatchHydrationRetainsAnExplicitDeletedRecordResult()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()) { MissingSubjectRecord = true });
        await using TestServerBuilder fixture = await CreateAsync(
            key: key, identity: identity, remoteEnabled: true, ozone: true);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "tools.ozone.moderation.getRecords",
            query: "?uris=at%3A%2F%2Fdid%3Aplc%3Areporter%2Fapp.bsky.feed.post%2F3loqo3h6jbb2g");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        JsonElement record = Assert.Single(body.RootElement.GetProperty("records").EnumerateArray());
        Assert.Equal("tools.ozone.moderation.defs#recordViewNotFound", record.GetProperty("$type").GetString());
    }

    [Theory]
    [InlineData("tools.ozone.moderation.getRepos", "")]
    [InlineData("tools.ozone.moderation.getRepos", "?dids=invalid")]
    [InlineData("tools.ozone.moderation.getRecords", "?uris=did:plc:subject")]
    [InlineData("tools.ozone.setting.listOptions", "?scope=invalid")]
    [InlineData("tools.ozone.setting.listOptions", "?cursor=invalid")]
    [InlineData("tools.ozone.setting.listOptions", "?keys=invalid")]
    [InlineData("app.bsky.actor.getProfile", "?actor=invalid%20actor")]
    [InlineData("app.bsky.feed.getPostThread", "")]
    [InlineData("app.bsky.feed.getPostThread", "?uri=https://example.com/post")]
    [InlineData("app.bsky.feed.getPostThread", "?uri=at://did:plc:subject/app.bsky.feed.post")]
    [InlineData("app.bsky.feed.getPostThread", "?uri=at://did:plc:subject/app.bsky.actor.profile/self")]
    [InlineData("app.bsky.feed.getPostThread", "?uri=at://did:plc:subject/app.bsky.feed.post/one&depth=-1")]
    [InlineData("app.bsky.feed.getPostThread", "?uri=at://did:plc:subject/app.bsky.feed.post/one&parentHeight=1001")]
    [InlineData("app.bsky.feed.getPostThread", "?uri=at://did:plc:subject/app.bsky.feed.post/one&depth=9999999999999999")]
    [InlineData("app.bsky.feed.getPostThread", "?uri=at://did:plc:subject/app.bsky.feed.post/one&depth=no")]
    [InlineData("app.bsky.feed.getPostThread", "?uri=at://did:plc:subject/app.bsky.feed.post/one&depth=0&depth=1")]
    [InlineData("app.bsky.feed.getPostThread", "?uri=at://did:plc:subject/app.bsky.feed.post/one&host=https://example.com")]
    public async Task OzonePanelReadsRejectInvalidInput(string method, string query)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key, identity: identity, remoteEnabled: true, ozone: true);
        using HttpResponseMessage response = await SendOzoneAsync(fixture: fixture, key: key, method: method, query: query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?scope=instance&keys=tools.ozone.setting.policyList&keys=tools.ozone.setting.severityLevels")]
    [InlineData("?scope=personal&prefix=queue&limit=100")]
    public async Task OzonePanelSettingsReadsReportNoConfiguredOzoneOptions(string query)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key, identity: identity, remoteEnabled: true, ozone: true);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture, key: key, method: "tools.ozone.setting.listOptions", query: query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(body.RootElement.GetProperty("options").EnumerateArray());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task OzonePanelProfileEnrichmentDoesNotForwardServiceCredentials(HttpStatusCode upstreamStatus)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        ProfilePanelHandler handler = new(new DeclarationHandler(key.ExportPublicKeyMultibase()), upstreamStatus);
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(
            key: key, identity: identity, remoteEnabled: true, ozone: true);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture, key: key, method: "app.bsky.actor.getProfile", query: "?actor=did:plc:subject");
        Assert.Equal(upstreamStatus, response.StatusCode);
        Assert.Equal(1, handler.Reads);
        if (upstreamStatus == HttpStatusCode.OK)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            Assert.Equal("did:plc:subject", body.RootElement.GetProperty("did").GetString());
        }
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"did":"did:plc:other","handle":"other.example"}""")]
    [InlineData("""{"did":"did:plc:subject"}""")]
    public async Task OzonePanelInvalidUpstreamProfilesAreDependencyFailures(string profile)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new ProfilePanelHandler(
            inner: new DeclarationHandler(key.ExportPublicKeyMultibase()),
            status: HttpStatusCode.OK,
            profile: profile));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "app.bsky.actor.getProfile",
            query: "?actor=did:plc:subject");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Theory]
    [InlineData("app.bsky.actor.getProfile", "?actor=did:plc:subject")]
    [InlineData("app.bsky.feed.getPostThread", "?uri=at://did:plc:subject/app.bsky.feed.post/one&depth=0")]
    [InlineData("tools.ozone.team.listOnlineMembers", "?limit=100")]
    [InlineData("tools.ozone.setting.listOptions", "")]
    [InlineData("tools.ozone.moderation.getRepos", "?dids=did:plc:subject")]
    [InlineData("tools.ozone.moderation.getRecords", "?uris=at://did:plc:subject/app.bsky.feed.post/one")]
    public async Task OzonePanelReadsRequireTheirOwnMethodBoundServiceToken(string method, string query)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: true,
            ozone: true);
        using HttpRequestMessage request = new(HttpMethod.Get, "/xrpc/" + method + query);
        AdministrationTests.AddTrustedProxyHeaders(request);
        request.Headers.Authorization = new(
            "Bearer",
            AdministrationTests.Token(
                key: key,
                now: fixture.Clock.GetUtcNow(),
                audience: TestServerBuilder.s_issuer.Value,
                method: "tools.ozone.server.getConfig"));
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("app.bsky.actor.getProfile", "?actor=did:plc:subject")]
    [InlineData("app.bsky.feed.getPostThread", "?uri=at://did:plc:subject/app.bsky.feed.post/one&depth=0")]
    [InlineData("tools.ozone.team.listOnlineMembers", "?limit=100")]
    [InlineData("tools.ozone.setting.listOptions", "")]
    [InlineData("tools.ozone.moderation.getRepos", "?dids=did:plc:subject")]
    public async Task OzonePanelReadsCannotBypassRemoteAdministrationDisablement(string method, string query)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: false,
            ozone: true);
        fixture.Services.GetRequiredService<IOptionsMonitor<LabelerAdministrationOptions>>()
            .CurrentValue.RemoteMode = RemoteAdministrationMode.Disabled;
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: method,
            query: query);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("app.bsky.feed.defs#threadViewPost", "")]
    [InlineData("app.bsky.feed.defs#threadViewPost", "&depth=0")]
    [InlineData("app.bsky.feed.defs#threadViewPost", "&depth=1000&parentHeight=0")]
    [InlineData("app.bsky.feed.defs#notFoundPost", "&depth=0")]
    [InlineData("app.bsky.feed.defs#blockedPost", "&depth=0")]
    public async Task OzonePostPreviewUsesAnonymousPublicAppView(string type, string parameters)
    {
        const string uri = "at://did:plc:subject/app.bsky.feed.post/one";
        string payload = type == "app.bsky.feed.defs#threadViewPost"
            ? $$$$"""{"thread":{"$type":"{{{{type}}}}","post":{"uri":"{{{{uri}}}}"}}}"""
            : $$$$"""{"thread":{"$type":"{{{{type}}}}","uri":"{{{{uri}}}}"}}""";
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        ThreadPanelHandler handler = new(new DeclarationHandler(key.ExportPublicKeyMultibase()), payload);
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(
            key: key, identity: identity, remoteEnabled: true, ozone: true);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "app.bsky.feed.getPostThread",
            query: "?uri=" + Uri.EscapeDataString(uri) + parameters);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(payload, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("?uri=" + Uri.EscapeDataString(uri) + parameters, handler.Query);
    }

    [Theory]
    [InlineData("not json", HttpStatusCode.OK)]
    [InlineData("oversized", HttpStatusCode.OK)]
    [InlineData("[]", HttpStatusCode.OK)]
    [InlineData("""{"thread":{}}""", HttpStatusCode.OK)]
    [InlineData("""{"thread":{"$type":"app.bsky.feed.defs#unknown","uri":"at://did:plc:subject/app.bsky.feed.post/one"}}""", HttpStatusCode.OK)]
    [InlineData("""{"thread":{"$type":"app.bsky.feed.defs#threadViewPost","post":{"uri":"at://did:plc:other/app.bsky.feed.post/one"}}}""", HttpStatusCode.OK)]
    [InlineData("""{"thread":{"$type":"app.bsky.feed.defs#blockedPost","uri":"at://did:plc:other/app.bsky.feed.post/one"}}""", HttpStatusCode.OK)]
    [InlineData("", HttpStatusCode.ServiceUnavailable)]
    public async Task OzonePostPreviewRejectsInvalidOrFailedDependencies(string payload, HttpStatusCode status)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new ThreadPanelHandler(
            inner: new DeclarationHandler(key.ExportPublicKeyMultibase()),
            payload: payload == "oversized" ? new string(' ', 1024 * 1024 + 1) : payload,
            status: status));
        await using TestServerBuilder fixture = await CreateAsync(
            key: key, identity: identity, remoteEnabled: true, ozone: true);
        using HttpResponseMessage response = await SendOzoneAsync(
            fixture: fixture,
            key: key,
            method: "app.bsky.feed.getPostThread",
            query: "?uri=at://did:plc:subject/app.bsky.feed.post/one&depth=0");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    /// <summary>Intercepts thread enrichment and checks that only the requested public read is made.</summary>
    private sealed class ThreadPanelHandler(
        HttpMessageHandler inner,
        string payload,
        HttpStatusCode status = HttpStatusCode.OK) : DelegatingHandler(inner)
    {
        /// <summary>Gets the forwarded, validated public query.</summary>
        internal string? Query { get; private set; }

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host != "public.api.bsky.app")
            {
                return base.SendAsync(request, cancellationToken);
            }
            Assert.Equal("/xrpc/app.bsky.feed.getPostThread", request.RequestUri.AbsolutePath);
            Assert.Null(request.Headers.Authorization);
            Assert.False(request.Headers.Contains("atproto-proxy"));
            Assert.False(request.Headers.Contains("Cookie"));
            Query = request.RequestUri.Query;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>Intercepts public profile enrichment while retaining the fixture's independent identity verifier.</summary>
    private sealed class ProfilePanelHandler(
        HttpMessageHandler inner,
        HttpStatusCode status,
        string? profile = null) : DelegatingHandler(inner)
    {
        /// <summary>Gets the number of anonymous public profile requests.</summary>
        internal int Reads { get; private set; }

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host != "public.api.bsky.app")
            {
                return base.SendAsync(request, cancellationToken);
            }
            Assert.Equal("/xrpc/app.bsky.actor.getProfile", request.RequestUri.AbsolutePath);
            Assert.Null(request.Headers.Authorization);
            if (request.Headers.TryGetValues("atproto-proxy", out IEnumerable<string>? proxies))
            {
                Assert.DoesNotContain(proxies, proxy => proxy.Contains("atproto_labeler", StringComparison.Ordinal));
            }
            Reads++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(
                    status == HttpStatusCode.OK
                        ? profile ?? """{"did":"did:plc:subject","handle":"subject.example","displayName":"Subject"}"""
                        : """{"error":"InternalServerError"}""",
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}
