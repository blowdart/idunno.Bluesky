// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;

using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public class XenonMuteUiTests
{
    private static readonly Did s_administrator = new("did:plc:xenon-mute-admin");
    private static readonly Did s_member = new("did:plc:xenon-mute-member");
    private static readonly Did s_target = new("did:plc:xenon-mute-target");

    [Theory]
    [InlineData(LabelTeamRole.Viewer)]
    [InlineData(LabelTeamRole.Moderator)]
    [InlineData(LabelTeamRole.Administrator)]
    public async Task MuteNavigationAndMutationRequireModeratorAuthority(LabelTeamRole role)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        await using TestServerBuilder fixture = await CreateAsync(key, identity, role);
        using HttpResponseMessage page = await GetAsync(fixture, s_member, "/Xenon/Mutes");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        string html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        bool canChange = role >= LabelTeamRole.Moderator;
        Assert.Equal(canChange, html.Contains("aria-hidden=\"true\"></span> Mutes</a>", StringComparison.Ordinal));
        Assert.Equal(canChange, html.Contains(">Confirm mute</button>", StringComparison.Ordinal));
        Assert.Contains("All active shared mutes", html, StringComparison.Ordinal);
        using HttpResponseMessage csrfPage = await GetAsync(fixture, s_member, "/Xenon/Labels");
        (string token, string cookie) = await CsrfAsync(csrfPage);
        using HttpResponseMessage posted = await PostAsync(
            fixture: fixture,
            actor: s_member,
            handler: "Mute",
            token: token,
            cookie: cookie,
            fields: MuteFields("role-mute"));
        Assert.Equal(canChange ? HttpStatusCode.Redirect : HttpStatusCode.Forbidden, posted.StatusCode);
        IReadOnlyList<MuteView> active = await ReadMutesAsync(fixture);
        Assert.Equal(canChange ? 1 : 0, active.Count);
        if (canChange)
        {
            Assert.Equal(s_member.Value, Assert.Single(active).Actor);
        }
    }

    [Theory]
    [InlineData("Subject")]
    [InlineData("Reporter")]
    public async Task ConfirmedFormsShareFiniteMutesAndFenceUnmuteAgainstReplacement(string kind)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        using HttpResponseMessage page = await GetAsync(fixture, s_administrator, "/Xenon/Mutes");
        (string token, string cookie) = await CsrfAsync(page);
        string html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        foreach (int hours in new[] { 6, 12, 24, 48, 72, 168, 720, 2160 })
        {
            Assert.Contains($"value=\"{hours}\"", html, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("Permanent", html, StringComparison.Ordinal);
        Dictionary<string, string> fields = MuteFields("confirmed-mute");
        fields["kind"] = kind;
        fields["confirmed"] = "false";
        using HttpResponseMessage unconfirmed = await PostAsync(
            fixture: fixture, actor: s_administrator, handler: "Mute", token: token, cookie: cookie, fields: fields);
        Assert.Equal(HttpStatusCode.OK, unconfirmed.StatusCode);
        Assert.Empty(await ReadMutesAsync(fixture));
        Assert.Contains("value=\"confirmed-mute\"", await unconfirmed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

        fields["confirmed"] = "true";
        using HttpResponseMessage created = await PostAsync(
            fixture: fixture, actor: s_administrator, handler: "Mute", token: token, cookie: cookie, fields: fields);
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        MuteView original = Assert.Single(await ReadMutesAsync(fixture));
        Assert.Equal(kind, original.Kind);
        Assert.Equal(fixture.DatabaseClock.GetUtcNow().AddHours(24), original.Until);
        using HttpResponseMessage replay = await PostAsync(
            fixture: fixture, actor: s_administrator, handler: "Mute", token: token, cookie: cookie, fields: fields);
        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Equal(original.EventId, Assert.Single(await ReadMutesAsync(fixture)).EventId);

        fields["operationId"] = "replacement-mute";
        fields["durationHours"] = "72";
        using HttpResponseMessage replacement = await PostAsync(
            fixture: fixture, actor: s_administrator, handler: "Mute", token: token, cookie: cookie, fields: fields);
        Assert.Equal(HttpStatusCode.Redirect, replacement.StatusCode);
        MuteView current = Assert.Single(await ReadMutesAsync(fixture));
        Assert.NotEqual(original.EventId, current.EventId);

        fields.Remove("durationHours");
        fields["operationId"] = "stale-unmute";
        fields["expectedMuteEventId"] = original.EventId.ToString(CultureInfo.InvariantCulture);
        using HttpResponseMessage conflict = await PostAsync(
            fixture: fixture, actor: s_administrator, handler: "Unmute", token: token, cookie: cookie, fields: fields);
        Assert.Equal(HttpStatusCode.OK, conflict.StatusCode);
        Assert.Contains("role=\"alert\"", await conflict.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(current.EventId, Assert.Single(await ReadMutesAsync(fixture)).EventId);

        fields["operationId"] = "current-unmute";
        fields["expectedMuteEventId"] = current.EventId.ToString(CultureInfo.InvariantCulture);
        fields["confirmed"] = "false";
        using HttpResponseMessage unconfirmedRemoval = await PostAsync(
            fixture: fixture, actor: s_administrator, handler: "Unmute", token: token, cookie: cookie, fields: fields);
        Assert.Equal(HttpStatusCode.OK, unconfirmedRemoval.StatusCode);
        Assert.Contains("value=\"current-unmute\"", await unconfirmedRemoval.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Single(await ReadMutesAsync(fixture));

        fields["confirmed"] = "true";
        using HttpResponseMessage removed = await PostAsync(
            fixture: fixture, actor: s_administrator, handler: "Unmute", token: token, cookie: cookie, fields: fields);
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.Empty(await ReadMutesAsync(fixture));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("7")]
    [InlineData("2161")]
    [InlineData("")]
    public async Task UnsupportedOrPermanentDurationsNeverCreateMutes(string duration)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        using HttpResponseMessage page = await GetAsync(fixture, s_administrator, "/Xenon/Mutes");
        (string token, string cookie) = await CsrfAsync(page);
        Dictionary<string, string> fields = MuteFields("invalid-duration");
        fields["durationHours"] = duration;
        using HttpResponseMessage response = await PostAsync(
            fixture: fixture, actor: s_administrator, handler: "Mute", token: token, cookie: cookie, fields: fields);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("choose a supported duration", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Empty(await ReadMutesAsync(fixture));
    }

    [Fact]
    public async Task MuteFormsRejectMissingAntiforgeryAndGetNeverMutates()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient identity = new();
        await using TestServerBuilder fixture = await CreateAsync(key, identity);
        using HttpResponseMessage get = await GetAsync(
            fixture,
            s_administrator,
            $"/Xenon/Mutes?handler=Mute&subject={s_target}&kind=Reporter&durationHours=24&confirmed=true&operationId=get-mute");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Empty(await ReadMutesAsync(fixture));
        using HttpResponseMessage response = await PostAsync(
            fixture: fixture,
            actor: s_administrator,
            handler: "Mute",
            token: string.Empty,
            cookie: string.Empty,
            fields: MuteFields("missing-antiforgery"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await ReadMutesAsync(fixture));
    }

    /// <summary>Creates a real UI host without changing the shared fixture or accessing external identity services.</summary>
    private static async Task<TestServerBuilder> CreateAsync(
        LabelSigningKey key,
        HttpClient identity,
        LabelTeamRole role = LabelTeamRole.Moderator)
    {
        TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            configureAdministration: services =>
            {
                services.AddLabelerModeration(issuer: TestServerBuilder.s_issuer, httpClient: identity);
                services.AddSingleton<IDidHandleResolver, TestHandles>();
                services.AddLabelerAdministration(new(TestServerBuilder.s_issuer, "labeler_admin"), key, identity);
            },
            moderation: true,
            xenonUi: true,
            controlDatabaseTime: true);
        ILabelAdministrationStore team = fixture.Services.GetRequiredService<ILabelAdministrationStore>();
        Assert.True((await team.BootstrapAsync(
            administrator: s_administrator,
            localIdentity: "uid:0",
            operationId: "mute-bootstrap",
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        LabelAdministrationCaller administrator = (await team.AuthenticateLocalAsync("uid:0", TestContext.Current.CancellationToken)).Result!;
        Assert.True((await team.ChangeTeamAsync(
            caller: administrator,
            change: new(
                operationId: "mute-enroll-member",
                action: LabelTeamAction.Enroll,
                target: s_member,
                reason: "Enroll a mute UI test member.",
                role: role),
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        return fixture;
    }

    /// <summary>Builds a complete confirmed finite-mute intent with a caller-selected stable identifier.</summary>
    private static Dictionary<string, string> MuteFields(string operationId) => new()
    {
        ["subject"] = s_target.Value,
        ["kind"] = "Subject",
        ["durationHours"] = "24",
        ["operationId"] = operationId,
        ["reason"] = string.Empty,
        ["confirmed"] = "true"
    };

    /// <summary>Reads provider-active entries independently of the rendered page.</summary>
    private static async Task<IReadOnlyList<MuteView>> ReadMutesAsync(TestServerBuilder fixture)
    {
        LabelAdministrationCaller administrator = (await fixture.Services.GetRequiredService<ILabelAdministrationStore>()
            .AuthenticateLocalAsync("uid:0", TestContext.Current.CancellationToken)).Result!;
        AdministrationResponse response = await fixture.Services.GetRequiredService<AdministrationDispatcher>()
            .DispatchAsync(
                caller: administrator,
                command: new(Method: "getMutes", Limit: 100, Moderation: new()),
                token: TestContext.Current.CancellationToken);
        Assert.True(response.Succeeded, response.Message);
        JsonElement data = JsonSerializer.SerializeToElement(response, AdministrationJsonContext.Default.AdministrationResponse);
        return data.GetProperty("moderation").GetProperty("mutes").EnumerateArray().Select(mute => new MuteView(
            EventId: mute.GetProperty("eventId").GetInt64(),
            Kind: mute.GetProperty("kind").GetInt32() == 0 ? "Subject" : "Reporter",
            Until: mute.GetProperty("until").GetDateTimeOffset(),
            Actor: mute.GetProperty("actor").GetString()!)).ToArray();
    }

    /// <summary>Loads a page using the existing fixture's authenticated browser identity.</summary>
    private static async Task<HttpResponseMessage> GetAsync(TestServerBuilder fixture, Did actor, string path)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Test-Did", actor.Value);
        return await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Submits an antiforgery-protected form through the Razor Pages handler.</summary>
    private static async Task<HttpResponseMessage> PostAsync(
        TestServerBuilder fixture,
        Did actor,
        string handler,
        string token,
        string cookie,
        Dictionary<string, string> fields)
    {
        Dictionary<string, string> submitted = new(fields) { ["__RequestVerificationToken"] = token };
        using HttpRequestMessage request = new(HttpMethod.Post, $"/Xenon/Mutes?handler={handler}")
        {
            Content = new FormUrlEncodedContent(submitted)
        };
        request.Headers.Add("X-Test-Did", actor.Value);
        if (cookie.Length > 0)
        {
            request.Headers.Add("Cookie", cookie);
        }
        return await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Extracts the real antiforgery token and its paired response cookie.</summary>
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

    /// <summary>Retains the observable mute fields without requiring friend access to storage contracts.</summary>
    private sealed record MuteView(long EventId, string Kind, DateTimeOffset Until, string Actor);

    /// <summary>Returns a cacheable resolution failure without making network requests for fictional DIDs.</summary>
    private sealed class TestHandles : IDidHandleResolver
    {
        /// <inheritdoc/>
        public ValueTask<Handle> ResolveHandleAsync(Did did, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Handle.Invalid);

        /// <inheritdoc/>
        public bool TryGetCachedHandle(Did did, [NotNullWhen(true)] out Handle? handle)
        {
            handle = Handle.Invalid;
            return true;
        }

        /// <inheritdoc/>
        public void Invalidate(Did did)
        {
        }
    }
}
