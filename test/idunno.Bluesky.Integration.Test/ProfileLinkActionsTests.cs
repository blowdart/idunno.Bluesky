// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class ProfileLinkActionsTests
{
    private const string ProfileCid = "bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i";
    private const string ExistingLinkCid = "bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une";
    private const string CreatedLinkCid = "bafyreigym5qkku4ar35whduera6iqp7gc6ut3kmxzsuavnjp5sagdfpzjq";
    private const string LinkRecordUri = "at://did:plc:test/app.bsky.actor.link/new-link";

    [Fact]
    public async Task GetProfileLinksReturnsHydratedLinksInDisplayOrder()
    {
        using TestServer server = Respond(
            """
            {
              "did":"did:plc:test",
              "handle":"test.invalid",
              "links":[
                {"uri":"at://did:plc:test/app.bsky.actor.link/blog","cid":"bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une","url":"https://example.com/","title":"Blog","labels":[]},
                {"uri":"at://did:plc:test/app.bsky.actor.link/github","cid":"bafyreigym5qkku4ar35whduera6iqp7gc6ut3kmxzsuavnjp5sagdfpzjq","url":"https://github.com/test/","title":"GitHub","labels":[]}
              ]
            }
            """,
            context =>
            {
                Assert.Equal("/xrpc/app.bsky.actor.getProfile", context.Request.Path);
                Assert.Equal("did:plc:test", context.Request.Query["actor"]);
            });
        using BlueskyAgent agent = CreateAgent(server);

        AtProtoHttpResult<IReadOnlyCollection<ProfileLinkView>> result =
            await agent.GetProfileLinks(TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(["Blog", "GitHub"], result.Result.Select(link => link.Title));
        Assert.Equal(
            ["https://example.com/", "https://github.com/test/"],
            result.Result.Select(link => link.Url.ToString()));
    }

    [Fact]
    public async Task AddProfileLinkCreatesLinkAndAppendsReferenceToProfile()
    {
        int requestNumber = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            switch (Interlocked.Increment(ref requestNumber))
            {
                case 1:
                    Assert.Equal("/xrpc/com.atproto.repo.getRecord", context.Request.Path);
                    Assert.Equal("did:plc:test", context.Request.Query["repo"]);
                    Assert.Equal("app.bsky.actor.profile", context.Request.Query["collection"]);
                    Assert.Equal("self", context.Request.Query["rkey"]);
                    await context.Response.WriteAsync(ProfileRecord(), TestContext.Current.CancellationToken);
                    break;

                case 2:
                    Assert.Equal("/xrpc/com.atproto.repo.createRecord", context.Request.Path);
                    using (JsonDocument request = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: TestContext.Current.CancellationToken))
                    {
                        Assert.Equal("did:plc:test", request.RootElement.GetProperty("repo").GetString());
                        Assert.Equal("app.bsky.actor.link", request.RootElement.GetProperty("collection").GetString());
                        Assert.Equal("https://new.example/", request.RootElement.GetProperty("record").GetProperty("url").GetString());
                    }

                    await context.Response.WriteAsync(
                        $$"""{"uri":"{{LinkRecordUri}}","cid":"{{CreatedLinkCid}}"}""",
                        TestContext.Current.CancellationToken);
                    break;

                case 3:
                    Assert.Equal("/xrpc/com.atproto.repo.putRecord", context.Request.Path);
                    using (JsonDocument request = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: TestContext.Current.CancellationToken))
                    {
                        Assert.Equal(ProfileCid, request.RootElement.GetProperty("swapRecord").GetString());
                        JsonElement links = request.RootElement.GetProperty("record").GetProperty("links");
                        Assert.Equal(2, links.GetArrayLength());
                        Assert.Equal("at://did:plc:test/app.bsky.actor.link/existing", links[0].GetProperty("uri").GetString());
                        Assert.Equal(LinkRecordUri, links[1].GetProperty("uri").GetString());
                        Assert.Equal(CreatedLinkCid, links[1].GetProperty("cid").GetString());
                    }

                    await context.Response.WriteAsync(
                        $$$"""{"uri":"at://did:plc:test/app.bsky.actor.profile/self","cid":"{{{ProfileCid}}}"}""",
                        TestContext.Current.CancellationToken);
                    break;

                default:
                    throw new InvalidOperationException("Unexpected request.");
            }
        });
        using BlueskyAgent agent = CreateAgent(server);

        AtProtoHttpResult<CreateRecordResult> result = await agent.AddProfileLink(
            new Link(url: new Uri("https://new.example/"), title: "New", icon: null, labels: null),
            TestContext.Current.CancellationToken);

        Assert.Equal(3, requestNumber);
        Assert.True(result.Succeeded);
        Assert.Equal(LinkRecordUri, result.Result.Uri.ToString());
        Assert.Equal(CreatedLinkCid, result.Result.Cid.ToString());
    }

    [Fact]
    public async Task AddProfileLinkRejectsAnAlreadyFullProfileWithoutCreatingRecord()
    {
        string links = string.Join(
            ",",
            Enumerable.Range(0, 10).Select(index =>
                $$$"""{"uri":"at://did:plc:test/app.bsky.actor.link/link{{{index}}}","cid":"{{{ExistingLinkCid}}}"}"""));
        string profile = $$$"""
            {"uri":"at://did:plc:test/app.bsky.actor.profile/self","cid":"{{{ProfileCid}}}",
             "value":{"$type":"app.bsky.actor.profile","links":[{{{links}}}]}}
            """;
        using TestServer server = Respond(
            profile,
            context =>
            {
                Assert.Equal("/xrpc/com.atproto.repo.getRecord", context.Request.Path);
            });
        using BlueskyAgent agent = CreateAgent(server);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            agent.AddProfileLink(new Link(new Uri("https://new.example/")), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveProfileLinkRemovesReferenceThenDeletesItsRecord()
    {
        int requestNumber = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            switch (Interlocked.Increment(ref requestNumber))
            {
                case 1:
                    Assert.Equal("/xrpc/com.atproto.repo.getRecord", context.Request.Path);
                    await context.Response.WriteAsync(ProfileRecordWithDuplicateLinkReferences(), TestContext.Current.CancellationToken);
                    break;

                case 2:
                    Assert.Equal("/xrpc/com.atproto.repo.putRecord", context.Request.Path);
                    using (JsonDocument request = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: TestContext.Current.CancellationToken))
                    {
                        Assert.Equal(
                            [
                                "at://did:plc:test/app.bsky.actor.link/first",
                                "at://did:plc:test/app.bsky.actor.link/second",
                                "at://did:plc:test/app.bsky.actor.link/third"
                            ],
                            request.RootElement.GetProperty("record").GetProperty("links")
                                .EnumerateArray()
                                .Select(reference => reference.GetProperty("uri").GetString()));
                    }

                    await context.Response.WriteAsync(
                        $$$"""{"uri":"at://did:plc:test/app.bsky.actor.profile/self","cid":"{{{ProfileCid}}}"}""",
                        TestContext.Current.CancellationToken);
                    break;

                case 3:
                    Assert.Equal("/xrpc/com.atproto.repo.deleteRecord", context.Request.Path);
                    using (JsonDocument request = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: TestContext.Current.CancellationToken))
                    {
                        Assert.Equal("did:plc:test", request.RootElement.GetProperty("repo").GetString());
                        Assert.Equal("app.bsky.actor.link", request.RootElement.GetProperty("collection").GetString());
                        Assert.Equal("existing", request.RootElement.GetProperty("rkey").GetString());
                        Assert.Equal(ExistingLinkCid, request.RootElement.GetProperty("swapRecord").GetString());
                    }

                    await context.Response.WriteAsync("{}", TestContext.Current.CancellationToken);
                    break;

                default:
                    throw new InvalidOperationException("Unexpected request.");
            }
        });
        using BlueskyAgent agent = CreateAgent(server);
        ProfileLinkView link = new(
            uri: new AtUri("at://did:plc:test/app.bsky.actor.link/existing"),
            cid: new Cid(ExistingLinkCid),
            url: new Uri("https://example.com/"),
            title: "Existing",
            icon: null,
            labels: []);

        AtProtoHttpResult<DeleteResult> result =
            await agent.RemoveProfileLink(link, TestContext.Current.CancellationToken);

        Assert.Equal(3, requestNumber);
        Assert.True(result.Succeeded);
    }

    private static string ProfileRecord() => $$$"""
        {"uri":"at://did:plc:test/app.bsky.actor.profile/self","cid":"{{{ProfileCid}}}",
         "value":{"$type":"app.bsky.actor.profile",
                  "links":[{"uri":"at://did:plc:test/app.bsky.actor.link/existing","cid":"{{{ExistingLinkCid}}}"}]}}
        """;

    private static string ProfileRecordWithDuplicateLinkReferences() => $$$"""
        {"uri":"at://did:plc:test/app.bsky.actor.profile/self","cid":"{{{ProfileCid}}}",
         "value":{"$type":"app.bsky.actor.profile",
                  "links":[
                    {"uri":"at://did:plc:test/app.bsky.actor.link/first","cid":"{{{ExistingLinkCid}}}"},
                    {"uri":"at://did:plc:test/app.bsky.actor.link/existing","cid":"{{{ExistingLinkCid}}}"},
                    {"uri":"at://did:plc:test/app.bsky.actor.link/second","cid":"{{{ExistingLinkCid}}}"},
                    {"uri":"at://did:plc:test/app.bsky.actor.link/existing","cid":"{{{ExistingLinkCid}}}"},
                    {"uri":"at://did:plc:test/app.bsky.actor.link/third","cid":"{{{ExistingLinkCid}}}"}
                  ]}}
        """;

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
