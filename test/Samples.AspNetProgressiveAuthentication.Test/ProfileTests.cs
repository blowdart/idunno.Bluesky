// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Duende.IdentityModel.OidcClient.DPoP;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky;
using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Samples.AspNetProgressiveAuthentication.Test;

public partial class ProfileTests
{
    private const string DidValue = "did:plc:ewvi7nxzyoun6zhxrhs64oiz";
    private const string OtherDid = "did:plc:ragtjsm2j2vknwkz3zp4oxrd";
    private const string ProfileCid = "bafyreie5cvv4hro5oz3x5i5m5agzojwzwczl7ddsxv2gm7vby7i2dqu2yi";
    private static readonly ProfileEdit Edit = new("Edited name", "Edited description", "they/them", ProfileCid);

    [Fact]
    public void InitialScopesAndBothMetadataFormsAreLeastPrivilege()
    {
        using var factory = new SampleFactory();
        var oauth = factory.Services.GetRequiredService<IOptions<BlueskyAgentOptions>>().Value.OAuthOptions!;
        Assert.Equal(ProfilePermissions.ReadScopes, oauth.GetRequestedScopes());
        var authentication = factory.Services.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>()
            .Get(BlueskyAuthenticationDefaults.AuthenticationScheme);
        Assert.Equal(".AspNetCore.Bluesky.Progressive", authentication.Cookie.Name);
        Assert.Equal(".AspNetCore.Bluesky.Progressive.Correlation", authentication.CorrelationCookie.Name);
        Assert.Empty(oauth.PermissionSets);
        Assert.DoesNotContain(ProfilePermissions.WriteScope, oauth.GetRequestedScopes());
        Assert.DoesNotContain(ProfilePermissions.CreateScope, oauth.GetRequestedScopes());
        Assert.DoesNotContain(oauth.GetRequestedScopes(), scope => scope.Contains("transition:", StringComparison.Ordinal));
        Assert.Equal(4, oauth.GetRequestedScopes().Count());
        var query = QueryHelpers.ParseQuery(new Uri(oauth.ClientId).Query);
        Assert.Equal(string.Join(' ', ProfilePermissions.MaximumScopes), query["scope"].ToString());
        Assert.Equal("http://127.0.0.1/Bluesky/Callback", query["redirect_uri"].ToString());

        var metadata = factory.Services.GetRequiredService<IOptions<BlueskyOAuthClientMetadataOptions>>().Value;
        var production = new OAuthOptions("https://app.example/oauth-client-metadata.json",
            new Uri("https://app.example/Bluesky/Callback"), oauth.GetRequestedScopes());
        using JsonDocument document = JsonDocument.Parse(metadata.GenerateJson(production));
        Assert.Equal(string.Join(' ', ProfilePermissions.MaximumScopes), document.RootElement.GetProperty("scope").GetString());
        Assert.Equal(ProfilePermissions.ReadScopes, oauth.GetRequestedScopes());
    }

    [Theory]
    [InlineData(5251)]
    [InlineData(5254)]
    [InlineData(5300)]
    public void LocalhostCallbackKeepsConfiguredPathAndBrowserPort(int port)
    {
        using var factory = new SampleFactory();
        using var scope = factory.Services.CreateScope();
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("127.0.0.1", port);
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        var manager = scope.ServiceProvider.GetRequiredService<BlueskySignInManager>();
        Assert.Equal(new Uri($"http://127.0.0.1:{port}/Bluesky/Callback"), manager.CreateReturnUri());
    }

    [Fact]
    public void ClaimsTransformerIsRegisteredOnlyOnce()
    {
        using var factory = new SampleFactory();
        _ = factory.Services;
        Assert.Equal(1, factory.ClaimsTransformerRegistrations);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    public async Task ProductionMetadataUsesTransportMiddleware(string scheme)
    {
        await using var factory = new SampleFactory { Production = true };
        using var browser = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new($"{scheme}://example.idunno.blue")
        });
        using var response = await browser.GetAsync("/oauth-client-metadata.json", TestContext.Current.CancellationToken);
        if (scheme == "http")
        {
            Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
            Assert.Equal("https://example.idunno.blue/oauth-client-metadata.json", response.Headers.Location?.ToString());
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("max-age=", Assert.Single(response.Headers.GetValues("Strict-Transport-Security")), StringComparison.Ordinal);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Theory]
    [InlineData("ref:opaque", true)]
    [InlineData("atproto", false)]
    public void RequestedPermissionsAreNotMistakenForExplicitGrants(string granted, bool canUpdate)
    {
        var tokenCredentials = Credentials(granted);
        var credentials = new DPoPAccessCredentials(
            tokenCredentials.Service, tokenCredentials.AccessJwt, tokenCredentials.RefreshToken,
            tokenCredentials.DPoPProofKey, tokenCredentials.DPoPNonce)
        {
            RequestedScope = string.Join(' ', ProfilePermissions.MaximumScopes)
        };
        Assert.Equal(canUpdate, ProfilePermissions.CanUpdate(credentials));
        Assert.Equal(ProfilePermissions.MaximumScopes, ProfilePermissions.ExpandedScopes(credentials));
    }

    [Fact]
    public void PendingConsentIsBoundToAccountAndSessionAndIsSingleUse()
    {
        var clock = new FakeTimeProvider();
        using var store = new ProfileEditStore(new EphemeralDataProtectionProvider(), clock);
        ProfileEditOwner owner = new(DidValue, "first-session");
        string id = store.Add(owner, Edit);
        Assert.Null(store.Get(new(OtherDid, owner.Session)));
        Assert.Null(store.Get(new(owner.Did, "second-session")));
        Assert.False(store.TakeConsent(new(OtherDid, owner.Session), id));
        Assert.False(store.TakeConsent(new(owner.Did, "second-session"), id));
        Assert.False(store.TakeConsent(owner, "tampered"));
        Assert.True(store.TakeConsent(owner, id));
        Assert.False(store.TakeConsent(owner, id));
        Assert.Null(store.TakeReady(owner, id));
        Assert.True(store.FinishConsent(owner, id, authorized: true));
        Assert.Equal(Edit, store.TakeReady(owner, id));
        Assert.Null(store.TakeReady(owner, id));
        Assert.Equal(Edit, store.Get(owner)?.Edit);
        store.Remove(owner, id);
        Assert.Null(store.Get(owner));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackCannotRestoreSessionDuringRealLogoutOrLoginReplacement(bool replacementLogin)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        string editor = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        using var pending = await PostEdit(browser);
        string callbackUrl = pending.Headers.Location!.ToString();
        string? replacementCallback = null;
        if (replacementLogin)
        {
            using var prepared = await browser.GetAsync("/test/prepare?returnUrl=%2FManage", TestContext.Current.CancellationToken);
            replacementCallback = prepared.Headers.Location!.ToString();
        }
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.SessionChangeEntered = entered;
        factory.ReleaseSessionChange = release;
        Task<HttpResponseMessage> changingSession = replacementLogin
            ? browser.GetAsync(replacementCallback, TestContext.Current.CancellationToken)
            : Post(browser, "/Bluesky/Logout", new()
            {
                ["__RequestVerificationToken"] = HiddenValue(editor, "__RequestVerificationToken")
            });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            using var callback = await browser.GetAsync(callbackUrl, TestContext.Current.CancellationToken);
            Assert.Equal("/Manage", callback.Headers.Location?.ToString());
            Assert.False(callback.Headers.TryGetValues("Set-Cookie", out var cookies) &&
                cookies.Any(cookie => cookie.StartsWith(".AspNetCore.Bluesky.Progressive=", StringComparison.Ordinal)));
            Assert.Equal(replacementLogin ? 1 : 0, factory.Trace.Authorizations);
            Assert.Equal(0, factory.Pds.Writes);
        }
        finally
        {
            release.TrySetResult();
            using var changed = await changingSession;
            Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        }

        if (replacementLogin)
        {
            Assert.Equal(ProfilePermissions.ReadScopes, ProfilePermissions.EffectiveScopes(await StoredCredentials(factory)));
        }
        else
        {
            var store = factory.Services.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>()
                .Get(BlueskyAuthenticationDefaults.AuthenticationScheme).IdentityStore!;
            Assert.Null(await store.GetIdentity(new Did(DidValue), TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task SessionInvalidationWaitsForAnInFlightCredentialCommit()
    {
        using var store = new ProfileEditStore(new EphemeralDataProtectionProvider(), new FakeTimeProvider());
        ProfileEditOwner owner = new(DidValue, "session");
        string id = store.Add(owner, Edit);
        Assert.True(store.TakeConsent(owner, id));
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool credentialsPersisted = false;
        Task<bool> commit = store.CommitConsent(owner, id, async () =>
        {
            entered.SetResult();
            await release.Task;
            credentialsPersisted = true;
        }, TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        Task invalidation = store.Invalidate(owner);
        Assert.False(invalidation.IsCompleted);
        Assert.False(credentialsPersisted);
        release.SetResult();
        Assert.True(await commit);
        await invalidation;
        Assert.True(credentialsPersisted);
        Assert.Null(store.Get(owner));
        Assert.Null(store.TakeReady(owner, id));
    }

    [Fact]
    public async Task SessionInvalidatedBeforeCredentialCommitCannotBeSignedInAgain()
    {
        using var store = new ProfileEditStore(new EphemeralDataProtectionProvider(), new FakeTimeProvider());
        ProfileEditOwner owner = new(DidValue, "session");
        string id = store.Add(owner, Edit);
        Assert.True(store.TakeConsent(owner, id));
        await store.Invalidate(owner);
        bool signedIn = false;

        Assert.False(await store.CommitConsent(owner, id, () =>
        {
            signedIn = true;
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken));
        Assert.False(signedIn);
        Assert.Null(store.Get(owner));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingEditExpiresWithoutExtendingLifetimeDuringConsent(bool consentStarted)
    {
        var clock = new FakeTimeProvider();
        using var store = new ProfileEditStore(new EphemeralDataProtectionProvider(), clock);
        ProfileEditOwner owner = new(DidValue, "session");
        string id = store.Add(owner, Edit);
        clock.Advance(TimeSpan.FromMinutes(9));
        if (consentStarted)
        {
            Assert.True(store.TakeConsent(owner, id));
        }

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Null(store.Get(owner));
        Assert.False(store.TakeConsent(owner, id));
        Assert.False(store.FinishConsent(owner, id, authorized: true));
        Assert.Null(store.TakeReady(owner, id));
    }

    [Fact]
    public async Task ReadOnlySaveChallengesAndConsentCompletesExactlyOneProtectedSave()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new("http://127.0.0.1:5251") });
        await Login(browser);
        using var save = await PostEdit(browser);
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Equal(0, factory.Pds.Writes);
        Assert.Equal(ProfilePermissions.MaximumScopes, factory.OAuth!.RequestedScopes);
        string callback = save.Headers.Location!.ToString();

        using var consent = await browser.GetAsync(callback + "&returnUrl=https://evil.example/", TestContext.Current.CancellationToken);
        Assert.Equal("/Manage", consent.Headers.Location?.ToString());
        Assert.Equal(DidValue, factory.OAuth.ExpectedDid?.Value);
        Assert.Equal(0, factory.Pds.Writes);
        string html = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        Assert.Contains("complete-profile-save", html, StringComparison.Ordinal);
        string id = HiddenValue(html, "completionId");
        string token = HiddenValue(html, "__RequestVerificationToken");
        using var completed = await Post(browser, "/Manage?handler=Complete", new()
        {
            ["completionId"] = id, ["__RequestVerificationToken"] = token,
            ["DisplayName"] = "Tampered", ["cid"] = "tampered"
        });
        Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);
        Assert.Equal(1, factory.Pds.Writes);
        using var payload = JsonDocument.Parse(factory.Pds.LastWrite!);
        Assert.Equal(ProfileCid, payload.RootElement.GetProperty("swapRecord").GetString());
        Assert.Equal("app.bsky.actor.profile", payload.RootElement.GetProperty("collection").GetString());
        Assert.Equal("self", payload.RootElement.GetProperty("rkey").GetString());
        Assert.Equal(Edit.DisplayName, payload.RootElement.GetProperty("record").GetProperty("displayName").GetString());
        Assert.Equal(Edit.Description, payload.RootElement.GetProperty("record").GetProperty("description").GetString());
        Assert.Equal(Edit.Pronouns, payload.RootElement.GetProperty("record").GetProperty("pronouns").GetString());

        using var replay = await Post(browser, "/Manage?handler=Complete", new()
        {
            ["completionId"] = id, ["__RequestVerificationToken"] = token
        });
        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        using var callbackReplay = await browser.GetAsync(callback, TestContext.Current.CancellationToken);
        Assert.Equal(1, factory.Pds.Writes);
        Assert.Equal(1, factory.OAuth.Authorizations);
    }

    [Fact]
    public async Task ExistingUpdateGrantSavesWithoutAnotherConsentRequest()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser, write: true);
        using var save = await PostEdit(browser);
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Equal(1, factory.Pds.Writes);
        Assert.Null(factory.OAuth);
        string html = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        Assert.Contains("Your profile was saved.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateOnlySessionRequestsCreatePermissionBeforePutRecord()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var login = await browser.GetAsync($"/test/login?did={DidValue}&updateOnly=True", TestContext.Current.CancellationToken);
        using var save = await PostEdit(browser);
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Equal(ProfilePermissions.MaximumScopes, factory.OAuth!.RequestedScopes);
        Assert.Equal(0, factory.Pds.Writes);
        using var callback = await browser.GetAsync(save.Headers.Location, TestContext.Current.CancellationToken);
        string html = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        using var complete = await Post(browser, "/Manage?handler=Complete", new()
        {
            ["completionId"] = HiddenValue(html, "completionId"),
            ["__RequestVerificationToken"] = HiddenValue(html, "__RequestVerificationToken")
        });
        Assert.Equal(HttpStatusCode.Redirect, complete.StatusCode);
        Assert.Equal(1, factory.Pds.Writes);
    }

    [Theory]
    [InlineData("deny")]
    [InlineData("error")]
    [InlineData("mismatch")]
    [InlineData("missing-write")]
    [InlineData("missing-create")]
    [InlineData("missing-reads")]
    public async Task FailedConsentPreservesEditsAndDoesNotReplaceCredentialsOrSave(string outcome)
    {
        await using var factory = new SampleFactory { Outcome = outcome };
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        using var save = await PostEdit(browser);
        using var callback = await browser.GetAsync(save.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal("/Manage", callback.Headers.Location?.ToString());
        string html = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        Assert.Contains(Edit.DisplayName, html, StringComparison.Ordinal);
        Assert.Contains("not been saved", html, StringComparison.Ordinal);
        Assert.DoesNotContain("complete-profile-save", html, StringComparison.Ordinal);
        Assert.Equal(0, factory.Pds.Writes);
        Assert.False(ProfilePermissions.CanUpdate(await StoredCredentials(factory)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AccountOrSessionSwitchInvalidatesPendingConsent(bool sameAccount)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        using var save = await PostEdit(browser);
        await Login(browser, sameAccount ? DidValue : OtherDid);
        using var callback = await browser.GetAsync(save.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(0, factory.Pds.Writes);
        Assert.Equal(0, factory.OAuth!.Authorizations);
    }

    [Fact]
    public async Task BrowserCancellationKeepsDraftAndNewAttemptSupersedesOldCallback()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        using var first = await PostEdit(browser);
        string html = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        Assert.Contains(Edit.DisplayName, html, StringComparison.Ordinal);
        Assert.Contains("not been saved", html, StringComparison.Ordinal);
        using var second = await PostEdit(browser);
        using var stale = await browser.GetAsync(first.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(0, factory.OAuth!.Authorizations);
        using var current = await browser.GetAsync(second.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(1, factory.OAuth.Authorizations);
        Assert.Equal(0, factory.Pds.Writes);
    }

    [Fact]
    public async Task ExpiredConsentCannotSaveAndReportsLostDraft()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        using var save = await PostEdit(browser);
        factory.Clock.Advance(TimeSpan.FromMinutes(10));
        using var callback = await browser.GetAsync(save.Headers.Location, TestContext.Current.CancellationToken);
        string html = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        Assert.Contains("expired", html, StringComparison.Ordinal);
        Assert.DoesNotContain(Edit.DisplayName, html, StringComparison.Ordinal);
        Assert.Equal(0, factory.OAuth!.Authorizations);
        Assert.Equal(0, factory.Pds.Writes);
    }

    [Fact]
    public async Task SignOutInvalidatesConsentEvenIfTheOldAuthenticationCookieIsReplayed()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var login = await browser.GetAsync($"/test/login?did={DidValue}&write=False", TestContext.Current.CancellationToken);
        string oldCookie = string.Join("; ", login.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';')[0]));
        using var save = await PostEdit(browser);
        string correlationCookies = string.Join("; ", save.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';')[0]));
        string html = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        using var logout = await Post(browser, "/Bluesky/Logout", new()
        {
            ["__RequestVerificationToken"] = HiddenValue(html, "__RequestVerificationToken")
        });
        await Login(browser);
        using var replayBrowser = factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, save.Headers.Location);
        request.Headers.Add("Cookie", oldCookie + "; " + correlationCookies);
        using var callback = await replayBrowser.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(0, factory.OAuth!.Authorizations);
        Assert.Equal(0, factory.Pds.Writes);
    }

    [Fact]
    public async Task DiscardIsBoundToTheCurrentDraftAndReloadsTheLatestProfile()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        using var save = await PostEdit(browser);
        string html = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        string id = HiddenValue(html, "pendingId");
        using var tampered = await Post(browser, "/Manage?handler=Discard", new()
        {
            ["pendingId"] = "tampered", ["__RequestVerificationToken"] = HiddenValue(html, "__RequestVerificationToken")
        });
        string unchanged = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        Assert.Contains(Edit.DisplayName, unchanged, StringComparison.Ordinal);
        using var discarded = await Post(browser, "/Manage?handler=Discard", new()
        {
            ["pendingId"] = id, ["__RequestVerificationToken"] = HiddenValue(unchanged, "__RequestVerificationToken")
        });
        string reloaded = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        Assert.Contains("Original description", reloaded, StringComparison.Ordinal);
        Assert.DoesNotContain(Edit.DisplayName, reloaded, StringComparison.Ordinal);
        using var callback = await browser.GetAsync(save.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(0, factory.OAuth!.Authorizations);
        Assert.Equal(0, factory.Pds.Writes);
    }

    [Fact]
    public async Task CompletionCannotUseAnotherBrowsersConsentOrATamperedIdentifier()
    {
        await using var factory = new SampleFactory();
        using var first = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var second = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(first);
        await Login(second);
        using var save = await PostEdit(first);
        using var consent = await first.GetAsync(save.Headers.Location, TestContext.Current.CancellationToken);
        string authorized = await first.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        string id = HiddenValue(authorized, "completionId");
        string otherHtml = await second.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        using var crossBrowser = await Post(second, "/Manage?handler=Complete", new()
        {
            ["completionId"] = id, ["__RequestVerificationToken"] = HiddenValue(otherHtml, "__RequestVerificationToken")
        });
        using var tampered = await Post(first, "/Manage?handler=Complete", new()
        {
            ["completionId"] = id + "0", ["__RequestVerificationToken"] = HiddenValue(authorized, "__RequestVerificationToken")
        });
        Assert.Equal(0, factory.Pds.Writes);
        using var legitimate = await Post(first, "/Manage?handler=Complete", new()
        {
            ["completionId"] = id, ["__RequestVerificationToken"] = HiddenValue(authorized, "__RequestVerificationToken")
        });
        Assert.Equal(1, factory.Pds.Writes);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("conflict")]
    [InlineData("transport")]
    public async Task FailedAuthorizedSaveRetainsDraftButDoesNotRetryOrRepeatConsent(string failure)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        using var save = await PostEdit(browser);
        using var callback = await browser.GetAsync(save.Headers.Location, TestContext.Current.CancellationToken);
        string html = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        string id = HiddenValue(html, "completionId");
        factory.Pds.Failure = failure;
        var fields = new Dictionary<string, string>
        {
            ["completionId"] = id, ["__RequestVerificationToken"] = HiddenValue(html, "__RequestVerificationToken")
        };
        using var failed = await Post(browser, "/Manage?handler=Complete", fields);
        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        string failedHtml = await failed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(Edit.DisplayName, failedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Your profile was saved.", failedHtml, StringComparison.Ordinal);
        Assert.Equal(1, factory.OAuth!.Authorizations);
        Assert.Equal(failure == "conflict" ? 0 : 1, factory.Pds.Writes);
        using var replay = await Post(browser, "/Manage?handler=Complete", fields);
        Assert.Equal(failure == "conflict" ? 0 : 1, factory.Pds.Writes);
        string recovered = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        Assert.Contains(Edit.DisplayName, recovered, StringComparison.Ordinal);
        Assert.DoesNotContain("complete-profile-save", recovered, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?handler=Complete")]
    [InlineData("?handler=Discard")]
    public async Task SaveAndConsentCompletionRequireAntiforgery(string query)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        using var result = await Post(browser, "/Manage" + query, new()
        {
            ["DisplayName"] = Edit.DisplayName, ["Description"] = Edit.Description, ["Pronouns"] = Edit.Pronouns,
            ["completionId"] = "tampered"
        });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(0, factory.Pds.Writes);
        Assert.Null(factory.OAuth);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("conflict")]
    [InlineData("http")]
    [InlineData("transport")]
    public async Task WriteFailurePreservesDraftAndOnlyMissingScopeChallenges(string failure)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser, write: true);
        factory.Pds.Failure = failure;
        using var result = await PostEdit(browser);
        if (failure == "scope")
        {
            Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
            Assert.Equal(ProfilePermissions.MaximumScopes, factory.OAuth!.RequestedScopes);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Contains(Edit.DisplayName, await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
            Assert.Null(factory.OAuth);
        }

        string recovered = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        Assert.Contains(Edit.DisplayName, recovered, StringComparison.Ordinal);
        Assert.DoesNotContain("Your profile was saved.", recovered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidEditKeepsCidAndDoesNotStartConsent()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        using var result = await PostEdit(browser, displayName: new string('x', 65));
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        string html = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(ProfileCid, html, StringComparison.Ordinal);
        Assert.Null(factory.OAuth);
        Assert.Equal(0, factory.Pds.Writes);
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("/Manage")]
    public async Task InitialCallbackUsesOnlyTrustedLocalReturnRoute(string storedRoute)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var prepared = await browser.GetAsync("/test/prepare?returnUrl=" + Uri.EscapeDataString(storedRoute), TestContext.Current.CancellationToken);
        using var callback = await browser.GetAsync(prepared.Headers.Location + "&returnUrl=https://evil.example/", TestContext.Current.CancellationToken);
        Assert.Equal(storedRoute == "/Manage" ? "/Manage" : "/", callback.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackFailureIsDisplayedOnlyOnItsDestinationPage(bool authenticated)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (authenticated)
        {
            await Login(browser);
        }

        using var callback = await browser.GetAsync("/Bluesky/Callback?state=missing", TestContext.Current.CancellationToken);
        string destination = authenticated ? "/Manage" : "/Bluesky/Login";
        Assert.Equal(destination, callback.Headers.Location?.ToString());
        string html = await browser.GetStringAsync(destination, TestContext.Current.CancellationToken);
        Assert.Contains("The authorization response expired or was already used.", html, StringComparison.Ordinal);
        if (authenticated)
        {
            using var signout = await browser.GetAsync("/test/signout", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, signout.StatusCode);
        }

        string login = await browser.GetStringAsync("/Bluesky/Login", TestContext.Current.CancellationToken);
        Assert.DoesNotContain("The authorization response expired or was already used.", login, StringComparison.Ordinal);
    }

    private static async Task<DPoPAccessCredentials> StoredCredentials(SampleFactory factory)
    {
        var store = factory.Services.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>()
            .Get(BlueskyAuthenticationDefaults.AuthenticationScheme).IdentityStore!;
        var identity = await store.GetIdentity(new Did(DidValue), TestContext.Current.CancellationToken);
        Assert.True(AtProtoCredential.TryCreate(identity!, out DPoPAccessCredentials? credentials));
        return Assert.IsType<DPoPAccessCredentials>(credentials);
    }

    private static async Task Login(HttpClient browser, string did = DidValue, bool write = false)
    {
        using var response = await browser.GetAsync($"/test/login?did={did}&write={write}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostEdit(HttpClient browser, string? displayName = null)
    {
        string html = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        return await Post(browser, "/Manage?cid=" + ProfileCid, new()
        {
            ["DisplayName"] = displayName ?? Edit.DisplayName, ["Description"] = Edit.Description, ["Pronouns"] = Edit.Pronouns,
            ["__RequestVerificationToken"] = HiddenValue(html, "__RequestVerificationToken")
        });
    }

    private static Task<HttpResponseMessage> Post(HttpClient browser, string path, Dictionary<string, string> fields) =>
        browser.PostAsync(path, new FormUrlEncodedContent(fields), TestContext.Current.CancellationToken);

    private static string HiddenValue(string html, string name) =>
        WebUtility.HtmlDecode(HiddenInput().Matches(html).First(match => match.Groups[1].Value == name).Groups[2].Value);

    [GeneratedRegex("name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"")]
    private static partial Regex HiddenInput();

    private static DPoPAccessCredentials Credentials(string scope, string did = DidValue)
    {
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $$"""{"sub":"{{did}}","exp":4102444800,"scope":"{{scope}}"}""")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new(new Uri("https://pds.example"), "eyJhbGciOiJub25lIn0." + payload + ".",
            "refresh", JsonWebKeys.CreateRsaJson(), "nonce") { RequestedScope = scope };
    }

    private sealed class SampleFactory : WebApplicationFactory<Program>
    {
        internal FakePds Pds { get; } = new();
        internal TaskCompletionSource? SessionChangeEntered { get; set; }
        internal TaskCompletionSource? ReleaseSessionChange { get; set; }
        internal OAuthTrace? OAuth { get; private set; }
        internal OAuthTrace Trace => OAuth ??= new();
        internal string Outcome { get; init; } = "success";
        internal bool Production { get; init; }
        internal int ClaimsTransformerRegistrations { get; private set; }
        internal FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Production ? "Production" : "Development");
            builder.ConfigureServices(services =>
            {
                Pds.BeforeRevoke = async () =>
                {
                    if (SessionChangeEntered is not null && ReleaseSessionChange is not null)
                    {
                        SessionChangeEntered.TrySetResult();
                        await ReleaseSessionChange.Task;
                    }
                };
                services.PostConfigure<BlueskyAuthenticationOptions>(BlueskyAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Events.OnSigningIn = async context =>
                    {
                        if (context.HttpContext.Request.Path == "/Bluesky/Callback" &&
                            SessionChangeEntered is not null && ReleaseSessionChange is not null)
                        {
                            SessionChangeEntered.TrySetResult();
                            await ReleaseSessionChange.Task;
                        }
                    };
                });
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(Pds);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                ClaimsTransformerRegistrations = services.Count(descriptor =>
                    descriptor.ServiceType == typeof(IClaimsTransformation) &&
                    descriptor.ImplementationType == typeof(BlueskyClaimsTransformer));
                services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options => options.HttpsPort = 443);
                if (Production)
                {
                    services.PostConfigure<Microsoft.AspNetCore.HostFiltering.HostFilteringOptions>(options =>
                        options.AllowedHosts = ["example.idunno.blue"]);
                }
                services.RemoveAll<IClaimsTransformation>();
                services.AddSingleton<IClaimsTransformation, NoClaimsTransformation>();
                services.RemoveAll<ProfileOAuthClient>();
                services.AddScoped<ProfileOAuthClient>(provider => new FakeOAuth(
                    provider.GetRequiredService<BlueskySignInManager>(), Pds, this));
                services.AddSingleton<IStartupFilter, TestEndpoints>();
            });
        }
    }

    private sealed class NoClaimsTransformation : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal) => Task.FromResult(principal);
    }

    private sealed class TestEndpoints : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path == "/test/signout")
                {
                    var session = await context.AuthenticateAsync(BlueskyAuthenticationDefaults.AuthenticationScheme);
                    var store = context.RequestServices.GetRequiredService<ProfileEditStore>();
                    await store.ChangeSession(
                        Areas.Bluesky.Pages.CallbackModel.GetOwner(session),
                        () => context.SignOutAsync(BlueskyAuthenticationDefaults.AuthenticationScheme),
                        context.RequestAborted);
                    context.Response.StatusCode = 200;
                    return;
                }

                if (context.Request.Path == "/test/login")
                {
                    string did = context.Request.Query["did"].ToString();
                    bool write = context.Request.Query["write"] == "True";
                    string[] scopes = context.Request.Query["updateOnly"] == "True"
                        ? [.. ProfilePermissions.ReadScopes, ProfilePermissions.WriteScope]
                        : write ? ProfilePermissions.MaximumScopes : ProfilePermissions.ReadScopes;
                    var credentials = Credentials(string.Join(' ', scopes), did);
                    var identity = IIdentityStore.BuildClaimsIdentity(credentials);
                    identity.AddClaim(new(idunno.Bluesky.ClaimTypes.Handle, "test.bsky.social"));
                    var properties = new AuthenticationProperties { IsPersistent = true, AllowRefresh = true };
                    properties.Items[ProfilePermissions.SessionKey] = Guid.NewGuid().ToString("N");
                    var session = await context.AuthenticateAsync(BlueskyAuthenticationDefaults.AuthenticationScheme);
                    var store = context.RequestServices.GetRequiredService<ProfileEditStore>();
                    await store.ChangeSession(
                        Areas.Bluesky.Pages.CallbackModel.GetOwner(session),
                        () => context.SignInAsync(BlueskyAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties),
                        context.RequestAborted);
                    context.Response.StatusCode = 200;
                    return;
                }

                if (context.Request.Path == "/test/prepare")
                {
                    var manager = context.RequestServices.GetRequiredService<BlueskySignInManager>();
                    var state = State(new() { [Constants.ReturnUrlKey] = context.Request.Query["returnUrl"].ToString() });
                    await manager.SaveStateAndCreateCorrelationCookie(state, markCookieAsSecure: false);
                    context.Response.Redirect("/Bluesky/Callback?state=" + state.State + "&code=test&iss=https://auth.example");
                    return;
                }

                await nextMiddleware(context);
            });
            next(app);
        };
    }

    private static OAuthLoginState State(Dictionary<string, string> extra) =>
        new("", Guid.NewGuid().ToString("N"), "verifier", "http://127.0.0.1/Bluesky/Callback", "", "",
            "https://auth.example", "https://pds.example", "proof", Guid.NewGuid(), extra);

    private sealed class OAuthTrace
    {
        internal string[]? RequestedScopes { get; set; }
        internal Did? ExpectedDid { get; set; }
        internal int Authorizations { get; set; }
    }

    private sealed class FakeOAuth : ProfileOAuthClient
    {
        private readonly BlueskySignInManager _manager;
        private readonly SampleFactory _factory;

        internal FakeOAuth(BlueskySignInManager manager, IHttpClientFactory clients, SampleFactory factory) : base(manager, clients)
        {
            _manager = manager;
            _factory = factory;
        }

        internal override async Task<Uri> Challenge(Handle handle, DPoPAccessCredentials credentials, string editId, CancellationToken cancellationToken)
        {
            _factory.Trace.RequestedScopes = ProfilePermissions.ExpandedScopes(credentials);
            var state = State(new() { [ProfilePermissions.PendingEditKey] = editId });
            await _manager.SaveStateAndCreateCorrelationCookie(state, markCookieAsSecure: false);
            return new Uri("/Bluesky/Callback?state=" + state.State + "&code=test&iss=https://auth.example", UriKind.Relative);
        }

        internal override Task<DPoPAccessCredentials?> Authorize(OAuthLoginState state, string callbackData, Did? expectedDid, CancellationToken cancellationToken)
        {
            _factory.Trace.ExpectedDid = expectedDid;
            _factory.Trace.Authorizations++;
            return _factory.Outcome switch
            {
                "deny" => Task.FromResult<DPoPAccessCredentials?>(null),
                "error" => throw new OAuthException("Test authorization error"),
                "mismatch" => Task.FromResult<DPoPAccessCredentials?>(Credentials(string.Join(' ', ProfilePermissions.MaximumScopes), OtherDid)),
                "missing-write" => Task.FromResult<DPoPAccessCredentials?>(Credentials(string.Join(' ', ProfilePermissions.ReadScopes))),
                "missing-create" => Task.FromResult<DPoPAccessCredentials?>(Credentials(string.Join(' ', ProfilePermissions.ReadScopes.Append(ProfilePermissions.WriteScope)))),
                "missing-reads" => Task.FromResult<DPoPAccessCredentials?>(Credentials("atproto " + ProfilePermissions.WriteScope)),
                _ => Task.FromResult<DPoPAccessCredentials?>(Credentials(string.Join(' ', expectedDid is null
                    ? ProfilePermissions.ReadScopes : ProfilePermissions.MaximumScopes)))
            };
        }
    }

    private sealed class FakePds : IHttpClientFactory
    {
        internal int Writes { get; set; }
        internal string? LastWrite { get; set; }
        internal string? Failure { get; set; }
        internal Func<Task>? BeforeRevoke { get; set; }

        public HttpClient CreateClient(string name) => new(new Handler(this));

        private sealed class Handler(FakePds pds) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.RequestUri!.AbsolutePath == "/.well-known/oauth-protected-resource" && pds.BeforeRevoke is not null)
                {
                    // Pause the real handler's logout before revocation/identity removal, during authority discovery.
                    await pds.BeforeRevoke();
                }

                if (request.RequestUri!.AbsolutePath == "/xrpc/com.atproto.repo.getRecord")
                {
                    string cid = pds.Failure == "conflict" ? "bafyreig7t6q4x7zbibjnyxkzsde2ndvgc7jevdzicwagmg36nwcb6qhigy" : ProfileCid;
                    string did = QueryHelpers.ParseQuery(request.RequestUri.Query)["repo"].ToString();
                    return Json(HttpStatusCode.OK, $$$"""
                        {"uri":"at://{{{did}}}/app.bsky.actor.profile/self","cid":"{{{cid}}}","value":{"$type":"app.bsky.actor.profile","displayName":"Original","description":"Original description","pronouns":"she/her"}}
                        """);
                }

                if (request.RequestUri.AbsolutePath == "/xrpc/com.atproto.repo.putRecord")
                {
                    pds.Writes++;
                    pds.LastWrite = await request.Content!.ReadAsStringAsync(cancellationToken);
                    var token = new JsonWebToken(request.Headers.Authorization!.Parameter!);
                    string[] granted = token.GetClaim("scope").Value.Split(' ');
                    foreach (string required in ProfilePermissions.WriteScopes)
                    {
                        if (!granted.Contains(required, StringComparer.Ordinal))
                        {
                            return Json(HttpStatusCode.Forbidden,
                                $$"""{"error":"ScopeMissingError","message":"Missing required scope \"{{required}}\""}""");
                        }
                    }

                    return pds.Failure switch
                    {
                        "scope" => Json(HttpStatusCode.Forbidden, """{"error":"ScopeMissingError","message":"Missing required scope \"repo:app.bsky.actor.profile?action=create\""}"""),
                        "http" => Json(HttpStatusCode.InternalServerError, """{"error":"InternalServerError"}"""),
                        "transport" => throw new HttpRequestException("Test transport failure"),
                        _ => Json(HttpStatusCode.OK, $$"""{"uri":"at://{{DidValue}}/app.bsky.actor.profile/self","cid":"{{ProfileCid}}"}""")
                    };
                }

                if (request.RequestUri.AbsolutePath == "/oauth/revoke")
                {
                    if (pds.BeforeRevoke is not null)
                    {
                        await pds.BeforeRevoke();
                    }
                    return Json(HttpStatusCode.OK, "{}");
                }

                return Json(HttpStatusCode.NotFound, """{"error":"UnexpectedRequest"}""");
            }

            private static HttpResponseMessage Json(HttpStatusCode code, string json) =>
                new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
