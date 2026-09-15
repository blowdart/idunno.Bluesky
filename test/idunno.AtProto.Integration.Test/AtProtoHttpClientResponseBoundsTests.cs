// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class AtProtoHttpClientResponseBoundsTests
{
    private const string Repo = "did:plc:identifier";
    private const string Collection = "test.idunno.lexiconType";
    private const string RKey = "rkey";

    private const string ValidRecord = """
        {
            "uri" : "at://did:plc:identifier/test.idunno.lexiconType/rkey",
            "cid" : "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
            "value" :
            {
                "testValue" : "test"
            }
        }
        """;

    [Fact]
    public void TheMaximumResponseSizeDefaultsToThirtyTwoMegabytes()
    {
        Assert.Equal(32 * 1024 * 1024, AtProtoHttpClient.DefaultMaximumResponseSize);
        Assert.Equal(AtProtoHttpClient.DefaultMaximumResponseSize, new AtProtoAgentOptions().MaximumResponseSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnInvalidMaximumResponseSizeThrows(int maximumResponseSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AtProtoAgentOptions { MaximumResponseSize = maximumResponseSize });
    }

    [Fact]
    public async Task AResponseLargerThanAConfiguredMaximumIsRejected()
    {
        const int maximumResponseSize = 256;

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpResponse response = context.Response;
            response.ContentType = "application/json";

            await response.WriteAsync("{\"padding\":\"" + new string(' ', maximumResponseSize * 4) + "\"}");
        });

        using (var agent = new AtProtoAgent(
            TestServerBuilder.DefaultUri,
            new TestHttpClientFactory(testServer),
            new AtProtoAgentOptions { MaximumResponseSize = maximumResponseSize }))
        {
            AtProtoHttpResult<AtProtoRepositoryRecord<TestRecord>> result = await agent.GetRecord<TestRecord>(
                repo: Repo,
                collection: Collection,
                rKey: RKey,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Null(result.Result);
            Assert.NotNull(result.AtErrorDetail);
            Assert.Equal("ResponseTooLarge", result.AtErrorDetail.Error);
        }
    }

    [Fact]
    public async Task AResponseLargerThanTheDefaultMaximumIsRejected()
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpResponse response = context.Response;
            response.ContentType = "application/json";

            byte[] chunk = new byte[64 * 1024];
            Array.Fill(chunk, (byte)' ');

            await response.WriteAsync("{\"padding\":\"");

            int chunks = (AtProtoHttpClient.DefaultMaximumResponseSize / chunk.Length) + 2;

            for (int i = 0; i < chunks && !context.RequestAborted.IsCancellationRequested; i++)
            {
                await context.Response.Body.WriteAsync(chunk, context.RequestAborted);
            }
        });

        using (var agent = new AtProtoAgent(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer)))
        {
            AtProtoHttpResult<AtProtoRepositoryRecord<TestRecord>> result = await agent.GetRecord<TestRecord>(
                repo: Repo,
                collection: Collection,
                rKey: RKey,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Null(result.Result);
            Assert.NotNull(result.AtErrorDetail);
            Assert.Equal("ResponseTooLarge", result.AtErrorDetail.Error);
        }
    }

    [Fact]
    public async Task AResponseWithinTheMaximumIsReturned()
    {
        AtProtoHttpResult<AtProtoRepositoryRecord<TestRecord>> result = await GetRecord();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Equal("test", result.Result.Value.TestValue);
    }

    private static async Task<AtProtoHttpResult<AtProtoRepositoryRecord<TestRecord>>> GetRecord()
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpResponse response = context.Response;
            response.ContentType = "application/json";
            await response.WriteAsync(ValidRecord);
        });

        using (var agent = new AtProtoAgent(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer)))
        {
            return await agent.GetRecord<TestRecord>(
                repo: Repo,
                collection: Collection,
                rKey: RKey,
                cancellationToken: TestContext.Current.CancellationToken);
        }
    }
}
