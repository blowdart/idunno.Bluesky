// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Labels;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Labeler;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using HostProgram = idunno.Bluesky.Labeler.Host.Program;
using TestProgram = Xenon.TestHost.Program;

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>Exercises the isolated host's persistent fixtures and actual request-boundary middleware.</summary>
public partial class XenonTestHostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicationAuthenticationOptionsResolveIndependentlyOfSchemeOrder(bool publicationFirst)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new NoNetworkHandler());
        IServiceCollection services = WebApplication.CreateBuilder().Services;
        string database = Path.Combine(Path.GetTempPath(), "xenon_options_" + Guid.NewGuid().ToString("N") + ".db");
        services.AddSqliteLabelStorage($"Data Source={database};Pooling=False");
        services.AddLabelerModeration(Xenon.TestHost.FixtureHandler.s_issuer, identity);
        services.AddLabelerAdministration(new(Xenon.TestHost.FixtureHandler.s_issuer, "labeler_admin"), key, identity);
        HostProgram.AddWebsiteAuthentication(services, new Uri("https://xenon-test.example"));
        services.AddLabelerUI();
        await using ServiceProvider provider = services.BuildServiceProvider();
        const string publicationScheme = "Xenon.PolicyPublication";
        (BlueskyAuthenticationOptions website, BlueskyAuthenticationOptions publication) = await Task.Run(() =>
        {
            IOptionsMonitor<BlueskyAuthenticationOptions> monitor = provider.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>();
            monitor.Get(publicationFirst ? publicationScheme : BlueskyAuthenticationDefaults.AuthenticationScheme);
            return (monitor.Get(BlueskyAuthenticationDefaults.AuthenticationScheme), monitor.Get(publicationScheme));
        }, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Same(website.DataProtectionProvider, publication.DataProtectionProvider);
        Assert.Same(website.TimeProvider, publication.TimeProvider);
        Assert.Same(website.CorrelationStateCache, publication.CorrelationStateCache);
        Assert.Same(website.CorrelationStateCacheEvents, publication.CorrelationStateCacheEvents);
        Assert.NotEqual(website.Cookie.Name, publication.Cookie.Name);
        Assert.NotEqual(website.CorrelationCookie.Name, publication.CorrelationCookie.Name);
        Assert.Equal("/Xenon/Policy", publication.CorrelationCookie.Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://test.example")]
    [InlineData("https://test.example:8443")]
    [InlineData("https://test.example/path")]
    [InlineData("https://test.example?query=value")]
    [InlineData("https://user@test.example")]
    [InlineData("https://test.example#fragment")]
    public void PublicOriginRejectsNonOriginConfiguration(string? value) =>
        Assert.Throws<InvalidOperationException>(() => TestProgram.ReadPublicOrigin(value));

    [Theory]
    [InlineData("https://test.example")]
    [InlineData("https://test.example/")]
    [InlineData("https://test.example:443")]
    public void PublicOriginNormalizesHttpsOrigins(string value) =>
        Assert.Equal("https://test.example/", TestProgram.ReadPublicOrigin(value).AbsoluteUri);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SeededHostPreservesStateAndFiltersHostsBeforeMetadata(bool trusted, bool legacyPolicy)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string directory = Path.Combine(Path.GetTempPath(), "xenon_harness_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string database = Path.Combine(directory, "test.db");
        try
        {
            using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
            using HttpClient network = new(new NoNetworkHandler());
            using HttpClient identity = new(new Xenon.TestHost.FixtureHandler(key.ExportPublicKeyMultibase(), network));
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(TestProgram).Assembly.GetName().Name
            });
            builder.WebHost.UseStaticWebAssets();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
            if (trusted)
            {
                builder.Configuration["Labeler:TrustedProxies:0"] = "127.0.0.1";
                builder.Configuration["Labeler:TrustedProxies:1"] = "::ffff:127.0.0.1";
            }

            Uri origin = TestProgram.ReadPublicOrigin("https://xenon-test.example");
            TestProgram.AddHostFiltering(builder.Services, origin);
            builder.Services.AddSqliteLabelStorage($"Data Source={database};Pooling=False");
            builder.Services.AddLabelerModeration(Xenon.TestHost.FixtureHandler.s_issuer, identity);
            builder.Services.AddLabelerAdministration(new(Xenon.TestHost.FixtureHandler.s_issuer, "labeler_admin"), key, identity);
            HostProgram.AddWebsiteAuthentication(builder.Services, origin);
            builder.Services.AddSingleton<IDidHandleResolver>(serviceProvider => new DidHandleCache(new DidHandleCacheOptions
            {
                HttpClient = identity,
                LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>(),
                ResolutionTimeout = TimeSpan.FromSeconds(5)
            }));
            builder.Services.AddLabelerUI();
            FakeTimeProvider previewClock = new(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
            builder.Services.AddSingleton<TimeProvider>(previewClock);
            builder.Services.AddHttpClient("Xenon.PublicPreviews")
                .ConfigurePrimaryHttpMessageHandler(() => new Xenon.TestHost.FixtureHandler(
                    publicKey: key.ExportPublicKeyMultibase(),
                    network: network,
                    clock: previewClock));
            await using WebApplication app = builder.Build();
            await app.Services.GetRequiredService<ILabelStore>().InitializeAsync(Xenon.TestHost.FixtureHandler.s_issuer, token);
            await app.Services.GetRequiredService<ILabelPolicyStore>().InitializePolicyAsync(cancellationToken: token);
            if (legacyPolicy)
            {
                LabelAdministrationReceipt setup = Xenon.TestHost.SeedData.Require(await app.Services.BootstrapLocalAdministratorAsync(
                    Xenon.TestHost.FixtureHandler.s_reporter, "xenon-test-bootstrap", token));
                ILabelAdministrationStore legacyTeam = app.Services.GetRequiredService<ILabelAdministrationStore>();
                LabelAdministrationCaller legacyCaller = Xenon.TestHost.SeedData.Require(
                    await legacyTeam.AuthenticateLocalAsync(setup.LocalIdentity!, token));
                ILabelPolicyStore policies = app.Services.GetRequiredService<ILabelPolicyStore>();
                LabelPolicyState initial = (await policies.ReadPolicyStateAsync(token))!;
                using JsonDocument declaration = JsonDocument.Parse(Xenon.TestHost.FixtureHandler.LegacyDeclaration);
                Xenon.TestHost.SeedData.Require(await policies.SavePolicyAsync(
                    caller: legacyCaller,
                    operationId: "xenon-test-policy-draft",
                    policy: new(
                        version: "test-v1",
                        declaration: declaration.RootElement,
                        allowAccounts: true,
                        allowRecords: true,
                        rules: "Fictional local test data. No label is distributed publicly."),
                    reason: "Exercise the original test policy upgrade.",
                    cancellationToken: token));
                LabelPolicyState publishing = Xenon.TestHost.SeedData.Require(await policies.BeginPolicyPublicationAsync(
                    caller: legacyCaller,
                    operationId: "xenon-test-policy-select",
                    version: "test-v1",
                    expectedRevision: initial.Revision,
                    previousCid: null,
                    reason: "Select the original fixture.",
                    cancellationToken: token));
                Xenon.TestHost.SeedData.Require(await policies.ObservePolicyAsync(
                    expectedRevision: publishing.Revision,
                    verificationStartedAt: publishing.DatabaseTime,
                    observedCid: new(Xenon.TestHost.FixtureHandler.Cid),
                    declaration: declaration.RootElement,
                    cancellationToken: token));
            }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                await Xenon.TestHost.SeedData.InitializeAsync(
                    services: app.Services,
                    administrator: Xenon.TestHost.FixtureHandler.s_reporter,
                    identity: identity,
                    key: key,
                    cancellationToken: token);
            }

            ILabelAdministrationStore team = app.Services.GetRequiredService<ILabelAdministrationStore>();
            LabelAdministrationCaller caller = Xenon.TestHost.SeedData.Require(await team.AuthenticateBrowserAsync(
                Xenon.TestHost.FixtureHandler.s_reporter, token));
            await VerifyAdministrationFixturesAsync(app.Services, caller, legacyPolicy, token);
            IReadOnlyList<LabelModerationReport> seededReports = Xenon.TestHost.SeedData.Require(
                await app.Services.GetRequiredService<ILabelReportStore>().GetReportsAsync(caller, cancellationToken: token));
            Assert.Equal(47, seededReports.Count);
            LabelModerationReport automatic = Assert.Single(seededReports, report => report.ResolutionReason == "Not Applicable");
            Assert.NotNull(automatic.HandledAt);
            LabelAdministrationReceipt automaticAudit = Assert.Single(Xenon.TestHost.SeedData.Require(
                await team.GetAdministrationAuditAsync(caller, cancellationToken: token)),
                entry => entry.Action == "report-not-applicable");
            Assert.Equal(Xenon.TestHost.FixtureHandler.s_issuer, automaticAudit.Actor);
            Assert.Equal("system:report-reason", automaticAudit.LocalIdentity);
            Assert.Equal("Not Applicable", automaticAudit.Reason);
            Assert.Equal(40, seededReports.Select(report => report.CaseId).Distinct().Count());
            Assert.Equal(3, seededReports.Count(report => report.CaseId == 1));
            Assert.Equal(3, seededReports.Count(report => report.CaseId == 2));
            Assert.Equal(4, seededReports.Count(report => report.CaseId == 4));
            Assert.Single(Xenon.TestHost.SeedData.Require(await app.Services.GetRequiredService<ILabelAppealStore>()
                .GetAppealsAsync(caller, cancellationToken: token)));
            app.UseDeveloperExceptionPage();
            app.Use(async (context, next) =>
            {
                if (context.Request.Headers["X-Test-Authenticated"] == "true")
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(AtProtoClaims.Did, Xenon.TestHost.FixtureHandler.s_reporter.Value),
                            new Claim(idunno.Bluesky.ClaimTypes.Handle, "reporter.xenon.invalid"),
                            new Claim(idunno.Bluesky.ClaimTypes.Avatar, "https://cdn.bsky.app/img/avatar/plain/did:web:reporter.xenon.invalid/example.png")
                        ],
                        "Test"));
                }

                await next(context);
            });
            TestProgram.ConfigurePipeline(app, origin);
            app.MapGet("/scheme", (HttpContext context) => context.Request.Scheme);
            await app.StartAsync(token);
            Uri address = new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            using HttpClientHandler clientHandler = new() { AllowAutoRedirect = false };
            using HttpClient client = new(clientHandler) { BaseAddress = address };
            using (HttpRequestMessage auditPage = new(HttpMethod.Get,
                $"/Xenon/Audit?Sort=Number&Ascending=True&PageNumber={(automaticAudit.Sequence - 1) / 16 + 1}"))
            {
                auditPage.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage audited = await client.SendAsync(auditPage, token);
                string auditHtml = await audited.Content.ReadAsStringAsync(token);
                Assert.Equal(HttpStatusCode.OK, audited.StatusCode);
                Assert.Contains("report-not-applicable", auditHtml, StringComparison.Ordinal);
                Assert.Contains("Not Applicable", auditHtml, StringComparison.Ordinal);
                Assert.Contains(">Automatic</span>", auditHtml, StringComparison.Ordinal);
                Assert.Contains("data-relative-time", auditHtml, StringComparison.Ordinal);
                Assert.DoesNotContain(">Sequence</a>", auditHtml, StringComparison.Ordinal);
                Assert.Contains("data-audit-local", auditHtml, StringComparison.Ordinal);
                Assert.Contains("Actor DID or handle", auditHtml, StringComparison.Ordinal);
                Assert.DoesNotContain("Actor DID or local identity", auditHtml, StringComparison.Ordinal);
                Assert.Contains("class=\"audit-date-limits\"", auditHtml, StringComparison.Ordinal);
                Assert.Contains("data-audit-zone-name", auditHtml, StringComparison.Ordinal);
                Assert.True(auditHtml.IndexOf("id=\"to\"", StringComparison.Ordinal) <
                    auditHtml.IndexOf("class=\"audit-filter-zone\"", StringComparison.Ordinal), auditHtml);
            }
            IReadOnlyList<LabelAdministrationReceipt> initialAudit = Xenon.TestHost.SeedData.Require(
                await team.GetAdministrationAuditAsync(caller, limit: 1000, cancellationToken: token));
            DateTimeOffset auditFrom = automaticAudit.CreatedAt!.Value.AddMilliseconds(-1);
            DateTimeOffset auditTo = automaticAudit.CreatedAt.Value.AddMilliseconds(1);
            TimeZoneInfo auditZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
            foreach ((string filter, int expected) in new (string, int)[]
            {
                ("Actor=" + Uri.EscapeDataString(automaticAudit.Actor!.Value), initialAudit.Count(entry => entry.Actor == automaticAudit.Actor)),
                ("Actor=labeler.xenon.invalid&Outcome=Succeeded", initialAudit.Count(entry => entry.Actor == automaticAudit.Actor && entry.Succeeded)),
                ("Actor=%40labeler.xenon.invalid&Outcome=Succeeded", initialAudit.Count(entry => entry.Actor == automaticAudit.Actor && entry.Succeeded)),
                ("Outcome=Failed", initialAudit.Count(entry => !entry.Succeeded)),
                ("TimeBasis=UTC&From=" + auditFrom.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                    "&To=" + auditTo.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    initialAudit.Count(entry => entry.CreatedAt >= auditFrom && entry.CreatedAt <= auditTo)),
                ("TimeBasis=Local&TimeZone=America%2FLos_Angeles&From=" +
                    TimeZoneInfo.ConvertTime(auditFrom, auditZone).ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                    "&To=" + TimeZoneInfo.ConvertTime(auditTo, auditZone).ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    initialAudit.Count(entry => entry.CreatedAt >= auditFrom && entry.CreatedAt <= auditTo))
            })
            {
                using HttpRequestMessage auditRequest = new(HttpMethod.Get, "/Xenon/Audit?Sort=Received&PageNumber=999&" + filter);
                auditRequest.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage audited = await client.SendAsync(auditRequest, token);
                string auditHtml = WebUtility.HtmlDecode(await audited.Content.ReadAsStringAsync(token));
                Assert.Equal(HttpStatusCode.OK, audited.StatusCode);
                Assert.DoesNotContain("Choose valid audit filters", auditHtml, StringComparison.Ordinal);
                Assert.Contains(expected == 0 ? "No audit events match these filters." : $"<caption>{expected} audit events</caption>", auditHtml, StringComparison.Ordinal);
                if (expected > 0)
                {
                    Assert.Contains(Uri.UnescapeDataString(filter.Split('&')[0]), Uri.UnescapeDataString(auditHtml), StringComparison.Ordinal);
                }
            }
            foreach (string invalidFilter in new[]
            {
                "From=not-a-date", "From=2026-02-01T00:00&To=2026-01-01T00:00", "Outcome=Unknown",
                "TimeBasis=Unknown", "TimeBasis=Local&TimeZone=Invalid",
                "TimeBasis=Local&TimeZone=America%2FLos_Angeles&From=2026-03-08T02:30",
                "TimeBasis=Local&TimeZone=America%2FLos_Angeles&From=2026-11-01T01:30"
            })
            {
                using HttpRequestMessage invalid = new(HttpMethod.Get, "/Xenon/Audit?" + invalidFilter);
                invalid.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage rejected = await client.SendAsync(invalid, token);
                string html = await rejected.Content.ReadAsStringAsync(token);
                Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
                Assert.Contains("Choose valid audit filters", html, StringComparison.Ordinal);
                Assert.DoesNotContain("<caption>", html, StringComparison.Ordinal);
                Assert.DoesNotContain("No audit events match these filters.", html, StringComparison.Ordinal);
            }
            foreach (string actor in new[] { "system:report-reason", "uid:0", "not a handle" })
            {
                using HttpRequestMessage invalid = new(HttpMethod.Get, "/Xenon/Audit?Actor=" + Uri.EscapeDataString(actor));
                invalid.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage response = await client.SendAsync(invalid, token);
                string html = await response.Content.ReadAsStringAsync(token);
                Assert.Contains("Enter a valid DID or handle.", html, StringComparison.Ordinal);
                Assert.DoesNotContain("<caption>", html, StringComparison.Ordinal);
            }
            await VerifyAuditDownloadsAsync(client, team, caller, token);
            foreach (string notice in new[] { "Terms", "Privacy" })
            {
                using HttpResponseMessage publicPage = await client.GetAsync("/Xenon/" + notice, token);
                string publicHtml = await publicPage.Content.ReadAsStringAsync(token);
                Assert.True(publicPage.StatusCode == HttpStatusCode.OK, publicHtml);
                Assert.Contains("href=\"/Xenon/Terms\"", publicHtml, StringComparison.Ordinal);
                Assert.Contains("href=\"/Xenon/Privacy\"", publicHtml, StringComparison.Ordinal);
                Assert.DoesNotContain("aria-label=\"Primary navigation\"", publicHtml, StringComparison.Ordinal);
                Assert.Contains(">Sign in</a>", publicHtml, StringComparison.Ordinal);
            }

            using HttpResponseMessage challenge = await client.GetAsync("/Xenon", token);
            Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
            Uri login = challenge.Headers.Location!;
            Assert.Equal("/Bluesky/Login", login.AbsolutePath);
            Assert.Equal("?ReturnUrl=%2FXenon", login.Query);
            using HttpResponseMessage loginPage = await client.GetAsync(login, token);
            Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
            string loginHtml = await loginPage.Content.ReadAsStringAsync(token);
            Assert.Contains("aria-label=\"Xenon home\"", loginHtml, StringComparison.Ordinal);
            Assert.Contains("id=\"theme-choice\"", loginHtml, StringComparison.Ordinal);
            Assert.Contains("/Xenon/css/xenon", loginHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("bluesky-auth.css", loginHtml, StringComparison.Ordinal);
            Assert.Contains("class=\"authentication-content\"", loginHtml, StringComparison.Ordinal);
            Assert.Contains("class=\"input-group\"", loginHtml, StringComparison.Ordinal);
            Assert.Contains("class=\"form-check\"", loginHtml, StringComparison.Ordinal);
            Assert.Single(HtmlDocumentRegex().Matches(loginHtml));
            Assert.True(loginHtml.IndexOf("/Xenon/js/theme", StringComparison.Ordinal) <
                loginHtml.IndexOf("<link rel=\"stylesheet\"", StringComparison.Ordinal), loginHtml);
            foreach ((string filter, int total) in new[] { ("", 46), ("Unresolved", 46), ("Handled", 1), ("All", 47) })
            {
                string path = "/Xenon/Reports?Sort=Number&Ascending=False" +
                    (filter.Length == 0 ? "" : "&Status=" + filter);
                using HttpRequestMessage filteredRequest = new(HttpMethod.Get, path);
                filteredRequest.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage filteredResponse = await client.SendAsync(filteredRequest, token);
                string filteredHtml = WebUtility.HtmlDecode(await filteredResponse.Content.ReadAsStringAsync(token));
                Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
                Assert.True(filteredHtml.Contains($"<caption>{total} accepted reports</caption>", StringComparison.Ordinal), filteredHtml);
                string expectedFilter = filter.Length == 0 ? "Unresolved" : filter;
                Assert.Contains($"value=\"{expectedFilter}\" selected", filteredHtml, StringComparison.Ordinal);
                Assert.Contains("Status=" + expectedFilter, filteredHtml, StringComparison.Ordinal);
                Assert.Contains("aria-label=\"Filter reports\"", filteredHtml, StringComparison.Ordinal);
                Assert.Contains("class=\"fa-icon fa-filter\" aria-hidden=\"true\"", filteredHtml, StringComparison.Ordinal);
                Assert.Equal(filter is "Handled" or "All", filteredHtml.Contains("Not Applicable", StringComparison.Ordinal));
            }
            foreach (string path in new[] { "/Xenon/Cases", "/Xenon/Appeals", "/Xenon/Audit", "/Xenon/Team", "/Xenon/Reports" })
            {
                using HttpRequestMessage page = new(HttpMethod.Get, path);
                page.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage rendered = await client.SendAsync(page, token);
                string html = await rendered.Content.ReadAsStringAsync(token);
                Assert.Contains("class=\"page page-wide\"", html, StringComparison.Ordinal);
                Assert.True(rendered.StatusCode == HttpStatusCode.OK, $"{path}: {html}");
                Assert.Contains("class=\"identity\"", html, StringComparison.Ordinal);
                Assert.Contains(">Cases (39)</a>", html, StringComparison.Ordinal);
                Assert.Contains(">Reports (46)</a>", html, StringComparison.Ordinal);
                Assert.Contains(">Appeals (1)</a>", html, StringComparison.Ordinal);
                Assert.DoesNotContain(">Audit (", html, StringComparison.Ordinal);
                Assert.Contains("role=\"tooltip\"", html, StringComparison.Ordinal);
                Assert.Contains("tabindex=\"0\"", html, StringComparison.Ordinal);
                string displayedHost = path is "/Xenon/Reports" or "/Xenon/Cases" ? "subject-40.xenon.invalid" : "reporter.xenon.invalid";
                Assert.Contains("@" + displayedHost, html, StringComparison.Ordinal);
                Assert.Contains("did:web:" + displayedHost, html, StringComparison.Ordinal);
                Assembly uiAssembly = typeof(LabelerUIExtensions).Assembly;
                string? version = uiAssembly.GetName().Version?.ToString(3);
                string? build = uiAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                Assert.Contains("<footer class=\"site-footer\">", html, StringComparison.Ordinal);
                Assert.Contains("rel=\"icon\" type=\"image/svg+xml\" sizes=\"any\"", html, StringComparison.Ordinal);
                Assert.Contains("aria-label=\"Xenon home\"", html, StringComparison.Ordinal);
                Assert.Contains("width=\"36\" height=\"36\" alt=\"\"", html, StringComparison.Ordinal);
                Assert.DoesNotContain("Xenon <span>Labeler</span>", html, StringComparison.Ordinal);
                Assert.Contains("<body data-date-times>", html, StringComparison.Ordinal);
                Assert.Contains("class=\"profile-avatar\" src=\"https://cdn.bsky.app/img/avatar/plain/did:web:reporter.xenon.invalid/example.png\"", html, StringComparison.Ordinal);
                Assert.Contains("alt=\"Your profile picture\" referrerpolicy=\"no-referrer\"", html, StringComparison.Ordinal);
                Assert.Contains("<details class=\"account-menu\">", html, StringComparison.Ordinal);
                Assert.Contains("aria-label=\"Account menu for reporter.xenon.invalid\"", html, StringComparison.Ordinal);
                Assert.Contains("<div class=\"account-menu-panel\">", html, StringComparison.Ordinal);
                Assert.Contains("<p class=\"account-role\">Administrator</p>", html, StringComparison.Ordinal);
                Assert.True(html.IndexOf("<p class=\"account-role\">Administrator</p>", StringComparison.Ordinal) < html.IndexOf(">Sign out</a>", StringComparison.Ordinal));
                Assert.Contains("class=\"fa-icon fa-chevron-down\"", html, StringComparison.Ordinal);
                Assert.Contains("class=\"fa-icon fa-globe\"", html, StringComparison.Ordinal);
                int clockPosition = html.IndexOf("data-time-zone-toggle", StringComparison.Ordinal);
                Assert.True(clockPosition < html.IndexOf(">Sign out</a>", StringComparison.Ordinal), html);
                Assert.True(clockPosition < html.IndexOf("</header>", StringComparison.Ordinal), html);
                Assert.Contains("id=\"theme-choice\" aria-describedby=\"theme-help\"", html, StringComparison.Ordinal);
                Assert.Contains("<option value=\"system\">System</option>", html, StringComparison.Ordinal);
                Assert.Contains("<option value=\"light\">Light</option>", html, StringComparison.Ordinal);
                Assert.Contains("<option value=\"dark\">Dark</option>", html, StringComparison.Ordinal);
                Match themeScript = ThemeScriptRegex().Match(html);
                Assert.True(themeScript.Success, html);
                Assert.True(themeScript.Index < html.IndexOf("<link rel=\"stylesheet\"", StringComparison.Ordinal), html);
                Assert.Contains("&copy; 2026 Barry Dorrans", html, StringComparison.Ordinal);
                Assert.Contains($"<p>V{version}</p>", html, StringComparison.Ordinal);
                Assert.Contains("href=\"https://github.com/blowdart/idunno.Bluesky\">Get the source on GitHub</a>", html, StringComparison.Ordinal);
                Assert.Contains("href=\"/Xenon/Terms\"", html, StringComparison.Ordinal);
                Assert.Contains("href=\"/Xenon/Privacy\"", html, StringComparison.Ordinal);
                Assert.Contains($"Build number <code>{build}</code>", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
                if (path is "/Xenon/Cases" or "/Xenon/Reports" or "/Xenon/Audit")
                {
                    Assert.Contains("class=\"table-pagination\"", html, StringComparison.Ordinal);
                    Assert.Contains("&lt; Previous", html, StringComparison.Ordinal);
                    Assert.Contains("Next &gt;", html, StringComparison.Ordinal);
                    Assert.Contains("aria-disabled=\"true\">&lt; Previous", html, StringComparison.Ordinal);
                }
                else
                {
                    Assert.DoesNotContain("aria-label=\"Table pagination\"", html, StringComparison.Ordinal);
                }
                if (path is not ("/Xenon/Team" or "/Xenon/Audit"))
                {
                    Assert.Contains("scope=\"col\" class=\"numeric\"", html, StringComparison.Ordinal);
                    Assert.Contains("<td class=\"numeric\">", html, StringComparison.Ordinal);
                }
                if (path == "/Xenon/Audit")
                {
                    Assert.Contains("aria-label=\"Sort by Time, ascending\"", html, StringComparison.Ordinal);
                    Assert.DoesNotContain("scope=\"col\" class=\"numeric\"", html, StringComparison.Ordinal);
                    Assert.DoesNotContain("<td class=\"numeric\">", html, StringComparison.Ordinal);
                }
                if (path == "/Xenon/Cases")
                {
                    Assert.Contains("<h1>Cases awaiting moderation</h1>", html, StringComparison.Ordinal);
                    Assert.Contains("<caption class=\"sr-only\">", html, StringComparison.Ordinal);
                    Assert.Contains("aria-label=\"Sort by Assignee, ascending\"", html, StringComparison.Ordinal);
                    Assert.Contains("Post by @subject-40.xenon.invalid", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
                    Assert.DoesNotContain("@subject-40.xenon.invalid/app.bsky.feed.post", html, StringComparison.Ordinal);
                    Assert.Contains("<code>at://did:web:subject-40.xenon.invalid/app.bsky.feed.post/example</code>", html, StringComparison.Ordinal);
                    Assert.Contains("class=\"fa-icon author-card fa-address-card\"", html, StringComparison.Ordinal);
                    Assert.Contains("aria-label=\"Author report summary\"", html, StringComparison.Ordinal);
                    Assert.Contains("<th scope=\"col\" class=\"numeric\"><span class=\"sr-only\">Author report summary</span></th>", html, StringComparison.Ordinal);
                    Assert.Contains("class=\"workflow-status-heading\"", html, StringComparison.Ordinal);
                    Assert.Contains("<form method=\"get\" class=\"panel\">", html, StringComparison.Ordinal);
                    Assert.Contains("aria-label=\"Filter cases\"", html, StringComparison.Ordinal);
                    Assert.DoesNotContain("<summary>Filters</summary>", html, StringComparison.Ordinal);
                    Assert.Contains("1 open post report</span>", html, StringComparison.Ordinal);
                    Assert.Contains("1 account report in total, including previously handled reports.", html, StringComparison.Ordinal);
                    Assert.Contains("identity-tooltip report-summary-tooltip", html, StringComparison.Ordinal);
                    Assert.DoesNotContain("Report volume is not a finding", html, StringComparison.Ordinal);
                    Assert.DoesNotContain("distinct record URIs", html, StringComparison.Ordinal);
                    Assert.DoesNotContain("1 records / 1 reports", html, StringComparison.Ordinal);
                    Assert.Contains("<title>Cases awaiting moderation", html, StringComparison.Ordinal);
                    MatchCollection firstCases = CaseLinkRegex().Matches(html);
                    Assert.Equal(16, firstCases.Count);
                    Assert.Equal("40", firstCases[0].Groups[1].Value);
                    Assert.Equal("25", firstCases[^1].Groups[1].Value);
                    Assert.DoesNotContain(firstCases.Cast<Match>(), match => match.Groups[1].Value == "3");
                    Assert.Contains("1 of 3 pages", html, StringComparison.Ordinal);
                    Assert.Contains("aria-sort=\"descending\"", html, StringComparison.Ordinal);
                    Match next = NextPageRegex().Match(html);
                    Assert.True(next.Success, html);
                    string continuation = WebUtility.HtmlDecode(next.Groups[1].Value);
                    Assert.Contains("Queue=Unresolved", continuation, StringComparison.Ordinal);
                    Assert.Contains("Sort=Received", continuation, StringComparison.Ordinal);
                    Assert.Contains("PageNumber=2", continuation, StringComparison.Ordinal);
                    using HttpRequestMessage nextPage = new(HttpMethod.Get, continuation);
                    nextPage.Headers.Add("X-Test-Authenticated", "true");
                    using HttpResponseMessage nextResponse = await client.SendAsync(nextPage, token);
                    Assert.Equal(HttpStatusCode.OK, nextResponse.StatusCode);
                    string nextHtml = await nextResponse.Content.ReadAsStringAsync(token);
                    MatchCollection remainingCases = CaseLinkRegex().Matches(nextHtml);
                    Assert.Equal(16, remainingCases.Count);
                    Assert.Equal("24", remainingCases[0].Groups[1].Value);
                    Assert.Equal("9", remainingCases[^1].Groups[1].Value);
                    Assert.Contains("2 of 3 pages", nextHtml, StringComparison.Ordinal);
                    Match previous = PreviousPageRegex().Match(nextHtml);
                    Assert.True(previous.Success, nextHtml);
                    Assert.Contains("PageNumber=1", WebUtility.HtmlDecode(previous.Groups[1].Value), StringComparison.Ordinal);
                    Match finalLink = NextPageRegex().Match(nextHtml);
                    Assert.True(finalLink.Success, nextHtml);
                    using HttpRequestMessage finalPage = new(HttpMethod.Get, WebUtility.HtmlDecode(finalLink.Groups[1].Value));
                    finalPage.Headers.Add("X-Test-Authenticated", "true");
                    using HttpResponseMessage finalResponse = await client.SendAsync(finalPage, token);
                    Assert.Equal(HttpStatusCode.OK, finalResponse.StatusCode);
                    string finalHtml = await finalResponse.Content.ReadAsStringAsync(token);
                    MatchCollection finalCases = CaseLinkRegex().Matches(finalHtml);
                    Assert.Equal(7, finalCases.Count);
                    Assert.Equal("8", finalCases[0].Groups[1].Value);
                    Assert.Equal("1", finalCases[^1].Groups[1].Value);
                    Assert.Contains("3 of 3 pages", finalHtml, StringComparison.Ordinal);
                    Assert.Contains("aria-disabled=\"true\">Next &gt;", finalHtml, StringComparison.Ordinal);
                    Assert.DoesNotContain("rel=\"next\"", finalHtml, StringComparison.Ordinal);
                    using HttpRequestMessage back = new(HttpMethod.Get, WebUtility.HtmlDecode(previous.Groups[1].Value));
                    back.Headers.Add("X-Test-Authenticated", "true");
                    using HttpResponseMessage backResponse = await client.SendAsync(back, token);
                    Assert.Equal(HttpStatusCode.OK, backResponse.StatusCode);
                    Assert.Contains("1 of 3 pages", await backResponse.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
                }
            }

            await VerifySortingAsync(client, token);
            await VerifyTeamFormAsync(client, token);
            await VerifyFilterFormsAsync(client, token);

            using (HttpRequestMessage search = new(HttpMethod.Get, "/Xenon/Search?Q=fictional"))
            {
                search.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage rendered = await client.SendAsync(search, token);
                string html = await rendered.Content.ReadAsStringAsync(token);
                Assert.True(rendered.StatusCode == HttpStatusCode.OK, html);
                Assert.Contains("role=\"search\" aria-label=\"Search moderation\"", html, StringComparison.Ordinal);
                Assert.Contains("aria-label=\"Search reports and appeals\"", html, StringComparison.Ordinal);
                Assert.Contains("class=\"fa-icon fa-magnifying-glass\"", html, StringComparison.Ordinal);
                Assert.Contains("46 results for <strong>fictional</strong>", html, StringComparison.Ordinal);
                Assert.Contains("1 of 3 pages", html, StringComparison.Ordinal);
                Assert.Equal(16, ReportHeadingRegex().Count(html));
            }

            using (HttpRequestMessage detail = new(HttpMethod.Get, "/Xenon/Cases/Detail/1"))
            {
                detail.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage rendered = await client.SendAsync(detail, token);
                Assert.Equal(HttpStatusCode.OK, rendered.StatusCode);
                string html = await rendered.Content.ReadAsStringAsync(token);
                Assert.Contains("<span>@reporter.xenon.invalid</span>", html, StringComparison.Ordinal);
                Assert.Matches("""class="identity-tooltip" role="tooltip" id="identity-[^"]+"><code>did:web:reporter.xenon.invalid</code></span>""", html);
                Assert.Contains("name=\"assignee\"", html, StringComparison.Ordinal);
                Assert.Contains(">@team-moderator.xenon.invalid</option>", html, StringComparison.Ordinal);
                Assert.Contains(">@team-administrator.xenon.invalid</option>", html, StringComparison.Ordinal);
                Assert.DoesNotContain(">@team-viewer.xenon.invalid</option>", html, StringComparison.Ordinal);
                Assert.DoesNotContain(">@team-suspended.xenon.invalid</option>", html, StringComparison.Ordinal);
                Assert.DoesNotContain(">@team-removed.xenon.invalid</option>", html, StringComparison.Ordinal);
                Assert.Contains("class=\"moderation-history\"", html, StringComparison.Ordinal);
                Assert.Contains("Moderation history", html, StringComparison.Ordinal);
                Assert.Matches("""<p>Assignee:\s*<span class="identity" tabindex="0" aria-describedby="identity-""", html);
                Assert.DoesNotContain("Reported CID:", html, StringComparison.Ordinal);
                Assert.DoesNotContain("and its reports, newest first", html, StringComparison.Ordinal);
                Assert.DoesNotContain("aria-label=\"Moderation history pages\"", html, StringComparison.Ordinal);
                Assert.Contains("class=\"history-reference\"", html, StringComparison.Ordinal);
                Assert.DoesNotMatch(@"Case event #\d+ ·", html);
                Assert.Contains("data-time-zone-toggle hidden", html, StringComparison.Ordinal);
                Assert.Contains("aria-pressed=\"false\"", html, StringComparison.Ordinal);
                Assert.Contains("aria-describedby=\"time-zone-help\"", html, StringComparison.Ordinal);
                Assert.Contains("aria-controls=\"page-content\"", html, StringComparison.Ordinal);
                Assert.Single(TimeZoneToggleRegex().Matches(html));
                Assert.Contains("class=\"form-actions\"><button type=\"submit\">Submit", html, StringComparison.Ordinal);
                Assert.Contains("name=\"handleOtherReports\" value=\"true\" checked", html, StringComparison.Ordinal);
                Assert.Contains("name=\"selectedLabels\" value=\"spam\" checked", html, StringComparison.Ordinal);
                Assert.Contains("class=\"subject-icon fa-user\" aria-hidden=\"true\"></span> Labels for this profile</legend>", html, StringComparison.Ordinal);
                Assert.DoesNotContain("Select labels to apply. Clear an applied label", html, StringComparison.Ordinal);
                Assert.DoesNotContain("name=\"policyVersion\"", html, StringComparison.Ordinal);
            }

            foreach ((int id, string kind) in new[] { (1, "profile"), (3, "post") })
            {
                using HttpRequestMessage detail = new(HttpMethod.Get, "/Xenon/Reports/Detail/" + id);
                detail.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage rendered = await client.SendAsync(detail, token);
                string html = await rendered.Content.ReadAsStringAsync(token);
                Assert.True(rendered.StatusCode == HttpStatusCode.OK, html);
                Assert.Contains($"Open {kind} on Bluesky", html, StringComparison.Ordinal);
                Assert.Contains("https://bsky.app/profile/", html, StringComparison.Ordinal);
                Assert.DoesNotContain("https://bsky.app/profile/did%3A", html, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("target=\"_blank\" rel=\"noopener noreferrer\"", html, StringComparison.Ordinal);
                Assert.Contains("class=\"fa-icon fa-bluesky\"", html, StringComparison.Ordinal);
                Assert.Equal(kind == "profile", html.Contains("not a snapshot", StringComparison.Ordinal));
                Assert.Contains("data:image/png;base64,", html, StringComparison.Ordinal);
                if (kind == "profile")
                {
                    Assert.Contains("class=\"profile-preview\"", html, StringComparison.Ordinal);
                    Assert.Contains("class=\"profile-preview-banner\"", html, StringComparison.Ordinal);
                    Assert.Contains("class=\"profile-preview-avatar\"", html, StringComparison.Ordinal);
                    Assert.Contains("<strong>20</strong> followers", html, StringComparison.Ordinal);
                    Assert.Contains("<strong>5</strong> following", html, StringComparison.Ordinal);
                    Assert.Contains("<strong>13</strong> posts", html, StringComparison.Ordinal);
                }
                Assert.Contains("&lt;script&gt;not executable&lt;/script&gt;", html, StringComparison.Ordinal);
                Assert.DoesNotContain("<script>not executable</script>", html, StringComparison.Ordinal);
                Assert.DoesNotContain("src=\"https://cdn.bsky.app/img/avatar/plain/did:web:subject-", html, StringComparison.Ordinal);
                Assert.Contains("Reporter</dt>", html, StringComparison.Ordinal);
                Assert.Contains($"class=\"subject-icon fa-{(kind == "profile" ? "user" : "envelope")}\"", html, StringComparison.Ordinal);
                Assert.DoesNotContain("Reported CID:", html, StringComparison.Ordinal);
                Assert.Contains("The user has been verified by Bluesky. They may be high profile, take care in applying or removing labels.", html, StringComparison.Ordinal);
                Assert.Contains("class=\"verification-warning\"", html, StringComparison.Ordinal);
                Assert.Contains("class=\"moderation-history\"", html, StringComparison.Ordinal);
            }

            foreach (int id in new[] { 6, 7, 11, 12, 13, 14 })
            {
                using HttpRequestMessage detail = new(HttpMethod.Get, "/Xenon/Reports/Detail/" + id);
                detail.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage response = await client.SendAsync(detail, token);
                string html = await response.Content.ReadAsStringAsync(token);
                Assert.True(response.StatusCode == HttpStatusCode.OK, html);
                Assert.DoesNotContain("class=\"verification-warning\"", html, StringComparison.Ordinal);
            }

            Xenon.TestHost.SeedData.Require(await team.ChangeTeamAsync(
                caller,
                new(
                    operationId: "ui-second-moderator",
                    action: LabelTeamAction.Enroll,
                    target: Xenon.TestHost.FixtureHandler.Subject(40),
                    reason: "Exercise the handle-based assignment picker.",
                    role: LabelTeamRole.Moderator),
                token));
            using (HttpRequestMessage multipleModerators = new(HttpMethod.Get, "/Xenon/Cases/Detail/1"))
            {
                multipleModerators.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage response = await client.SendAsync(multipleModerators, token);
                string html = await response.Content.ReadAsStringAsync(token);
                Assert.True(response.StatusCode == HttpStatusCode.OK, html);
                Assert.Contains("<select id=\"case-assignee\" name=\"assignee\">", html, StringComparison.Ordinal);
                Assert.Contains(">@subject-40.xenon.invalid</option>", html, StringComparison.Ordinal);
                Assert.Contains(">@reporter.xenon.invalid</option>", html, StringComparison.Ordinal);
                Assert.Contains("class=\"form-actions\"><button type=\"submit\">Save assignment", html, StringComparison.Ordinal);
                Assert.DoesNotContain("Assignee DID", html, StringComparison.Ordinal);
            }

            ILabelModerationStore moderationStore = app.Services.GetRequiredService<ILabelModerationStore>();
            foreach (long caseId in new long[] { 1, 2 })
            {
                LabelModerationCase current = Xenon.TestHost.SeedData.Require(await moderationStore.GetCaseAsync(caller, caseId, token));
                Xenon.TestHost.SeedData.Require(await moderationStore.ChangeCaseAsync(
                    caller: caller,
                    change: new(
                        operationId: $"ui-sort-assignee-{caseId}",
                        subject: current.Subject,
                        action: LabelCaseAction.Assign,
                        reason: "Exercise global assignee ordering, including unassigned cases.",
                        caseId: caseId,
                        expectedRevision: current.Revision,
                        assignee: caseId == 1 ? null : Xenon.TestHost.FixtureHandler.Subject(40)),
                    cancellationToken: token));
            }
            foreach ((bool ascending, int page, int first, int last) in new[]
            {
                (true, 1, 1, 18),
                (true, 3, 35, 2),
                (false, 1, 2, 26),
                (false, 3, 9, 1)
            })
            {
                using HttpRequestMessage sorted = new(HttpMethod.Get, $"/Xenon/Cases?Sort=Assignee&Ascending={ascending}&PageNumber={page}");
                sorted.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage response = await client.SendAsync(sorted, token);
                string html = await response.Content.ReadAsStringAsync(token);
                Assert.True(response.StatusCode == HttpStatusCode.OK, html);
                MatchCollection rows = CaseLinkRegex().Matches(html);
                Assert.Equal(first.ToString(System.Globalization.CultureInfo.InvariantCulture), rows[0].Groups[1].Value);
                Assert.Equal(last.ToString(System.Globalization.CultureInfo.InvariantCulture), rows[^1].Groups[1].Value);
                Assert.Contains(ascending ? "aria-sort=\"ascending\"" : "aria-sort=\"descending\"", html, StringComparison.Ordinal);
            }

            foreach (string query in new[] { "report 40", "no-matching-fixture-text" })
            {
                using HttpRequestMessage search = new(HttpMethod.Get, "/Xenon/Search?Q=" + Uri.EscapeDataString(query));
                search.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage response = await client.SendAsync(search, token);
                string html = await response.Content.ReadAsStringAsync(token);
                Assert.True(response.StatusCode == HttpStatusCode.OK, html);
                Assert.Contains(query == "report 40" ? "1 results for" : "0 results for", html, StringComparison.Ordinal);
                Assert.DoesNotContain("aria-label=\"Search result pages\"", html, StringComparison.Ordinal);
            }

            IReadOnlyList<LabelCaseReceipt> events = Xenon.TestHost.SeedData.Require(await moderationStore.GetCaseHistoryAsync(
                caller: caller, caseId: 1, cancellationToken: token));
            IReadOnlyList<LabelModerationReport> submissions = Xenon.TestHost.SeedData.Require(await app.Services.GetRequiredService<ILabelReportStore>().GetReportsAsync(
                caller: caller, caseId: 1, cancellationToken: token));
            for (int count = events.Count + submissions.Count; count <= 17; count++)
            {
                if (count is 16 or 17)
                {
                    using HttpRequestMessage detail = new(HttpMethod.Get, "/Xenon/Cases/Detail/1");
                    detail.Headers.Add("X-Test-Authenticated", "true");
                    using HttpResponseMessage response = await client.SendAsync(detail, token);
                    string html = await response.Content.ReadAsStringAsync(token);
                    Assert.True(response.StatusCode == HttpStatusCode.OK, html);
                    Assert.Equal(count == 17, html.Contains("aria-label=\"Moderation history pages\"", StringComparison.Ordinal));
                }
                if (count < 17)
                {
                    LabelModerationCase current = Xenon.TestHost.SeedData.Require(await moderationStore.GetCaseAsync(caller, 1, token));
                    Xenon.TestHost.SeedData.Require(await moderationStore.ChangeCaseAsync(
                        caller: caller,
                        change: new(
                            operationId: $"ui-history-pagination-{count}",
                            subject: current.Subject,
                            action: LabelCaseAction.Note,
                            reason: "Exercise the exact single-page history boundary.",
                            caseId: 1,
                            expectedRevision: current.Revision),
                        cancellationToken: token));
                }
            }

            IDidHandleResolver cache = app.Services.GetRequiredService<IDidHandleResolver>();
            Assert.True(cache.TryGetCachedHandle(Xenon.TestHost.FixtureHandler.s_reporter, out Handle? cached));
            Assert.Equal(new Handle("reporter.xenon.invalid"), cached);
            foreach (string path in new[] { "/oauth-client-metadata.json", "/native-client-metadata.json" })
            {
                using HttpRequestMessage permitted = new(HttpMethod.Get, path);
                permitted.Headers.Host = origin.Host;
                permitted.Headers.Add("X-Forwarded-Host", "untrusted.example");
                using HttpResponseMessage response = await client.SendAsync(permitted, token);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                Assert.Equal(new Uri(origin, path).AbsoluteUri, document.RootElement.GetProperty("client_id").GetString());
                using HttpRequestMessage blocked = new(HttpMethod.Get, path);
                blocked.Headers.Host = "untrusted.example";
                blocked.Headers.Add("X-Forwarded-Host", origin.Host);
                using HttpResponseMessage denied = await client.SendAsync(blocked, token);
                Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
            }

            using HttpRequestMessage forwarded = new(HttpMethod.Get, "/scheme");
            forwarded.Headers.Host = origin.Host;
            forwarded.Headers.Add("X-Forwarded-For", "198.51.100.1");
            forwarded.Headers.Add("X-Forwarded-Proto", "https");
            using HttpResponseMessage scheme = await client.SendAsync(forwarded, token);
            Assert.Equal(trusted ? "https" : "http", await scheme.Content.ReadAsStringAsync(token));
            using HttpResponseMessage distribution = await client.GetAsync("/xrpc/com.atproto.label.queryLabels", token);
            Assert.Equal(HttpStatusCode.NotFound, distribution.StatusCode);
            using HttpResponseMessage stylesheet = await client.GetAsync("/Xenon/css/xenon.css", token);
            Assert.Equal(HttpStatusCode.OK, stylesheet.StatusCode);
            Assert.Equal("text/css", stylesheet.Content.Headers.ContentType!.MediaType);
            string css = await stylesheet.Content.ReadAsStringAsync(token);
            Assert.Contains(".site-header nav a, .sign-out { color: white; }", css, StringComparison.Ordinal);
            Assert.Contains(".numeric { text-align: right; font-variant-numeric: tabular-nums; }", css, StringComparison.Ordinal);
            Assert.Contains(".table-pagination { justify-content: flex-end;", css, StringComparison.Ordinal);
            foreach (string icon in new[]
            {
                "envelope", "user", "list", "newspaper", "users", "file-lines",
                "address-card", "magnifying-glass", "circle-exclamation", "clipboard-check",
                "circle-check", "scale-balanced", "globe", "circle-user", "chevron-down", "triangle-exclamation", "bluesky"
            })
            {
                Assert.Contains($"url(\"../vendor/fontawesome/{icon}.svg\")", css, StringComparison.Ordinal);
                using HttpResponseMessage iconResponse = await client.GetAsync($"/Xenon/vendor/fontawesome/{icon}.svg", token);
                Assert.Equal(HttpStatusCode.OK, iconResponse.StatusCode);
                Assert.Equal("image/svg+xml", iconResponse.Content.Headers.ContentType!.MediaType);
                Assert.Contains("Font Awesome Free", await iconResponse.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
            }
            Assert.Contains("text-align: right; color: var(--muted)", css, StringComparison.Ordinal);
            Assert.DoesNotContain("\nnav a, .sign-out", css, StringComparison.Ordinal);
            using HttpResponseMessage logo = await client.GetAsync("/Xenon/images/xenon.svg", token);
            Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
            Assert.Equal("image/svg+xml", logo.Content.Headers.ContentType!.MediaType);
            Assert.Contains("viewBox=\"0 0 64 64\"", await logo.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
            using HttpResponseMessage themeScriptResponse = await client.GetAsync("/Xenon/js/theme.js", token);
            Assert.Equal(HttpStatusCode.OK, themeScriptResponse.StatusCode);
            Assert.Contains("xenon.theme", await themeScriptResponse.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
            await VerifyDecisionWorkflowAsync(client, app.Services, caller, token);
            await VerifyAppealDecisionWorkflowAsync(client, app.Services, caller, token);
            await VerifySubjectDescriptionsAsync(client, app.Services, caller, token);
            await VerifyRepositoryPreviewsAsync(client, token);
            await VerifySensitivePreviewsAsync(client, seededReports, token);
            await VerifyManualLabelsAsync(client, app.Services, token);
            await VerifyPolicyReasonEditorAsync(client, app.Services, caller, token);
            await VerifyPolicyDraftDeletionAsync(client, app.Services, caller, token);
            Xenon.TestHost.SeedData.Require(await team.ChangeTeamAsync(
                caller: caller,
                change: new(
                    operationId: "test-customize-fixture-role",
                    action: LabelTeamAction.SetRole,
                    target: Xenon.TestHost.FixtureHandler.TeamMember("viewer"),
                    reason: "Keep operator edits when fixtures are resumed.",
                    role: LabelTeamRole.Moderator),
                cancellationToken: token));
            await app.StopAsync(token);
            ServiceCollection restored = new();
            restored.AddLogging();
            restored.AddSqliteLabelStorage($"Data Source={database};Pooling=False");
            restored.AddLabelerModeration(Xenon.TestHost.FixtureHandler.s_issuer, identity);
            restored.AddLabelerAdministration(new(Xenon.TestHost.FixtureHandler.s_issuer, "labeler_admin"), key, identity);
            await using ServiceProvider reopened = restored.BuildServiceProvider();
            await reopened.GetRequiredService<ILabelStore>().InitializeAsync(Xenon.TestHost.FixtureHandler.s_issuer, token);
            await reopened.GetRequiredService<ILabelPolicyStore>().InitializePolicyAsync(cancellationToken: token);
            await Xenon.TestHost.SeedData.InitializeAsync(
                services: reopened,
                administrator: Xenon.TestHost.FixtureHandler.s_reporter,
                identity: identity,
                key: key,
                cancellationToken: token);
            LabelAdministrationCaller restoredCaller = Xenon.TestHost.SeedData.Require(await reopened.GetRequiredService<ILabelAdministrationStore>()
                .AuthenticateBrowserAsync(Xenon.TestHost.FixtureHandler.s_reporter, token));
            IReadOnlyList<LabelTeamMember> restoredTeam = Xenon.TestHost.SeedData.Require(
                await reopened.GetRequiredService<ILabelAdministrationStore>().GetTeamAsync(restoredCaller, token));
            Assert.Equal(LabelTeamRole.Moderator, Assert.Single(restoredTeam,
                member => member.Did == Xenon.TestHost.FixtureHandler.TeamMember("viewer")).Role);
            Assert.Equal(8, restoredTeam.Count);
            Assert.Equal(47, Xenon.TestHost.SeedData.Require(await reopened.GetRequiredService<ILabelReportStore>()
                .GetReportsAsync(restoredCaller, cancellationToken: token)).Count);
            using HttpClient botSeeds = new(new BotSeedHandler(identity));
            for (int attempt = 0; attempt < 2; attempt++)
            {
                await Xenon.TestHost.SeedData.InitializeRealPostsAsync(
                    services: reopened,
                    administrator: Xenon.TestHost.FixtureHandler.s_reporter,
                    identity: botSeeds,
                    key: key,
                    cancellationToken: token);
                IReadOnlyList<LabelModerationReport> all = Xenon.TestHost.SeedData.Require(await reopened.GetRequiredService<ILabelReportStore>()
                    .GetReportsAsync(restoredCaller, cancellationToken: token));
                Assert.Equal(53, all.Count);
                Assert.Equal(6, all.Count(report => report.Submission.Subject.Repository.Value == BotSeedHandler.Author));
                Assert.Single(all, report => report.Submission.Subject.RecordUri?.ToString() ==
                    $"at://{BotSeedHandler.Author}/app.bsky.graph.list/3lnxvrbetf32n");
                Assert.Single(all, report => report.Submission.Subject.RecordUri?.ToString() ==
                    $"at://{BotSeedHandler.Author}/app.bsky.actor.profile/self");
                Assert.All(all.Where(report => report.Submission.Subject.Repository.Value == BotSeedHandler.Author),
                    report => Assert.Contains("not an allegation", report.Submission.Reason!, StringComparison.Ordinal));
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Checks repeatable role, revocation, local identity and policy examples without granting fictional identities browser access.</summary>
    private static async Task VerifyAdministrationFixturesAsync(
        IServiceProvider services,
        LabelAdministrationCaller caller,
        bool legacyPolicy,
        CancellationToken cancellationToken)
    {
        ILabelAdministrationStore team = services.GetRequiredService<ILabelAdministrationStore>();
        IReadOnlyList<LabelTeamMember> members = Xenon.TestHost.SeedData.Require(await team.GetTeamAsync(caller, cancellationToken));
        Assert.Equal(7, members.Count);
        foreach ((string name, LabelTeamRole role, bool active, bool removed) in new[]
        {
            ("administrator", LabelTeamRole.Administrator, true, false),
            ("moderator", LabelTeamRole.Moderator, true, false),
            ("viewer", LabelTeamRole.Viewer, true, false),
            ("suspended", LabelTeamRole.Moderator, false, false),
            ("removed", LabelTeamRole.Viewer, false, true),
            ("local", LabelTeamRole.Viewer, true, false)
        })
        {
            LabelTeamMember member = Assert.Single(members, member => member.Did == Xenon.TestHost.FixtureHandler.TeamMember(name));
            Assert.Equal(role, member.Role);
            Assert.Equal(active, member.Active);
            Assert.Equal(removed, member.Removed);
            Assert.Equal(active, (await team.AuthenticateBrowserAsync(member.Did, cancellationToken)).Succeeded);
        }

        LabelAdministrationCaller local = Xenon.TestHost.SeedData.Require(await team.AuthenticateLocalAsync("uid:4294967294", cancellationToken));
        Assert.Equal(Xenon.TestHost.FixtureHandler.TeamMember("local"), local.Actor);
        Assert.False((await team.AuthorizeAsync(
            caller: local,
            permission: LabelAdministrationPermission.ManageTeam,
            reason: "The fictional local CLI viewer cannot administer the team.",
            cancellationToken: cancellationToken)).Succeeded);
        ILabelPolicyStore policies = services.GetRequiredService<ILabelPolicyStore>();
        LabelPolicyState state = Xenon.TestHost.SeedData.Require(await policies.GetPolicyStateAsync(caller, cancellationToken));
        Assert.Equal("test-v2", state.Policy!.Issuance.Version);
        foreach (string version in new[] { "xenon-draft-accounts", "xenon-draft-records" })
        {
            LabelPolicyDefinition draft = Xenon.TestHost.SeedData.Require(await policies.GetPolicyVersionAsync(caller, version, cancellationToken));
            Assert.Equal(version == "xenon-draft-accounts", draft.Issuance.AllowAccounts);
            Assert.Contains("Fictional", draft.Rules, StringComparison.Ordinal);
        }
        IReadOnlyList<LabelPolicyHistoryEntry> history = Xenon.TestHost.SeedData.Require(
            await policies.GetPolicyHistoryAsync(caller, cancellationToken: cancellationToken));
        Assert.Single(history, entry => entry.OperationId == "xenon-draft-accounts");
        Assert.Single(history, entry => entry.OperationId == "xenon-draft-records");
        Assert.Equal(!legacyPolicy, history.Any(entry =>
            entry.State?.Policy?.Issuance.Version == "xenon-history-v1" &&
            entry.State.Phase == LabelPolicyPhase.VerifiedActive));
    }

    /// <summary>Saves all, selected, empty and custom reason restrictions without changing the active policy or declaration extensions.</summary>
    private static async Task VerifyPolicyReasonEditorAsync(
        HttpClient client,
        IServiceProvider services,
        LabelAdministrationCaller caller,
        CancellationToken token)
    {
        ILabelPolicyStore policies = services.GetRequiredService<ILabelPolicyStore>();
        LabelPolicyState before = (await policies.ReadPolicyStateAsync(token))!;
        foreach (string mode in new[] { "all", "selected", "none" })
        {
            using HttpRequestMessage get = new(HttpMethod.Get, "/Xenon/Policy");
            get.Headers.Add("X-Test-Authenticated", "true");
            using HttpResponseMessage page = await client.SendAsync(get, token);
            string html = await page.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Contains("Accepted report reasons", html, StringComparison.Ordinal);
            Assert.Contains("Only the selected reasons below", html, StringComparison.Ordinal);
            Assert.Contains("automatically handled as Not Applicable", html, StringComparison.Ordinal);
            Assert.Contains(">Current State</a>", html, StringComparison.Ordinal);
            Assert.Contains(">Labels</a>", html, StringComparison.Ordinal);
            Assert.Contains(">Moderation Policy</a>", html, StringComparison.Ordinal);
            Assert.Contains(">Advanced</a>", html, StringComparison.Ordinal);
            Assert.Contains(">Publication</a>", html, StringComparison.Ordinal);
            Assert.True(html.IndexOf(">Publication</a>", StringComparison.Ordinal) > html.IndexOf(">Advanced</a>", StringComparison.Ordinal));
            Assert.True(html.IndexOf("id=\"policy-publication\"", StringComparison.Ordinal) < html.IndexOf("<h2>Begin policy publication</h2>", StringComparison.Ordinal));
            Assert.Contains("Current policy: test-v2", html, StringComparison.Ordinal);
            Assert.Contains("<h2>Saved drafts</h2>", html, StringComparison.Ordinal);
            Assert.Contains("<table class=\"policy-state\" aria-label=\"Current policy state\">", html, StringComparison.Ordinal);
            Assert.Matches("""<th scope="row">Phase</th><td>.*?<strong>Verified Active</strong>""", html);
            Assert.Contains("<th scope=\"row\">Verification freshness</th>", html, StringComparison.Ordinal);
            Assert.Contains("<th scope=\"row\">Verification valid until</th>", html, StringComparison.Ordinal);
            Assert.Contains("data-policy-load=\"draft\"", html, StringComparison.Ordinal);
            Assert.Contains("data-policy-clear", html, StringComparison.Ordinal);
            Assert.Contains("Accepted report subjects", html, StringComparison.Ordinal);
            Assert.Contains("not executable and not part of the public declaration", html, StringComparison.Ordinal);
            Assert.Matches("""value="tools.ozone.report.defs#reasonOther"[^>]*checked""", html);
            Dictionary<string, string> form = new()
            {
                ["__RequestVerificationToken"] = ReadField(html, "__RequestVerificationToken"),
                ["operationId"] = "policy-reasons-" + mode,
                ["version"] = "reason-editor-" + mode,
                ["reason"] = "Exercise friendly reason selection.",
                ["rules"] = "Test draft only.",
                ["allowAccounts"] = "true",
                ["allowRecords"] = "true",
                ["declaration"] = Xenon.TestHost.FixtureHandler.s_declaration.TrimEnd()[..^1] + ""","customExtension":{"retained":true}}""",
                ["reasonMode"] = mode == "all" ? "all" : "selected"
            };
            if (mode == "selected")
            {
                form["reasonTypes"] = "tools.ozone.report.defs#reasonOther";
                form["customReasons"] = "example.custom.report#reasonOne\r\nexample.custom.report#reasonOne";
            }
            using HttpRequestMessage post = new(HttpMethod.Post, "/Xenon/Policy?handler=Save");
            post.Headers.Add("X-Test-Authenticated", "true");
            post.Content = new FormUrlEncodedContent(form);
            using HttpResponseMessage saved = await client.SendAsync(post, token);
            Assert.True(saved.StatusCode == HttpStatusCode.Redirect, await saved.Content.ReadAsStringAsync(token));
            LabelPolicyDefinition draft = Xenon.TestHost.SeedData.Require(await policies.GetPolicyVersionAsync(
                caller, form["version"], token));
            if (mode == "all")
            {
                Assert.Null(draft.ReportScope.ReasonTypes);
            }
            else
            {
                Assert.Equal(mode == "selected" ? ["tools.ozone.report.defs#reasonOther", "example.custom.report#reasonOne"] : Array.Empty<string>(),
                    draft.ReportScope.ReasonTypes);
            }
            using JsonDocument declaration = JsonDocument.Parse(draft.DeclarationJson);
            Assert.True(declaration.RootElement.GetProperty("customExtension").GetProperty("retained").GetBoolean());
            Assert.Equal(before.Revision, (await policies.ReadPolicyStateAsync(token))!.Revision);
        }

        foreach (string source in new[] { "current", "draft", "draft&draft=reason-editor-selected", "clear" })
        {
            using HttpRequestMessage get = new(HttpMethod.Get, "/Xenon/Policy?editor=" + source);
            get.Headers.Add("X-Test-Authenticated", "true");
            using HttpResponseMessage page = await client.SendAsync(get, token);
            string html = await page.Content.ReadAsStringAsync(token);
            Assert.Contains(source switch
            {
                "draft" => "Saved draft: reason-editor-none",
                "draft&draft=reason-editor-selected" => "Saved draft: reason-editor-selected",
                "current" => "Current policy: test-v2",
                _ => "New policy"
            }, html, StringComparison.Ordinal);
            Assert.Contains("Policy ID", html, StringComparison.Ordinal);
            Assert.Contains("name=\"version\" required maxlength=\"256\" value=\"\" aria-describedby=\"policy-id-help\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("readonly", html, StringComparison.Ordinal);
            Assert.Contains("The Policy ID is internal to this app and is not published as part of the policy.", html, StringComparison.Ordinal);
            Assert.Contains("aria-describedby=\"publication-policy-id-help\"", html, StringComparison.Ordinal);
            int publicationStart = html.IndexOf("<div id=\"policy-publication\"", StringComparison.Ordinal);
            string publicationHtml = html[publicationStart..];
            Assert.Contains("<select name=\"version\" required aria-describedby=\"publication-policy-id-help\"", publicationHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("<input name=\"version\"", publicationHtml, StringComparison.Ordinal);
            foreach (string draftId in new[] { "reason-editor-all", "reason-editor-selected", "reason-editor-none" })
            {
                Assert.Contains($"<option value=\"{draftId}\">{draftId}</option>", publicationHtml, StringComparison.Ordinal);
            }
            Assert.DoesNotContain("<option value=\"test-v2\">", publicationHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("Confirm this immutable policy version", html, StringComparison.Ordinal);
        }

        using HttpRequestMessage initial = new(HttpMethod.Get, "/Xenon/Policy");
        initial.Headers.Add("X-Test-Authenticated", "true");
        using HttpResponseMessage initialPage = await client.SendAsync(initial, token);
        string initialHtml = await initialPage.Content.ReadAsStringAsync(token);
        Dictionary<string, string> scopedForm = new()
        {
            ["__RequestVerificationToken"] = ReadField(initialHtml, "__RequestVerificationToken"),
            ["operationId"] = "policy-structured-scope",
            ["version"] = "structured-scope",
            ["reason"] = "Test independent report and issuance scopes.",
            ["rules"] = "Preserved guidance.",
            ["allowAccounts"] = "true",
            ["collections"] = "app.bsky.graph.list",
            ["declaration"] = Xenon.TestHost.FixtureHandler.s_declaration,
            ["reasonMode"] = "all",
            ["structuredScope"] = "true",
            ["structuredIssuance"] = "true",
            ["reportSubjects"] = "record",
            ["reportCollections"] = "app.bsky.feed.post"
        };
        using HttpRequestMessage scopedPost = new(HttpMethod.Post, "/Xenon/Policy?handler=Save");
        scopedPost.Headers.Add("X-Test-Authenticated", "true");
        scopedPost.Content = new FormUrlEncodedContent(scopedForm);
        using HttpResponseMessage scopedSaved = await client.SendAsync(scopedPost, token);
        Assert.True(scopedSaved.StatusCode == HttpStatusCode.Redirect, await scopedSaved.Content.ReadAsStringAsync(token));
        LabelPolicyDefinition scopedPolicy = Xenon.TestHost.SeedData.Require(await policies.GetPolicyVersionAsync(caller, "structured-scope", token));
        Assert.Equal(["record"], scopedPolicy.ReportScope.SubjectTypes);
        Assert.Equal("app.bsky.feed.post", Assert.Single(scopedPolicy.ReportScope.SubjectCollections!).ToString());
        Assert.True(scopedPolicy.Issuance.AllowAccounts);
        Assert.False(scopedPolicy.Issuance.AllowRecords);
        Assert.Equal("app.bsky.graph.list", Assert.Single(scopedPolicy.Issuance.AllowedRecordCollections!).ToString());

        foreach (bool all in new[] { false, true })
        {
            scopedForm["operationId"] = "policy-collection-mode-" + all;
            scopedForm["version"] = "collection-mode-" + all;
            scopedForm["reportCollections"] = string.Empty;
            scopedForm["collections"] = string.Empty;
            scopedForm["reportAllCollections"] = all ? "true" : "false";
            scopedForm["issueAllCollections"] = all ? "true" : "false";
            using HttpRequestMessage modePost = new(HttpMethod.Post, "/Xenon/Policy?handler=Save");
            modePost.Headers.Add("X-Test-Authenticated", "true");
            modePost.Content = new FormUrlEncodedContent(scopedForm);
            using HttpResponseMessage modeSaved = await client.SendAsync(modePost, token);
            Assert.True(modeSaved.StatusCode == HttpStatusCode.Redirect, await modeSaved.Content.ReadAsStringAsync(token));
            LabelPolicyDefinition modePolicy = Xenon.TestHost.SeedData.Require(await policies.GetPolicyVersionAsync(caller, scopedForm["version"], token));
            if (all)
            {
                Assert.Null(modePolicy.ReportScope.SubjectCollections);
                Assert.Null(modePolicy.Issuance.AllowedRecordCollections);
            }
            else
            {
                Assert.Empty(modePolicy.ReportScope.SubjectCollections!);
                Assert.Empty(modePolicy.Issuance.AllowedRecordCollections!);
            }
        }

        scopedForm["operationId"] = "policy-missing-id";
        scopedForm.Remove("version");
        using (HttpRequestMessage missingIdPost = new(HttpMethod.Post, "/Xenon/Policy?handler=Save"))
        {
            missingIdPost.Headers.Add("X-Test-Authenticated", "true");
            missingIdPost.Content = new FormUrlEncodedContent(scopedForm);
            using HttpResponseMessage missingId = await client.SendAsync(missingIdPost, token);
            Assert.Contains("Enter a Policy ID before saving.", await missingId.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        }
        Assert.False((await policies.GetPolicyOperationAsync(caller, scopedForm["operationId"], token)).Succeeded);

        scopedForm["operationId"] = "policy-invalid-editor";
        scopedForm["version"] = "retained-invalid-version";
        scopedForm["declaration"] = "{ invalid JSON";
        using HttpRequestMessage invalidPost = new(HttpMethod.Post, "/Xenon/Policy?handler=Save");
        invalidPost.Headers.Add("X-Test-Authenticated", "true");
        invalidPost.Content = new FormUrlEncodedContent(scopedForm);
        using HttpResponseMessage invalid = await client.SendAsync(invalidPost, token);
        string invalidHtml = await invalid.Content.ReadAsStringAsync(token);
        Assert.Contains("The policy declaration must be valid JSON.", invalidHtml, StringComparison.Ordinal);
        Assert.Contains("{ invalid JSON</textarea>", invalidHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"retained-invalid-version\"", invalidHtml, StringComparison.Ordinal);
        Assert.Contains("Preserved guidance.</textarea>", invalidHtml, StringComparison.Ordinal);
        Assert.Equal(before.Revision, (await policies.ReadPolicyStateAsync(token))!.Revision);
    }

    /// <summary>Checks two-step draft deletion, retry recovery and protection of the selected policy.</summary>
    private static async Task VerifyPolicyDraftDeletionAsync(
        HttpClient client,
        IServiceProvider services,
        LabelAdministrationCaller caller,
        CancellationToken token)
    {
        ILabelPolicyStore policies = services.GetRequiredService<ILabelPolicyStore>();
        LabelPolicyState before = (await policies.ReadPolicyStateAsync(token))!;
        using HttpRequestMessage initial = new(HttpMethod.Get, "/Xenon/Policy");
        initial.Headers.Add("X-Test-Authenticated", "true");
        using HttpResponseMessage page = await client.SendAsync(initial, token);
        string html = await page.Content.ReadAsStringAsync(token);
        Assert.Contains("data-policy-version=\"reason-editor-none\"", html, StringComparison.Ordinal);
        Assert.Contains("data-policy-version=\"reason-editor-selected\"", html, StringComparison.Ordinal);
        Dictionary<string, string> form = new()
        {
            ["__RequestVerificationToken"] = ReadField(html, "__RequestVerificationToken"),
            ["version"] = "reason-editor-none",
            ["operationId"] = "editor-delete-draft",
            ["reason"] = "Discard unused editor test draft."
        };
        using HttpRequestMessage unconfirmed = new(HttpMethod.Post, "/Xenon/Policy?handler=Delete");
        unconfirmed.Headers.Add("X-Test-Authenticated", "true");
        unconfirmed.Content = new FormUrlEncodedContent(form);
        using HttpResponseMessage rejected = await client.SendAsync(unconfirmed, token);
        Assert.Contains("Confirm draft deletion before submitting.", await rejected.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        Assert.True((await policies.GetPolicyVersionAsync(caller, form["version"], token)).Succeeded);

        using HttpRequestMessage request = new(HttpMethod.Post, "/Xenon/Policy?handler=DeleteRequest");
        request.Headers.Add("X-Test-Authenticated", "true");
        request.Content = new FormUrlEncodedContent(form);
        using HttpResponseMessage confirmation = await client.SendAsync(request, token);
        string confirmationHtml = await confirmation.Content.ReadAsStringAsync(token);
        Assert.Contains(">Are you sure?</h2>", confirmationHtml, StringComparison.Ordinal);
        Assert.True((await policies.GetPolicyVersionAsync(caller, form["version"], token)).Succeeded);
        form["confirmed"] = "true";
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using HttpRequestMessage delete = new(HttpMethod.Post, "/Xenon/Policy?handler=Delete");
            delete.Headers.Add("X-Test-Authenticated", "true");
            delete.Content = new FormUrlEncodedContent(form);
            using HttpResponseMessage deleted = await client.SendAsync(delete, token);
            Assert.True(deleted.StatusCode == HttpStatusCode.Redirect, await deleted.Content.ReadAsStringAsync(token));
        }
        Assert.False((await policies.GetPolicyVersionAsync(caller, form["version"], token)).Succeeded);
        IReadOnlyList<LabelPolicyHistoryEntry> history = Xenon.TestHost.SeedData.Require(
            await policies.GetPolicyHistoryAsync(caller, cancellationToken: token));
        Assert.Single(history, entry => entry.Action == LabelPolicyHistoryAction.DraftDeletion && entry.Policy?.Issuance.Version == form["version"]);
        using HttpRequestMessage after = new(HttpMethod.Get, "/Xenon/Policy");
        after.Headers.Add("X-Test-Authenticated", "true");
        using HttpResponseMessage afterPage = await client.SendAsync(after, token);
        string afterHtml = await afterPage.Content.ReadAsStringAsync(token);
        Assert.DoesNotContain("data-policy-version=\"reason-editor-none\"", afterHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"reason-editor-none\">", afterHtml, StringComparison.Ordinal);
        Assert.Contains("data-policy-version=\"reason-editor-selected\"", afterHtml, StringComparison.Ordinal);

        form["version"] = before.Policy!.Issuance.Version;
        form["operationId"] = "editor-delete-current-denied";
        using HttpRequestMessage active = new(HttpMethod.Post, "/Xenon/Policy?handler=Delete");
        active.Headers.Add("X-Test-Authenticated", "true");
        active.Content = new FormUrlEncodedContent(form);
        using HttpResponseMessage activeRejected = await client.SendAsync(active, token);
        Assert.Contains("Selected or previously published policies cannot be deleted.", await activeRejected.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        Assert.Equal(before.Revision, (await policies.ReadPolicyStateAsync(token))!.Revision);
    }

    /// <summary>Renders blocked AppView identities directly from their repositories and protects deferred video streaming.</summary>
    private static async Task VerifyRepositoryPreviewsAsync(HttpClient client, CancellationToken token)
    {
        foreach (int reportId in new[] { 14, 15 })
        {
            using HttpRequestMessage request = new(HttpMethod.Get, "/Xenon/Reports/Detail/" + reportId);
            request.Headers.Add("X-Test-Authenticated", "true");
            using HttpResponseMessage response = await client.SendAsync(request, token);
            string html = await response.Content.ReadAsStringAsync(token);
            Assert.True(response.StatusCode == HttpStatusCode.OK, html);
            Assert.Equal(reportId == 15, html.Contains("Current CID:", StringComparison.Ordinal));
            if (reportId == 14)
            {
                Assert.DoesNotContain("Current public content, not a snapshot", html, StringComparison.Ordinal);
                Assert.Contains("<dt>AT URI:</dt><dd><code>at://", html, StringComparison.Ordinal);
                Assert.Contains("<dt>CID:</dt><dd><code>", html, StringComparison.Ordinal);
                Assert.True(html.IndexOf("<dt>AT URI:", StringComparison.Ordinal) < html.IndexOf("<dt>CID:", StringComparison.Ordinal));
            }
            Assert.DoesNotContain("· Revision", html, StringComparison.Ordinal);
            Assert.Contains("data:image/png;base64,", html, StringComparison.Ordinal);
            Assert.Contains("&lt;script&gt;not executable&lt;/script&gt;", html, StringComparison.Ordinal);
            Assert.DoesNotContain("The record is unavailable", html, StringComparison.Ordinal);
            Assert.DoesNotContain("class=\"verification-warning\"", html, StringComparison.Ordinal);
            Assert.Contains("Bluesky moderation labels could not be fully checked.", html, StringComparison.Ordinal);
            if (reportId == 15)
            {
                Assert.Contains("Current public content, not a snapshot", html, StringComparison.Ordinal);
                Assert.Contains("class=\"profile-preview\"", html, StringComparison.Ordinal);
                Assert.DoesNotContain("class=\"profile-preview-counts\"", html, StringComparison.Ordinal);
            }
        }

        using HttpRequestMessage videoPage = new(HttpMethod.Get, "/Xenon/Reports/Detail/18");
        videoPage.Headers.Add("X-Test-Authenticated", "true");
        using HttpResponseMessage rendered = await client.SendAsync(videoPage, token);
        string videoHtml = await rendered.Content.ReadAsStringAsync(token);
        Assert.True(rendered.StatusCode == HttpStatusCode.OK, videoHtml);
        Assert.Contains("class=\"fa-icon fa-circle-play\" aria-hidden=\"true\"", videoHtml, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Open video player\"", videoHtml, StringComparison.Ordinal);
        Assert.Contains("data-video-width=\"9\" data-video-height=\"16\"", videoHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Video — open player", WebUtility.HtmlDecode(videoHtml), StringComparison.Ordinal);
        Assert.Contains("Videos require opening the player.", videoHtml, StringComparison.Ordinal);
        Assert.Contains("<video controls preload=\"none\" playsinline", videoHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("autoplay", videoHtml, StringComparison.Ordinal);
        Assert.DoesNotMatch("<video[^>]+src=", videoHtml);
        Match reference = VideoUrlRegex().Match(videoHtml);
        Assert.True(reference.Success, videoHtml);
        string videoUrl = WebUtility.HtmlDecode(reference.Groups[1].Value);
        Assert.StartsWith("/Xenon/Media?token=", videoUrl, StringComparison.Ordinal);
        using HttpResponseMessage unauthenticated = await client.GetAsync(videoUrl, token);
        Assert.Equal(HttpStatusCode.Redirect, unauthenticated.StatusCode);

        using HttpRequestMessage video = new(HttpMethod.Get, videoUrl);
        video.Headers.Add("X-Test-Authenticated", "true");
        using HttpResponseMessage streamed = await client.SendAsync(video, token);
        Assert.Equal(HttpStatusCode.OK, streamed.StatusCode);
        Assert.Equal("video/mp4", streamed.Content.Headers.ContentType!.MediaType);
        Assert.Equal(20, (await streamed.Content.ReadAsByteArrayAsync(token)).Length);
        Assert.True(streamed.Headers.CacheControl!.NoStore);
        Assert.Equal("nosniff", streamed.Headers.GetValues("X-Content-Type-Options").Single());
        using HttpRequestMessage invalid = new(HttpMethod.Get, "/Xenon/Media?token=invalid");
        invalid.Headers.Add("X-Test-Authenticated", "true");
        using HttpResponseMessage rejected = await client.SendAsync(invalid, token);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        using HttpRequestMessage gallery = new(HttpMethod.Get, "/Xenon/Reports/Detail/20");
        gallery.Headers.Add("X-Test-Authenticated", "true");
        using HttpResponseMessage galleryResponse = await client.SendAsync(gallery, token);
        string galleryHtml = await galleryResponse.Content.ReadAsStringAsync(token);
        Assert.True(galleryResponse.StatusCode == HttpStatusCode.OK, galleryHtml);
        Assert.Contains("alt=\"Fictional gallery image one\"", galleryHtml, StringComparison.Ordinal);
        Assert.Contains("alt=\"Fictional gallery image two\"", galleryHtml, StringComparison.Ordinal);
        Assert.Contains("<figcaption>Fictional gallery image one</figcaption>", galleryHtml, StringComparison.Ordinal);
        Assert.Contains("<figcaption>Fictional gallery image two</figcaption>", galleryHtml, StringComparison.Ordinal);
        Assert.Contains("class=\"preview-images preview-gallery\" tabindex=\"0\" role=\"region\"", galleryHtml, StringComparison.Ordinal);
        Assert.Contains("https://bsky.app/profile/did:web:subject-14.xenon.invalid/post/example", galleryHtml, StringComparison.Ordinal);
        Assert.Equal(2, GalleryImageRegex().Count(galleryHtml));
    }

    /// <summary>Protects media using exact current targets and trusted warnings without hiding ordinary content or treating failed lookups as clean results.</summary>
    private static async Task VerifySensitivePreviewsAsync(
        HttpClient client,
        IReadOnlyList<LabelModerationReport> reports,
        CancellationToken token)
    {
        foreach (int caseId in Enumerable.Range(17, 24))
        {
            long reportId = reports.First(report => report.CaseId == caseId).Id;
            foreach (string path in new[] { "/Xenon/Cases/Detail/" + caseId, "/Xenon/Reports/Detail/" + reportId })
            {
                using HttpRequestMessage request = new(HttpMethod.Get, path);
                request.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage response = await client.SendAsync(request, token);
                string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(token));
                Assert.True(response.StatusCode == HttpStatusCode.OK, html);
                bool blurred = caseId is 18 or 19 or 21 or 22 or 23 or 24 or 25 or 31 or 32 or 33 or 34 or 36 or 38 or 39;
                Assert.Contains($"data-media-warning=\"{blurred.ToString().ToLowerInvariant()}\"", html, StringComparison.Ordinal);
                Assert.Equal(blurred, html.Contains("data-media-toggle aria-pressed=\"false\" aria-label=\"Show images\"", StringComparison.Ordinal));
                Assert.Equal(blurred, html.Contains("data-sensitive-image=\"\" aria-hidden=\"true\"", StringComparison.Ordinal));
                Assert.Contains("data:image/png;base64,", html, StringComparison.Ordinal);
                Assert.Equal(caseId % 2 != 0, html.Contains("Current CID:", StringComparison.Ordinal));
                Assert.Equal(caseId is 17 or 18 or 19 or 35 or 36, html.Contains("Bluesky moderation labels could not be fully checked.", StringComparison.Ordinal));
                if (blurred)
                {
                    Assert.True(html.IndexOf("class=\"media-warning-caption\"", StringComparison.Ordinal) >
                        html.IndexOf("</article>", html.IndexOf("data-media-warning=", StringComparison.Ordinal), StringComparison.Ordinal));
                    Assert.Contains("class=\"fa-icon fa-circle-exclamation\" aria-hidden=\"true\"></span> Sensitive media:", html, StringComparison.Ordinal);
                    Assert.DoesNotContain("Use the eye button", html, StringComparison.Ordinal);
                    Assert.DoesNotContain("Videos require opening the player.", html, StringComparison.Ordinal);
                    Assert.Contains(caseId is 21 or 22 or 23 or 24 or 36 ? "(self-label)" : "(Bluesky moderation)", html, StringComparison.Ordinal);
                    Assert.Contains("Showing blurred images requires JavaScript.", html, StringComparison.Ordinal);
                }
            }
        }
    }

    /// <summary>Checks identity selection and audited membership submission without redundant confirmation.</summary>
    private static async Task VerifyTeamFormAsync(HttpClient client, CancellationToken token)
    {
        string did = Xenon.TestHost.FixtureHandler.TeamMember("viewer").Value;
        using HttpRequestMessage request = new(HttpMethod.Get, "/Xenon/Team?member=" + Uri.EscapeDataString(did));
        request.Headers.Add("X-Test-Authenticated", "true");
        using HttpResponseMessage response = await client.SendAsync(request, token);
        string html = await response.Content.ReadAsStringAsync(token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("#change-member", html, StringComparison.Ordinal);
        Assert.Contains("@team-viewer.xenon.invalid", html, StringComparison.Ordinal);
        Assert.Contains($"name=\"target\" value=\"{did}\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"Viewer\" selected=\"selected\"", html, StringComparison.Ordinal);
        Assert.Contains(">Update</button>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"confirmed\"", html, StringComparison.Ordinal);

        foreach (string icon in new[] { "eye", "gavel", "user-gear", "circle-check", "circle-pause", "circle-xmark" })
        {
            Assert.Contains($"class=\"fa-icon fa-{icon}\" aria-hidden=\"true\"", html, StringComparison.Ordinal);
            using HttpResponseMessage asset = await client.GetAsync($"/Xenon/vendor/fontawesome/{icon}.svg", token);
            Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
            Assert.Contains("<svg", await asset.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        }

        string removedDid = Xenon.TestHost.FixtureHandler.TeamMember("removed").Value;
        int removedRow = html.IndexOf("<tr class=\"team-member-removed\">", StringComparison.Ordinal);
        Assert.True(removedRow >= 0);
        int removedRowEnd = html.IndexOf("</tr>", removedRow, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString(removedDid), html[removedRow..removedRowEnd], StringComparison.Ordinal);
        Assert.Contains("class=\"fa-icon fa-circle-xmark\" aria-hidden=\"true\"></span> Removed</td>", html[removedRow..removedRowEnd], StringComparison.Ordinal);
        Assert.Contains("class=\"fa-icon fa-circle-check\" aria-hidden=\"true\"></span> Active</td>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"fa-icon fa-circle-pause\" aria-hidden=\"true\"></span> Suspended</td>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<th scope=\"col\">Removed</th>", html, StringComparison.Ordinal);
        Assert.Contains("data-team-change", html, StringComparison.Ordinal);
        Assert.Contains("data-team-role", html, StringComparison.Ordinal);
        Assert.Contains("class=\"panel team-search\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"team-search-actions\"", html, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"DID or handle\"", html, StringComparison.Ordinal);

        foreach (string search in new[] { did, "team-viewer.xenon.invalid", "@team-viewer.xenon.invalid" })
        {
            using HttpRequestMessage filtered = new(HttpMethod.Get, "/Xenon/Team?Search=" + Uri.EscapeDataString(search) + "&PageNumber=999&Sort=Role");
            filtered.Headers.Add("X-Test-Authenticated", "true");
            using HttpResponseMessage filteredResponse = await client.SendAsync(filtered, token);
            string filteredHtml = await filteredResponse.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
            Assert.Contains("<caption>1 members", filteredHtml, StringComparison.Ordinal);
            Assert.Contains("@team-viewer.xenon.invalid", filteredHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("@team-removed.xenon.invalid", filteredHtml, StringComparison.Ordinal);
            Assert.Contains("Search=", filteredHtml, StringComparison.Ordinal);
        }

        foreach ((string search, string expected) in new[]
        {
            ("did:plc:aaaaaaaaaaaaaaaaaaaaaaaa", "<caption>0 members"),
            ("invalid identity", "Enter a valid DID or handle.")
        })
        {
            using HttpRequestMessage filtered = new(HttpMethod.Get, "/Xenon/Team?Search=" + Uri.EscapeDataString(search));
            filtered.Headers.Add("X-Test-Authenticated", "true");
            using HttpResponseMessage filteredResponse = await client.SendAsync(filtered, token);
            Assert.Contains(expected, await filteredResponse.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        }

        using HttpRequestMessage update = new(HttpMethod.Post, "/Xenon/Team?handler=Change");
        update.Headers.Add("X-Test-Authenticated", "true");
        update.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = ReadField(html, "__RequestVerificationToken"),
            ["operationId"] = Guid.NewGuid().ToString("N"),
            ["target"] = did,
            ["action"] = "SetRole",
            ["role"] = "Viewer",
            ["reason"] = "Keep the fixture viewer role."
        });
        using HttpResponseMessage updated = await client.SendAsync(update, token);
        Assert.Equal(HttpStatusCode.Redirect, updated.StatusCode);

        using HttpRequestMessage enroll = new(HttpMethod.Post, "/Xenon/Team?handler=Enroll");
        enroll.Headers.Add("X-Test-Authenticated", "true");
        enroll.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = ReadField(html, "__RequestVerificationToken"),
            ["operationId"] = Guid.NewGuid().ToString("N"),
            ["target"] = did,
            ["role"] = "Invalid",
            ["reason"] = "Reject invalid roles."
        });
        using HttpResponseMessage rejected = await client.SendAsync(enroll, token);
        Assert.Contains("Choose a valid team role.", await rejected.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
    }

    /// <summary>Exercises background and no-script manual labels, immutable retries and stale or forged browser state.</summary>
    private static async Task VerifyManualLabelsAsync(
        HttpClient client,
        IServiceProvider services,
        CancellationToken token)
    {
        ILabelStore store = services.GetRequiredService<ILabelStore>();
        LabelTarget account = LabelTarget.Account(Xenon.TestHost.FixtureHandler.Subject(40));
        using HttpRequestMessage initial = new(HttpMethod.Get, "/Xenon/Labels");
        initial.Headers.Add("X-Test-Authenticated", "true");
        using HttpResponseMessage initialResponse = await client.SendAsync(initial, token);
        string initialHtml = await initialResponse.Content.ReadAsStringAsync(token);
        Assert.True(initialResponse.StatusCode == HttpStatusCode.OK, initialHtml);
        Assert.Contains("Reason for labelling", initialHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"selectedLabels\" value=\"spam\"", initialHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"confirmed\"", initialHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"value\"", initialHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"action\"", initialHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"targetKind\"", initialHtml, StringComparison.Ordinal);
        string antiforgery = ReadField(initialHtml, "__RequestVerificationToken");
        foreach (string repository in new[]
        {
            Xenon.TestHost.FixtureHandler.Subject(40).Value,
            "subject-40.xenon.invalid"
        })
        {
            foreach (bool background in new[] { false, true })
            {
                Dictionary<string, string> converted = await LoadAsync("at://" + repository, string.Empty, background);
                Assert.Equal(Xenon.TestHost.FixtureHandler.Subject(40).Value, converted["target"]);
            }
        }

        Dictionary<string, string> incomplete = await LoadAsync(
            target: $"at://{Xenon.TestHost.FixtureHandler.Subject(40)}/app.bsky.feed.post",
            cid: string.Empty,
            background: true,
            valid: false);
        Assert.Contains("Enter a complete record AT URI.", incomplete["html"], StringComparison.Ordinal);
        foreach ((string path, string expected) in new[]
        {
            ("profile/subject-40.xenon.invalid", Xenon.TestHost.FixtureHandler.Subject(40).Value),
            ($"profile/{Xenon.TestHost.FixtureHandler.Subject(40)}", Xenon.TestHost.FixtureHandler.Subject(40).Value),
            ("profile/subject-40.xenon.invalid/post/example/?ref=share#fragment", $"at://{Xenon.TestHost.FixtureHandler.Subject(40)}/app.bsky.feed.post/example"),
            ("profile/subject-40.xenon.invalid/lists/example", $"at://{Xenon.TestHost.FixtureHandler.Subject(40)}/app.bsky.graph.list/example"),
            ("profile/subject-40.xenon.invalid/feed/example", $"at://{Xenon.TestHost.FixtureHandler.Subject(40)}/app.bsky.feed.generator/example"),
            ("starter-pack/subject-40.xenon.invalid/example", $"at://{Xenon.TestHost.FixtureHandler.Subject(40)}/app.bsky.graph.starterpack/example")
        })
        {
            Dictionary<string, string> converted = await LoadAsync("https://bsky.app/" + path, string.Empty, true);
            Assert.Equal(expected, converted["target"]);
        }

        foreach (string invalidUrl in new[]
        {
            "https://example.com/profile/subject-40.xenon.invalid/post/example",
            "https://bsky.app.evil.example/profile/subject-40.xenon.invalid/post/example",
            "http://bsky.app/profile/subject-40.xenon.invalid/post/example",
            "https://user@bsky.app/profile/subject-40.xenon.invalid/post/example",
            "https://bsky.app:8443/profile/subject-40.xenon.invalid/post/example",
            "https://bsky.app/profile/subject-40.xenon.invalid/post",
            "https://bsky.app/profile/subject-40.xenon.invalid/post/one%2Ftwo",
            "https://bsky.app/profile/subject-40.xenon.invalid/followers"
        })
        {
            Dictionary<string, string> rejected = await LoadAsync(
                target: invalidUrl,
                cid: string.Empty,
                background: true,
                valid: false);
            Assert.Contains("role=\"alert\"", rejected["html"], StringComparison.Ordinal);
        }

        Dictionary<string, string> loaded = await LoadAsync(Xenon.TestHost.FixtureHandler.Subject(40).Value, string.Empty, false);
        Assert.DoesNotMatch("""name="selectedLabels" value="spam"[^>]*checked""", loaded["html"]);
        Dictionary<string, string> issue = new(loaded) { ["selectedLabels"] = "spam", ["reason"] = "Manual account label test." };
        string issued = await SubmitAsync(issue);
        Assert.Contains("1 label changes completed.", issued, StringComparison.Ordinal);
        long sequence = Xenon.TestHost.SeedData.Require(await store.ReadDecisionAsync(account, "spam", token)).CurrentLabel!.Sequence;
        Assert.Contains("1 label changes completed.", await SubmitAsync(issue), StringComparison.Ordinal);
        Assert.Equal(sequence, Xenon.TestHost.SeedData.Require(await store.ReadDecisionAsync(account, "spam", token)).CurrentLabel!.Sequence);
        Dictionary<string, string> active = await LoadAsync(Xenon.TestHost.FixtureHandler.Subject(40).Value, string.Empty, true);
        Assert.Matches("""name="selectedLabels" value="spam"[^>]*checked""", active["html"]);
        Dictionary<string, string> handle = await LoadAsync("subject-40.xenon.invalid", string.Empty, true);
        Assert.Matches("""name="selectedLabels" value="spam"[^>]*checked""", handle["html"]);

        foreach (Dictionary<string, string> rejected in new[]
        {
            new Dictionary<string, string>(active) { ["selectedLabels"] = "not-declared", ["reason"] = "Forged choice." },
            new Dictionary<string, string>(active) { ["selectedLabels"] = "scam", ["reason"] = " " },
            new Dictionary<string, string>(active) { ["target"] = Xenon.TestHost.FixtureHandler.Subject(39).Value, ["reason"] = "Changed target." },
            new Dictionary<string, string>(active) { ["labelState"] = "forged", ["reason"] = "Invalid state." }
        })
        {
            Assert.Contains("role=\"alert\"", await SubmitAsync(rejected), StringComparison.Ordinal);
            Assert.Equal(sequence, Xenon.TestHost.SeedData.Require(await store.ReadDecisionAsync(account, "spam", token)).CurrentLabel!.Sequence);
        }

        Dictionary<string, string> staleBaseline = await LoadAsync(Xenon.TestHost.FixtureHandler.Subject(40).Value, string.Empty, true);
        Dictionary<string, string> partialBaseline = await LoadAsync(Xenon.TestHost.FixtureHandler.Subject(40).Value, string.Empty, true);
        Dictionary<string, string> remove = new(active) { ["reason"] = "Remove the account label." };
        Assert.Contains("1 label changes completed.", await SubmitAsync(remove), StringComparison.Ordinal);
        Assert.True(Xenon.TestHost.SeedData.Require(await store.ReadDecisionAsync(account, "spam", token)).CurrentLabel!.SignedLabel.Label.IsNegationLabel);
        Dictionary<string, string> stale = new(staleBaseline) { ["reason"] = "Stale removal." };
        string staleHtml = await SubmitAsync(stale);
        Assert.Contains("0 of 1 changes confirmed.", staleHtml, StringComparison.Ordinal);
        Assert.Contains("The label changed since it was loaded.", staleHtml, StringComparison.Ordinal);
        Dictionary<string, string> partial = new(partialBaseline) { ["selectedLabels"] = "scam", ["reason"] = "One addition followed by a stale removal." };
        string partialHtml = await SubmitAsync(partial);
        Assert.Contains("1 of 2 changes confirmed.", partialHtml, StringComparison.Ordinal);
        Assert.Contains("Retry unchanged changes", partialHtml, StringComparison.Ordinal);
        long scamSequence = Xenon.TestHost.SeedData.Require(await store.ReadDecisionAsync(account, "scam", token)).CurrentLabel!.Sequence;
        Dictionary<string, string> retry = new(partial)
        {
            ["labelState"] = ReadField(partialHtml, "labelState"),
            ["selectedLabels"] = "spam",
            ["reason"] = "Tampered retry reason must not replace protected intent."
        };
        Assert.Contains("1 of 2 changes confirmed.", await SubmitAsync(retry), StringComparison.Ordinal);
        Assert.Equal(scamSequence, Xenon.TestHost.SeedData.Require(await store.ReadDecisionAsync(account, "scam", token)).CurrentLabel!.Sequence);

        string record = $"at://{Xenon.TestHost.FixtureHandler.Subject(40)}/app.bsky.feed.post/example";
        Dictionary<string, string> recordLabels = await LoadAsync(record, Xenon.TestHost.FixtureHandler.Cid, true);
        Assert.DoesNotMatch("""name="selectedLabels" value="scam"[^>]*checked""", recordLabels["html"]);
        Dictionary<string, string> recordIssue = new(recordLabels) { ["selectedLabels"] = "spam", ["reason"] = "Manual pinned record label." };
        Assert.Contains("1 label changes completed.", await SubmitAsync(recordIssue), StringComparison.Ordinal);
        Dictionary<string, string> unpinned = await LoadAsync(record, string.Empty, true);
        Assert.Matches("""name="selectedLabels" value="spam"[^>]*checked""", unpinned["html"]);
        Dictionary<string, string> pinned = await LoadAsync(record, Xenon.TestHost.FixtureHandler.Cid, true);
        Assert.Matches("""name="selectedLabels" value="spam"[^>]*checked""", pinned["html"]);
        const string otherCid = "bafyreiblzx4whyc6nkd4qq242cnplo3shliifwulbnb5baplnnwhmqpgfa";
        Dictionary<string, string> different = await LoadAsync(record, otherCid, true);
        Assert.Matches("""name="selectedLabels" value="spam"[^>]*disabled""", different["html"]);
        Assert.DoesNotMatch("""name="selectedLabels" value="spam"[^>]*checked""", different["html"]);
        Assert.Contains("Applied to a different record version", different["html"], StringComparison.Ordinal);
        Assert.Contains("1 label changes completed.", await SubmitAsync(new(unpinned) { ["reason"] = "Remove the pinned label without needing a live record." }), StringComparison.Ordinal);
        LabelTarget pinnedTarget = LabelTarget.Record(new(record), new(Xenon.TestHost.FixtureHandler.Cid));
        LabelDecisionSnapshot removed = Xenon.TestHost.SeedData.Require(await store.ReadDecisionAsync(pinnedTarget, "spam", token));
        Assert.True(removed.CurrentLabel!.SignedLabel.Label.IsNegationLabel);
        Assert.Equal(new Cid(Xenon.TestHost.FixtureHandler.Cid), removed.CurrentLabel.SignedLabel.Label.Cid);
        Assert.Equal(scamSequence, Xenon.TestHost.SeedData.Require(await store.ReadDecisionAsync(account, "scam", token)).CurrentLabel!.Sequence);

        Dictionary<string, string> invalid = await LoadAsync(
            target: record,
            cid: "invalid-cid",
            background: true,
            valid: false);
        Assert.Contains("role=\"alert\"", invalid["html"], StringComparison.Ordinal);
        Assert.Equal(string.Empty, invalid["labelState"]);
        Dictionary<string, string> unavailable = await LoadAsync(
            target: "missing.xenon.invalid",
            cid: string.Empty,
            background: true,
            valid: false);
        Assert.Contains("role=\"alert\"", unavailable["html"], StringComparison.Ordinal);
        Assert.Equal(string.Empty, unavailable["labelState"]);
        using HttpRequestMessage noAntiforgery = new(HttpMethod.Post, "/Xenon/Labels?handler=Lookup")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["target"] = Xenon.TestHost.FixtureHandler.Subject(40).Value, ["targetKind"] = "Account" })
        };
        noAntiforgery.Headers.Add("X-Test-Authenticated", "true");
        using HttpResponseMessage denied = await client.SendAsync(noAntiforgery, token);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);

        async Task<Dictionary<string, string>> LoadAsync(
            string target,
            string cid,
            bool background,
            bool valid = true)
        {
            Dictionary<string, string> fields = new()
            {
                ["__RequestVerificationToken"] = antiforgery,
                ["target"] = target,
                ["cid"] = cid
            };
            using HttpRequestMessage lookup = new(HttpMethod.Post, "/Xenon/Labels?handler=Lookup") { Content = new FormUrlEncodedContent(fields) };
            lookup.Headers.Add("X-Test-Authenticated", "true");
            if (background)
            {
                lookup.Headers.Add("X-Requested-With", "XMLHttpRequest");
            }

            using HttpResponseMessage response = await client.SendAsync(lookup, token);
            string html = await response.Content.ReadAsStringAsync(token);
            Assert.True(response.StatusCode == HttpStatusCode.OK, html);
            Assert.Equal(!background, html.Contains("<!DOCTYPE html>", StringComparison.Ordinal));
            fields["labelState"] = ReadField(html, "labelState");
            Assert.Equal(valid, fields["labelState"].Length > 0);
            if (valid)
            {
                Match canonical = LabelStateRegex().Match(html);
                Assert.True(canonical.Success, html);
                fields["target"] = WebUtility.HtmlDecode(canonical.Groups[1].Value);
            }

            fields["html"] = html;
            return fields;
        }

        async Task<string> SubmitAsync(Dictionary<string, string> fields)
        {
            using HttpRequestMessage request = new(HttpMethod.Post, "/Xenon/Labels") { Content = new FormUrlEncodedContent(fields.Where(pair => pair.Key != "html")) };
            request.Headers.Add("X-Test-Authenticated", "true");
            using HttpResponseMessage response = await client.SendAsync(request, token);
            string html = await response.Content.ReadAsStringAsync(token);
            Assert.True(response.StatusCode == HttpStatusCode.OK, html);
            return html;
        }
    }

    /// <summary>Posts real protected forms to add/remove record labels without touching author labels or the wrong report.</summary>
    private static async Task VerifyDecisionWorkflowAsync(
        HttpClient client,
        IServiceProvider services,
        LabelAdministrationCaller caller,
        CancellationToken token)
    {
        ILabelStore store = services.GetRequiredService<ILabelStore>();
        ILabelReportStore reports = services.GetRequiredService<ILabelReportStore>();
        LabelTarget account = LabelTarget.Account(Xenon.TestHost.FixtureHandler.Subject(2));
        long accountSequence = Xenon.TestHost.SeedData.Require(await store.ReadDecisionAsync(account, "spam", token)).CurrentLabel!.Sequence;
        LabelTarget post = LabelTarget.Record(
            new($"at://{Xenon.TestHost.FixtureHandler.Subject(2)}/app.bsky.feed.post/example"), new(Xenon.TestHost.FixtureHandler.Cid));
        Dictionary<string, string> first = await ReadFormAsync(4);
        first["selectedLabels"] = "spam";
        first["reason"] = "Replace the post label without changing the author account.";
        first["ReportId"] = "3";
        using (HttpResponseMessage submitted = await PostAsync(first))
        {
            string html = await submitted.Content.ReadAsStringAsync(token);
            Assert.True(submitted.StatusCode == HttpStatusCode.Redirect, html);
        }

        LabelModerationReport selected = Xenon.TestHost.SeedData.Require(await reports.GetReportAsync(caller, 4, token));
        Assert.NotNull(selected.DecisionOperationId);
        Assert.Null(Xenon.TestHost.SeedData.Require(await reports.GetReportAsync(caller, 3, token)).DecisionOperationId);
        Assert.Null(Xenon.TestHost.SeedData.Require(await reports.GetReportAsync(caller, 5, token)).DecisionOperationId);
        Assert.True(Xenon.TestHost.SeedData.Require(await store.ReadDecisionAsync(post, "scam", token)).CurrentLabel!.SignedLabel.Label.IsNegationLabel);
        Assert.False(Xenon.TestHost.SeedData.Require(await store.ReadDecisionAsync(post, "spam", token)).CurrentLabel!.SignedLabel.Label.IsNegationLabel);
        Assert.Equal(accountSequence, Xenon.TestHost.SeedData.Require(await store.ReadDecisionAsync(account, "spam", token)).CurrentLabel!.Sequence);
        using (HttpResponseMessage retry = await PostAsync(first))
        {
            Assert.Equal(HttpStatusCode.Redirect, retry.StatusCode);
        }

        Assert.Equal(selected.HandledAt, Xenon.TestHost.SeedData.Require(await reports.GetReportAsync(caller, 4, token)).HandledAt);
        Dictionary<string, string> all = await ReadFormAsync(3);
        all["selectedLabels"] = "spam";
        all["reason"] = "   ";
        all["handleOtherReports"] = "true";
        using (HttpResponseMessage submitted = await PostAsync(all))
        {
            string html = await submitted.Content.ReadAsStringAsync(token);
            Assert.True(submitted.StatusCode == HttpStatusCode.Redirect, html);
        }

        foreach (long id in new long[] { 3, 4, 5 })
        {
            Assert.NotNull(Xenon.TestHost.SeedData.Require(await reports.GetReportAsync(caller, id, token)).DecisionOperationId);
        }

        Assert.Equal(accountSequence, Xenon.TestHost.SeedData.Require(await store.ReadDecisionAsync(account, "spam", token)).CurrentLabel!.Sequence);
        using HttpRequestMessage updated = new(HttpMethod.Get, "/Xenon/Reports/Detail/4");
        updated.Headers.Add("X-Test-Authenticated", "true");
        using HttpResponseMessage rendered = await client.SendAsync(updated, token);
        string updatedHtml = await rendered.Content.ReadAsStringAsync(token);
        Assert.Contains(">Reports (43)</a>", updatedHtml, StringComparison.Ordinal);
        Assert.Contains(">Cases (38)</a>", updatedHtml, StringComparison.Ordinal);
        Assert.Contains("Removed:", updatedHtml, StringComparison.Ordinal);
        Assert.Contains("Applied:", updatedHtml, StringComparison.Ordinal);
        Assert.Contains("Account labels", updatedHtml, StringComparison.Ordinal);
        Assert.Contains("Post / record labels", updatedHtml, StringComparison.Ordinal);
        Assert.Contains("Reviewed report #4", updatedHtml, StringComparison.Ordinal);

        async Task<Dictionary<string, string>> ReadFormAsync(long reportId)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, "/Xenon/Cases/Detail/2?ReportId=" + reportId);
            request.Headers.Add("X-Test-Authenticated", "true");
            using HttpResponseMessage response = await client.SendAsync(request, token);
            string html = await response.Content.ReadAsStringAsync(token);
            Assert.True(response.StatusCode == HttpStatusCode.OK, html);
            return new()
            {
                ["__RequestVerificationToken"] = ReadField(html, "__RequestVerificationToken"),
                ["decisionState"] = ReadField(html, "decisionState"),
            };
        }

        async Task<HttpResponseMessage> PostAsync(Dictionary<string, string> form)
        {
            using HttpRequestMessage request = new(HttpMethod.Post, "/Xenon/Cases/Detail/2?handler=Decide");
            request.Headers.Add("X-Test-Authenticated", "true");
            request.Content = new FormUrlEncodedContent(form);
            return await client.SendAsync(request, token);
        }
    }

    /// <summary>Retains label removal through rejected appeal submissions and commits it without a confirmation checkbox.</summary>
    private static async Task VerifyAppealDecisionWorkflowAsync(
            HttpClient client,
            IServiceProvider services,
            LabelAdministrationCaller caller,
            CancellationToken token)
    {
        ILabelAppealStore appeals = services.GetRequiredService<ILabelAppealStore>();
        LabelAppeal appeal = Assert.Single(Xenon.TestHost.SeedData.Require(await appeals.GetAppealsAsync(
            caller: caller, caseId: 1, status: LabelAppealStatus.Pending, cancellationToken: token)));
        ILabelStore store = services.GetRequiredService<ILabelStore>();
        long sequence = (await store.GetStateAsync(token)).IssuedThrough;
        using (HttpRequestMessage list = new(HttpMethod.Get, "/Xenon/Appeals"))
        {
            list.Headers.Add("X-Test-Authenticated", "true");
            using HttpResponseMessage listed = await client.SendAsync(list, token);
            string listHtml = await listed.Content.ReadAsStringAsync(token);
            Assert.Contains($"/Xenon/Cases/Detail/1?AppealId={appeal.Id}", listHtml, StringComparison.Ordinal);
        }

        using (HttpRequestMessage wrongCase = new(HttpMethod.Get, $"/Xenon/Cases/Detail/2?AppealId={appeal.Id}"))
        {
            wrongCase.Headers.Add("X-Test-Authenticated", "true");
            using HttpResponseMessage rejected = await client.SendAsync(wrongCase, token);
            string rejectedHtml = await rejected.Content.ReadAsStringAsync(token);
            Assert.Contains("does not belong to this case", rejectedHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"decisionState\"", rejectedHtml, StringComparison.Ordinal);
        }

        using HttpRequestMessage get = new(HttpMethod.Get, $"/Xenon/Cases/Detail/1?AppealId={appeal.Id}");
        get.Headers.Add("X-Test-Authenticated", "true");
        using HttpResponseMessage page = await client.SendAsync(get, token);
        string html = await page.Content.ReadAsStringAsync(token);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.DoesNotContain("Confirm this moderation decision", html, StringComparison.Ordinal);
        Assert.Contains($"value=\"{appeal.Id}\" selected=\"selected\">Appeal #{appeal.Id}", html, StringComparison.Ordinal);
        Assert.DoesNotMatch("""name="reason"[^>]*required""", html);
        Dictionary<string, string> form = new()
        {
            ["__RequestVerificationToken"] = ReadField(html, "__RequestVerificationToken"),
            ["decisionState"] = ReadField(html, "decisionState"),
            ["reason"] = "Remove the challenged label after reviewing the appeal.",
            ["appealOutcome"] = "Withdrawn",
            ["handleOtherReports"] = "true"
        };
        foreach (bool selectAppeal in new[] { false, true })
        {
            if (selectAppeal)
            {
                form["appealId"] = appeal.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                form["appealOutcome"] = "Upheld";
            }

            using HttpRequestMessage post = new(HttpMethod.Post, "/Xenon/Cases/Detail/1?handler=Decide");
            post.Headers.Add("X-Test-Authenticated", "true");
            post.Content = new FormUrlEncodedContent(form);
            using HttpResponseMessage rejected = await client.SendAsync(post, token);
            html = await rejected.Content.ReadAsStringAsync(token);
            Assert.True(rejected.StatusCode == HttpStatusCode.OK, html);
            Assert.Contains(selectAppeal ? "The appeal outcome does not match" : "Pending appeals require", html, StringComparison.Ordinal);
            Assert.DoesNotMatch("""name="selectedLabels" value="spam"[^>]*checked""", html);
            Assert.Contains("data-applied=\"true\"", html, StringComparison.Ordinal);
            Assert.Contains(form["reason"], html, StringComparison.Ordinal);
            Assert.Contains($"value=\"{form["appealOutcome"]}\" selected=\"selected\"", html, StringComparison.Ordinal);
            Assert.Contains("name=\"handleOtherReports\" value=\"true\" checked", html, StringComparison.Ordinal);
            form["decisionState"] = ReadField(html, "decisionState");
            form["__RequestVerificationToken"] = ReadField(html, "__RequestVerificationToken");
            Assert.Equal(sequence, (await store.GetStateAsync(token)).IssuedThrough);
            Assert.Equal(LabelAppealStatus.Pending, Xenon.TestHost.SeedData.Require(
                await appeals.GetAppealAsync(caller, appeal.Id, token)).Status);
        }

        form["appealOutcome"] = "Withdrawn";
        form.Remove("reason");
        using HttpRequestMessage submit = new(HttpMethod.Post, "/Xenon/Cases/Detail/1?handler=Decide");
        submit.Headers.Add("X-Test-Authenticated", "true");
        submit.Content = new FormUrlEncodedContent(form);
        using HttpResponseMessage resolved = await client.SendAsync(submit, token);
        Assert.True(resolved.StatusCode == HttpStatusCode.Redirect, await resolved.Content.ReadAsStringAsync(token));
        LabelAppeal resolvedAppeal = Xenon.TestHost.SeedData.Require(await appeals.GetAppealAsync(caller, appeal.Id, token));
        Assert.Equal(LabelAppealStatus.Withdrawn, resolvedAppeal.Status);
        Assert.Equal("No reason provided.", resolvedAppeal.ResolutionReason);
        Assert.Equal(sequence + 1, (await store.GetStateAsync(token)).IssuedThrough);
        foreach (bool all in new[] { false, true })
        {
            using HttpRequestMessage list = new(HttpMethod.Get, all ? "/Xenon/Appeals?Status=" : "/Xenon/Appeals");
            list.Headers.Add("X-Test-Authenticated", "true");
            using HttpResponseMessage listed = await client.SendAsync(list, token);
            string listHtml = await listed.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
            Assert.Equal(all, listHtml.Contains("<span>Labels withdrawn</span>", StringComparison.Ordinal));
            if (all)
            {
                Assert.Contains("Status=", listHtml, StringComparison.Ordinal);
                Assert.DoesNotContain("Status=Pending", listHtml, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains("value=\"Pending\" selected", listHtml, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>Exercises antiforgery, compression negotiation, multi-page JSON and the audit entry created by each export.</summary>
    private static async Task VerifyAuditDownloadsAsync(
        HttpClient client,
        ILabelAdministrationStore team,
        LabelAdministrationCaller caller,
        CancellationToken token)
    {
        using HttpRequestMessage auditPage = new(HttpMethod.Get, "/Xenon/Audit");
        auditPage.Headers.Add("X-Test-Authenticated", "true");
        using HttpResponseMessage page = await client.SendAsync(auditPage, token);
        string html = await page.Content.ReadAsStringAsync(token);
        string antiforgery = ReadField(html, "__RequestVerificationToken");
        Assert.Contains("fa-download", html, StringComparison.Ordinal);
        Assert.Contains("Downloads are recorded in this log.", html, StringComparison.Ordinal);
        Assert.Contains("handler=Download", html, StringComparison.Ordinal);
        Assert.Contains("class=\"audit-page-heading\"", html, StringComparison.Ordinal);
        int heading = html.IndexOf("<h1>Administrative audit</h1>", StringComparison.Ordinal);
        int download = html.IndexOf("aria-label=\"Download matching audit events as JSON\"", StringComparison.Ordinal);
        Assert.True(heading < download && download < html.IndexOf("Downloads are recorded", StringComparison.Ordinal), html);
        foreach ((string? accepted, string? expectedEncoding, string filter) in new (string?, string?, string)[]
        {
            (null, null, ""),
            ("gzip", "gzip", ""),
            ("br, gzip", "br", ""),
            ("gzip;q=1, br;q=0.5", "gzip", ""),
            ("gzip;q=0, br;q=0", null, ""),
            ("*;q=0.8, identity;q=0", "br", ""),
            ("GZIP", "gzip", "&Actor=labeler.xenon.invalid&Outcome=Succeeded"),
            ("gzip", "gzip", "&Actor=did%3Aweb%3Ano-matching-actor.invalid")
        })
        {
            IReadOnlyList<LabelAdministrationReceipt> before = Xenon.TestHost.SeedData.Require(
                await team.GetAdministrationAuditAsync(caller, limit: 1000, cancellationToken: token));
            using HttpRequestMessage request = new(HttpMethod.Post, "/Xenon/Audit?handler=Download" + filter);
            request.Headers.Add("X-Test-Authenticated", "true");
            if (accepted is not null)
            {
                request.Headers.TryAddWithoutValidation("Accept-Encoding", accepted);
            }

            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = antiforgery });
            using HttpResponseMessage downloaded = await client.SendAsync(request, token);
            byte[] bytes = await downloaded.Content.ReadAsByteArrayAsync(token);
            Assert.True(downloaded.StatusCode == HttpStatusCode.OK, System.Text.Encoding.UTF8.GetString(bytes));
            Assert.Equal("application/json", downloaded.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", downloaded.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal("xenon-audit.json", downloaded.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
            Assert.True(downloaded.Headers.CacheControl?.NoStore);
            Assert.Contains("Accept-Encoding", downloaded.Headers.Vary);
            Assert.Equal(expectedEncoding is null ? [] : new[] { expectedEncoding }, downloaded.Content.Headers.ContentEncoding);
            using MemoryStream memory = new(bytes);
            await using Stream input = expectedEncoding switch
            {
                "br" => new BrotliStream(memory, CompressionMode.Decompress),
                "gzip" => new GZipStream(memory, CompressionMode.Decompress),
                _ => memory
            };
            using JsonDocument document = await JsonDocument.ParseAsync(input, cancellationToken: token);
            IReadOnlyList<LabelAdministrationReceipt> after = Xenon.TestHost.SeedData.Require(
                await team.GetAdministrationAuditAsync(caller, limit: 1000, cancellationToken: token));
            LabelAdministrationReceipt started = Assert.Single(after, entry =>
                entry.Sequence > before[^1].Sequence && entry.Action == "audit-export-started");
            IEnumerable<LabelAdministrationReceipt> prefix = after.Where(entry => entry.Sequence < started.Sequence);
            IEnumerable<LabelAdministrationReceipt> expected = filter switch
            {
                "" => prefix,
                "&Actor=did%3Aweb%3Ano-matching-actor.invalid" => [],
                _ => prefix.Where(entry => entry.Actor == Xenon.TestHost.FixtureHandler.s_issuer && entry.Succeeded)
            };
            Assert.Equal(expected.Select(entry => entry.Sequence),
                document.RootElement.EnumerateArray().Select(entry => entry.GetProperty("sequence").GetInt64()));
            Assert.Equal("audit-export-started", started.Action);
            Assert.Equal(caller.Actor, started.Actor);
            Assert.NotNull(started.CreatedAt);
            Assert.Contains("completion is not confirmed", started.Reason, StringComparison.Ordinal);
        }

        foreach ((string? encoding, HttpStatusCode expectedStatus) in new (string?, HttpStatusCode)[]
        {
            ("*;q=0", HttpStatusCode.NotAcceptable),
            ("gzip;q=invalid", HttpStatusCode.BadRequest),
            (null, HttpStatusCode.BadRequest)
        })
        {
            long before = Xenon.TestHost.SeedData.Require(
                await team.GetAdministrationAuditAsync(caller, limit: 1000, cancellationToken: token))[^1].Sequence;
            using HttpRequestMessage rejected = new(HttpMethod.Post, "/Xenon/Audit?handler=Download");
            rejected.Headers.Add("X-Test-Authenticated", "true");
            if (encoding is not null)
            {
                rejected.Headers.TryAddWithoutValidation("Accept-Encoding", encoding);
                rejected.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = antiforgery });
            }

            using HttpResponseMessage response = await client.SendAsync(rejected, token);
            Assert.Equal(expectedStatus, response.StatusCode);
            Assert.Null(response.Content.Headers.ContentDisposition);
            Assert.DoesNotContain(Xenon.TestHost.SeedData.Require(
                await team.GetAdministrationAuditAsync(caller, limit: 1000, cancellationToken: token)),
                entry => entry.Sequence > before && entry.Action == "audit-export-started");
        }
    }

    /// <summary>Reads HTML-encoded hidden values from the actual server-generated form.</summary>
    private static string ReadField(string html, string name)
    {
        Match match = Regex.Match(html, "name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]*)\"");
        Assert.True(match.Success, $"Missing {name} field.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    /// <summary>Renders each supported record kind without exposing its full path as the primary subject text.</summary>
    private static async Task VerifySubjectDescriptionsAsync(
        HttpClient client,
        IServiceProvider services,
        LabelAdministrationCaller caller,
        CancellationToken token)
    {
        ILabelModerationStore cases = services.GetRequiredService<ILabelModerationStore>();
        foreach (string collection in new[]
        {
            "app.bsky.actor.profile", "app.bsky.graph.list", "app.bsky.feed.generator",
            "app.bsky.graph.starterpack", "example.custom.record"
        })
        {
            LabelCaseReceipt opened = Xenon.TestHost.SeedData.Require(await cases.ChangeCaseAsync(
                caller,
                new(
                    operationId: "subject-display-" + collection,
                    subject: LabelReportSubject.Record(
                        new($"at://{Xenon.TestHost.FixtureHandler.Subject(40)}/{collection}/example"),
                        new(Xenon.TestHost.FixtureHandler.Cid)),
                    action: LabelCaseAction.Open,
                    reason: "Exercise the subject display without changing report evidence."),
                token));
            using HttpRequestMessage detailRequest = new(HttpMethod.Get, "/Xenon/Cases/Detail/" + opened.Case.Id);
            detailRequest.Headers.Add("X-Test-Authenticated", "true");
            using HttpResponseMessage detailResponse = await client.SendAsync(detailRequest, token);
            string detailHtml = await detailResponse.Content.ReadAsStringAsync(token);
            string subjectKind = collection switch
            {
                "app.bsky.actor.profile" => "profile",
                "app.bsky.graph.list" => "list",
                "app.bsky.feed.generator" => "feed",
                "app.bsky.graph.starterpack" => "starter pack",
                _ => "record"
            };
            Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
            Assert.Contains($"Labels for this {subjectKind}</legend>", detailHtml, StringComparison.Ordinal);
            if (collection is "app.bsky.graph.list" or "app.bsky.graph.starterpack")
            {
                Assert.Contains($"<strong>Fictional Xenon {subjectKind}</strong>", detailHtml, StringComparison.Ordinal);
                Assert.Contains("A fictional collection for local moderation testing. &lt;script&gt;not executable&lt;/script&gt;", detailHtml, StringComparison.Ordinal);
                string path = collection == "app.bsky.graph.list"
                    ? $"profile/{Xenon.TestHost.FixtureHandler.Subject(40)}/lists/example"
                    : $"starter-pack/{Xenon.TestHost.FixtureHandler.Subject(40)}/example";
                Assert.Contains("https://bsky.app/" + path, detailHtml, StringComparison.Ordinal);
                Assert.DoesNotContain("Public previews are available only", detailHtml, StringComparison.Ordinal);
            }
        }

        using HttpRequestMessage request = new(HttpMethod.Get, "/Xenon/Cases?Sort=Number&Ascending=False");
        request.Headers.Add("X-Test-Authenticated", "true");
        using HttpResponseMessage response = await client.SendAsync(request, token);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(token));
        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        foreach ((string icon, string prefix) in new[]
        {
            ("envelope", "Post by "), ("user", string.Empty), ("list", "List by "),
            ("newspaper", "Feed by "), ("users", "Starter pack by "), ("file-lines", "Record by ")
        })
        {
            Assert.Contains($"class=\"subject-icon fa-{icon}\" aria-hidden=\"true\"></span>", html, StringComparison.Ordinal);
            Assert.Contains($">{prefix}@subject-40.xenon.invalid</span>", html, StringComparison.Ordinal);
            using HttpResponseMessage asset = await client.GetAsync($"/Xenon/vendor/fontawesome/{icon}.svg", token);
            Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
            Assert.Equal("image/svg+xml", asset.Content.Headers.ContentType!.MediaType);
            Assert.Contains("Font Awesome Free", await asset.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        }
        Assert.Contains("Icons by <a href=\"https://fontawesome.com/\">Font Awesome Free</a>", html, StringComparison.Ordinal);
        Assert.Contains("href=\"https://creativecommons.org/licenses/by/4.0/\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("subject-glyph", html, StringComparison.Ordinal);
        using HttpResponseMessage license = await client.GetAsync("/Xenon/vendor/fontawesome/LICENSE.txt", token);
        Assert.Equal(HttpStatusCode.OK, license.StatusCode);
        Assert.Contains("CC BY 4.0", await license.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
    }

    /// <summary>Submits the actual rendered filter values, including both sort directions and recovery from invalid navigation.</summary>
    private static async Task VerifyFilterFormsAsync(HttpClient client, CancellationToken token)
    {
        foreach (string path in new[]
        {
            "/Xenon/Cases",
            "/Xenon/Cases?Sort=Number&Ascending=True&PageNumber=2",
            "/Xenon/Cases?Sort=garbage",
            "/Xenon/Appeals",
            "/Xenon/Appeals?Sort=Number&Ascending=True",
            "/Xenon/Appeals?Ascending=garbage"
        })
        {
            using HttpRequestMessage request = new(HttpMethod.Get, path);
            request.Headers.Add("X-Test-Authenticated", "true");
            using HttpResponseMessage response = await client.SendAsync(request, token);
            string html = await response.Content.ReadAsStringAsync(token);
            string sort = ReadField(html, "Sort");
            string direction = ReadField(html, "Ascending");
            Assert.True(bool.TryParse(direction, out bool ascending), html);
            bool invalid = path.Contains("garbage", StringComparison.Ordinal);
            Assert.Equal(!invalid && path.Contains("Ascending=True", StringComparison.Ordinal), ascending);
            Assert.Equal(!invalid && path.Contains("Sort=Number", StringComparison.Ordinal) ? "Number" : "Received", sort);
            bool cases = path.StartsWith("/Xenon/Cases", StringComparison.Ordinal);
            if (cases)
            {
                Assert.Contains("<form method=\"get\" class=\"panel\">", html, StringComparison.Ordinal);
                Assert.DoesNotContain("<summary>Filters</summary>", html, StringComparison.Ordinal);
                foreach ((string value, string label) in new[]
                {
                    ("InReview", "In review"),
                    ("AssignedToMe", "Assigned to me"),
                    ("PendingAppeals", "Pending appeals")
                })
                {
                    Assert.Matches($"<option value=\"{value}\"[^>]*>{label}</option>", html);
                }
            }
            string[] filters = cases
                ? Enum.GetNames<LabelCaseWorkflow>()
                : [string.Empty, .. Enum.GetNames<LabelAppealStatus>()];
            foreach (string filter in filters)
            {
                using FormUrlEncodedContent form = new(new Dictionary<string, string>
                {
                    ["Sort"] = sort,
                    ["Ascending"] = direction,
                    [cases ? "Queue" : "Status"] = filter
                });
                string url = (cases ? "/Xenon/Cases?" : "/Xenon/Appeals?") + await form.ReadAsStringAsync(token);
                using HttpRequestMessage submitted = new(HttpMethod.Get, url);
                submitted.Headers.Add("X-Test-Authenticated", "true");
                using HttpResponseMessage filtered = await client.SendAsync(submitted, token);
                string result = await filtered.Content.ReadAsStringAsync(token);
                Assert.True(filtered.StatusCode == HttpStatusCode.OK, result);
                Assert.DoesNotContain("role=\"alert\"", result, StringComparison.Ordinal);
                Assert.Equal(sort, ReadField(result, "Sort"));
                Assert.Equal(direction, ReadField(result, "Ascending"));
                if (cases)
                {
                    Assert.Contains("<form method=\"get\" class=\"panel\">", result, StringComparison.Ordinal);
                    Assert.DoesNotContain("<summary>Filters</summary>", result, StringComparison.Ordinal);
                    Assert.Matches($"<option value=\"{filter}\" selected=\"selected\">", result);
                }
                if (cases && filter == nameof(LabelCaseWorkflow.AssignedToMe))
                {
                    Assert.Contains("39 cases in the AssignedToMe queue", result, StringComparison.Ordinal);
                }
            }
        }
    }

    /// <summary>Checks global ordering, filter preservation, page reset, stale-page clamping and malformed-input feedback.</summary>
    private static async Task VerifySortingAsync(HttpClient client, CancellationToken token)
    {
        foreach ((string sort, bool ascending, int first) in new[]
        {
                ("Number", true, 1),
                ("Number", false, 40),
                ("Received", true, 1),
                ("Received", false, 40),
                ("Subject", true, 2),
                ("Subject", false, 39),
                ("Assignee", true, 1),
                ("Assignee", false, 40)
            })
        {
            string path = $"/Xenon/Cases?Sort={sort}&Ascending={ascending}&Queue=Unresolved";
            using HttpRequestMessage request = new(HttpMethod.Get, path);
            request.Headers.Add("X-Test-Authenticated", "true");
            using HttpResponseMessage response = await client.SendAsync(request, token);
            string html = await response.Content.ReadAsStringAsync(token);
            Assert.True(response.StatusCode == HttpStatusCode.OK, html);
            MatchCollection rows = CaseLinkRegex().Matches(html);
            Assert.Equal(16, rows.Count);
            Assert.Equal(first.ToString(System.Globalization.CultureInfo.InvariantCulture), rows[0].Groups[1].Value);
            Assert.Contains(ascending ? "aria-sort=\"ascending\"" : "aria-sort=\"descending\"", html, StringComparison.Ordinal);
            Match sortLink = Regex.Match(html, $"""href="([^"]+)" aria-label="Sort by {(sort == "Number" ? "Case" : sort)}, """);
            Assert.True(sortLink.Success, html);
            string url = WebUtility.HtmlDecode(sortLink.Groups[1].Value);
            Assert.Contains("PageNumber=1", url, StringComparison.Ordinal);
            Assert.Contains("Ascending=" + !ascending, url, StringComparison.Ordinal);
            Assert.Contains("Queue=Unresolved", url, StringComparison.Ordinal);
        }

        foreach ((string path, string expected) in new[]
        {
                ("/Xenon/Cases?Queue=Resolved", "<caption class=\"sr-only\">1 cases"),
                ("/Xenon/Cases?PageNumber=999999", "3 of 3 pages"),
                ("/Xenon/Reports?PageNumber=2&Sort=Subject&Ascending=True", "2 of 3 pages"),
                ("/Xenon/Appeals?Status=Pending&Sort=Actor&Ascending=True", "appeal records</caption>"),
                ("/Xenon/Audit?Sort=Actor&Ascending=True", "class=\"table-pagination\""),
                ("/Xenon/Team?Sort=Role&Ascending=True", "members, including retained tombstones</caption>"),
                ("/Xenon/Cases?PageNumber=0", "role=\"alert\""),
                ("/Xenon/Cases?Sort=garbage", "role=\"alert\""),
                ("/Xenon/Reports?Ascending=garbage", "role=\"alert\""),
                ("/Xenon/Reports?Status=garbage", "role=\"alert\""),
                ("/Xenon/Team?Sort=Received", "role=\"alert\"")
            })
        {
            using HttpRequestMessage request = new(HttpMethod.Get, path);
            request.Headers.Add("X-Test-Authenticated", "true");
            using HttpResponseMessage response = await client.SendAsync(request, token);
            string html = await response.Content.ReadAsStringAsync(token);
            Assert.True(response.StatusCode == HttpStatusCode.OK, html);
            Assert.Contains(expected, html, StringComparison.Ordinal);
            Assert.DoesNotContain("1 of 1 pages", html, StringComparison.Ordinal);
            if (path.Contains("Queue=Resolved", StringComparison.Ordinal) ||
                path.StartsWith("/Xenon/Appeals", StringComparison.Ordinal) ||
                path.Contains("Sort=Role", StringComparison.Ordinal))
            {
                Assert.DoesNotContain("aria-label=\"Table pagination\"", html, StringComparison.Ordinal);
            }
        }
    }
    /// <summary>Fails fixture tests if any reserved identity or record unexpectedly requires network access.</summary>
    private sealed class NoNetworkHandler : HttpMessageHandler
    {
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Fixture setup must not access the network.");
    }

    /// <summary>Exercises the approved real-post seed path deterministically without making network calls or publishing anything.</summary>
    private sealed class BotSeedHandler(HttpClient fixtures) : HttpMessageHandler
    {
        internal const string Author = "did:plc:ec72yg6n2sydzjvtovvdlxrk";

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri uri = request.RequestUri!;
            Assert.Equal(HttpMethod.Get, request.Method);
            string? json = null;
            if (uri.Host == "bot.idunno.blue" && uri.AbsolutePath == "/.well-known/atproto-did")
            {
                return new(HttpStatusCode.OK) { Content = new StringContent(Author) };
            }
            if (uri.Host == "plc.directory" && uri.AbsolutePath == "/" + Author)
            {
                json = $$"""
                    {"id":"{{Author}}","alsoKnownAs":["at://bot.idunno.blue"],
                     "service":[{"id":"#atproto_pds","type":"AtprotoPersonalDataServer","serviceEndpoint":"https://bot-seed.invalid"}]}
                    """;
            }
            if (uri.Host == "bot-seed.invalid" && uri.AbsolutePath == "/xrpc/com.atproto.repo.getRecord")
            {
                Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                Assert.Equal(Author, query["repo"].ToString());
                string recordKey = query["rkey"].ToString();
                string collection = query["collection"].ToString();
                json = $$$"""
                    {"uri":"at://{{{Author}}}/{{{collection}}}/{{{recordKey}}}","cid":"{{{Xenon.TestHost.FixtureHandler.Cid}}}",
                     "value":{"$type":"{{{collection}}}","text":"Local-only seed test"}}
                    """;
            }
            if (json is not null)
            {
                return new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
            }

            using HttpRequestMessage forwarded = new(request.Method, uri);
            return await fixtures.SendAsync(forwarded, cancellationToken);
        }
    }

    [GeneratedRegex("<!DOCTYPE html>", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex HtmlDocumentRegex();

    [GeneratedRegex("""<script src="[^"]*/Xenon/js/theme[^"]*"></script>""")]
    private static partial Regex ThemeScriptRegex();

    [GeneratedRegex("""/Xenon/Cases/Detail/(\d+)""")]
    private static partial Regex CaseLinkRegex();

    [GeneratedRegex("""href="([^"]+)"[^>]*rel="next"[^>]*>Next &gt;</a>""")]
    private static partial Regex NextPageRegex();

    [GeneratedRegex("""href="([^"]+)"[^>]*rel="prev"[^>]*>&lt; Previous</a>""")]
    private static partial Regex PreviousPageRegex();

    [GeneratedRegex("<h2><a href=\"/Xenon/Reports/Detail/")]
    private static partial Regex ReportHeadingRegex();

    [GeneratedRegex("data-time-zone-toggle")]
    private static partial Regex TimeZoneToggleRegex();

    [GeneratedRegex("data-video-url=\"([^\"]+)\"")]
    private static partial Regex VideoUrlRegex();

    [GeneratedRegex("src=\"data:image/png;base64,")]
    private static partial Regex GalleryImageRegex();

    [GeneratedRegex("name=\"labelState\"[^>]*data-target=\"([^\"]+)\"")]
    private static partial Regex LabelStateRegex();
}
