// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using Duende.IdentityModel.OidcClient.DPoP;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AgeAssurance;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class AgeAssuranceAndChatAccountRequestTests
{
    private static readonly Did s_did = "did:plc:hfgp6pj3akhqxntgqwramlbg";

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    private static BlueskyAgent CreateAgent(TestServer testServer, bool authenticated = true)
    {
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer), new BlueskyAgentOptions { PublicAppViewUri = TestServerBuilder.DefaultUri })
        {
            Service = TestServerBuilder.DefaultUri
        };

        if (authenticated)
        {
            agent.Credentials = CreateCredentials();
        }

        return agent;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAgeAssuranceConfigCallsTheLexiconEndpoint(bool authenticated)
    {
        string? path = null;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                path = context.Request.Path;
                Assert.Equal(TestServerBuilder.DefaultUri.Host, context.Request.Host.Host);
                Assert.False(context.Request.Headers.ContainsKey("Authorization"));
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("""{"regions":[]}""", TestContext.Current.CancellationToken);
            });
        using BlueskyAgent agent = CreateAgent(testServer, authenticated);
        agent.Service = new Uri("https://pds.example.com");

        AtProtoHttpResult<Config> result = await agent.GetAgeAssuranceConfig(TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("/xrpc/app.bsky.ageassurance.getConfig", path);
        Assert.Empty(result.Result.Regions);
    }

    [Fact]
    public async Task GetAgeAssuranceConfigFiltersNullRegionsAndRules()
    {
        using TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            context.Response.Headers["X-Test"] = "preserved";
            await context.Response.WriteAsync(
                """
                {"regions":[null,
                  {"countryCode":"US","minAccessAge":13,"rules":[null,{"$type":"app.bsky.ageassurance.defs#configRegionRuleDefault","access":"full"},null]},
                  {"countryCode":"GB","minAccessAge":13,"rules":[null,null]},
                  null]}
                """, TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<Config> result = await agent.GetAgeAssuranceConfig(TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("preserved", Assert.Single(result.HttpResponseHeaders!.GetValues("X-Test")));
        Assert.Collection(result.Result.Regions,
            region =>
            {
                Assert.Equal("US", region.CountryCode);
                Assert.IsType<ConfigRegionRuleDefault>(Assert.Single(region.Rules));
            },
            region =>
            {
                Assert.Equal("GB", region.CountryCode);
                Assert.Empty(region.Rules);
            });
    }

    [Fact]
    public async Task GetAgeAssuranceConfigPreservesErrorResponses()
    {
        using TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"InvalidRequest","message":"Invalid configuration request."}""", TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<Config> result = await agent.GetAgeAssuranceConfig(TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("InvalidRequest", result.AtErrorDetail?.Error);
    }

    [Fact]
    public async Task GetAgeAssuranceStateEncodesCountryAndRegionCodes()
    {
        string? queryString = null;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                queryString = context.Request.QueryString.Value;
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    """{"state":{"status":"assured","access":"safe"},"metadata":{}}""",
                    TestContext.Current.CancellationToken);
            });
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<StateResult> result = await agent.GetAgeAssuranceStateForRegion(
            "US&x=y",
            "CA/West",
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("?countryCode=US%26x%3Dy&regionCode=CA%2FWest", queryString);
        Assert.Equal(AgeAssuranceAccess.Safe, result.Result.State.Access);
    }

    [Fact]
    public async Task DeleteChatAccountPostsToTheChatEndpoint()
    {
        string? path = null;
        string? proxy = null;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                path = context.Request.Path;
                proxy = context.Request.Headers["atproto-proxy"];
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{}", TestContext.Current.CancellationToken);
            });
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<EmptyResponse> result = await agent.DeleteChatAccount(TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("/xrpc/chat.bsky.actor.deleteAccount", path);
        Assert.Equal("did:web:api.bsky.chat#bsky_chat", proxy);
    }

    [Fact]
    public async Task ExportChatAccountDataPreservesNonceRetryAndCredentialCallbacks()
    {
        int requests = 0;
        int callbacks = 0;
        using TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal("DPoP", context.Request.Headers.Authorization.ToString().Split(' ')[0]);
            Assert.Equal("did:web:api.bsky.chat#bsky_chat", context.Request.Headers["atproto-proxy"]);
            if (Interlocked.Increment(ref requests) == 1)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.ContentType = "application/json";
                context.Response.Headers["DPoP-Nonce"] = "updatedNonce";
                await context.Response.WriteAsync("""{"error":"use_dpop_nonce"}""", TestContext.Current.CancellationToken);
            }
            else
            {
                context.Response.ContentType = "application/jsonl";
                await context.Response.WriteAsync("""{"type":"account"}""", TestContext.Current.CancellationToken);
            }
        });
        using HttpClient httpClient = testServer.CreateClient();
        DPoPAccessCredentials credentials = new(
            TestServerBuilder.DefaultUri,
            JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
            "refreshToken",
            JsonWebKeys.CreateRsaJson(),
            "initialNonce");

        AtProtoHttpResult<Stream> result = await BlueskyServer.ExportChatAccountData(
            TestServerBuilder.DefaultUri, credentials, httpClient,
            onCredentialsUpdated: (credential, _) =>
            {
                Assert.Equal("updatedNonce", Assert.IsType<DPoPAccessCredentials>(credential).DPoPNonce);
                callbacks++;
                return Task.CompletedTask;
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        await using Stream stream = result.Result;
        using StreamReader reader = new(stream);
        Assert.Equal("""{"type":"account"}""", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, requests);
        Assert.Equal(1, callbacks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamingResponseCallbacksRemainBounded(bool exceedsLimit)
    {
        using TrackingStream content = new(2);
        using HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StreamContent(content) };
        using ExportHandler handler = new(response);
        using HttpClient httpClient = new(handler);
        bool callbackInvoked = false;
        AtProtoHttpClient<Stream> client = new("did:web:api.bsky.chat#bsky_chat", loggerFactory: null)
        {
            MaximumResponseSize = exceedsLimit ? 1 : 2,
            OnResponseReceived = async (receivedResponse, cancellationToken) =>
            {
                callbackInvoked = true;
                Assert.Equal(2, (await receivedResponse.Content.ReadAsByteArrayAsync(cancellationToken)).Length);
            }
        };

        AtProtoHttpResult<Stream> result = await client.Get(
            TestServerBuilder.DefaultUri, "/xrpc/chat.bsky.actor.exportAccountData", CreateCredentials(), httpClient,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            cancellationToken: TestContext.Current.CancellationToken);

        if (exceedsLimit)
        {
            Assert.False(result.Succeeded);
            Assert.Equal("ResponseTooLarge", result.AtErrorDetail?.Error);
            Assert.False(callbackInvoked);
        }
        else
        {
            Assert.True(result.Succeeded);
            Assert.True(callbackInvoked);
            await using Stream stream = result.Result;
            using MemoryStream copy = new();
            await stream.CopyToAsync(copy, TestContext.Current.CancellationToken);
            Assert.Equal(2, copy.Length);
        }

        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task ExportChatAccountDataStreamsBeyondTheNormalLimitAndOwnsTheResponse()
    {
        using TrackingStream content = new(AtProtoHttpClient.DefaultMaximumResponseSize + 1);
        using HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StreamContent(content) };
        response.Content.Headers.ContentType = new("application/jsonl");
        using ExportHandler handler = new(response);
        using HttpClient httpClient = new(handler);

        AtProtoHttpResult<Stream> result = await BlueskyServer.ExportChatAccountData(
            TestServerBuilder.DefaultUri, CreateCredentials(), httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(0, content.Position);
        Assert.False(content.Disposed);
        Assert.Equal("did:web:api.bsky.chat#bsky_chat", handler.Proxy);
        Assert.StartsWith("Bearer ", handler.Authorization);
        await using (Stream stream = result.Result)
        {
            await stream.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken);
            Assert.Equal(AtProtoHttpClient.DefaultMaximumResponseSize + 1, content.Position);
            using CancellationTokenSource cancelled = new();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await stream.ReadExactlyAsync(new byte[1], cancelled.Token));
        }

        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task ExportChatAccountDataReturnsErrorsAndDisposesTheResponse()
    {
        using TrackingStream content = new(System.Text.Encoding.UTF8.GetBytes("""{"error":"AccountNotFound","message":"Missing account."}"""));
        using HttpResponseMessage response = new(HttpStatusCode.NotFound) { Content = new StreamContent(content) };
        response.Content.Headers.ContentType = new("application/json");
        using ExportHandler handler = new(response);
        using HttpClient httpClient = new(handler);

        AtProtoHttpResult<Stream> result = await BlueskyServer.ExportChatAccountData(
            TestServerBuilder.DefaultUri, CreateCredentials(), httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
        Assert.Equal("AccountNotFound", result.AtErrorDetail?.Error);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task ExportChatAccountDataReturnsJsonLinesFromTheChatEndpoint()
    {
        const string export = """{"type":"account"}""" + "\n";
        string? path = null;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                path = context.Request.Path;
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/jsonl";
                await context.Response.WriteAsync(export, TestContext.Current.CancellationToken);
            });
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<Stream> result = await agent.ExportChatAccountData(TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("/xrpc/chat.bsky.actor.exportAccountData", path);
        await using Stream stream = result.Result;
        using StreamReader reader = new(stream);
        Assert.Equal(export, await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public TrackingStream(int length) : this(new byte[length])
        {
        }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class ExportHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? Proxy { get; private set; }
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/xrpc/chat.bsky.actor.exportAccountData", request.RequestUri?.AbsolutePath);
            Proxy = Assert.Single(request.Headers.GetValues("atproto-proxy"));
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(response);
        }
    }
}
