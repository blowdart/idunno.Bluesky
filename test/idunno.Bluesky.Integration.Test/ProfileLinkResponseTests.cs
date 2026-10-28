// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class ProfileLinkResponseTests
{
    private const string LinkJson = """
        {"uri":"at://did:plc:test/app.bsky.actor.link/test",
         "cid":"bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i",
         "url":"https://example.com","labels":[null]}
        """;

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task HydratedProfileReadsReturnInvalidResponseForMalformedLinkCollections(bool multiple, bool useAgent, bool nullLabel)
    {
        string profile = $$"""{"did":"did:plc:test","handle":"test.invalid","links":[{{(nullLabel ? LinkJson : "null")}}]}""";
        using TestServer server = Respond(multiple ? $$"""{"profiles":[{{profile}}]}""" : profile, context =>
        {
            Assert.Equal(multiple ? "/xrpc/app.bsky.actor.getProfiles" : "/xrpc/app.bsky.actor.getProfile", context.Request.Path);
            Assert.Equal("did:plc:test", context.Request.Query[multiple ? "actors" : "actor"]);
        });
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);
        CancellationToken token = TestContext.Current.CancellationToken;

        if (multiple)
        {
            AtProtoHttpResult<IReadOnlyCollection<ProfileViewDetailed>> result = useAgent
                ? await agent.GetProfiles([new Did("did:plc:test")], cancellationToken: token)
                : await BlueskyServer.GetProfiles([new Did("did:plc:test")], agent.Service, agent.Credentials, client, cancellationToken: token);
            Assert.False(result.Succeeded);
            Assert.Null(result.Result);
            Assert.Equal("InvalidResponse", result.AtErrorDetail?.Error);
        }
        else
        {
            AtProtoHttpResult<ProfileViewDetailed> result = useAgent
                ? await agent.GetProfile(new Did("did:plc:test"), cancellationToken: token)
                : await BlueskyServer.GetProfile(new Did("did:plc:test"), agent.Service, agent.Credentials, client, cancellationToken: token);
            Assert.False(result.Succeeded);
            Assert.Null(result.Result);
            Assert.Equal("InvalidResponse", result.AtErrorDetail?.Error);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    public async Task RepositoryProfileReadsReturnInvalidResponseForMalformedLinkCollections(int count)
    {
        string item = count == 1
            ? "null"
            : """
              {"uri":"at://did:plc:test/app.bsky.actor.link/test",
               "cid":"bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i"}
              """;
        string body = $$$"""
            {"uri":"at://did:plc:test/app.bsky.actor.profile/self",
             "cid":"bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i",
             "value":{"$type":"app.bsky.actor.profile","links":[{{{string.Join(",", Enumerable.Repeat(item, count))}}}]}}
            """;
        using TestServer server = Respond(body, context =>
        {
            Assert.Equal("/xrpc/com.atproto.repo.getRecord", context.Request.Path);
            Assert.Equal("did:plc:test", context.Request.Query["repo"]);
            Assert.Equal("app.bsky.actor.profile", context.Request.Query["collection"]);
            Assert.Equal("self", context.Request.Query["rkey"]);
        });
        using BlueskyAgent agent = CreateAgent(server);

        AtProtoHttpResult<AtProtoRepositoryRecord<Profile>> result = await agent.GetProfileRecord(TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
        Assert.Equal("InvalidResponse", result.AtErrorDetail?.Error);
    }

    private static TestServer Respond(string body, Action<HttpContext> inspect) =>
        TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            inspect(context);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body, TestContext.Current.CancellationToken);
        });

    private static BlueskyAgent CreateAgent(TestServer server) => new(new TestHttpClientFactory(server))
    {
        Service = TestServerBuilder.DefaultUri,
        Credentials = new AccessCredentials(
            TestServerBuilder.DefaultUri,
            AuthenticationType.UsernamePassword,
            JwtBuilder.CreateJwt(new Did("did:plc:test"), TestServerBuilder.DefaultUri.ToString()),
            "refreshToken")
    };
}
