// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class UnknownMetadataTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetPostRecordAcceptsUnknownDollarProperties(bool useAgent)
    {
        AtUri uri = new("at://did:plc:heztswuhu24kudwdaz7y4bi3/app.bsky.feed.post/3mxqswzprj42r");
        const string cid = "bafyreififokk7ii62nxnsoldrnm33bro62a2k36xp5fhfhgptuanobuwuq";
        string json = $$"""
            {"uri":"{{uri}}","cid":"{{cid}}","value":{"$type":"app.bsky.feed.post","$via":"kazahana","text":"hello","createdAt":"2026-10-13T10:24:57.970Z","langs":["ja"]} }
            """;
        bool requested = false;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.repo.getRecord", context.Request.Path.Value);
            Assert.Equal(uri.Repo.Value, context.Request.Query["repo"]);
            Assert.Equal("app.bsky.feed.post", context.Request.Query["collection"]);
            Assert.Equal(uri.RecordKey!.Value, context.Request.Query["rkey"]);
            requested = true;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(json, TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = new(new TestHttpClientFactory(server),
            options: new BlueskyAgentOptions { PublicAppViewUri = TestServerBuilder.DefaultUri });

        AtProtoHttpResult<AtProtoRepositoryRecord<Post>> result = useAgent
            ? await agent.GetPostRecord(uri, cancellationToken: TestContext.Current.CancellationToken)
            : await BlueskyServer.GetPostRecord(uri, null, TestServerBuilder.DefaultUri, null, client,
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(requested);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Equal("hello", result.Result.Value.Text);
        Assert.Equal("kazahana", result.Result.Value.ExtensionData!["$via"].GetString());
    }
}
