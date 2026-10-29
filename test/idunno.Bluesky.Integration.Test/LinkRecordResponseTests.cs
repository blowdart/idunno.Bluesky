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
public class LinkRecordResponseTests
{
    [Theory]
    [InlineData("graphemes", false)]
    [InlineData("graphemes", true)]
    [InlineData("bytes", false)]
    [InlineData("bytes", true)]
    [InlineData("size", false)]
    [InlineData("size", true)]
    [InlineData("mime", false)]
    [InlineData("mime", true)]
    [InlineData("url", false)]
    [InlineData("url", true)]
    public async Task MalformedRemoteLinksUseStandardRepositoryResponseHandling(string invalidField, bool paged)
    {
        string property = invalidField switch
        {
            "graphemes" => $"""
                "title":"{new string('a', 41)}"
                """,
            "bytes" => $"""
                "title":"a{new string('\u0301', 160)}"
                """,
            "size" => """
                "icon":{"$type":"blob","ref":{"$link":"bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i"},"mimeType":"image/png","size":100001}
                """,
            "mime" => """
                "icon":{"$type":"blob","ref":{"$link":"bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i"},"mimeType":"image/svg+xml","size":100}
                """,
            "url" => """
                "url":null
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(invalidField))
        };
        string invalid = Record("invalid", property, invalidField != "url");
        string valid = Record("valid", """
            "title":"Valid","icon":{"$type":"blob","ref":{"$link":"bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i"},"mimeType":"image/png","size":100000}
            """);
        string body = paged ? $$"""{"records":[{{invalid}},{{valid}}],"cursor":"next-page"}""" : invalid;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            if (context.Request.Path == "/did:plc:test")
            {
                await context.Response.WriteAsync(
                    $$"""
                    {"id":"did:plc:test","service":[{"id":"#atproto_pds","type":"AtprotoPersonalDataServer","serviceEndpoint":"{{TestServerBuilder.DefaultUri}}"}]}
                    """,
                    TestContext.Current.CancellationToken);
                return;
            }

            Assert.Equal(paged ? "/xrpc/com.atproto.repo.listRecords" : "/xrpc/com.atproto.repo.getRecord", context.Request.Path);
            Assert.Equal("did:plc:test", context.Request.Query["repo"]);
            Assert.Equal("app.bsky.actor.link", context.Request.Query["collection"]);
            await context.Response.WriteAsync(body, TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = new(new TestHttpClientFactory(server))
        {
            Service = TestServerBuilder.DefaultUri,
            Credentials = new AccessCredentials(
                TestServerBuilder.DefaultUri,
                AuthenticationType.UsernamePassword,
                JwtBuilder.CreateJwt(new Did("did:plc:test"), TestServerBuilder.DefaultUri.ToString()),
                "refreshToken")
        };

        if (paged)
        {
            AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<Link>>> result = await agent.ListBlueskyRecords<Link>(
                new Did("did:plc:test"),
                new Nsid("app.bsky.actor.link"),
                service: TestServerBuilder.DefaultUri,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);
            Assert.Equal("next-page", result.Result.Cursor);
            Link link = Assert.Single(result.Result).Value;
            Assert.Equal("Valid", link.Title);
            Assert.Equal(100000, link.Icon?.Size);
        }
        else
        {
            AtProtoHttpResult<AtProtoRepositoryRecord<Link>> result = await agent.GetBlueskyRecord<Link>(
                new AtUri("at://did:plc:test/app.bsky.actor.link/invalid"),
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Null(result.Result);
            Assert.Equal("InvalidResponse", result.AtErrorDetail?.Error);
        }
    }

    private static string Record(string key, string property, bool includeUrl = true) => $$$"""
        {"uri":"at://did:plc:test/app.bsky.actor.link/{{{key}}}",
         "cid":"bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i",
         "value":{"$type":"app.bsky.actor.link","createdAt":"2026-10-15T00:00:00Z",
                  {{{(includeUrl ? "\"url\":\"https://example.com\"," : "")}}}{{{property}}}}}
        """;
}
