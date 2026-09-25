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

    [Fact]
    public void AnOnResponseReceivedHandlerCanBeAttached()
    {
        Func<HttpResponseMessage, CancellationToken, Task> handler = (responseMessage, cancellationToken) => Task.CompletedTask;

        var client = new AtProtoHttpClient
        {
            MaximumResponseSize = 1024,
            OnResponseReceived = handler
        };

        Assert.Same(handler, client.OnResponseReceived);
        Assert.Equal(1024, client.MaximumResponseSize);
    }

    [Fact]
    public void AnOnSendingRequestHandlerCanBeAttached()
    {
        Func<HttpRequestMessage, CancellationToken, Task> handler = (requestMessage, cancellationToken) => Task.CompletedTask;

        var client = new AtProtoHttpClient
        {
            OnSendingRequest = handler
        };

        Assert.Same(handler, client.OnSendingRequest);
    }

    [Fact]
    public void ANullHandlerThrows()
    {
        var client = new AtProtoHttpClient();

        Assert.Throws<ArgumentNullException>(() => client.OnResponseReceived = null!);
        Assert.Throws<ArgumentNullException>(() => client.OnSendingRequest = null!);
    }

    [Fact]
    public async Task AnOnResponseReceivedHandlerIsGivenABufferedBody()
    {
        string? bodySeenByHandler = null;

        AtProtoHttpResult<string> result = await Get(
            responseBody: ValidRecord,
            maximumResponseSize: 4096,
            onResponseReceived: async (responseMessage, cancellationToken) =>
            {
                bodySeenByHandler = await responseMessage.Content.ReadAsStringAsync(cancellationToken);
            });

        Assert.Equal(ValidRecord, bodySeenByHandler);

        // Reading the body in the handler must not stop the client reading it afterwards.
        Assert.True(result.Succeeded);
        Assert.Equal(ValidRecord, result.Result);
    }

    [Fact]
    public async Task AnOnResponseReceivedHandlerIsNotCalledForAnOverLargeResponse()
    {
        const int maximumResponseSize = 256;

        bool handlerCalled = false;

        AtProtoHttpResult<string> result = await Get(
            responseBody: "{\"padding\":\"" + new string(' ', maximumResponseSize * 4) + "\"}",
            maximumResponseSize: maximumResponseSize,
            onResponseReceived: (responseMessage, cancellationToken) =>
            {
                handlerCalled = true;
                return Task.CompletedTask;
            });

        Assert.False(handlerCalled);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
        Assert.NotNull(result.AtErrorDetail);
        Assert.Equal("ResponseTooLarge", result.AtErrorDetail.Error);
    }

    private static async Task<AtProtoHttpResult<string>> Get(
        string responseBody,
        int maximumResponseSize,
        Func<HttpResponseMessage, CancellationToken, Task> onResponseReceived)
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpResponse response = context.Response;
            response.ContentType = "application/json";
            await response.WriteAsync(responseBody, context.RequestAborted);
        });

        // Deliberately set the handler before MaximumResponseSize, as the client must not create its inner client
        // until the init accessor for MaximumResponseSize has run.
        var client = new AtProtoHttpClient
        {
            OnResponseReceived = onResponseReceived,
            MaximumResponseSize = maximumResponseSize
        };

        using (HttpClient httpClient = testServer.CreateClient())
        {
            return await client.Get(
                service: TestServerBuilder.DefaultUri,
                endpoint: $"/xrpc/com.atproto.repo.getRecord?repo={Repo}&collection={Collection}&rkey={RKey}",
                httpClient: httpClient,
                cancellationToken: TestContext.Current.CancellationToken);
        }
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
