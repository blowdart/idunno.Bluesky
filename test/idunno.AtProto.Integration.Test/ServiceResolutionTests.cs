// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using idunno.AtProto.Repo;
using System.Text.Json;

namespace idunno.AtProto.Integration.Test
{
    public class ServiceResolutionTests
    {
        [Fact]
        public async Task GetRecordResolvesServerWhenNotSpecified()
        {
            const string expectedValue = "test";

            Uri server = new("https://test.invalid");

            const string repo = "did:plc:identifier";
            const string collection = "test.idunno.lexiconType";
            const string rkey = "rkey";
            const string cid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            string getRecordReturnValue = """
                {
                    "uri" : "at://did:plc:identifier/test.idunno.lexiconType/rkey",
                    "cid" : "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
                    "value" :
                    {
                        "testValue" : "test"
                    }
                }
                """;

            string didDoc = """
                {
                    "@context": [
                        "https://www.w3.org/ns/did/v1"
                    ],
                    "id": "did:plc:identifier",
                    "service": [
                        {
                            "id": "#atproto_pds",
                            "type": "AtprotoPersonalDataServer",
                            "serviceEndpoint": "https://pds.example.org"
                        }
                    ]
                }
                """;

            bool didDocRequested = false;

            TestServer testServer = TestServerBuilder.CreateServer(server, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.IsHttps &&
                    request.Host.Host == "plc.directory")
                {
                    if (request.Path.HasValue &&
                        request.Path.Value == $"/{repo}")
                    {
                        didDocRequested = true;
                        response.StatusCode = 200;
                        response.ContentType = "application/json";
                        await response.WriteAsync(didDoc);
                        return;
                    }
                    else
                    {
                        response.StatusCode = 404;
                        return;
                    }
                }

                if (request.IsHttps &&
                    request.Host.Host == "pds.example.org")
                {
                    if (request.Path == AtProtoServer.GetRecordEndpoint &&
                        request.QueryString.HasValue &&
                        request.Query["repo"].FirstOrDefault() == repo &&
                        request.Query["collection"].FirstOrDefault() == collection &&
                        request.Query["rkey"].FirstOrDefault() == rkey &&
                        request.Query["cid"].FirstOrDefault() == cid)
                    {
                        response.StatusCode = 200;
                        response.ContentType = "application/json";
                        await response.WriteAsync(getRecordReturnValue);
                        return;
                    }
                    else
                    {
                        response.StatusCode = 500;
                        return;
                    }
                }
                else
                {
                    response.StatusCode = 404;
                    return;
                }
            });

            using (AtProtoAgent agent = new(server, new TestHttpClientFactory(testServer)))
            {
                AtProtoHttpResult<AtProtoRepositoryRecord<TestRecord>> result = await agent.GetRecord<TestRecord>(
                    uri: new($"at://{repo}/{collection}/{rkey}"),
                    cid: new(cid),
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(didDocRequested);

                Assert.True(result.Succeeded);
                Assert.Equal(expectedValue, result.Result.Value.TestValue);

                Assert.Equal("at://did:plc:identifier/test.idunno.lexiconType/rkey", result.Result.Uri);
                Assert.Equal("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4", result.Result.Cid);
            }
        }

        [Fact]
        public async Task GetRecordDoesNotResolvesServerWhenServiceIsSpecified()
        {
            const string expectedValue = "test";

            Uri server = new("https://test.invalid");

            const string repo = "did:plc:identifier";
            const string collection = "test.idunno.lexiconType";
            const string rkey = "rkey";
            const string cid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            string getRecordReturnValue = """
                {
                    "uri" : "at://did:plc:identifier/test.idunno.lexiconType/rkey",
                    "cid" : "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
                    "value" :
                    {
                        "testValue" : "test"
                    }
                }
                """;

            string didDoc = """
                {
                    "@context": [
                        "https://www.w3.org/ns/did/v1"
                    ],
                    "id": "did:plc:identifier",
                    "service": [
                        {
                            "id": "#atproto_pds",
                            "type": "AtprotoPersonalDataServer",
                            "serviceEndpoint": "https://pds.example.org"
                        }
                    ]
                }
                """;

            bool didDocRequested = false;

            TestServer testServer = TestServerBuilder.CreateServer(server, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.IsHttps &&
                    request.Host.Host == "plc.directory")
                {
                    if (request.Path.HasValue &&
                        request.Path.Value == $"/{repo}")
                    {
                        didDocRequested = true;
                        response.StatusCode = 200;
                        response.ContentType = "application/json";
                        await response.WriteAsync(didDoc);
                        return;
                    }
                    else
                    {
                        response.StatusCode = 404;
                        return;
                    }
                }

                if (request.IsHttps &&
                    request.Host.Host == "pds.example.org")
                {
                    if (request.Path == AtProtoServer.GetRecordEndpoint &&
                        request.QueryString.HasValue &&
                        request.Query["repo"].FirstOrDefault() == repo &&
                        request.Query["collection"].FirstOrDefault() == collection &&
                        request.Query["rkey"].FirstOrDefault() == rkey &&
                        request.Query["cid"].FirstOrDefault() == cid)
                    {
                        response.StatusCode = 200;
                        response.ContentType = "application/json";
                        await response.WriteAsync(getRecordReturnValue);
                        return;
                    }
                    else
                    {
                        response.StatusCode = 500;
                        return;
                    }
                }
                else
                {
                    response.StatusCode = 404;
                    return;
                }
            });

            using (AtProtoAgent agent = new(server, new TestHttpClientFactory(testServer)))
            {
                AtProtoHttpResult<AtProtoRepositoryRecord<TestRecord>> result = await agent.GetRecord<TestRecord>(
                    uri: new($"at://{repo}/{collection}/{rkey}"),
                    cid: new(cid),
                    service: new Uri("https://pds.example.org"),
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.False(didDocRequested);

                Assert.True(result.Succeeded);
                Assert.Equal(expectedValue, result.Result.Value.TestValue);

                Assert.Equal("at://did:plc:identifier/test.idunno.lexiconType/rkey", result.Result.Uri);
                Assert.Equal("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4", result.Result.Cid);
            }
        }

    }
}
