// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.AspNet.Labeler;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public class XenonUiTests
{
    private static readonly Did s_administrator = new("did:plc:xenon-admin");
    private static readonly Did s_member = new("did:plc:xenon-member");
    private static readonly AtProtoServiceReference s_audience = new(TestServerBuilder.s_issuer, "labeler_admin");

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task OwnershipTransferRequiresCurrentOwnerAndExplicitConfirmation(bool ownerSubmits, bool confirms, bool accepted)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, UiPolicy>();
                services.AddSingleton<IDidHandleResolver, TeamHandles>();
                services.AddLabelerAdministration(s_audience, key, identity);
            },
            xenonUi: true);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(s_administrator, "uid:0", "ownership-bootstrap", token)).Succeeded);
        LabelAdministrationCaller owner = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        Assert.True((await team.ChangeTeamAsync(
            caller: owner,
            change: new(
                operationId: "ownership-enroll",
                action: LabelTeamAction.Enroll,
                target: s_member,
                reason: "Enroll a potential owner.",
                role: LabelTeamRole.Administrator),
            cancellationToken: token)).Succeeded);
        Did actor = ownerSubmits ? s_administrator : s_member;
        using HttpResponseMessage page = await GetAsAsync(fixture, actor, "/Xenon/Team");
        string html = await page.Content.ReadAsStringAsync(token);
        Assert.Equal(ownerSubmits, html.Contains("name=\"action\" value=\"TransferOwnership\"", StringComparison.Ordinal));
        if (ownerSubmits)
        {
            const string ownershipPanel = "<details class=\"panel\" id=\"labeler-ownership\">";
            int ownershipStart = html.IndexOf(ownershipPanel, StringComparison.Ordinal);
            Assert.True(ownershipStart > html.IndexOf("id=\"change-member\"", StringComparison.Ordinal));
            Assert.Contains("<summary><strong>Labeler ownership</strong></summary>", html[ownershipStart..], StringComparison.Ordinal);
            Assert.DoesNotContain("<section class=\"panel\"", html[ownershipStart..], StringComparison.Ordinal);
        }
        (string csrf, string cookie) = await CsrfAsync(page);
        using HttpRequestMessage request = new(HttpMethod.Post, "/Xenon/Team?handler=Change")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = csrf,
                ["target"] = "xenon-member.example",
                ["action"] = "TransferOwnership",
                ["role"] = "Administrator",
                ["reason"] = "Hand over ownership.",
                ["operationId"] = "ownership-transfer",
                ["confirmTransfer"] = confirms ? "true" : ""
            })
        };
        request.Headers.Add("X-Test-Did", actor.Value);
        request.Headers.Add("Cookie", cookie);
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, token);
        Assert.Equal(accepted ? HttpStatusCode.Redirect : HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(!accepted, (await team.GetRemoteAdministrationStatusAsync(owner, token)).Result!.IsOwner);
        LabelAdministrationCaller member = (await team.AuthenticateBrowserAsync(s_member, token)).Result!;
        Assert.Equal(accepted, (await team.GetRemoteAdministrationStatusAsync(member, token)).Result!.IsOwner);
        using HttpResponseMessage formerOwnerPage = await GetAsAsync(fixture, s_administrator, "/Xenon/Team");
        using HttpResponseMessage newOwnerPage = await GetAsAsync(fixture, s_member, "/Xenon/Team");
        Assert.Equal(!accepted, (await formerOwnerPage.Content.ReadAsStringAsync(token))
            .Contains("name=\"action\" value=\"TransferOwnership\"", StringComparison.Ordinal));
        Assert.Equal(accepted, (await newOwnerPage.Content.ReadAsStringAsync(token))
            .Contains("name=\"action\" value=\"TransferOwnership\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TeamMemberSuggestionsUseAllRegisteredHandlesRegardlessOfTableSearchOrPage()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, UiPolicy>();
                services.AddSingleton<IDidHandleResolver, TeamHandles>();
                services.AddLabelerAdministration(s_audience, key, identity);
            },
            xenonUi: true);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(s_administrator, "uid:0", "dropdown-bootstrap", token)).Succeeded);
        LabelAdministrationCaller caller = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        for (int index = 0; index < 19; index++)
        {
            Did member = new(index == 18 ? "did:plc:unavailable" : $"did:plc:team-member-{index:D2}");
            Assert.True((await team.ChangeTeamAsync(
                caller: caller,
                change: new(
                    operationId: $"dropdown-enroll-{index}",
                    action: LabelTeamAction.Enroll,
                    target: member,
                    reason: "Enroll a dropdown test member.",
                    role: LabelTeamRole.Moderator),
                cancellationToken: token)).Succeeded);
            if (index < 2)
            {
                Assert.True((await team.ChangeTeamAsync(
                    caller: caller,
                    change: new(
                        operationId: $"dropdown-status-{index}",
                        action: index == 0 ? LabelTeamAction.Suspend : LabelTeamAction.Remove,
                        target: member,
                        reason: "Retain an inactive dropdown test member."),
                    cancellationToken: token)).Succeeded);
            }
        }

        foreach (string query in new[] { "", "&Search=did:plc:xenon-admin", "&PageNumber=2" })
        {
            using HttpResponseMessage response = await GetAsAsync(
                fixture, s_administrator, "/Xenon/Team?member=did:plc:team-member-17" + query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string html = await response.Content.ReadAsStringAsync(token);
            string memberInput = System.Text.RegularExpressions.Regex.Match(
                html, """<input name="target"[^>]*data-team-member[^>]*>""").Value;
            Assert.NotEmpty(memberInput);
            Assert.Contains("value=\"team-member-17.example\"", memberInput, StringComparison.Ordinal);
            Assert.Contains("list=\"team-search-handles\"", memberInput, StringComparison.Ordinal);
            Assert.DoesNotContain("did:", memberInput, StringComparison.Ordinal);
            Assert.Contains("list=\"team-search-handles\"", html, StringComparison.Ordinal);
            string searchSuggestions = System.Text.RegularExpressions.Regex.Match(
                html, """<datalist id="team-search-handles">[\s\S]*?</datalist>""").Value;
            Assert.NotEmpty(searchSuggestions);
            Assert.Equal(19, System.Text.RegularExpressions.Regex.Matches(searchSuggestions, "<option ").Count);
            Assert.Contains("value=\"team-member-00.example\" label=\"@team-member-00.example - Suspended\"", searchSuggestions, StringComparison.Ordinal);
            Assert.Contains("value=\"team-member-01.example\" label=\"@team-member-01.example - Removed\"", searchSuggestions, StringComparison.Ordinal);
            Assert.Contains("value=\"team-member-17.example\"", searchSuggestions, StringComparison.Ordinal);
            Assert.Contains("data-role=\"Moderator\"", searchSuggestions, StringComparison.Ordinal);
            Assert.DoesNotContain("did:", searchSuggestions, StringComparison.Ordinal);
            Assert.DoesNotContain("handle.invalid", searchSuggestions, StringComparison.Ordinal);
            Assert.Contains("data-handle-typeahead", html, StringComparison.Ordinal);
            Assert.Contains("id=\"enroll-target-suggestions\"", html, StringComparison.Ordinal);
            Assert.Contains("id=\"enroll-target-status\" role=\"status\"", html, StringComparison.Ordinal);
            Assert.Contains("/Xenon/js/team.js", html, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("xenon-member.example", true)]
    [InlineData("@XENON-MEMBER.EXAMPLE", true)]
    [InlineData("outsider.example", false)]
    [InlineData("did:plc:xenon-member", false)]
    public async Task TeamMemberChangesMapVerifiedRegisteredHandlesToInternalIdentities(string target, bool accepted)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, UiPolicy>();
                services.AddSingleton<IDidHandleResolver, TeamHandles>();
                services.AddLabelerAdministration(s_audience, key, identity);
            },
            xenonUi: true);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(s_administrator, "uid:0", "handle-change-bootstrap", token)).Succeeded);
        LabelAdministrationCaller caller = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        Assert.True((await team.ChangeTeamAsync(
            caller: caller,
            change: new(
                operationId: "handle-change-enroll",
                action: LabelTeamAction.Enroll,
                target: s_member,
                reason: "Enroll a handle change test member.",
                role: LabelTeamRole.Viewer),
            cancellationToken: token)).Succeeded);
        using HttpResponseMessage page = await GetAsAsync(fixture, s_administrator, "/Xenon/Team");
        (string csrf, string cookie) = await CsrfAsync(page);
        using HttpRequestMessage request = new(HttpMethod.Post, "/Xenon/Team?handler=Change")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = csrf,
                ["target"] = target,
                ["action"] = "SetRole",
                ["role"] = "Moderator",
                ["reason"] = "Change a registered member's role.",
                ["operationId"] = "handle-change-submit"
            })
        };
        request.Headers.Add("X-Test-Did", s_administrator.Value);
        request.Headers.Add("Cookie", cookie);
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, token);
        Assert.Equal(accepted ? HttpStatusCode.Redirect : HttpStatusCode.OK, response.StatusCode);
        IReadOnlyList<LabelTeamMember> members = (await team.GetTeamAsync(caller, token)).Result!;
        Assert.Equal(accepted ? LabelTeamRole.Moderator : LabelTeamRole.Viewer,
            Assert.Single(members, member => member.Did == s_member).Role);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EmailHostLinkRequiresRegisteredUiAndConfiguredPublicOrigin(bool uiEnabled, bool publicOrigin)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            options: new() { PublicEndpoint = publicOrigin ? new Uri("https://labeler.example/") : null },
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, UiPolicy>();
                services.AddLabelerAdministration(s_audience, key, identity);
            },
            xenonUi: uiEnabled);
        LabelerEmailUiLink? link = fixture.Services.GetService<LabelerEmailUiLink>();
        Assert.Equal(uiEnabled && publicOrigin ? new Uri("https://labeler.example/Xenon/") : null, link?.Endpoint);
        Assert.Equal(uiEnabled, link is not null);
    }

    [Theory]
    [InlineData("/Xenon/Cases?Queue=All&Sort=Number&Ascending=true")]
    [InlineData("/Xenon/Reports?Queue=All&Sort=Number&Ascending=true")]
    [InlineData("/Xenon/Appeals?Queue=All&Status=Pending&Sort=Number&Ascending=true")]
    public async Task QueueListsExposeRefreshableResultsWithoutIncludingFilterControls(string path)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, UiPolicy>();
                services.AddLabelerAdministration(s_audience, key, identity);
            },
            xenonUi: true);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(s_administrator, "uid:0", "queue-updates-bootstrap", token)).Succeeded);
        using HttpResponseMessage response = await GetAsAsync(fixture, s_administrator, path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        string html = await response.Content.ReadAsStringAsync(token);
        Assert.Contains("/Xenon/js/queue-updates.js", html, StringComparison.Ordinal);
        Assert.Contains("data-queue-update-status", html, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "data-live-queue"));
        int results = html.IndexOf("data-live-queue", StringComparison.Ordinal);
        Assert.True(html.LastIndexOf("</form>", results, StringComparison.Ordinal) >= 0);
        Assert.Contains("value=\"All\" selected=\"selected\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Sort\" value=\"Number\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LabelTeamRole.Viewer)]
    [InlineData(LabelTeamRole.Moderator)]
    [InlineData(LabelTeamRole.Administrator)]
    public async Task MediaPreferencesArePrivateDurableAndProtectedByLiveMembershipAndAntiforgery(LabelTeamRole role)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, UiPolicy>();
                services.AddSingleton<IDidHandleResolver, TestHandles>();
                services.AddLabelerAdministration(s_audience, key, identity);
            },
            xenonUi: true);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(s_administrator, "uid:0", "preferences-bootstrap", token)).Succeeded);
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        Assert.True((await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "preferences-enroll",
                action: LabelTeamAction.Enroll,
                target: s_member,
                reason: "Enroll a preferences test member.",
                role: role),
            cancellationToken: token)).Succeeded);
        using HttpResponseMessage initial = await GetAsAsync(fixture, s_member, "/Xenon/Preferences");
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        Assert.True(initial.Headers.CacheControl?.NoStore);
        string html = await initial.Content.ReadAsStringAsync(token);
        Assert.Contains("href=\"/Xenon/Preferences\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"blur\" value=\"porn\" checked=\"checked\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"grayscale\" value=\"porn\" checked", html, StringComparison.Ordinal);
        (string csrf, string cookie) = await CsrfAsync(initial);
        Dictionary<string, string> fields = new()
        {
            ["__RequestVerificationToken"] = csrf,
            ["grayscale"] = "porn",
            ["translucent"] = "graphic-media",
            ["target"] = s_administrator.Value
        };
        using HttpRequestMessage save = new(HttpMethod.Post, "/Xenon/Preferences")
        {
            Content = new FormUrlEncodedContent(fields)
        };
        save.Headers.Add("X-Test-Did", s_member.Value);
        save.Headers.Add("Cookie", cookie);
        using HttpResponseMessage saved = await fixture.Client.SendAsync(save, token);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        using HttpResponseMessage administratorPage = await GetAsAsync(fixture, s_administrator, "/Xenon/Preferences");
        Assert.Contains("name=\"blur\" value=\"porn\" checked=\"checked\"", await administratorPage.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        using HttpResponseMessage anotherBrowser = await GetAsAsync(fixture, s_member, "/Xenon/Preferences");
        string reloaded = await anotherBrowser.Content.ReadAsStringAsync(token);
        Assert.Contains("name=\"grayscale\" value=\"porn\" checked=\"checked\"", reloaded, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"blur\" value=\"porn\" checked", reloaded, StringComparison.Ordinal);

        fields["grayscale"] = "unknown";
        using HttpRequestMessage invalid = new(HttpMethod.Post, "/Xenon/Preferences")
        {
            Content = new FormUrlEncodedContent(fields)
        };
        invalid.Headers.Add("X-Test-Did", s_member.Value);
        invalid.Headers.Add("Cookie", cookie);
        using HttpResponseMessage rejected = await fixture.Client.SendAsync(invalid, token);
        Assert.Contains("Choose only the available media labels", await rejected.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        using HttpResponseMessage unchanged = await GetAsAsync(fixture, s_member, "/Xenon/Preferences");
        Assert.Contains("name=\"grayscale\" value=\"porn\" checked=\"checked\"", await unchanged.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        using HttpRequestMessage noCsrf = new(HttpMethod.Post, "/Xenon/Preferences")
        {
            Content = new FormUrlEncodedContent([])
        };
        noCsrf.Headers.Add("X-Test-Did", s_member.Value);
        using HttpResponseMessage denied = await fixture.Client.SendAsync(noCsrf, token);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        using HttpResponseMessage anonymous = await fixture.Client.GetAsync("/Xenon/Preferences", token);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using HttpResponseMessage outsider = await GetAsAsync(fixture, new("did:plc:outsider"), "/Xenon/Preferences");
        Assert.Equal(HttpStatusCode.Forbidden, outsider.StatusCode);
        Assert.True((await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "preferences-suspend",
                action: LabelTeamAction.Suspend,
                target: s_member,
                reason: "Suspend the preferences test member."),
            cancellationToken: token)).Succeeded);
        using HttpResponseMessage suspended = await GetAsAsync(fixture, s_member, "/Xenon/Preferences");
        Assert.Equal(HttpStatusCode.Forbidden, suspended.StatusCode);
        fields["grayscale"] = "porn";
        using HttpRequestMessage suspendedSave = new(HttpMethod.Post, "/Xenon/Preferences")
        {
            Content = new FormUrlEncodedContent(fields)
        };
        suspendedSave.Headers.Add("X-Test-Did", s_member.Value);
        suspendedSave.Headers.Add("Cookie", cookie);
        using HttpResponseMessage suspendedResult = await fixture.Client.SendAsync(suspendedSave, token);
        Assert.Equal(HttpStatusCode.Forbidden, suspendedResult.StatusCode);
    }

    [Theory]
    [InlineData(LabelTeamRole.Viewer)]
    [InlineData(LabelTeamRole.Moderator)]
    [InlineData(LabelTeamRole.Administrator)]
    public async Task OnlineTeamIndicatorSharesPresenceAndHidesForSingleActiveMember(LabelTeamRole role)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, UiPolicy>();
                services.AddSingleton<IDidHandleResolver, TestHandles>();
                services.AddLabelerAdministration(s_audience, key, identity);
            },
            xenonUi: true);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(s_administrator, "uid:0", "online-bootstrap", token)).Succeeded);
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", token)).Result!;
        using HttpResponseMessage singlePage = await GetAsAsync(fixture, s_administrator);
        Assert.DoesNotContain("data-online-presence", await singlePage.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        using HttpResponseMessage singleStatus = await GetAsAsync(fixture, s_administrator, "/Xenon?handler=Online");
        Assert.Equal(HttpStatusCode.NoContent, singleStatus.StatusCode);
        Assert.True(singleStatus.Headers.CacheControl?.NoStore);
        Assert.True((await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "online-enroll",
                action: LabelTeamAction.Enroll,
                target: s_member,
                reason: "Enroll a presence test member.",
                role: role),
            cancellationToken: token)).Succeeded);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        using HttpResponseMessage memberPage = await GetAsAsync(fixture, s_member);
        string html = await memberPage.Content.ReadAsStringAsync(token);
        Assert.Contains("data-online-presence", html, StringComparison.Ordinal);
        Assert.Contains("2 online</span>", html, StringComparison.Ordinal);
        Assert.Contains("fa-users", html, StringComparison.Ordinal);
        Assert.Contains("data-online-url=\"/Xenon?handler=Online\"", html, StringComparison.Ordinal);
        Assert.Contains($"<li title=\"{s_administrator}\"><span class=\"fa-icon fa-user\" aria-hidden=\"true\"></span> <span>@admin.example</span></li>", html, StringComparison.Ordinal);
        Assert.Contains($"<li title=\"{s_member}\"><span class=\"fa-icon fa-user\" aria-hidden=\"true\"></span> <span>Handle unavailable</span></li>", html, StringComparison.Ordinal);
        using HttpResponseMessage initialOnline = await GetAsAsync(fixture, s_member, "/Xenon?handler=Online");
        using JsonDocument initialBody = JsonDocument.Parse(await initialOnline.Content.ReadAsByteArrayAsync(token));
        Assert.Contains(initialBody.RootElement.GetProperty("members").EnumerateArray(),
            member => member.GetProperty("displayName").GetString() == "@admin.example");
        OzoneTeamPresence shared = fixture.Services.GetRequiredService<OzoneTeamPresence>();
        IReadOnlyList<LabelTeamMember> members = (await team.GetTeamAsync(administrator, token)).Result!;
        Assert.Equal(2, shared.List(members, 100).Length);
        fixture.Clock.Advance(TimeSpan.FromMinutes(4));
        using HttpResponseMessage online = await GetAsAsync(fixture, s_member, "/Xenon?handler=Online");
        Assert.Equal(HttpStatusCode.OK, online.StatusCode);
        Assert.True(online.Headers.CacheControl?.NoStore);
        using JsonDocument body = JsonDocument.Parse(await online.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(s_member.Value, Assert.Single(body.RootElement.GetProperty("members").EnumerateArray()).GetProperty("did").GetString());
        Assert.Equal("Handle unavailable", Assert.Single(body.RootElement.GetProperty("members").EnumerateArray()).GetProperty("displayName").GetString());
        using HttpResponseMessage anonymous = await fixture.Client.GetAsync("/Xenon?handler=Online", token);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using HttpResponseMessage outsider = await GetAsAsync(fixture, new("did:plc:outsider"), "/Xenon?handler=Online");
        Assert.Equal(HttpStatusCode.Forbidden, outsider.StatusCode);
        Assert.True((await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "online-suspend",
                action: LabelTeamAction.Suspend,
                target: s_member,
                reason: "Suspend the presence test member."),
            cancellationToken: token)).Succeeded);
        using HttpResponseMessage hidden = await GetAsAsync(fixture, s_administrator, "/Xenon?handler=Online");
        Assert.Equal(HttpStatusCode.NoContent, hidden.StatusCode);
        using HttpResponseMessage revoked = await GetAsAsync(fixture, s_member, "/Xenon?handler=Online");
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public async Task AppealOutcomesReflectDeclaredPolicyAndChangedChallenges(bool multipleLabels, bool expired, bool reissued)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new AppealIdentityHandler());
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddLabelerModeration(issuer: TestServerBuilder.s_issuer, httpClient: identity);
                services.AddSingleton<IDidHandleResolver, TestHandles>();
                services.AddLabelerAdministration(s_audience, key, identity);
            },
            moderation: true,
            xenonUi: true,
            controlDatabaseTime: true);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(
            administrator: s_administrator,
            localIdentity: "uid:0",
            operationId: "appeal-bootstrap",
            cancellationToken: token)).Succeeded);
        LabelAdministrationCaller caller = Assert.IsType<LabelAdministrationCaller>((await team.AuthenticateLocalAsync("uid:0", token)).Result);
        ILabelPolicyStore policies = fixture.Services.GetRequiredService<ILabelPolicyStore>();
        string values = multipleLabels ? """["spam","scam"]""" : """["spam"]""";
        using JsonDocument declaration = JsonDocument.Parse(
            $$"""{"$type":"app.bsky.labeler.service","createdAt":"2026-01-01T00:00:00Z","policies":{"labelValues":{{values}}},"subjectTypes":["account"]}""");
        Assert.True((await policies.SavePolicyAsync(
            caller: caller,
            operationId: "appeal-policy",
            policy: new(
                version: "xenon-ui-test",
                declaration: declaration.RootElement,
                allowAccounts: true,
                allowRecords: false,
                rules: "Test appeal controls."),
            reason: "Configure appeal controls.",
            cancellationToken: token)).Succeeded);
        LabelPolicyState initial = (await policies.ReadPolicyStateAsync(token))!;
        LabelPolicyState publishing = Assert.IsType<LabelPolicyState>((await policies.BeginPolicyPublicationAsync(
            caller: caller,
            operationId: "publish-appeal-policy",
            version: "xenon-ui-test",
            expectedRevision: initial.Revision,
            previousCid: null,
            reason: "Publish test policy.",
            cancellationToken: token)).Result);
        Assert.True((await policies.ObservePolicyAsync(
            expectedRevision: publishing.Revision,
            verificationStartedAt: publishing.DatabaseTime,
            observedCid: new("bafyreihdwdcefgh4dqkjv67uzcmw7ojee6xedzdetojuzjevtenxquvyku"),
            declaration: declaration.RootElement,
            cancellationToken: token)).Succeeded);
        LabelIssuanceService issuance = new(
            issuer: TestServerBuilder.s_issuer,
            signingKey: key,
            store: fixture.Store,
            authorizer: new LabelTeamAuthorizer(team, caller),
            policyProvider: new UiPolicy(),
            httpClient: identity,
            timeProvider: fixture.DatabaseClock);
        LabelServiceResult<LabelOperationReceipt> issued = await issuance.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "appealed-label",
                target: LabelTarget.Account(s_member),
                value: "spam",
                context: new(s_administrator, "Apply a test label."),
                expiresAt: fixture.DatabaseClock.GetUtcNow().AddMinutes(1)),
            token);
        Assert.True(issued.Succeeded, issued.Error?.Message);
        LabelCaseReceipt opened = Assert.IsType<LabelCaseReceipt>((await fixture.Services.GetRequiredService<ILabelModerationStore>().ChangeCaseAsync(
            caller: caller,
            change: new(
                operationId: "test-appeal",
                subject: LabelReportSubject.Account(s_member),
                action: LabelCaseAction.OpenAppeal,
                reason: "Review the test label."),
            cancellationToken: token)).Result);
        LabelServiceResult<IReadOnlyList<LabelAppeal>> appeals = await fixture.Services.GetRequiredService<ILabelAppealStore>()
            .GetAppealsAsync(caller: caller, caseId: opened.Case.Id, cancellationToken: token);
        Assert.True(appeals.Succeeded);
        LabelAppeal appeal = Assert.Single(appeals.Result);
        if (expired)
        {
            fixture.DatabaseClock.Advance(TimeSpan.FromMinutes(1));
        }
        if (reissued)
        {
            Assert.True((await issuance.ExecuteAsync(
                LabelOperationRequest.Negate(
                    operationId: "remove-appealed-label",
                    target: LabelTarget.Account(s_member),
                    value: "spam",
                    context: new(s_administrator, "Remove the test label.")),
                token)).Succeeded);
            Assert.True((await issuance.ExecuteAsync(
                LabelOperationRequest.Issue(
                    operationId: "reissue-appealed-label",
                    target: LabelTarget.Account(s_member),
                    value: "spam",
                    context: new(s_administrator, "Apply a new label outcome.")),
                token)).Succeeded);
        }
        using HttpResponseMessage response = await GetAsAsync(fixture, s_administrator, $"/Xenon/Cases/Detail/{opened.Case.Id}?AppealId={appeal.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = await response.Content.ReadAsStringAsync(token);
        Assert.Equal(multipleLabels, html.Contains("value=\"Replaced\"", StringComparison.Ordinal));
        Assert.Contains("Challenged labels changed", html, StringComparison.Ordinal);
        bool changed = expired || reissued;
        Assert.Equal(changed, html.Contains("value=\"Superseded\" selected", StringComparison.Ordinal));
        Assert.Contains($"data-changed=\"{changed.ToString().ToLowerInvariant()}\" selected", html, StringComparison.Ordinal);
        Assert.Contains(changed ? "data-appeal-changed>" : "data-appeal-changed hidden", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/Xenon/Terms")]
    [InlineData("/Xenon/Privacy")]
    public async Task PublicNoticesRetainOperatorResponsibilitiesAndDataLimitations(string path)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, UiPolicy>();
                services.AddSingleton<IDidHandleResolver, TestHandles>();
                services.AddLabelerAdministration(s_audience, key, identity);
            },
            xenonUi: true);
        using HttpResponseMessage response = await fixture.Client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains("person or organization", html, StringComparison.Ordinal);
        Assert.Contains("operator", html, StringComparison.Ordinal);
        if (path.EndsWith("Terms", StringComparison.Ordinal))
        {
            Assert.Contains("A report is not proof of wrongdoing", html, StringComparison.Ordinal);
            Assert.Contains("does not guarantee that copies elsewhere will be removed", html, StringComparison.Ordinal);
            Assert.Contains("do not remove any rights or obligations", html, StringComparison.Ordinal);
            Assert.Contains("MIT license", html, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("not a complete privacy policy", html, StringComparison.Ordinal);
            Assert.Contains("legal reasons for using personal information", html, StringComparison.Ordinal);
            Assert.Contains("not promise that there is no additional tracking", html, StringComparison.Ordinal);
            Assert.Contains("without embedded third-party pages or your browser fetching remote images directly", html, StringComparison.Ordinal);
            Assert.Contains("not a saved copy", html, StringComparison.Ordinal);
            Assert.Contains("do not include report text or private case notes", html, StringComparison.Ordinal);
            Assert.Contains("data protection authority", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task WorkflowCountsRefreshUsesAuthorizedUncachedTotalsAndCountedPageTitles()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddLabelerModeration(issuer: TestServerBuilder.s_issuer, httpClient: identity);
                services.AddSingleton<IDidHandleResolver, TestHandles>();
                services.AddLabelerAdministration(s_audience, key, identity);
            },
            moderation: true,
            xenonUi: true);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(
            s_administrator,
            "uid:0",
            "counts-bootstrap",
            TestContext.Current.CancellationToken)).Succeeded);
        using HttpResponseMessage counts = await GetAsAsync(fixture, s_administrator, "/Xenon?handler=Counts");
        Assert.Equal(HttpStatusCode.OK, counts.StatusCode);
        Assert.Equal("application/json", counts.Content.Headers.ContentType?.MediaType);
        Assert.True(counts.Headers.CacheControl?.NoStore);
        Assert.Equal("""{"cases":0,"reports":0,"appeals":0}""", await counts.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        foreach (string path in new[] { "/Xenon", "/Xenon/Cases", "/Xenon/Reports", "/Xenon/Appeals", "/Xenon/Team" })
        {
            using HttpResponseMessage countedPage = await GetAsAsync(fixture, s_administrator, path);
            Assert.Equal(HttpStatusCode.OK, countedPage.StatusCode);
            string html = await countedPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("<title>C:0 R:0 A:0", html, StringComparison.Ordinal);
            Assert.Contains("data-counts-url=\"/Xenon?handler=Counts\"", html, StringComparison.Ordinal);
            Assert.Contains("/Xenon/js/workflow-counts.js", html, StringComparison.Ordinal);
            Assert.Contains("Inter font by", html, StringComparison.Ordinal);
            Assert.Contains("The Inter Project Authors", html, StringComparison.Ordinal);
            Assert.Contains("href=\"/Xenon/vendor/inter/LICENSE.txt\"", html, StringComparison.Ordinal);
            Assert.Contains("The Google Sans Code Project Authors", html, StringComparison.Ordinal);
            Assert.Contains("href=\"/Xenon/vendor/google-sans-code/OFL.txt\"", html, StringComparison.Ordinal);
            Assert.Contains("href=\"/Xenon/vendor/google-sans-code/TRADEMARKS.md\"", html, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("confirmed")]
    [InlineData("generic-checkbox")]
    [InlineData("changed-intent")]
    [InlineData("missing-antiforgery")]
    [InlineData("revoked")]
    [InlineData("missing-grace")]
    [InlineData("missing-host")]
    public async Task LifecycleUsesSeparateActionBoundPreflightAndFreshAuthorization(string scenario)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new(new RetirementIdentityHandler(key.ExportPublicKeyMultibase()));
        LifecycleTests.TestKeyRing keyRing = new(key, key);
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            options: new() { PublicEndpoint = new Uri("https://labeler.example") },
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, UiPolicy>();
                services.AddSingleton<IDidHandleResolver, TestHandles>();
                services.AddLabelerAdministration(s_audience, keyRing, identity);
            },
            xenonUi: true);
        await fixture.Services.GetRequiredService<ILabelSigningKeySelectionStore>().InitializeSigningKeySelectionAsync(
            keyId: keyRing.ActiveKeyId,
            publicKey: key.ExportPublicKeyMultibase(),
            cancellationToken: TestContext.Current.CancellationToken);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(
            s_administrator,
            "uid:0",
            "lifecycle-bootstrap",
            TestContext.Current.CancellationToken)).Succeeded);
        using HttpResponseMessage initial = await GetAsAsync(fixture, s_administrator, "/Xenon/Lifecycle");
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        string html = await initial.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Matches(
            """(?s)<form method="post">\s*<input type="hidden" name="stage" value="preview" />\s*<input type="hidden" name="method" value="beginRetirement" />.*?<button type="submit">Review: Start permanent retirement</button>""",
            html);
        Assert.DoesNotContain("name=\"method\" value=\"purgeRetiredData\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"method\" value=\"requestKeyRemoval\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Review your stopped-process check", html, StringComparison.Ordinal);
        Assert.Contains("Retiring this labeler is permanent.", html, StringComparison.Ordinal);
        Assert.Contains("pausing does not reopen the labeler", html, StringComparison.Ordinal);
        Assert.Contains("Xenon cannot confirm that every app has received them", html, StringComparison.Ordinal);
        Assert.Contains("remove its labeler service address", html, StringComparison.Ordinal);
        Assert.Contains("Xenon does not update the DID document for you", html, StringComparison.Ordinal);
        Assert.Contains("Keep the public verification keys", html, StringComparison.Ordinal);
        Assert.Contains("You do not need to delete the account", html, StringComparison.Ordinal);
        Assert.Contains("No maintenance in progress.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Key replacement and server shutdown status:", html, StringComparison.Ordinal);
        Assert.DoesNotContain("An unreachable server is not proof that its signing process has stopped.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<summary>Technical details</summary>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"30\"", html, StringComparison.Ordinal);
        (string token, string cookie) = await CsrfAsync(initial);
        Dictionary<string, string> fields = new()
        {
            ["__RequestVerificationToken"] = token,
            ["stage"] = "preview",
            ["method"] = scenario switch
            {
                "missing-grace" => "beginGracePeriod",
                "missing-host" => "confirmRetirementHostStopped",
                _ => "beginRetirement"
            },
            ["operationId"] = "lifecycle-start",
            ["reason"] = "Close the service."
        };
        using HttpRequestMessage preview = new(HttpMethod.Post, "/Xenon/Lifecycle")
        {
            Content = new FormUrlEncodedContent(fields)
        };
        preview.Headers.Add("X-Test-Did", s_administrator.Value);
        preview.Headers.Add("Cookie", cookie);
        using HttpResponseMessage prepared = await fixture.Client.SendAsync(preview, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, prepared.StatusCode);
        ILabelStore store = fixture.Services.GetRequiredService<ILabelStore>();
        Assert.Equal(LabelerLifecyclePhase.Active, (await store.GetStateAsync(TestContext.Current.CancellationToken)).Phase);
        string preparedHtml = await prepared.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (scenario == "missing-host")
        {
            Assert.Contains("Select the server process you have checked and confirmed has stopped.", preparedHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"confirmationText\"", preparedHtml, StringComparison.Ordinal);
            return;
        }

        if (scenario == "missing-grace")
        {
            Assert.Contains("Enter a waiting period between 1 and 365 days.", preparedHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"confirmationText\"", preparedHtml, StringComparison.Ordinal);
            return;
        }

        Assert.Contains("Ready for review. Nothing has changed yet.", preparedHtml, StringComparison.Ordinal);
        Assert.Contains("Review and confirm permanent retirement", preparedHtml, StringComparison.Ordinal);
        Assert.Contains("Confirm permanent retirement</button>", preparedHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmationText\"", preparedHtml, StringComparison.Ordinal);
        Assert.Contains(key.ExportPublicKeyMultibase(), preparedHtml, StringComparison.Ordinal);
        Assert.Contains("Registered servers", preparedHtml, StringComparison.Ordinal);
        const string proofPrefix = "name=\"confirmation\" value=\"";
        int proofStart = preparedHtml.IndexOf(proofPrefix, StringComparison.Ordinal);
        Assert.NotEqual(-1, proofStart);
        string proof = WebUtility.HtmlDecode(preparedHtml[(proofStart + proofPrefix.Length)..].Split('"')[0]);
        Assert.NotEmpty(proof);
        fields["stage"] = "execute";
        fields["confirmation"] = proof;
        fields["confirmationText"] = "beginRetirement";
        if (scenario == "generic-checkbox")
        {
            fields.Remove("confirmation");
            fields.Remove("confirmationText");
            fields["confirmed"] = "true";
        }
        if (scenario == "changed-intent")
        {
            fields["reason"] = "Changed after preview.";
        }
        if (scenario == "missing-antiforgery")
        {
            fields.Remove("__RequestVerificationToken");
        }
        if (scenario == "revoked")
        {
            LabelAdministrationCaller caller = (await team.AuthenticateLocalAsync("uid:0", TestContext.Current.CancellationToken)).Result!;
            Assert.True((await team.ChangeTeamAsync(
                caller,
                new(
                    operationId: "lifecycle-second-admin",
                    action: LabelTeamAction.Enroll,
                    target: s_member,
                    reason: "Keep another administrator.",
                    role: LabelTeamRole.Administrator),
                TestContext.Current.CancellationToken)).Succeeded);
            Assert.True((await team.ChangeTeamAsync(
                caller,
                new(
                    operationId: "lifecycle-transfer-owner",
                    action: LabelTeamAction.TransferOwnership,
                    target: s_member,
                    reason: "Allow the former owner's authority to be revoked after preview."),
                TestContext.Current.CancellationToken)).Succeeded);
            Assert.True((await team.ChangeTeamAsync(
                caller,
                new(
                    operationId: "lifecycle-revoke",
                    action: LabelTeamAction.Suspend,
                    target: s_administrator,
                    reason: "Revoke after preview."),
                TestContext.Current.CancellationToken)).Succeeded);
        }

        using HttpRequestMessage execute = new(HttpMethod.Post, "/Xenon/Lifecycle")
        {
            Content = new FormUrlEncodedContent(fields)
        };
        execute.Headers.Add("X-Test-Did", s_administrator.Value);
        execute.Headers.Add("Cookie", cookie);
        using HttpResponseMessage executed = await fixture.Client.SendAsync(execute, TestContext.Current.CancellationToken);
        HttpStatusCode expected = scenario switch
        {
            "confirmed" => HttpStatusCode.Redirect,
            "revoked" => HttpStatusCode.Forbidden,
            "missing-antiforgery" => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.OK
        };
        Assert.Equal(expected, executed.StatusCode);
        LabelerLifecyclePhase phase = (await store.GetStateAsync(TestContext.Current.CancellationToken)).Phase;
        Assert.Equal(scenario != "confirmed", phase == LabelerLifecyclePhase.Active);
        if (scenario == "changed-intent")
        {
            string failedHtml = await executed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("review the same action again without changing its details", failedHtml, StringComparison.Ordinal);
        }
        if (scenario == "confirmed")
        {
            using HttpResponseMessage gracePage = await GetAsAsync(fixture, s_administrator, "/Xenon/Lifecycle");
            string graceHtml = await gracePage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("Review: Start the waiting period", graceHtml, StringComparison.Ordinal);
            Assert.Contains("name=\"method\" value=\"beginGracePeriod\"", graceHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("Preview beginRetirement", graceHtml, StringComparison.Ordinal);
            Assert.Matches("<input[^>]*name=\"graceDays\"[^>]*required", graceHtml);
            Assert.DoesNotMatch("<input[^>]*name=\"graceDays\"[^>]*value=", graceHtml);
        }
        if (scenario == "generic-checkbox")
        {
            using LabelSigningKey replacement = LabelSigningKey.Generate(LabelSigningCurve.P256);
            await fixture.Services.GetRequiredService<ILabelMaintenanceStore>().BeginRotationAsync(
                operationId: "ui-maintenance",
                previousKeyId: keyRing.ActiveKeyId,
                replacementKeyId: "replacement",
                replacementPublicKey: replacement.ExportPublicKeyMultibase(),
                cancellationToken: TestContext.Current.CancellationToken);
            using HttpResponseMessage maintenancePage = await GetAsAsync(fixture, s_administrator, "/Xenon/Lifecycle");
            string maintenanceHtml = await maintenancePage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain("No maintenance in progress.", maintenanceHtml, StringComparison.Ordinal);
            Assert.Contains("Key replacement and server shutdown status:", maintenanceHtml, StringComparison.Ordinal);
            Assert.Contains("An unreachable server is not proof that its signing process has stopped.", maintenanceHtml, StringComparison.Ordinal);
            Assert.Contains("<summary>Technical details</summary>", maintenanceHtml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task XenonPagesRequireAnAuthenticatedActiveMember()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        LifecycleTests.TestKeyRing keyRing = new(key, key);
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, UiPolicy>();
                services.AddSingleton<IDidHandleResolver, TestHandles>();
                services.AddLabelerAdministration(s_audience, keyRing, identity);
            },
            xenonUi: true);

        using HttpResponseMessage unauthenticated = await fixture.Client.GetAsync("/Xenon", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        using HttpResponseMessage anonymousCounts = await fixture.Client.GetAsync("/Xenon?handler=Counts", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCounts.StatusCode);

        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(
            s_administrator,
            "uid:0",
            "xenon-bootstrap",
            TestContext.Current.CancellationToken)).Succeeded);
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync(
            "uid:0",
            TestContext.Current.CancellationToken)).Result!;

        using HttpResponseMessage outsider = await GetAsAsync(fixture, new("did:plc:xenon-outsider"));
        Assert.Equal(HttpStatusCode.Forbidden, outsider.StatusCode);
        using HttpResponseMessage outsiderCounts = await GetAsAsync(fixture, new("did:plc:xenon-outsider"), "/Xenon?handler=Counts");
        Assert.Equal(HttpStatusCode.Forbidden, outsiderCounts.StatusCode);
        using HttpResponseMessage outsiderVideo = await GetAsAsync(fixture, new("did:plc:xenon-outsider"), "/Xenon/Media?token=invalid");
        Assert.Equal(HttpStatusCode.Forbidden, outsiderVideo.StatusCode);
        foreach (string notice in new[] { "/Xenon/Terms", "/Xenon/Privacy" })
        {
            using HttpResponseMessage anonymousNotice = await fixture.Client.GetAsync(notice, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, anonymousNotice.StatusCode);
            using HttpResponseMessage outsiderNotice = await GetAsAsync(fixture, new("did:plc:xenon-outsider"), notice);
            Assert.Equal(HttpStatusCode.OK, outsiderNotice.StatusCode);
        }

        Assert.True((await team.ChangeTeamAsync(
            administrator,
            new(
                operationId: "xenon-enroll",
                action: LabelTeamAction.Enroll,
                target: s_member,
                reason: "Enroll a UI test member.",
                role: LabelTeamRole.Viewer),
            TestContext.Current.CancellationToken)).Succeeded);
        using HttpResponseMessage activeMember = await GetAsAsync(fixture, s_member);
        Assert.Equal(HttpStatusCode.OK, activeMember.StatusCode);
        using HttpResponseMessage activeCounts = await GetAsAsync(fixture, s_member, "/Xenon?handler=Counts");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, activeCounts.StatusCode);
        Assert.True(activeCounts.Headers.CacheControl?.NoStore);
        using HttpResponseMessage invalidVideo = await GetAsAsync(fixture, s_member, "/Xenon/Media?token=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, invalidVideo.StatusCode);

        using HttpResponseMessage teamPage = await GetAsAsync(fixture, s_administrator, "/Xenon/Team");
        Assert.Equal(HttpStatusCode.OK, teamPage.StatusCode);
        string teamHtml = await teamPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Administrator email notifications", teamHtml, StringComparison.Ordinal);
        Assert.Contains("@admin.example", teamHtml, StringComparison.Ordinal);
        Assert.Contains("role=\"tooltip\"", teamHtml, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"identity-", teamHtml, StringComparison.Ordinal);
        Assert.Contains("<code>did:plc:xenon-member</code>", teamHtml, StringComparison.Ordinal);
        Assert.Contains("Handle unavailable", teamHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("@handle.invalid", teamHtml, StringComparison.Ordinal);
        Assert.Matches("""<td><span class="team-member-role">\s*<span class="fa-icon fa-eye" aria-hidden="true"></span>\s*Viewer\s*</span></td>""", teamHtml);

        string[] invalidFilters =
        [
            "/Xenon/Cases?Queue=invalid",
            "/Xenon/Reports?AfterId=invalid",
            "/Xenon/Appeals?Status=invalid"
        ];
        foreach (string path in invalidFilters)
        {
            using HttpResponseMessage invalidFilter = await GetAsAsync(fixture, s_member, path);
            Assert.Equal(HttpStatusCode.OK, invalidFilter.StatusCode);
            string html = await invalidFilter.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        }

        using HttpRequestMessage missingAntiforgery = new(HttpMethod.Post, "/Xenon/Labels")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["targetKind"] = "Account",
                ["target"] = s_member.Value
            })
        };
        missingAntiforgery.Headers.Add("X-Test-Did", s_member.Value);
        using HttpResponseMessage rejectedPost = await fixture.Client.SendAsync(
            missingAntiforgery,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, rejectedPost.StatusCode);

        Assert.True((await team.ChangeTeamAsync(
            administrator,
            new(
                operationId: "xenon-suspend",
                action: LabelTeamAction.Suspend,
                target: s_member,
                reason: "Suspend the UI test member."),
            TestContext.Current.CancellationToken)).Succeeded);
        using HttpResponseMessage suspendedMember = await GetAsAsync(fixture, s_member);
        Assert.Equal(HttpStatusCode.Forbidden, suspendedMember.StatusCode);
        using HttpResponseMessage suspendedCounts = await GetAsAsync(fixture, s_member, "/Xenon?handler=Counts");
        Assert.Equal(HttpStatusCode.Forbidden, suspendedCounts.StatusCode);
        using HttpResponseMessage suspendedVideo = await GetAsAsync(fixture, s_member, "/Xenon/Media?token=invalid");
        Assert.Equal(HttpStatusCode.Forbidden, suspendedVideo.StatusCode);
    }

    [Fact]
    public async Task AdministratorsCanRegisterPolicyAlertEmailButModeratorsCannotViewOrChangeIt()
    {
        int testEmails = 0;
        string? testRecipient = null;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        LifecycleTests.TestKeyRing keyRing = new(key, key);
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddSingleton<ILabelIssuancePolicyProvider, UiPolicy>();
                services.AddSingleton<IDidHandleResolver, TestHandles>();
                services.AddSingleton(new LabelerModerationOptions
                {
                    PolicyEmail = new()
                    {
                        Enabled = true,
                        Server = "smtp.example.invalid",
                        FromAddress = "labeler@example.invalid"
                    }
                });
                services.AddSingleton(new LabelerPolicyEmailService(
                    issuer: TestServerBuilder.s_issuer,
                    alerts: null,
                    settings: new()
                    {
                        Enabled = true,
                        Server = "smtp.example.invalid",
                        FromAddress = "labeler@example.invalid"
                    },
                    clock: new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
                        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                    logger: Microsoft.Extensions.Logging.Abstractions.NullLogger<LabelerPolicyEmailService>.Instance)
                {
                    TestMessageSender = (_, message, _) =>
                    {
                        testEmails++;
                        testRecipient = Assert.Single(message.To).Address;
                        return Task.CompletedTask;
                    }
                });
                services.AddLabelerAdministration(s_audience, keyRing, identity);
            },
            xenonUi: true);

        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(
            s_administrator,
            "uid:0",
            "email-bootstrap",
            TestContext.Current.CancellationToken)).Succeeded);
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync(
            "uid:0",
            TestContext.Current.CancellationToken)).Result!;
        Assert.True((await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "email-enroll-moderator",
                action: LabelTeamAction.Enroll,
                target: s_member,
                reason: "Enroll a moderator.",
                role: LabelTeamRole.Moderator),
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded);

        using HttpResponseMessage administratorPage = await GetAsAsync(fixture, s_administrator, "/Xenon/Team");
        Assert.Equal(HttpStatusCode.OK, administratorPage.StatusCode);
        string initialHtml = await administratorPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Add an email address for administrator notifications.", initialHtml, StringComparison.Ordinal);
        Assert.Contains("<div class=\"notification-email-entry\">", initialHtml, StringComparison.Ordinal);
        Assert.Contains(">Save alert email</button>", initialHtml, StringComparison.Ordinal);
        Assert.Contains("<details class=\"panel\" open", initialHtml, StringComparison.Ordinal);
        (string token, string cookie) = await CsrfAsync(administratorPage);
        const string address = "admin@example.invalid";
        using HttpRequestMessage save = new(HttpMethod.Post, "/Xenon/Team?handler=NotificationEmail")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["operationId"] = "email-save",
                ["notificationEmail"] = address
            })
        };
        save.Headers.Add("X-Test-Did", s_administrator.Value);
        save.Headers.Add("Cookie", cookie);
        using HttpResponseMessage saved = await fixture.Client.SendAsync(save, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        using HttpResponseMessage updatedPage = await GetAsAsync(fixture, s_administrator, "/Xenon/Team");
        string updatedHtml = await updatedPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(address, updatedHtml, StringComparison.Ordinal);
        Assert.Contains("You have configured your email address. Expand to edit or remove it.", updatedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("<details class=\"panel\" open", updatedHtml, StringComparison.Ordinal);
        Assert.Contains("Update your email address for administrator notifications.", updatedHtml, StringComparison.Ordinal);
        Assert.Contains("<div class=\"notification-email-entry\">", updatedHtml, StringComparison.Ordinal);
        Assert.Contains(">Update email</button>", updatedHtml, StringComparison.Ordinal);
        Assert.Contains("class=\"notification-email-test\"", updatedHtml, StringComparison.Ordinal);
        Assert.Contains("<p class=\"muted\">Tests use your saved address, not unsaved changes above. Allow 30 seconds between tests.</p>", updatedHtml, StringComparison.Ordinal);
        using HttpResponseMessage preferencesPage = await GetAsAsync(fixture, s_administrator, "/Xenon/Preferences");
        string preferencesHtml = await preferencesPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Administrator email notifications", preferencesHtml, StringComparison.Ordinal);
        Assert.Contains(">Test my email</button>", preferencesHtml, StringComparison.Ordinal);
        Assert.Contains($"value=\"{address}\"", preferencesHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"returnToPreferences\" value=\"true\"", preferencesHtml, StringComparison.Ordinal);
        Assert.Contains("<div class=\"notification-email-entry\">", preferencesHtml, StringComparison.Ordinal);
        Assert.Contains("class=\"notification-email-test\"", preferencesHtml, StringComparison.Ordinal);
        Assert.Contains("<p class=\"muted\">Tests use your saved address, not unsaved changes above. Allow 30 seconds between tests.</p>", preferencesHtml, StringComparison.Ordinal);
        (string preferencesToken, string preferencesCookie) = await CsrfAsync(preferencesPage);
        using HttpRequestMessage testSaved = new(HttpMethod.Post, "/Xenon/Team?handler=TestEmail")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = preferencesToken,
                ["returnToPreferences"] = "true",
                ["notificationEmail"] = "ignored@example.invalid"
            })
        };
        testSaved.Headers.Add("X-Test-Did", s_administrator.Value);
        testSaved.Headers.Add("Cookie", preferencesCookie);
        using HttpResponseMessage tested = await fixture.Client.SendAsync(testSaved, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, tested.StatusCode);
        Assert.Equal("/Xenon/Preferences", tested.Headers.Location!.OriginalString);
        Assert.Equal(1, testEmails);
        Assert.Equal(address, testRecipient);
        using HttpRequestMessage invalidEmail = new(HttpMethod.Post, "/Xenon/Team?handler=NotificationEmail")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = preferencesToken,
                ["returnToPreferences"] = "true",
                ["operationId"] = "email-preferences-invalid",
                ["notificationEmail"] = "not-an-email"
            })
        };
        invalidEmail.Headers.Add("X-Test-Did", s_administrator.Value);
        invalidEmail.Headers.Add("Cookie", preferencesCookie);
        using HttpResponseMessage invalid = await fixture.Client.SendAsync(invalidEmail, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, invalid.StatusCode);
        Assert.Equal("/Xenon/Preferences", invalid.Headers.Location!.OriginalString);
        Assert.Equal(address, Assert.Single((await team.GetTeamAsync(administrator, TestContext.Current.CancellationToken)).Result!,
            member => member.Did == s_administrator).NotificationEmail);
        using HttpRequestMessage clear = new(HttpMethod.Post, "/Xenon/Team?handler=NotificationEmail")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = preferencesToken,
                ["operationId"] = "email-preferences-clear",
                ["notificationEmail"] = "",
                ["returnToPreferences"] = "true"
            })
        };
        clear.Headers.Add("X-Test-Did", s_administrator.Value);
        clear.Headers.Add("Cookie", preferencesCookie);
        using HttpResponseMessage cleared = await fixture.Client.SendAsync(clear, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, cleared.StatusCode);
        Assert.Equal("/Xenon/Preferences", cleared.Headers.Location!.OriginalString);
        Assert.Null(Assert.Single((await team.GetTeamAsync(administrator, TestContext.Current.CancellationToken)).Result!,
            member => member.Did == s_administrator).NotificationEmail);
        using HttpRequestMessage testMissing = new(HttpMethod.Post, "/Xenon/Team?handler=TestEmail")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = preferencesToken,
                ["returnToPreferences"] = "true",
                ["notificationEmail"] = "ignored@example.invalid"
            })
        };
        testMissing.Headers.Add("X-Test-Did", s_administrator.Value);
        testMissing.Headers.Add("Cookie", preferencesCookie);
        using HttpResponseMessage missingResponse = await fixture.Client.SendAsync(testMissing, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, missingResponse.StatusCode);
        Assert.Equal("/Xenon/Preferences", missingResponse.Headers.Location!.OriginalString);
        Assert.Equal(1, testEmails);
        Assert.Contains("Notifications go only to active administrators with a registered address. Leave the field blank to clear your address.", updatedHtml, StringComparison.Ordinal);
        using HttpResponseMessage moderatorPage = await GetAsAsync(fixture, s_member, "/Xenon/Team");
        string moderatorHtml = await moderatorPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Administrator email notifications", moderatorHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(address, moderatorHtml, StringComparison.Ordinal);
        using HttpResponseMessage moderatorPreferences = await GetAsAsync(fixture, s_member, "/Xenon/Preferences");
        Assert.DoesNotContain("Administrator email notifications",
            await moderatorPreferences.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

        using HttpResponseMessage moderatorCsrfPage = await GetAsAsync(fixture, s_member, "/Xenon/Team");
        (string moderatorToken, string moderatorCookie) = await CsrfAsync(moderatorCsrfPage);
        using HttpRequestMessage forbidden = new(HttpMethod.Post, "/Xenon/Team?handler=NotificationEmail")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = moderatorToken,
                ["operationId"] = "email-forbidden",
                ["notificationEmail"] = "moderator@example.invalid"
            })
        };
        forbidden.Headers.Add("X-Test-Did", s_member.Value);
        forbidden.Headers.Add("Cookie", moderatorCookie);
        using HttpResponseMessage rejected = await fixture.Client.SendAsync(forbidden, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        using HttpRequestMessage forbiddenTest = new(HttpMethod.Post, "/Xenon/Team?handler=TestEmail")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = moderatorToken,
                ["notificationEmail"] = "ignored@example.invalid"
            })
        };
        forbiddenTest.Headers.Add("X-Test-Did", s_member.Value);
        forbiddenTest.Headers.Add("Cookie", moderatorCookie);
        using HttpResponseMessage testRejected = await fixture.Client.SendAsync(forbiddenTest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, testRejected.StatusCode);
        using HttpRequestMessage noCsrf = new(HttpMethod.Post, "/Xenon/Team?handler=TestEmail");
        noCsrf.Headers.Add("X-Test-Did", s_administrator.Value);
        using HttpResponseMessage csrfRejected = await fixture.Client.SendAsync(noCsrf, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, csrfRejected.StatusCode);
        Assert.Equal(1, testEmails);
    }

    private static async Task<HttpResponseMessage> GetAsAsync(TestServerBuilder fixture, Did actor, string path = "/Xenon")
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Test-Did", actor.Value);
        return await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Confirms the fictional appeal subject's DID without accessing public infrastructure.</summary>
    private sealed class AppealIdentityHandler : HttpMessageHandler
    {
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"id":"{{s_member}}","service":[]}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            });
    }

    /// <summary>Publishes the selected label verification key and configured endpoint without external requests.</summary>
    private sealed class RetirementIdentityHandler(string publicKey) : HttpMessageHandler
    {
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string did = TestServerBuilder.s_issuer.Value;
            string document = $$"""
                {
                  "id":"{{did}}",
                  "verificationMethod":[{
                    "id":"{{did}}#atproto_label","type":"Multikey",
                    "controller":"{{did}}","publicKeyMultibase":"{{publicKey}}"
                  }],
                  "service":[{
                    "id":"{{did}}#atproto_labeler","type":"AtprotoLabeler",
                    "serviceEndpoint":"https://labeler.example"
                  }]
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(document, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private static async Task<(string Token, string Cookie)> CsrfAsync(HttpResponseMessage response)
    {
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        const string prefix = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        int start = html.IndexOf(prefix, StringComparison.Ordinal);
        Assert.NotEqual(-1, start);
        string token = WebUtility.HtmlDecode(html[(start + prefix.Length)..].Split('"')[0]);
        string cookie = string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]));
        return (token, cookie);
    }

    private sealed class UiPolicy : ILabelIssuancePolicyProvider
    {
        public Task<LabelServiceResult<LabelIssuancePolicy>> GetPolicyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(LabelServiceResult<LabelIssuancePolicy>.Success(new(
                version: "xenon-ui-test",
                declaredValues: ["spam"],
                allowAccounts: true,
                allowRecords: true)));
    }

    /// <summary>Supplies a verified display identity and an explicit resolution failure without network access.</summary>
    private sealed class TestHandles : IDidHandleResolver
    {
        /// <inheritdoc/>
        public ValueTask<Handle> ResolveHandleAsync(Did did, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(did == s_administrator ? new Handle("admin.example") : Handle.Invalid);

        /// <inheritdoc/>
        public bool TryGetCachedHandle(Did did, [NotNullWhen(true)] out Handle? handle)
        {
            handle = did == s_administrator ? new Handle("admin.example") : Handle.Invalid;
            return true;
        }

        /// <inheritdoc/>
        public void Invalidate(Did did)
        {
        }

    }

    /// <summary>Provides deterministic registered-member handles, including one unavailable identity, without network access.</summary>
    private sealed class TeamHandles : IDidHandleResolver
    {
        /// <inheritdoc/>
        public ValueTask<Handle> ResolveHandleAsync(Did did, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(MemberHandle(did));

        /// <inheritdoc/>
        public bool TryGetCachedHandle(Did did, [NotNullWhen(true)] out Handle? handle)
        {
            handle = MemberHandle(did);
            return true;
        }

        /// <inheritdoc/>
        public void Invalidate(Did did)
        {
        }

        /// <summary>Maps a fictional member identity to a deterministic public handle or an explicit lookup failure.</summary>
        private static Handle MemberHandle(Did did) =>
            did.Value == "did:plc:unavailable" ? Handle.Invalid : new Handle(did.Value.Split(':')[^1] + ".example");
    }
}
