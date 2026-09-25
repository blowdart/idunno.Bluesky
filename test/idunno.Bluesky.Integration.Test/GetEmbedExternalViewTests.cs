// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Embed;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

public class GetEmbedExternalViewTests
{
    private static AccessCredentials CreateCredentials()
    {
        Did did = "did:plc:test";

        return new AccessCredentials(
            service: TestServerBuilder.DefaultUri,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt(did, TestServerBuilder.DefaultUri.ToString()),
            refreshToken: "refreshToken");
    }

    private static TestServer CreateServer(string json)
    {
        return TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (context.Request.Path == "/xrpc/app.bsky.embed.getEmbedExternalView")
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(json);
            }
        });
    }

    [Fact]
    public async Task AnEmptyResponseIsReportedAsNoContent()
    {
        using TestServer testServer = CreateServer("{}");

        using BlueskyAgent agent = new(new TestHttpClientFactory(testServer))
        {
            Credentials = CreateCredentials()
        };

        AtProtoHttpResult<EmbeddedExternalView> result = await agent.GetEmbedExternalView(
            new Uri("https://example.org/article"),
            [new AtUri("at://did:plc:test/app.bsky.feed.post/abcdefghijklm")],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Null(result.Result);
        Assert.Null(result.AtErrorDetail);
    }

    [Fact]
    public async Task AResolvedViewIsReturned()
    {
        const string json = """
            {"view":{"external":{"uri":"https://example.org/article","title":"Title","description":"Description"}}}
            """;

        using TestServer testServer = CreateServer(json);

        using BlueskyAgent agent = new(new TestHttpClientFactory(testServer))
        {
            Credentials = CreateCredentials()
        };

        AtProtoHttpResult<EmbeddedExternalView> result = await agent.GetEmbedExternalView(
            new Uri("https://example.org/article"),
            [new AtUri("at://did:plc:test/app.bsky.feed.post/abcdefghijklm")],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("https://example.org/article", result.Result.External.Uri);
        Assert.Equal("Title", result.Result.External.Title);
        Assert.Equal("Description", result.Result.External.Description);
    }

    [Fact]
    public async Task MoreUrisThanTheMaximumAreRejected()
    {
        using TestServer testServer = CreateServer("{}");

        using BlueskyAgent agent = new(new TestHttpClientFactory(testServer))
        {
            Credentials = CreateCredentials()
        };

        AtUri[] uris =
        [
            .. Enumerable
                .Range(0, Maximum.EmbedExternalViewUris + 1)
                .Select(index => new AtUri($"at://did:plc:test/app.bsky.feed.post/abcdefghijkl{index}"))
        ];

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => agent.GetEmbedExternalView(
                new Uri("https://example.org/article"),
                uris,
                cancellationToken: TestContext.Current.CancellationToken));
    }
}
