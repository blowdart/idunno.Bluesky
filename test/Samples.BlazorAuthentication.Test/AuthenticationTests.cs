// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Duende.IdentityModel.OidcClient.DPoP;

using idunno.AtProto.Authentication;
using idunno.Bluesky;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Samples.BlazorAuthentication.Test;

public partial class AuthenticationTests
{
    private const string Did = "did:plc:ewvi7nxzyoun6zhxrhs64oiz";
    private const string ProfileCid = "bafyreie5cvv4hro5oz3x5i5m5agzojwzwczl7ddsxv2gm7vby7i2dqu2yi";
    private const string ChangedCid = "bafyreig7t6q4x7zbibjnyxkzsde2ndvgc7jevdzicwagmg36nwcb6qhigy";

    [Theory]
    [InlineData("/")]
    [InlineData("/Privacy")]
    [InlineData("/Bluesky/Login")]
    [InlineData("/Bluesky/Logout")]
    [InlineData("/site.css")]
    [InlineData("/Bluesky/css/bluesky-auth.css")]
    public async Task AnonymousPagesAndLocalAssetsAreAvailable(string path)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var response = await browser.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        if (response.Content.Headers.ContentType?.MediaType == "text/html")
        {
            Assert.Contains("Blazor Bluesky", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
    }

    [Theory]
    [InlineData("/Claims")]
    [InlineData("/Manage")]
    public async Task ProtectedPagesChallengeAnonymousRequests(string path)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var response = await browser.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Bluesky/Login", response.Headers.Location?.AbsolutePath);
        Assert.Equal(path, QueryHelpers.ParseQuery(response.Headers.Location!.Query)["ReturnUrl"].ToString());
    }

    [Fact]
    public void ConfigurationMatchesDeclaredLocalhostPermissions()
    {
        using var factory = new SampleFactory();
        var oauth = factory.Services.GetRequiredService<IOptions<BlueskyAgentOptions>>().Value.OAuthOptions!;
        Assert.Equal(new[] { "atproto", BlueskyOAuthPermissionSets.FullApp.ToString() }, oauth.GetRequestedScopes());
        var query = QueryHelpers.ParseQuery(new Uri(oauth.ClientId).Query);
        Assert.Equal(string.Join(' ', oauth.GetRequestedScopes()), query["scope"].ToString());
        Assert.NotNull(oauth.ReturnUri);
        Assert.Equal(oauth.ReturnUri.ToString(), query["redirect_uri"].ToString());
        var options = factory.Services.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>()
            .Get(BlueskyAuthenticationDefaults.AuthenticationScheme);
        Assert.Equal(".AspNetCore.Bluesky.Blazor", options.Cookie.Name);
        Assert.Equal(".AspNetCore.Bluesky.Blazor.Correlation", options.CorrelationCookie.Name);
        Assert.False(factory.Services.GetRequiredService<IOptions<BlueskyAgentOptions>>().Value.EnableBackgroundTokenRefresh);
    }

    [Fact]
    public async Task AuthenticatedTimelineAndClaimsUseTheRequestIdentity()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        string timeline = await browser.GetStringAsync("/", TestContext.Current.CancellationToken);
        Assert.Contains("Hello from the timeline", timeline, StringComparison.Ordinal);
        Assert.Contains("Manage profile", timeline, StringComparison.Ordinal);
        Assert.Contains("Sign out", timeline, StringComparison.Ordinal);
        Assert.Equal(1, factory.Pds.TimelineRequests);
        string claims = await browser.GetStringAsync("/Claims", TestContext.Current.CancellationToken);
        Assert.Contains(Did, claims, StringComparison.Ordinal);
        Assert.Contains("Access token expires on", claims, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-refresh-token", claims, StringComparison.Ordinal);
        Assert.DoesNotContain(AtProtoClaims.AccessToken, claims, StringComparison.Ordinal);
        Assert.DoesNotContain(AtProtoClaims.DPoPProof, claims, StringComparison.Ordinal);
        Assert.DoesNotContain(AtProtoClaims.DPoPNonce, claims, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("preferences")]
    [InlineData("timeline")]
    public async Task TimelineFailuresAreVisible(string failure)
    {
        await using var factory = new SampleFactory();
        factory.Pds.Failure = failure;
        using var browser = factory.CreateClient();
        await Login(browser);
        string html = await browser.GetStringAsync("/", TestContext.Current.CancellationToken);
        Assert.Contains("Could not load", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Your timeline is empty", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileFormSavesConditionallyAndPreservesOtherFields()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        string editor = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        Assert.Contains("Original name", editor, StringComparison.Ordinal);
        using var response = await PostEdit(browser, editor);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Manage?saved=true", response.Headers.Location?.PathAndQuery);
        Assert.Equal(1, factory.Pds.Writes);
        using var write = JsonDocument.Parse(factory.Pds.LastWrite!);
        Assert.Equal(ProfileCid, write.RootElement.GetProperty("swapRecord").GetString());
        Assert.Equal(Did, write.RootElement.GetProperty("repo").GetString());
        var record = write.RootElement.GetProperty("record");
        Assert.Equal("Edited name", record.GetProperty("displayName").GetString());
        Assert.Equal("Edited description", record.GetProperty("description").GetString());
        Assert.Equal("they/them", record.GetProperty("pronouns").GetString());
        Assert.Equal("!no-unauthenticated", record.GetProperty("labels").GetProperty("values")[0].GetProperty("val").GetString());
        string saved = await browser.GetStringAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Contains("Your profile was saved", saved, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Input.Pronouns", "123456789012345678901")]
    [InlineData("Input.Cid", "invalid")]
    [InlineData("Input.Cid", "")]
    public async Task InvalidFormValuesDoNotWrite(string field, string value)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient();
        await Login(browser);
        string editor = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        using var response = await PostEdit(browser, editor, field, value);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("validation", html, StringComparison.Ordinal);
        Assert.Equal(0, factory.Pds.Writes);
        Assert.Contains("Edited name", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyProfileFieldsCanBeCleared()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        string editor = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        using var response = await PostEdit(browser, editor, "Input.Pronouns", "");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var write = JsonDocument.Parse(factory.Pds.LastWrite!);
        Assert.Equal("", write.RootElement.GetProperty("record").GetProperty("pronouns").GetString());
    }

    [Theory]
    [InlineData("conflict", "Your profile changed", 0)]
    [InlineData("write", "Could not save your profile", 1)]
    [InlineData("profile", "Could not load your profile", 0)]
    public async Task FailedSavesRetainEditsAndDoNotClaimSuccess(string failure, string message, int writes)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        string editor = await browser.GetStringAsync("/Manage", TestContext.Current.CancellationToken);
        factory.Pds.Failure = failure;
        using var response = await PostEdit(browser, editor);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, html, StringComparison.Ordinal);
        Assert.Contains("Edited name", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Your profile was saved", html, StringComparison.Ordinal);
        Assert.Equal(writes, factory.Pds.Writes);
    }

    [Theory]
    [InlineData("/Manage")]
    [InlineData("/Bluesky/Logout")]
    public async Task StateChangingPostsRequireAntiforgery(string path)
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        using var response = await browser.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "profile",
            ["Input.Cid"] = ProfileCid,
            ["Input.DisplayName"] = "Tampered"
        }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Pds.Writes);
    }

    [Fact]
    public async Task LogoutRequiresPostAndRemovesAccessToProtectedPages()
    {
        await using var factory = new SampleFactory();
        using var browser = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Login(browser);
        string logout = await browser.GetStringAsync("/Bluesky/Logout", TestContext.Current.CancellationToken);
        using var stillSignedIn = await browser.GetAsync("/Claims", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stillSignedIn.StatusCode);
        using var posted = await browser.PostAsync("/Bluesky/Logout?returnUrl=%2F", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = HiddenValue(logout, "__RequestVerificationToken")
        }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
        using var signedOut = await browser.GetAsync("/Claims", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void DisplayNameUsesGraphemesNotUtf16Length(int length, bool valid)
    {
        var input = new ProfileInput { Cid = ProfileCid, DisplayName = string.Concat(Enumerable.Repeat("\U0001F600", length)) };
        Assert.Equal(valid, Validator.TryValidateObject(input, new(input), [], validateAllProperties: true));
    }

    [Fact]
    public void ProfileValidationChecksByteLimitsEvenForOneGrapheme()
    {
        var input = new ProfileInput { Cid = ProfileCid, Pronouns = "a" + new string('\u0301', 100) };
        List<ValidationResult> errors = [];
        Assert.False(Validator.TryValidateObject(input, new(input), errors, validateAllProperties: true));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(ProfileInput.Pronouns)));
    }

    private static async Task Login(HttpClient browser)
    {
        using var response = await browser.GetAsync("/test/login", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<HttpResponseMessage> PostEdit(HttpClient browser, string html, string? field = null, string? value = null)
    {
        Dictionary<string, string> fields = new()
        {
            ["_handler"] = HiddenValue(html, "_handler"),
            ["__RequestVerificationToken"] = HiddenValue(html, "__RequestVerificationToken"),
            ["Input.Cid"] = ProfileCid,
            ["Input.DisplayName"] = "Edited name",
            ["Input.Description"] = "Edited description",
            ["Input.Pronouns"] = "they/them"
        };
        if (field is not null)
        {
            fields[field] = value!;
        }

        return browser.PostAsync("/Manage", new FormUrlEncodedContent(fields), TestContext.Current.CancellationToken);
    }

    private static string HiddenValue(string html, string name) =>
        WebUtility.HtmlDecode(HiddenInput().Matches(html).First(match => match.Groups[1].Value == name).Groups[2].Value);

    [GeneratedRegex("name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"")]
    private static partial Regex HiddenInput();

    private sealed class SampleFactory : WebApplicationFactory<Program>
    {
        internal FakePds Pds { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(Pds);
                services.AddSingleton<IStartupFilter, TestLogin>();
            });
        }
    }

    private sealed class TestLogin : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path == "/test/login")
                {
                    string scope = "atproto " + BlueskyOAuthPermissionSets.FullApp.ToString();
                    string payload = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
                        $$"""{"sub":"{{Did}}","exp":4102444800,"scope":"{{scope}}"}"""));
                    DPoPAccessCredentials credentials = new(new Uri("https://pds.example"), "eyJhbGciOiJub25lIn0." + payload + ".",
                        "secret-refresh-token", JsonWebKeys.CreateRsaJson(), "secret-nonce");
                    var identity = IIdentityStore.BuildClaimsIdentity(credentials);
                    await context.SignInAsync(BlueskyAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    return;
                }

                await nextMiddleware(context);
            });
            next(app);
        };
    }

    private sealed class FakePds : IHttpClientFactory
    {
        internal string? Failure { get; set; }
        internal int Writes { get; set; }
        internal int TimelineRequests { get; set; }
        internal string? LastWrite { get; set; }

        public HttpClient CreateClient(string name) => new(new Handler(this));

        private sealed class Handler(FakePds pds) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string path = request.RequestUri!.AbsolutePath;
                if (path.StartsWith("/xrpc/", StringComparison.Ordinal))
                {
                    Assert.Equal("DPoP", request.Headers.Authorization?.Scheme);
                }

                switch (path)
                {
                    case "/xrpc/app.bsky.actor.getProfile":
                        return Json(HttpStatusCode.OK, $$"""{"did":"{{Did}}","handle":"test.bsky.social","displayName":"Test user"}""");
                    case "/xrpc/app.bsky.actor.getPreferences":
                        return pds.Failure == "preferences" ? Failure() : Json(HttpStatusCode.OK, """{"preferences":[]}""");
                    case "/xrpc/app.bsky.feed.getTimeline":
                        pds.TimelineRequests++;
                        return pds.Failure == "timeline" ? Failure() : Json(HttpStatusCode.OK, $$$"""
                            {"feed":[{"post":{"uri":"at://{{{Did}}}/app.bsky.feed.post/3kexample","cid":"{{{ProfileCid}}}",
                            "author":{"did":"{{{Did}}}","handle":"test.bsky.social","displayName":"Test user"},
                            "record":{"$type":"app.bsky.feed.post","text":"Hello from the timeline","createdAt":"2026-10-01T12:00:00Z"},
                            "indexedAt":"2026-10-01T12:00:00Z","replyCount":0,"likeCount":1,"repostCount":0}}]}
                            """);
                    case "/xrpc/com.atproto.repo.getRecord":
                        string cid = pds.Failure == "conflict" ? ChangedCid : ProfileCid;
                        return pds.Failure == "profile" ? Failure() : Json(HttpStatusCode.OK, $$$$"""
                            {"uri":"at://{{{{Did}}}}/app.bsky.actor.profile/self","cid":"{{{{cid}}}}",
                            "value":{"$type":"app.bsky.actor.profile","displayName":"Original name","description":"Original description","pronouns":"she/her",
                            "labels":{"$type":"com.atproto.label.defs#selfLabels","values":[{"val":"!no-unauthenticated"}]}}}
                            """);
                    case "/xrpc/com.atproto.repo.putRecord":
                        pds.Writes++;
                        pds.LastWrite = await request.Content!.ReadAsStringAsync(cancellationToken);
                        return pds.Failure == "write" ? Failure() : Json(HttpStatusCode.OK,
                            $$"""{"uri":"at://{{Did}}/app.bsky.actor.profile/self","cid":"{{ProfileCid}}"}""");
                    default:
                        return Json(HttpStatusCode.NotFound, """{"error":"NotFound"}""");
                }
            }

            private static HttpResponseMessage Failure() =>
                Json(HttpStatusCode.BadRequest, """{"error":"InvalidRequest","message":"Test failure"}""");

            private static HttpResponseMessage Json(HttpStatusCode code, string json) =>
                new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
