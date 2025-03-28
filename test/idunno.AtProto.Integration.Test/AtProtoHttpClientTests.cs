// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using Microsoft.Extensions.Primitives;

using System.Text.Json;

namespace idunno.AtProto.Integration.Test
{
    [ExcludeFromCodeCoverage]
    public class AtProtoHttpClientTests
    {
        [Fact]
        public async Task AgentGetRecordSpecifingAServiceProxySendsTheProxyHeader()
        {
            const string serviceProxy = "did:web:api.test.invalid#proxy";
            const string expectedValue = "test";

            Uri server = new("https://test.invalid");

            const string repo = "did:plc:identifier";
            const string collection = "test.idunno.lexiconType";
            const string rkey = "rkey";
            const string cid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            string jsonReturnValue = $"{{\"testValue\":\"{expectedValue}\"}}";

            TestServer testServer = TestServerBuilder.CreateServer(server, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (!request.Headers.TryGetValue("atproto-proxy", out StringValues atProxyHeader) ||
                    atProxyHeader.FirstOrDefault() != serviceProxy)
                {
                    response.StatusCode = 500;
                    return;
                }

                if (request.Path == AtProtoServer.GetRecordEndpoint &&
                    request.QueryString.HasValue &&
                    request.Query["repo"].FirstOrDefault() == repo &&
                    request.Query["collection"].FirstOrDefault() == collection &&
                    request.Query["rkey"].FirstOrDefault() == rkey &&
                    request.Query["cid"].FirstOrDefault() == cid)
                {
                    response.StatusCode = 200;
                    response.ContentType = "application/json";
                    await response.WriteAsync(jsonReturnValue);
                }
            });

            using (AtProtoAgent agent = new(server, new TestHttpClientFactory(testServer)))
            {
                AtProtoHttpResult<TestRecordValue> result = await agent.GetRecord<TestRecordValue>(
                    uri: new($"at://{repo}/{collection}/{rkey}"),
                    cid: new(cid),
                    serviceProxy: serviceProxy,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(result.Succeeded);
                Assert.IsType<TestRecordValue>(result.Result);

                Assert.Equal(expectedValue, result.Result.TestValue);
            }
        }

        [Fact]
        public async Task AgentGetRecordWithoutAServiceProxyDoesNotSendTheProxyHeader()
        {
            const string expectedValue = "test";

            Uri server = new("https://test.invalid");

            const string repo = "did:plc:identifier";
            const string collection = "test.idunno.lexiconType";
            const string rkey = "rkey";
            const string cid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            string jsonReturnValue = $"{{\"testValue\":\"{expectedValue}\"}}";

            TestServer testServer = TestServerBuilder.CreateServer(server, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Headers.TryGetValue("atproto-proxy", out StringValues _))
                {
                    response.StatusCode = 500;
                    return;
                }

                if (request.Path == AtProtoServer.GetRecordEndpoint &&
                    request.QueryString.HasValue &&
                    request.Query["repo"].FirstOrDefault() == repo &&
                    request.Query["collection"].FirstOrDefault() == collection &&
                    request.Query["rkey"].FirstOrDefault() == rkey &&
                    request.Query["cid"].FirstOrDefault() == cid)
                {
                    response.StatusCode = 200;
                    response.ContentType = "application/json";
                    await response.WriteAsync(jsonReturnValue);
                }
            });

            using (AtProtoAgent agent = new(server, new TestHttpClientFactory(testServer)))
            {
                AtProtoHttpResult<TestRecordValue> result = await agent.GetRecord<TestRecordValue>(
                    uri: new($"at://{repo}/{collection}/{rkey}"),
                    cid: new(cid),
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(result.Succeeded);
                Assert.IsType<TestRecordValue>(result.Result);

                Assert.Equal(expectedValue, result.Result.TestValue);
            }
        }
    }
}
