// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public class ModerationTests
{
    private static readonly Did s_administrator = new("did:plc:administration-actor");
    private static readonly Did s_reporter = new("did:plc:reporter");
    private const string Cid = "bafyreihdwdcefgh4dqkjv67uzcmw7ojee6xedzdetojuzjevtenxquvyku";
    private const string Declaration = """
        {"$type":"app.bsky.labeler.service","createdAt":"2026-01-01T00:00:00Z",
         "policies":{"labelValues":["spam"],"labelValueDefinitions":[]},"subjectTypes":["account","record"]}
        """;
    private const string Report = """
        {"reasonType":"tools.ozone.report.defs#reasonMisleadingSpam","reason":"Please review.",
         "subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"}}
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostedWithdrawalPublishesNegationsBeforeAllowingTheNewDeclaration(bool remoteEnabled)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase());
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            remoteEnabled: remoteEnabled,
            xenonUi: true);
        await fixture.Clock.WithdrawalRegistrations.Reader.ReadAsync(token);
        await fixture.Clock.PolicyVerificationRegistrations.Reader.ReadAsync(token);
        LabelPolicyService verifier = fixture.Services.GetRequiredService<LabelPolicyService>();
        LabelServiceResult<LabelPolicyState> active = await verifier.GetVerifiedStateAsync(cancellationToken: token);
        Assert.True(active.Succeeded, active.Error?.Message);
        LabelPolicyState activeState = active.Result!;
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        await fixture.Store.AppendAsync("initial-label", [new(key.CreateLabel(
            source: TestServerBuilder.s_issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: activeState.DatabaseTime))], cancellationToken: token);
        using JsonDocument declaration = JsonDocument.Parse(Declaration.Replace("[\"spam\"]", "[]", StringComparison.Ordinal));
        AdministrationDispatcher dispatcher = fixture.Services.GetRequiredService<AdministrationDispatcher>();
        AdministrationResponse saved = await dispatcher.DispatchAsync(
            caller: administrator,
            command: new(
                Method: "savePolicy",
                OperationId: "draft-reduced",
                Reason: "Withdraw spam.",
                Moderation: new(
                    PolicyVersion: "v2",
                    Declaration: declaration.RootElement,
                    AllowAccounts: true,
                    AllowRecords: true,
                    Rules: "Retire spam.")),
            token: token);
        Assert.True(saved.Succeeded, saved.Message);
        using HttpRequestMessage confirmationPage = new(HttpMethod.Get, "/Xenon/Policy");
        confirmationPage.Headers.Add("X-Test-Did", s_administrator.Value);
        using HttpResponseMessage confirmation = await fixture.Client.SendAsync(confirmationPage, token);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        string form = await confirmation.Content.ReadAsStringAsync(token);
        const string prefix = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        int start = form.IndexOf(prefix, StringComparison.Ordinal);
        Assert.NotEqual(-1, start);
        string antiforgery = form[(start + prefix.Length)..].Split('"')[0];
        using HttpRequestMessage unconfirmed = new(HttpMethod.Post, "/Xenon/Policy?handler=Publish")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(antiforgery),
                ["version"] = "v2",
                ["revision"] = activeState.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["operationId"] = "unconfirmed-publication",
                ["reason"] = "Must not withdraw without confirmation."
            })
        };
        unconfirmed.Headers.Add("X-Test-Did", s_administrator.Value);
        unconfirmed.Headers.Add("Cookie", string.Join("; ", confirmation.Headers.GetValues("Set-Cookie")
            .Select(cookie => cookie.Split(';')[0])));
        using HttpResponseMessage rejected = await fixture.Client.SendAsync(unconfirmed, token);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Contains("Confirm policy publication before submitting.", await rejected.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        Assert.Equal(LabelPolicyPhase.VerifiedActive, (await fixture.Services.GetRequiredService<ILabelPolicyStore>()
            .ReadPolicyStateAsync(token))!.Phase);
        AdministrationResponse started = await dispatcher.DispatchAsync(
            caller: administrator,
            command: new(
                Method: "beginPolicyPublication",
                OperationId: "publish-reduced",
                Reason: "Confirm permanent withdrawal.",
                Moderation: new(PolicyVersion: "v2", ExpectedRevision: activeState.Revision, PreviousCid: Cid)),
            token: token);
        Assert.True(started.Succeeded, started.Message);
        Assert.Equal("Withdrawing", started.Moderation!.Policy!.Phase);
        using HttpRequestMessage get = new(HttpMethod.Get, "/Xenon/Policy");
        get.Headers.Add("X-Test-Did", s_administrator.Value);
        using HttpResponseMessage page = await fixture.Client.SendAsync(get, token);
        string html = await page.Content.ReadAsStringAsync(token);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Label withdrawal in progress", html, StringComparison.Ordinal);
        Assert.Contains("Removing: spam", html, StringComparison.Ordinal);
        Assert.Contains("completed negations are not automatically undone", html, StringComparison.Ordinal);
        Assert.Contains("Resume cleanup", html, StringComparison.Ordinal);
        Assert.Contains("Last worker activity:", html, StringComparison.Ordinal);
        Assert.Contains("Last progress:", html, StringComparison.Ordinal);
        Assert.Contains("Waiting for worker activity", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Cleanup may be running or paused.", html, StringComparison.Ordinal);
        int reads = handler.DeclarationReads;
        handler.Declaration = declaration.RootElement.GetRawText();
        Assert.False((await verifier.GetVerifiedStateAsync(forceVerification: true, cancellationToken: token)).Succeeded);
        Assert.Equal(reads, handler.DeclarationReads);
        ILabelPolicyStore policies = fixture.Services.GetRequiredService<ILabelPolicyStore>();
        for (int batch = 0; batch < 3; batch++)
        {
            fixture.Clock.Advance(TimeSpan.FromSeconds(5));
            await fixture.Clock.WithdrawalRegistrations.Reader.ReadAsync(token);
            LabelPolicyState state = (await policies.ReadPolicyStateAsync(token))!;
            Assert.Equal(batch == 2 ? LabelPolicyPhase.Publishing : LabelPolicyPhase.Withdrawing, state.Phase);
            if (batch == 0)
            {
                AdministrationResponse status = await dispatcher.DispatchAsync(
                    caller: administrator, command: new(Method: "getPolicy"), token: token);
                JsonElement statusJson = JsonSerializer.SerializeToElement(status, AdministrationJsonContext.Default.AdministrationResponse);
                JsonElement progress = statusJson.GetProperty("moderation").GetProperty("policy").GetProperty("withdrawal");
                DateTimeOffset activityAt = progress.GetProperty("lastActivityAt").GetDateTimeOffset();
                DateTimeOffset progressAt = progress.GetProperty("lastProgressAt").GetDateTimeOffset();
                using HttpRequestMessage progressRequest = new(HttpMethod.Get, "/Xenon/Policy");
                progressRequest.Headers.Add("X-Test-Did", s_administrator.Value);
                using HttpResponseMessage progressPage = await fixture.Client.SendAsync(progressRequest, token);
                Assert.Equal(HttpStatusCode.OK, progressPage.StatusCode);
                string progressHtml = WebUtility.HtmlDecode(await progressPage.Content.ReadAsStringAsync(token));
                Assert.Contains(activityAt.ToString("O"), progressHtml, StringComparison.Ordinal);
                Assert.Contains(progressAt.ToString("O"), progressHtml, StringComparison.Ordinal);
            }
        }
        LabelStoreState stream = await fixture.Store.GetStateAsync(token);
        Assert.Equal(2, stream.IssuedThrough);
        Assert.Equal(stream.IssuedThrough, stream.PublishedThrough);
        LabelDistributionPage labels = await fixture.Store.ReadDistributionAsync(
            streamId: stream.StreamId,
            after: 0,
            through: stream.PublishedThrough,
            uriPatterns: [],
            sources: [],
            cancellationToken: token);
        Assert.True(Assert.Single(labels.Labels).SignedLabel.Label.IsNegationLabel);
        Assert.True((await verifier.GetVerifiedStateAsync(forceVerification: true, cancellationToken: token)).Succeeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VerificationTabUsesSubmissionAloneAndRetainsItsTabAfterFailure(bool unavailable)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase());
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(
            key: key,
            identity: identity,
            xenonUi: true);
        using HttpRequestMessage get = new(HttpMethod.Get, "/Xenon/Policy");
        get.Headers.Add("X-Test-Did", s_administrator.Value);
        using HttpResponseMessage page = await fixture.Client.SendAsync(get, token);
        string html = await page.Content.ReadAsStringAsync(token);
        Assert.True(html.IndexOf(">Verification</a>", StringComparison.Ordinal) > html.IndexOf(">Publication</a>", StringComparison.Ordinal));
        string verification = html[html.IndexOf("<div id=\"policy-verification\"", StringComparison.Ordinal)..];
        Assert.Contains("can activate the policy", verification, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"reason\"", verification, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"confirmed\"", verification, StringComparison.Ordinal);
        const string prefix = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        int start = verification.IndexOf(prefix, StringComparison.Ordinal);
        Assert.NotEqual(-1, start);
        string antiforgery = verification[(start + prefix.Length)..].Split('"')[0];
        if (unavailable)
        {
            handler.Declaration = Declaration.Replace("[\"spam\"]", "[]", StringComparison.Ordinal);
        }
        using HttpRequestMessage post = new(HttpMethod.Post, "/Xenon/Policy?handler=Verify")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(antiforgery)
            })
        };
        post.Headers.Add("X-Test-Did", s_administrator.Value);
        post.Headers.Add("Cookie", string.Join("; ", page.Headers.GetValues("Set-Cookie")
            .Select(cookie => cookie.Split(';')[0])));
        using HttpResponseMessage response = await fixture.Client.SendAsync(post, token);
        Assert.Equal(unavailable ? HttpStatusCode.OK : HttpStatusCode.Redirect, response.StatusCode);
        if (unavailable)
        {
            string failed = await response.Content.ReadAsStringAsync(token);
            Assert.Contains("data-policy-verification-submitted=\"true\"", failed, StringComparison.Ordinal);
            Assert.DoesNotContain("Confirm policy verification before submitting.", failed, StringComparison.Ordinal);
        }
        else
        {
            Assert.EndsWith("#policy-verification", response.Headers.Location!.ToString(), StringComparison.Ordinal);
            Assert.Equal(LabelPolicyPhase.VerifiedActive, (await fixture.Services.GetRequiredService<ILabelPolicyStore>()
                .ReadPolicyStateAsync(token))!.Phase);
        }
    }

    [Fact]
    public async Task HostedWithdrawalResumesFromDurableCheckpointOnRestartWithoutRemoteAdministration()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder original = await CreateAsync(key, identity);
        await original.Clock.WithdrawalRegistrations.Reader.ReadAsync(token);
        LabelPolicyState active = (await original.Services.GetRequiredService<LabelPolicyService>()
            .GetVerifiedStateAsync(cancellationToken: token)).Result!;
        LabelAdministrationCaller administrator = (await original.Services.GetRequiredService<ILabelAdministrationStore>()
            .AuthenticateLocalAsync("uid:0", token)).Result!;
        await original.Store.AppendAsync("initial-label", [new(key.CreateLabel(
            source: TestServerBuilder.s_issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: active.DatabaseTime))], cancellationToken: token);
        using JsonDocument declaration = JsonDocument.Parse(Declaration.Replace("[\"spam\"]", "[]", StringComparison.Ordinal));
        ILabelPolicyStore policies = original.Services.GetRequiredService<ILabelPolicyStore>();
        Assert.True((await policies.SavePolicyAsync(
            caller: administrator,
            operationId: "draft-reduced",
            policy: new(
                version: "v2",
                declaration: declaration.RootElement,
                allowAccounts: true,
                allowRecords: true,
                rules: "Retire spam."),
            reason: "Withdraw spam.",
            cancellationToken: token)).Succeeded);
        Assert.True((await policies.BeginPolicyPublicationAsync(
            caller: administrator,
            operationId: "publish-reduced",
            version: "v2",
            expectedRevision: active.Revision,
            previousCid: new(Cid),
            reason: "Confirm permanent withdrawal.",
            cancellationToken: token)).Succeeded);
        original.Clock.Advance(TimeSpan.FromSeconds(5));
        await original.Clock.WithdrawalRegistrations.Reader.ReadAsync(token);
        LabelPolicyState checkpoint = (await policies.ReadPolicyStateAsync(token))!;
        Assert.Equal(LabelPolicyPhase.Withdrawing, checkpoint.Phase);
        Assert.Equal(1, (await original.Store.GetStateAsync(token)).IssuedThrough);
        await original.StopAsync();

        await using TestServerBuilder restarted = await TestServerBuilder.CreateAsync(
            database: original.DatabasePath,
            configureAdministration: services =>
            {
                services.AddLabelerModeration(issuer: TestServerBuilder.s_issuer, httpClient: identity);
                services.AddLabelerAdministration(new(TestServerBuilder.s_issuer, "labeler_admin"), key, identity);
            },
            remoteAdministrationEnabled: false,
            moderation: true);
        await restarted.Clock.WithdrawalRegistrations.Reader.ReadAsync(token);
        ILabelPolicyStore recoveredPolicies = restarted.Services.GetRequiredService<ILabelPolicyStore>();
        LabelPolicyState recovered = (await recoveredPolicies.ReadPolicyStateAsync(token))!;
        Assert.Equal("publish-reduced", recovered.PublicationOperationId);
        Assert.Equal(checkpoint.Revision + 1, recovered.Revision);
        Assert.Equal(LabelPolicyPhase.Withdrawing, recovered.Phase);
        Assert.Equal(2, (await restarted.Store.GetStateAsync(token)).IssuedThrough);
        restarted.Clock.Advance(TimeSpan.FromSeconds(5));
        await restarted.Clock.WithdrawalRegistrations.Reader.ReadAsync(token);
        Assert.Equal(LabelPolicyPhase.Publishing, (await recoveredPolicies.ReadPolicyStateAsync(token))!.Phase);
        LabelStoreState stream = await restarted.Store.GetStateAsync(token);
        Assert.Equal(2, stream.IssuedThrough);
        Assert.Equal(stream.IssuedThrough, stream.PublishedThrough);
    }

    [Theory]
    [InlineData(10001)]
    [InlineData(15005000)]
    [InlineData(863999999999)]
    public void RegistrationRejectsFractionalMillisecondFreshness(long ticks)
    {
        ServiceCollection services = new();
        services.AddSqliteLabelStorage("Data Source=registration-only.db");
        using HttpClient identity = new();
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddLabelerModeration(
                issuer: TestServerBuilder.s_issuer,
                httpClient: identity,
                options: new() { VerificationFreshness = TimeSpan.FromTicks(ticks) }));
        Assert.Contains(nameof(LabelerModerationOptions.VerificationFreshness), exception.ParamName!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1500)]
    [InlineData(86400000)]
    public void RegistrationAcceptsWholeMillisecondFreshness(int milliseconds)
    {
        ServiceCollection services = new();
        services.AddSqliteLabelStorage("Data Source=registration-only.db");
        using HttpClient identity = new();
        Assert.Same(services, services.AddLabelerModeration(
            issuer: TestServerBuilder.s_issuer,
            httpClient: identity,
            options: new() { VerificationFreshness = TimeSpan.FromMilliseconds(milliseconds) }));
    }

    [Theory]
    [InlineData(1500)]
    [InlineData(86400000)]
    public async Task WholeMillisecondFreshnessPersistsExactlyAndPolicyHealthSucceeds(int milliseconds)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        TimeSpan freshness = TimeSpan.FromMilliseconds(milliseconds);
        await using TestServerBuilder fixture = await CreateAsync(
            key: key, identity: identity, options: new() { VerificationFreshness = freshness });
        LabelPolicyState state = (await fixture.Services.GetRequiredService<ILabelPolicyStore>()
            .ReadPolicyStateAsync(TestContext.Current.CancellationToken))!;
        Assert.Equal(freshness, state.VerificationFreshness);
        Assert.True((await fixture.Services.GetRequiredService<LabelPolicyService>()
            .GetVerifiedStateAsync(cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        using HttpResponseMessage health = await fixture.Client.GetAsync(
            "/health/labeler/moderation", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScheduledVerificationRefreshesActivePolicyOrRecordsDriftWithoutTraffic(bool drift)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase());
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        await fixture.Clock.PolicyVerificationRegistrations.Reader.ReadAsync(token);
        LabelPolicyService verifier = fixture.Services.GetRequiredService<LabelPolicyService>();
        Assert.True((await verifier.GetVerifiedStateAsync(cancellationToken: token)).Succeeded);
        ILabelPolicyStore store = fixture.Services.GetRequiredService<ILabelPolicyStore>();
        LabelPolicyState before = (await store.ReadPolicyStateAsync(token))!;
        if (drift)
        {
            handler.Declaration = Declaration.Replace("\"spam\"", "\"scam\"", StringComparison.Ordinal);
        }
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        await fixture.Clock.PolicyVerificationRegistrations.Reader.ReadAsync(token);
        LabelPolicyState after = (await store.ReadPolicyStateAsync(token))!;
        Assert.Equal(2, handler.DeclarationReads);
        Assert.Equal(drift ? LabelPolicyPhase.Conflict : LabelPolicyPhase.VerifiedActive, after.Phase);
        if (drift)
        {
            Assert.Null(after.VerifiedUntil);
        }
        else
        {
            Assert.True(after.VerifiedUntil >= before.VerifiedUntil);
            Assert.True(after.Revision > before.Revision);
            Assert.Equal(TimeSpan.FromMinutes(30), after.VerificationFreshness);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScheduledVerificationDoesNotActivatePendingOrAbandonedPublication(bool abandon)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase());
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        await fixture.Clock.PolicyVerificationRegistrations.Reader.ReadAsync(token);
        ILabelPolicyStore store = fixture.Services.GetRequiredService<ILabelPolicyStore>();
        if (abandon)
        {
            LabelAdministrationCaller administrator = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
                .AuthenticateLocalAsync("uid:0", token)).Result!;
            LabelPolicyState pending = (await store.ReadPolicyStateAsync(token))!;
            Assert.True((await store.AbandonPolicyPublicationAsync(
                caller: administrator,
                operationId: "abandon-for-verifier",
                expectedRevision: pending.Revision,
                reason: "Stop publication.",
                cancellationToken: token)).Succeeded);
        }
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        await fixture.Clock.PolicyVerificationRegistrations.Reader.ReadAsync(token);
        Assert.Equal(0, handler.DeclarationReads);
        Assert.Equal(abandon ? LabelPolicyPhase.Conflict : LabelPolicyPhase.Publishing,
            (await store.ReadPolicyStateAsync(token))!.Phase);
    }

    [Fact]
    public async Task HealthReadsStoredFreshnessWithoutStartingRemoteVerification()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase());
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        await fixture.Clock.PolicyVerificationRegistrations.Reader.ReadAsync(token);
        using HttpResponseMessage pending = await fixture.Client.GetAsync("/health/labeler/moderation", token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, pending.StatusCode);
        Assert.Equal(0, handler.DeclarationReads);
        Assert.True((await fixture.Services.GetRequiredService<LabelPolicyService>()
            .GetVerifiedStateAsync(cancellationToken: token)).Succeeded);
        handler.BlockDeclarationReads = true;
        using HttpResponseMessage fresh = await fixture.Client.GetAsync("/health/labeler/moderation", token);
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        Assert.Equal(1, handler.DeclarationReads);
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        await handler.DeclarationRequested.Task.WaitAsync(token);
        fixture.Clock.Advance(TimeSpan.FromMinutes(26));
        await fixture.Clock.PolicyVerificationRegistrations.Reader.ReadAsync(token);
        await using Microsoft.Data.Sqlite.SqliteConnection connection = new($"Data Source={fixture.DatabasePath};Pooling=False");
        await connection.OpenAsync(token);
        await using Microsoft.Data.Sqlite.SqliteCommand expire = connection.CreateCommand();
        expire.CommandText = """
            UPDATE label_policy_gate
            SET payload=CAST(json_set(CAST(payload AS TEXT),'$.VerifiedUntil',
                CAST((julianday('now')-2440587.5)*86400000 AS INTEGER)-1) AS BLOB)
            WHERE id=1
            """;
        Assert.Equal(1, await expire.ExecuteNonQueryAsync(token));
        LabelPolicyState expired = (await fixture.Services.GetRequiredService<ILabelPolicyStore>().ReadPolicyStateAsync(token))!;
        Assert.True(expired.VerifiedUntil <= expired.DatabaseTime);
        using HttpResponseMessage unhealthy = await fixture.Client.GetAsync("/health/labeler/moderation", token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unhealthy.StatusCode);
        Assert.Equal(2, handler.DeclarationReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StandardReportWorksWithRemoteAdministrationDisabledAndDoesNotCreateTeamAuthority(bool legacy)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase());
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        using MetricCollector<long> metrics = new(
            fixture.Services.GetRequiredService<IMeterFactory>(), LabelerModerationHost.MeterName,
            "idunno.bluesky.labeler.moderation.reports");
        using HttpRequestMessage request = ReportRequest(key, fixture.Clock.GetUtcNow(), legacy: legacy);
        string assertion = request.Headers.Authorization!.Parameter!;
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument output = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, output.RootElement.GetProperty("id").GetInt64());
        Assert.Equal(s_reporter.Value, output.RootElement.GetProperty("reportedBy").GetString());
        Assert.Equal("com.atproto.admin.defs#repoRef", output.RootElement.GetProperty("subject").GetProperty("$type").GetString());
        Assert.False(output.RootElement.TryGetProperty("succeeded", out _));
        Assert.False(output.RootElement.TryGetProperty("caseId", out _));
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal(1, handler.DeclarationReads);
        using HttpRequestMessage replay = ReportRequest(key, fixture.Clock.GetUtcNow());
        replay.Headers.Authorization = new("Bearer", assertion);
        using HttpResponseMessage replayed = await fixture.Client.SendAsync(replay, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);
        using HttpResponseMessage administration = await fixture.Client.GetAsync(
            "/xrpc/dev.idunno.labeler.admin.getTeam", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, administration.StatusCode);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).IssuedThrough);
        Assert.Contains(metrics.GetMeasurementSnapshot(), measurement => measurement.Value == 1 &&
            measurement.Tags.Any(tag => tag.Key == "outcome" && Equals(tag.Value, "accepted")));
        Assert.All(metrics.GetMeasurementSnapshot(), measurement => Assert.All(measurement.Tags, tag => Assert.Equal("outcome", tag.Key)));
    }

    [Fact]
    public async Task OutOfScopeReportReasonsReturnNormalReceiptsButAreAutomaticallyHandled()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        string declaration = Declaration.TrimEnd()[..^1] + ""","reasonTypes":["tools.ozone.report.defs#reasonOther"]}""";
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()) { Declaration = declaration });
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, declarationJson: declaration);
        using HttpRequestMessage request = ReportRequest(key, fixture.Clock.GetUtcNow());
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument output = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        Assert.Equal("tools.ozone.report.defs#reasonMisleadingSpam", output.RootElement.GetProperty("reasonType").GetString());
        Assert.False(output.RootElement.TryGetProperty("resolutionReason", out _));
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller caller = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        LabelModerationReport report = (await fixture.Services.GetRequiredService<ILabelReportStore>().GetReportAsync(
            caller, output.RootElement.GetProperty("id").GetInt64(), token)).Result!;
        Assert.Equal("Not Applicable", report.ResolutionReason);
        Assert.NotNull(report.DecisionOperationId);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        using HttpRequestMessage replay = ReportRequest(key, fixture.Clock.GetUtcNow());
        replay.Headers.Authorization = request.Headers.Authorization;
        using HttpResponseMessage replayed = await fixture.Client.SendAsync(replay, token);
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);
    }

    [Theory]
    [InlineData("""{"reasonType":"x","subject":null}""")]
    [InlineData("""{"reasonType":"x","reportedBy":"did:plc:forged","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"}}""")]
    [InlineData("""{"reasonType":"x","reasonType":"y","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"}}""")]
    [InlineData("""{"reasonType":"x","subject":{"$type":"chat.bsky.convo.defs#messageRef","did":"did:plc:subject"}}""")]
    [InlineData("""{"reasonType":"x","subject":{"$type":"com.atproto.repo.strongRef","uri":"at://subject.example/app.bsky.feed.post/key","cid":"bafyreihdwdcefgh4dqkjv67uzcmw7ojee6xedzdetojuzjevtenxquvyku"}}""")]
    [InlineData("""{"reasonType":"x","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},"modTool":{"name":"tool","meta":{"x":1,"x":2}}}""")]
    [InlineData("""{"reasonType":"\uD800","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"}}""")]
    [InlineData("""{"\uD800":"x","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"}}""")]
    [InlineData("""{"reasonType":"x","subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"},"modTool":{"name":"tool","meta":{"x":"\uD800"}}}""")]
    public async Task RejectsMalformedUnsupportedAndForgedInputBeforeAuthentication(string body)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase());
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        using HttpRequestMessage request = ReportRequest(key, fixture.Clock.GetUtcNow(), body);
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, handler.DeclarationReads);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller caller = (await team.AuthenticateLocalAsync("uid:0", TestContext.Current.CancellationToken)).Result!;
        Assert.Empty((await fixture.Services.GetRequiredService<ILabelReportStore>().GetReportsAsync(
            caller, cancellationToken: TestContext.Current.CancellationToken)).Result!);
    }

    [Fact]
    public async Task AdmissionWindowUsesFakeTimeAndBoundsRequestsBeforeIdentityResolution()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase());
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(
            key, identity, new LabelerModerationOptions { ReportsPerMinute = 1 });
        for (int index = 0; index < 2; index++)
        {
            using HttpRequestMessage request = ReportRequest(key, fixture.Clock.GetUtcNow());
            request.Headers.Authorization = new("Bearer", "not-a-service-token");
            using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(index == 0 ? HttpStatusCode.Unauthorized : HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        using HttpRequestMessage afterWindow = ReportRequest(key, fixture.Clock.GetUtcNow());
        afterWindow.Headers.Authorization = new("Bearer", "not-a-service-token");
        using HttpResponseMessage admitted = await fixture.Client.SendAsync(afterWindow, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, admitted.StatusCode);
        Assert.Equal(0, handler.DeclarationReads);
    }

    [Fact]
    public async Task PolicyDriftClosesReportsButLeavesDistributionRoutingReady()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase());
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, remoteEnabled: true);
        using HttpResponseMessage initiallyReady = await fixture.Client.GetAsync("/health/labeler/ready", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, initiallyReady.StatusCode);
        LabelPolicyService verifier = fixture.Services.GetRequiredService<LabelPolicyService>();
        Assert.True((await verifier.GetVerifiedStateAsync(cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        handler.Declaration = Declaration.Replace("\"spam\"", "\"scam\"", StringComparison.Ordinal);
        Assert.False((await verifier.GetVerifiedStateAsync(forceVerification: true, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        using HttpRequestMessage request = ReportRequest(key, fixture.Clock.GetUtcNow());
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using HttpResponseMessage distribution = await fixture.Client.GetAsync(
            "/xrpc/com.atproto.label.queryLabels?uriPatterns=*", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, distribution.StatusCode);
        using HttpResponseMessage health = await fixture.Client.GetAsync("/health/labeler/ready", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        using HttpResponseMessage moderation = await fixture.Client.GetAsync("/health/labeler/moderation", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, moderation.StatusCode);
        handler.Declaration = Declaration;
        Assert.False((await verifier.GetVerifiedStateAsync(forceVerification: true, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        LabelPolicyState conflict = (await fixture.Services.GetRequiredService<ILabelPolicyStore>()
            .ReadPolicyStateAsync(TestContext.Current.CancellationToken))!;
        using HttpRequestMessage inspect = AdministrativeRequest(
            key: key, now: fixture.Clock.GetUtcNow(), method: "getPolicy", command: new());
        using HttpResponseMessage inspected = await fixture.Client.SendAsync(inspect, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, inspected.StatusCode);
        using HttpRequestMessage reconcile = AdministrativeRequest(
            key: key,
            now: fixture.Clock.GetUtcNow(),
            method: "beginPolicyPublication",
            command: new(
                OperationId: "reconcile", Reason: "Recover detected drift.",
                Moderation: new(PolicyVersion: "v1", ExpectedRevision: conflict.Revision, PreviousCid: Cid)));
        using HttpResponseMessage reconciled = await fixture.Client.SendAsync(reconcile, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, reconciled.StatusCode);
        Assert.True((await verifier.GetVerifiedStateAsync(forceVerification: true, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportAndReadinessDeadlinesBoundDeclarationLookupsUsingTheHostClock(bool health)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase()) { BlockDeclarationReads = true };
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(
            key, identity, new LabelerModerationOptions { MaximumConcurrentReports = 1 });
        using HttpRequestMessage request = health
            ? new(HttpMethod.Get, "/health/labeler/moderation")
            : ReportRequest(key, fixture.Clock.GetUtcNow());
        Task<HttpResponseMessage> pending = fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        if (health)
        {
            using HttpResponseMessage storedHealth = await pending;
            Assert.Equal(HttpStatusCode.ServiceUnavailable, storedHealth.StatusCode);
            Assert.Equal(0, handler.DeclarationReads);
            return;
        }
        await handler.DeclarationRequested.Task.WaitAsync(TestContext.Current.CancellationToken);
        if (!health)
        {
            using HttpRequestMessage concurrent = ReportRequest(key, fixture.Clock.GetUtcNow());
            using HttpResponseMessage throttled = await fixture.Client.SendAsync(concurrent, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        }

        await fixture.Clock.WaitForTimerAsync(TimeSpan.FromSeconds(30));
        fixture.Clock.Advance(TimeSpan.FromSeconds(30));
        using HttpResponseMessage response = await pending;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(LabelPolicyPhase.Publishing, (await fixture.Services.GetRequiredService<ILabelPolicyStore>()
            .ReadPolicyStateAsync(TestContext.Current.CancellationToken))!.Phase);
        handler.BlockDeclarationReads = false;
        using HttpRequestMessage retry = ReportRequest(key, fixture.Clock.GetUtcNow());
        using HttpResponseMessage recovered = await fixture.Client.SendAsync(retry, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidRemoteDeclarationDurablyConflictsWithoutRefreshingPermission(bool deep)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase());
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        Assert.True((await fixture.Services.GetRequiredService<LabelPolicyService>().GetVerifiedStateAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        handler.Declaration = Declaration.TrimEnd()[..^1] + ",\"invalid\":" +
            (deep ? new string('[', 33) + "0" + new string(']', 33) : "\"" + new string('x', 65536) + "\"") + "}";
        LabelServiceResult<LabelPolicyState> result = await fixture.Services.GetRequiredService<LabelPolicyService>()
            .GetVerifiedStateAsync(forceVerification: true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.Equal(LabelServiceErrorCode.PolicyUnavailable, result.Error!.Code);
        Assert.Equal(LabelPolicyPhase.Conflict, (await fixture.Services.GetRequiredService<ILabelPolicyStore>()
            .ReadPolicyStateAsync(TestContext.Current.CancellationToken))!.Phase);
        handler.Declaration = Declaration;
        Assert.False((await fixture.Services.GetRequiredService<LabelPolicyService>().GetVerifiedStateAsync(
            forceVerification: true, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task AVerifierBoundToAnotherIssuerCannotReuseFreshPermission()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DeclarationHandler handler = new(key.ExportPublicKeyMultibase());
        using HttpClient identity = new(handler);
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        Assert.True((await fixture.Services.GetRequiredService<LabelPolicyService>()
            .GetVerifiedStateAsync(cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        LabelPolicyService wrongIssuer = new(
            issuer: s_reporter,
            store: fixture.Services.GetRequiredService<ILabelPolicyStore>(),
            httpClient: identity);
        Assert.Equal(LabelServiceErrorCode.PolicyUnavailable, (await wrongIssuer.GetVerifiedStateAsync(
            cancellationToken: TestContext.Current.CancellationToken)).Error!.Code);
        Assert.Equal(1, handler.DeclarationReads);
    }

    [Theory]
    [InlineData("""{"\uD800":true}""")]
    [InlineData("""{"value":"\uD800"}""")]
    [InlineData("""{"value":0.0}""")]
    [InlineData("""{"value":1e0}""")]
    public async Task InvalidPolicyExtensionsReturnInvalidRequestOnLocalAndHttpsAdministration(string extension)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, remoteEnabled: true);
        string json = Declaration.TrimEnd()[..^1] + ",\"invalid\":" + extension + "}";
        using JsonDocument declaration = JsonDocument.Parse(json);
        LabelAdministrationCaller caller = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .AuthenticateLocalAsync("uid:0", token)).Result!;
        AdministrationResponse local = await fixture.Services.GetRequiredService<AdministrationDispatcher>().DispatchAsync(
            caller,
            new(
                Method: "savePolicy", OperationId: "invalid", Reason: "Validate input.",
                Moderation: new(
                    PolicyVersion: "v2", Declaration: declaration.RootElement,
                    AllowAccounts: true, AllowRecords: true, Rules: "Review.")),
            token);
        Assert.False(local.Succeeded);
        Assert.Equal("InvalidRequest", local.Error);
        using HttpRequestMessage request = AdministrativeRequest(
            key: key, now: fixture.Clock.GetUtcNow(), method: "savePolicy", command: new());
        request.Content = new StringContent(
            """{"operationId":"invalid","reason":"Validate input.","moderation":{"policyVersion":"v2","declaration":""" +
                json + ""","allowAccounts":true,"allowRecords":true,"rules":"Review."}}""",
            Encoding.UTF8,
            "application/json");
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument output = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        Assert.Equal("InvalidRequest", output.RootElement.GetProperty("error").GetString());
        Assert.Single((await fixture.Services.GetRequiredService<ILabelPolicyStore>()
            .GetPolicyVersionsAsync(caller, cancellationToken: token)).Result!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task BothAdministrativeAdaptersConsumeTheSameCaseAndPolicyServices(int actions)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, remoteEnabled: true);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", TestContext.Current.CancellationToken)).Result!;
        AdministrationDispatcher dispatcher = fixture.Services.GetRequiredService<AdministrationDispatcher>();
        using JsonDocument subject = JsonDocument.Parse("""{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:subject"}""");
        AdministrationResponse local = await dispatcher.DispatchAsync(
            caller: administrator,
            command: new(
                Method: "changeCase", OperationId: "open", Reason: "Independent manual review.",
                Moderation: new(Action: LabelCaseAction.Open, Subject: subject.RootElement)),
            token: TestContext.Current.CancellationToken);
        Assert.True(local.Succeeded, local.Message);
        using HttpRequestMessage request = new(HttpMethod.Post, "/xrpc/dev.idunno.labeler.admin.getCase");
        request.Content = new StringContent(
            JsonSerializer.Serialize(
                new AdministrationCommand(Moderation: new(CaseId: local.Moderation!.Event!.Case.Id)),
                AdministrationJsonContext.Default.AdministrationCommand),
            Encoding.UTF8,
            "application/json");
        AdministrationTests.AddTrustedProxyHeaders(request);
        request.Headers.Authorization = new("Bearer", AdministrationTests.Token(
            key: key, now: fixture.Clock.GetUtcNow(), method: "dev.idunno.labeler.admin.getCase"));
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument output = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(local.Moderation.Event.Case.Id, output.RootElement.GetProperty("moderation").GetProperty("case").GetProperty("id").GetInt64());
        Assert.True((await dispatcher.DispatchAsync(
            administrator, new(Method: "getPolicy"), TestContext.Current.CancellationToken)).Succeeded);
        AdministrationCommand decision = new(
            Method: "decideCase",
            OperationId: "decision",
            Reason: "Complete the independent review.",
            Moderation: new(
                CaseId: local.Moderation.Event.Case.Id,
                ExpectedRevision: local.Moderation.Event.Case.Revision,
                PolicyVersion: "v1",
                Actions: actions == 0 ? [] : [new(LabelOperationKind.Issue, "spam"), new(LabelOperationKind.Negate, "other")]));
        using HttpRequestMessage decide = new(HttpMethod.Post, "/xrpc/dev.idunno.labeler.admin.decideCase");
        decide.Content = new StringContent(
            JsonSerializer.Serialize(decision, AdministrationJsonContext.Default.AdministrationCommand),
            Encoding.UTF8,
            "application/json");
        AdministrationTests.AddTrustedProxyHeaders(decide);
        decide.Headers.Authorization = new("Bearer", AdministrationTests.Token(
            key: key, now: fixture.Clock.GetUtcNow(), method: "dev.idunno.labeler.admin.decideCase"));
        using HttpResponseMessage decided = await fixture.Client.SendAsync(decide, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
        AdministrationResponse recovered = await dispatcher.DispatchAsync(
            administrator, new(Method: "getCaseDecision", OperationId: "decision"), TestContext.Current.CancellationToken);
        Assert.True(recovered.Succeeded, recovered.Message);
        Assert.Equal(actions, recovered.Moderation!.Decision!.Actions.Length);
        Assert.Equal("Resolved", recovered.Moderation.Decision.Event.Case.Status);
        Assert.Equal(actions == 0 ? 0 : 1, recovered.Moderation.Decision.State.IssuedThrough);
    }

    [Fact]
    public async Task OwnerAppealsExposeCapturedLabelsOnlyThroughSharedProtectedAdministration()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, remoteEnabled: true);
        LabelAdministrationCaller administrator = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .AuthenticateLocalAsync("uid:0", token)).Result!;
        AdministrationDispatcher dispatcher = fixture.Services.GetRequiredService<AdministrationDispatcher>();
        AdministrationResponse issued = await dispatcher.DispatchAsync(
            administrator,
            new(Method: "issueLabel", OperationId: "manual", Reason: "Case-independent labeling.", Target: s_reporter.Value, Value: "spam"),
            token);
        Assert.True(issued.Succeeded, issued.Message);
        const string body = """
            {"reasonType":"com.atproto.moderation.defs#reasonAppeal","reason":"Review this label.",
             "subject":{"$type":"com.atproto.admin.defs#repoRef","did":"did:plc:reporter"}}
            """;
        using HttpRequestMessage report = ReportRequest(key, fixture.Clock.GetUtcNow(), body);
        using HttpResponseMessage accepted = await fixture.Client.SendAsync(report, token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using JsonDocument publicOutput = JsonDocument.Parse(await accepted.Content.ReadAsByteArrayAsync(token));
        Assert.False(publicOutput.RootElement.TryGetProperty("challenges", out _));
        AdministrationResponse local = await dispatcher.DispatchAsync(administrator, new(Method: "getAppeals"), token);
        Assert.True(local.Succeeded, local.Message);
        ModerationAppealView appeal = Assert.Single(local.Moderation!.Appeals!);
        Assert.Single(appeal.Challenges);
        foreach (string method in new[] { "getAppeal", "getAppeals", "getAppealReports" })
        {
            using HttpRequestMessage inspect = AdministrativeRequest(
                key: key,
                now: fixture.Clock.GetUtcNow(),
                method: method,
                command: new(Moderation: new(AppealId: appeal.Id, CaseId: appeal.CaseId)));
            using HttpResponseMessage inspected = await fixture.Client.SendAsync(inspect, token);
            Assert.Equal(HttpStatusCode.OK, inspected.StatusCode);
            using JsonDocument output = JsonDocument.Parse(await inspected.Content.ReadAsByteArrayAsync(token));
            JsonElement moderation = output.RootElement.GetProperty("moderation");
            if (method == "getAppealReports")
            {
                Assert.Equal(1, moderation.GetProperty("reportIds").GetArrayLength());
            }

            else
            {
                Assert.Equal(1, moderation.GetProperty("appeals")[0].GetProperty("challenges")[0].GetProperty("sequence").GetInt64());
            }
        }

        LabelModerationCase moderationCase = (await fixture.Services.GetRequiredService<ILabelModerationStore>()
            .GetCaseAsync(administrator, appeal.CaseId, token)).Result!;
        using HttpRequestMessage decide = AdministrativeRequest(
            key: key,
            now: fixture.Clock.GetUtcNow(),
            method: "decideCase",
            command: new(
                OperationId: "appeal-review",
                Reason: "Explicit review without automatic corrections.",
                Moderation: new(
                    CaseId: moderationCase.Id,
                    ExpectedRevision: moderationCase.Revision,
                    PolicyVersion: "v1",
                    Actions: [],
                    AppealIds: [appeal.Id],
                    AppealOutcome: LabelAppealStatus.Upheld)));
        using HttpResponseMessage decided = await fixture.Client.SendAsync(decide, token);
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
        AdministrationResponse retained = await dispatcher.DispatchAsync(
            administrator, new(Method: "getAppeal", Moderation: new(AppealId: appeal.Id)), token);
        Assert.Equal("Upheld", Assert.Single(retained.Moderation!.Appeals!).Status);
        Assert.Equal(1, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory]
    [InlineData(LabelAppealStatus.Upheld, true)]
    [InlineData(LabelAppealStatus.Superseded, true)]
    [InlineData(LabelAppealStatus.Withdrawn, true)]
    [InlineData(LabelAppealStatus.Withdrawn, false)]
    [InlineData(LabelAppealStatus.Replaced, false)]
    public async Task MismatchedAppealActionsReturnAuditedInvalidRequestsInsteadOfExceptions(LabelAppealStatus outcome, bool issue)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, remoteEnabled: true);
        LabelAdministrationCaller administrator = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .AuthenticateLocalAsync("uid:0", token)).Result!;
        AdministrationDispatcher dispatcher = fixture.Services.GetRequiredService<AdministrationDispatcher>();
        AdministrationCommand command = new(
            Method: "decideCase",
            OperationId: "invalid-appeal",
            Reason: "Reject incompatible appeal intent.",
            Moderation: new(
                CaseId: 1,
                ExpectedRevision: 1,
                PolicyVersion: "v1",
                Actions: issue ? [new(LabelOperationKind.Issue, "spam")] : [],
                AppealIds: [1],
                AppealOutcome: outcome));
        AdministrationResponse local = await dispatcher.DispatchAsync(administrator, command, token);
        Assert.False(local.Succeeded);
        Assert.Equal("InvalidRequest", local.Error);
        Assert.Contains("does not match the label selection", local.Message, StringComparison.Ordinal);
        using HttpRequestMessage request = AdministrativeRequest(
            key: key,
            now: fixture.Clock.GetUtcNow(),
            method: "decideCase",
            command: command);
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument output = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        Assert.Equal("InvalidRequest", output.RootElement.GetProperty("error").GetString());
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        IReadOnlyList<LabelAdministrationReceipt> audit = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .GetAdministrationAuditAsync(administrator, cancellationToken: token)).Result!;
        Assert.Equal(2, audit.Count(entry => !entry.Succeeded &&
            entry.Reason.Contains("InvalidRequest", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("getPolicyVersion")]
    [InlineData("getPolicyVersions")]
    [InlineData("getPolicyHistory")]
    [InlineData("getPolicyOperation")]
    public async Task RetainedPolicyInspectionUsesProtectedPostBodies(string method)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, remoteEnabled: true);
        Assert.True((await fixture.Services.GetRequiredService<LabelPolicyService>().GetVerifiedStateAsync(cancellationToken: token)).Succeeded);
        using HttpRequestMessage request = AdministrativeRequest(
            key: key,
            now: fixture.Clock.GetUtcNow(),
            method: method,
            command: new(OperationId: "publish", Moderation: new(PolicyVersion: "v1")));
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument output = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        JsonElement moderation = output.RootElement.GetProperty("moderation");
        switch (method)
        {
            case "getPolicyVersion":
                Assert.Equal("v1", moderation.GetProperty("definition").GetProperty("version").GetString());
                break;
            case "getPolicyVersions":
                Assert.Equal(1, moderation.GetProperty("definitions").GetArrayLength());
                break;
            case "getPolicyHistory":
                Assert.Equal(3, moderation.GetProperty("policyHistory").GetArrayLength());
                break;
            default:
                Assert.Equal("Publishing", moderation.GetProperty("policyHistory")[0].GetProperty("state").GetProperty("phase").GetString());
                break;
        }
    }

    [Fact]
    public async Task WorkflowCursorCannotBeReusedWithChangedHttpsFilters()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new DeclarationHandler(key.ExportPublicKeyMultibase()));
        await using TestServerBuilder fixture = await CreateAsync(key: key, identity: identity, remoteEnabled: true);
        LabelAdministrationCaller caller = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>().AuthenticateLocalAsync("uid:0", token)).Result!;
        ILabelModerationStore cases = fixture.Services.GetRequiredService<ILabelModerationStore>();
        for (int index = 0; index < 2; index++)
        {
            string suffix = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.True((await cases.ChangeCaseAsync(
                caller,
                new(
                    operationId: "open-" + suffix, subject: LabelReportSubject.Account(new("did:plc:subject" + suffix)),
                    action: LabelCaseAction.Open, reason: "Independent review."),
                token)).Succeeded);
        }

        using HttpRequestMessage first = AdministrativeRequest(
            key: key, now: fixture.Clock.GetUtcNow(), method: "getCaseQueue",
            command: new(Limit: 1, Moderation: new(Queue: LabelCaseWorkflow.All)));
        using HttpResponseMessage page = await fixture.Client.SendAsync(first, token);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        using JsonDocument output = JsonDocument.Parse(await page.Content.ReadAsByteArrayAsync(token));
        string cursor = output.RootElement.GetProperty("moderation").GetProperty("cursor").GetString()!;
        using HttpRequestMessage changed = AdministrativeRequest(
            key: key, now: fixture.Clock.GetUtcNow(), method: "getCaseQueue",
            command: new(Limit: 1, Moderation: new(Queue: LabelCaseWorkflow.Open, Cursor: cursor)));
        using HttpResponseMessage rejected = await fixture.Client.SendAsync(changed, token);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using JsonDocument error = JsonDocument.Parse(await rejected.Content.ReadAsByteArrayAsync(token));
        Assert.Equal("InvalidCursor", error.RootElement.GetProperty("error").GetString());
    }

    /// <summary>Builds a method-bound authenticated request without placing confidential filters in URLs.</summary>
    private static HttpRequestMessage AdministrativeRequest(
        LabelSigningKey key,
        DateTimeOffset now,
        string method,
        AdministrationCommand command)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/xrpc/dev.idunno.labeler.admin." + method)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(command, AdministrationJsonContext.Default.AdministrationCommand),
                Encoding.UTF8,
                "application/json")
        };
        AdministrationTests.AddTrustedProxyHeaders(request);
        request.Headers.Authorization = new("Bearer", AdministrationTests.Token(
            key: key, now: now, method: "dev.idunno.labeler.admin." + method));
        return request;
    }

    /// <summary>Builds an explicitly enabled reporter host with a retained publication intent but no standing PDS credentials.</summary>
    private static async Task<TestServerBuilder> CreateAsync(
        LabelSigningKey key,
        HttpClient identity,
        LabelerModerationOptions? options = null,
        bool remoteEnabled = false,
        string declarationJson = Declaration,
        bool xenonUi = false)
    {
        TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddLabelerModeration(issuer: TestServerBuilder.s_issuer, httpClient: identity, options: options);
                services.AddLabelerAdministration(new(TestServerBuilder.s_issuer, "labeler_admin"), key, identity);
            },
            initialize: async store =>
            {
                ILabelAdministrationStore team = (ILabelAdministrationStore)store;
                Assert.True((await team.BootstrapAsync(s_administrator, "uid:0", "bootstrap", TestContext.Current.CancellationToken)).Succeeded);
                LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", TestContext.Current.CancellationToken)).Result!;
                ILabelPolicyStore policies = (ILabelPolicyStore)store;
                using JsonDocument declaration = JsonDocument.Parse(declarationJson);
                LabelPolicyDefinition draft = new(
                    version: "v1", declaration: declaration.RootElement, allowAccounts: true, allowRecords: true, rules: "Review reports.");
                Assert.True((await policies.SavePolicyAsync(
                    administrator, "draft", draft, "Reviewed policy.", TestContext.Current.CancellationToken)).Succeeded);
                LabelPolicyState state = (await policies.ReadPolicyStateAsync(TestContext.Current.CancellationToken))!;
                Assert.True((await policies.BeginPolicyPublicationAsync(
                    caller: administrator, operationId: "publish", version: "v1", expectedRevision: state.Revision,
                    previousCid: null, reason: "Publish declaration.", cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
            },
            remoteAdministrationEnabled: remoteEnabled,
            moderation: true,
            xenonUi: xenonUi);
        return fixture;
    }

    /// <summary>Creates an exact-method reporter assertion over the test account's repository key.</summary>
    private static HttpRequestMessage ReportRequest(LabelSigningKey key, DateTimeOffset now, string body = Report, bool legacy = false)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/xrpc/com.atproto.moderation.createReport")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        AdministrationTests.AddTrustedProxyHeaders(request);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AdministrationTests.Token(
            key: key, now: now, audience: TestServerBuilder.s_issuer.Value + (legacy ? string.Empty : "#atproto_labeler"),
            method: "com.atproto.moderation.createReport", actor: s_reporter.Value));
        return request;
    }

    /// <summary>Serves exact DID documents and complete declaration records without fetching report subjects.</summary>
    private sealed class DeclarationHandler(string publicKey) : HttpMessageHandler
    {
        internal string Declaration { get; set; } = ModerationTests.Declaration;
        internal int DeclarationReads { get; private set; }
        internal bool BlockDeclarationReads { get; set; }
        internal TaskCompletionSource DeclarationRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri uri = request.RequestUri!;
            string json;
            if (uri.Host == "plc.directory")
            {
                string did = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
                json = $$"""
                    {"id":"{{did}}","verificationMethod":[
                    {"id":"{{did}}#atproto","type":"Multikey","controller":"{{did}}","publicKeyMultibase":"{{publicKey}}"}],
                    "service":[{"id":"#atproto_pds","type":"AtprotoPersonalDataServer","serviceEndpoint":"https://pds.example"}]}
                    """;
            }
            else if (uri.Host == "pds.example" && uri.AbsolutePath == "/xrpc/com.atproto.repo.getRecord")
            {
                DeclarationReads++;
                Assert.Contains("collection=app.bsky.labeler.service", uri.Query, StringComparison.Ordinal);
                Assert.Contains("rkey=self", uri.Query, StringComparison.OrdinalIgnoreCase);
                if (BlockDeclarationReads)
                {
                    DeclarationRequested.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }

                json = $$"""{"uri":"at://{{TestServerBuilder.s_issuer}}/app.bsky.labeler.service/self","cid":"{{Cid}}","value":{{Declaration}}}""";
            }
            else
            {
                throw new InvalidOperationException("The test must never fetch reported subjects.");
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
