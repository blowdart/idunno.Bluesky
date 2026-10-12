// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

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
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer))
        {
            Service = TestServerBuilder.DefaultUri
        };

        if (authenticated)
        {
            agent.Credentials = CreateCredentials();
        }

        return agent;
    }

    [Fact]
    public async Task GetAgeAssuranceConfigCallsTheLexiconEndpoint()
    {
        string? path = null;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                path = context.Request.Path;
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("""{"regions":[]}""", TestContext.Current.CancellationToken);
            });
        using BlueskyAgent agent = CreateAgent(testServer, authenticated: false);

        AtProtoHttpResult<Config> result = await agent.GetAgeAssuranceConfig(TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("/xrpc/app.bsky.ageassurance.getConfig", path);
        Assert.Empty(result.Result.Regions);
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

        AtProtoHttpResult<string> result = await agent.ExportChatAccountData(TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("/xrpc/chat.bsky.actor.exportAccountData", path);
        Assert.Equal(export, result.Result);
    }
}
