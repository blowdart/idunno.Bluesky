// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public class XenonUiTests
{
    private static readonly Did s_administrator = new("did:plc:xenon-admin");
    private static readonly Did s_member = new("did:plc:xenon-member");
    private static readonly AtProtoServiceReference s_audience = new(TestServerBuilder.s_issuer, "labeler_admin");

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
        using HttpResponseMessage invalidVideo = await GetAsAsync(fixture, s_member, "/Xenon/Media?token=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, invalidVideo.StatusCode);

        using HttpResponseMessage teamPage = await GetAsAsync(fixture, s_administrator, "/Xenon/Team");
        Assert.Equal(HttpStatusCode.OK, teamPage.StatusCode);
        string teamHtml = await teamPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("@admin.example", teamHtml, StringComparison.Ordinal);
        Assert.Contains("role=\"tooltip\"", teamHtml, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"identity-", teamHtml, StringComparison.Ordinal);
        Assert.Contains("<code>did:plc:xenon-member</code>", teamHtml, StringComparison.Ordinal);
        Assert.Contains("Handle unavailable", teamHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("@handle.invalid", teamHtml, StringComparison.Ordinal);
        Assert.Matches("""<td>\s*<span class="fa-icon fa-eye" aria-hidden="true"></span>\s*Viewer</td>""", teamHtml);

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
        using HttpResponseMessage suspendedVideo = await GetAsAsync(fixture, s_member, "/Xenon/Media?token=invalid");
        Assert.Equal(HttpStatusCode.Forbidden, suspendedVideo.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetAsAsync(TestServerBuilder fixture, Did actor, string path = "/Xenon")
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("X-Test-Did", actor.Value);
        return await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
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
}
