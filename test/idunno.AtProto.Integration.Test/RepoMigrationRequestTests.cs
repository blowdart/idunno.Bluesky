// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class RepoMigrationRequestTests
{
    private static readonly Did s_did = "did:plc:hfgp6pj3akhqxntgqwramlbg";

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    [Fact]
    public async Task ListMissingBlobsSendsPaginationParametersAndReturnsBlobs()
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
                    """
                    {
                      "blobs": [
                        {
                          "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
                          "recordUri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3mei4u5pu6226"
                        }
                      ],
                      "cursor": "next page"
                    }
                    """,
                    TestContext.Current.CancellationToken);
            });
        using AtProtoAgent agent = new(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer))
        {
            Credentials = CreateCredentials()
        };

        AtProtoHttpResult<PagedReadOnlyCollection<MissingBlob>> result = await agent.ListMissingBlobs(
            limit: 20,
            cursor: "next page",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("?limit=20&cursor=next%20page", queryString);
        Assert.Equal("next page", result.Result.Cursor);
        Assert.Equal(new Cid("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"), Assert.Single(result.Result).Cid);
    }

    [Fact]
    public async Task ImportRepoPostsCarWithRequiredContentTypeAndContentLength()
    {
        const string car = "CAR";
        string? contentType = null;
        long? contentLength = null;
        string? body = null;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                contentType = context.Request.ContentType;
                contentLength = context.Request.ContentLength;
                using StreamReader reader = new(context.Request.Body);
                body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{}", TestContext.Current.CancellationToken);
            });
        using AtProtoAgent agent = new(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer))
        {
            Credentials = CreateCredentials()
        };

        AtProtoHttpResult<EmptyResponse> result = await agent.ImportRepo(
            System.Text.Encoding.UTF8.GetBytes(car),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("application/vnd.ipld.car", contentType);
        Assert.Equal(car.Length, contentLength);
        Assert.Equal(car, body);
    }
}
