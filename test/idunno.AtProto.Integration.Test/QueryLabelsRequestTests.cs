// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Labels;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class QueryLabelsRequestTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueryLabelsSeparatesAndEscapesRepeatedFilters(bool filterSources)
    {
        string[] patterns = ["at://did:plc:test/app.bsky.feed.post/*", "did:plc:other"];
        Did[] sources = [new("did:plc:first"), new("did:plc:second")];
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.label.queryLabels", context.Request.Path);
            Assert.Equal(patterns, context.Request.Query["uriPatterns"].ToArray());
            Assert.Equal(filterSources ? sources.Select(did => did.Value) : [], context.Request.Query["sources"].ToArray());
            Assert.Equal("250", context.Request.Query["limit"]);
            Assert.Equal("a cursor & more", context.Request.Query["cursor"]);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"labels":[],"cursor":"next"}""", TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();

        AtProtoHttpResult<PagedReadOnlyCollection<Label>> result = await AtProtoServer.QueryLabels(
            patterns, filterSources ? sources : null, 250, "a cursor & more",
            TestServerBuilder.DefaultUri, null, client, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("next", result.Result.Cursor);
    }

    [Theory]
    [InlineData(null, "first-page")]
    [InlineData("previous", "next-page")]
    [InlineData("previous", null)]
    public async Task QueryLabelsUsesTheResponseCursor(string? requestCursor, string? responseCursor)
    {
        string body = responseCursor is null ? """{"labels":[]}""" : $$"""{"labels":[],"cursor":"{{responseCursor}}"}""";
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.False(context.Request.Query.ContainsKey("sources"));
            Assert.False(context.Request.Query.ContainsKey("limit"));
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body, TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();

        AtProtoHttpResult<PagedReadOnlyCollection<Label>> result = await AtProtoServer.QueryLabels(
            ["*"], [], null, requestCursor, TestServerBuilder.DefaultUri, null, client,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(responseCursor, result.Result.Cursor);
    }
}
