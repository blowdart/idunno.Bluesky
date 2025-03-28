// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using idunno.DidPlcDirectory;

namespace idunno.AtProto.Integration.Test
{
    [ExcludeFromCodeCoverage]
    public class DirectoryTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public DirectoryTests()
        {
            _jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            _jsonSerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);
        }

        /// Server, and then directory agent tests
        /// Then also via atproto tests to check configuration chaining

        [Fact]
        public async Task DirectServerResolveDidDocumentSucceedsWithKnownPlcDid()
        {
            const string directoryServerHostName = "directory.invalid";
            const string expectedPdsHostName = "pds.invalid";
            const string knownDid = "did:plc:ec72yg6n2sydzjvtovvdlxrk";

            TestServer testServer = TestServerBuilder.CreateServer(directoryServerHostName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == $"/{knownDid}" && request.Host.Host == directoryServerHostName && request.IsHttps)
                {
                    response.StatusCode = 200;
                    DidDocument didDocument = new(
                        id: $"{knownDid}",
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services:
                        [
                            new(
                                id : "#atproto_pds",
                                type : "atprotopds",
                                serviceEndpoint : new Uri($"https://{expectedPdsHostName}")
                                )
                        ]);
                    await response.WriteAsJsonAsync(didDocument, _jsonSerializerOptions);
                }
                else if (request.Host.Host == directoryServerHostName && request.IsHttps)
                {
                    response.StatusCode = 400;
                }
                else if (request.IsHttps)
                {
                    response.StatusCode = 404;
                }
                else
                {
                    response.StatusCode = 500;
                }
            });

            AtProtoHttpResult<DidDocument> result = await DirectoryServer.ResolveDidDocument(
                did: new Did(knownDid),
                directory: new Uri($"https://{directoryServerHostName}"),
                httpClient: testServer.CreateClient(),
                loggerFactory: null,
                TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);
            Assert.Equal(new Did(knownDid), result.Result.Id);
        }

        [Fact]
        public async Task DirectServerResolveDidDocumentSucceedsWithKnownWebDid()
        {
            const string expectedPdsHostName = "test.invalid";
            const string knownDid = $"did:web:{expectedPdsHostName}";

            TestServer testServer = TestServerBuilder.CreateServer(expectedPdsHostName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == $"/.well-known/did.json" && request.Host.Host == expectedPdsHostName && request.IsHttps)
                {
                    response.StatusCode = 200;
                    DidDocument didDocument = new(
                        id: $"{knownDid}",
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services:
                        [
                            new(
                                id : "#atproto_pds",
                                type : "atprotopds",
                                serviceEndpoint : new Uri($"https://{expectedPdsHostName}")
                                )
                        ]);
                    await response.WriteAsJsonAsync(didDocument, _jsonSerializerOptions);
                }
                else if (request.Host.Host == expectedPdsHostName && request.IsHttps)
                {
                    response.StatusCode = 404;
                }
                else if (request.IsHttps)
                {
                    response.StatusCode = 400;
                }
                else
                {
                    response.StatusCode = 500;
                }
            });

            AtProtoHttpResult<DidDocument> result = await DirectoryServer.ResolveDidDocument(
                did: new Did(knownDid),
                directory: new Uri($"https://directory.invalid"),
                httpClient: testServer.CreateClient(),
                loggerFactory: null,
                TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);
            Assert.Equal(new Did(knownDid), result.Result.Id);
        }

        [Fact]
        public async Task DirectServerResolveDidDocumentFailsWithWithUnknownDidType()
        {
            const string expectedPdsHostName = "test.invalid";
            const string did = $"did:invalid:{expectedPdsHostName}";

            TestServer testServer = TestServerBuilder.CreateServer(expectedPdsHostName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == $"/{did}" && request.Host.Host == expectedPdsHostName && request.IsHttps)
                {
                    response.StatusCode = 200;
                    DidDocument didDocument = new(
                        id: $"{did}",
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services:
                        [
                            new(
                                id : "#atproto_pds",
                                type : "atprotopds",
                                serviceEndpoint : new Uri($"https://{expectedPdsHostName}")
                                )
                        ]);
                    await response.WriteAsJsonAsync(didDocument, _jsonSerializerOptions);
                }
                else if (request.Host.Host == expectedPdsHostName && request.IsHttps)
                {
                    response.StatusCode = 404;
                }
                else if (request.IsHttps)
                {
                    response.StatusCode = 400;
                }
                else
                {
                    response.StatusCode = 500;
                }
            });

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                _ = await DirectoryServer.ResolveDidDocument(
                    did: new Did(did),
                    directory: new Uri($"https://directory.invalid"),
                    httpClient: testServer.CreateClient(),
                    loggerFactory: null,
                    TestContext.Current.CancellationToken);
            });
        }

        [Fact]
        public async Task DirectServerResolveDidDocumentFailsWithUnknownPlcDid()
        {
            const string directoryServerHostName = "directory.invalid";
            const string expectedPdsHostName = "pds.invalid";
            const string knownDid = "did:plc:ec72yg6n2sydzjvtovvdlxrk";
            const string unknownDid = "did:plc:ec72yg6n2sydzjvtovvdlxrz";

            TestServer testServer = TestServerBuilder.CreateServer(directoryServerHostName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == $"/{knownDid}" && request.Host.Host == directoryServerHostName && request.IsHttps)
                {
                    response.StatusCode = 200;
                    DidDocument didDocument = new(
                        id: $"{knownDid}",
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services:
                        [
                            new(
                                id : "#atproto_pds",
                                type : "atprotopds",
                                serviceEndpoint : new Uri($"https://{expectedPdsHostName}")
                                )
                        ]);
                    await response.WriteAsJsonAsync(didDocument, _jsonSerializerOptions);
                }
                else if (request.Host.Host == directoryServerHostName && request.IsHttps)
                {
                    response.StatusCode = 404;
                }
                else if (request.IsHttps)
                {
                    response.StatusCode = 400;
                }
                else
                {
                    response.StatusCode = 500;
                }
            });

            AtProtoHttpResult<DidDocument> result = await DirectoryServer.ResolveDidDocument(
                did: new Did(unknownDid),
                directory: new Uri($"https://{directoryServerHostName}"),
                httpClient: testServer.CreateClient(),
                loggerFactory: null,
                TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        }

        [Fact]
        public async Task DirectServerResolveDidDocumentFailsWithUnknownWebDid()
        {
            const string expectedPdsHostName = "test2.invalid";
            const string unknownDid = $"did:web:test2.invalid";

            TestServer testServer = TestServerBuilder.CreateServer(expectedPdsHostName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == $"/.well-known/did.json" && request.Host.Host == expectedPdsHostName && request.IsHttps)
                {
                    response.StatusCode = 404;
                    await response.WriteAsync("Not Found");

                }
                else if (request.IsHttps)
                {
                    response.StatusCode = 400;
                    await response.WriteAsync("Bad request");
                }
                else
                {
                    response.StatusCode = 500;
                }
            });

            AtProtoHttpResult<DidDocument> result = await DirectoryServer.ResolveDidDocument(
                did: new Did(unknownDid),
                directory: new Uri($"https://directory.invalid"),
                httpClient: testServer.CreateClient(),
                loggerFactory: null,
                TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        }

        [Fact]
        public async Task DirectoryAgentResolveDidDocumentSucceedsWithKnownPlcDid()
        {
            const string directoryServerHostName = "directory.invalid";
            const string expectedPdsHostName = "pds.invalid";
            const string knownDid = "did:plc:ec72yg6n2sydzjvtovvdlxrk";

            TestServer testServer = TestServerBuilder.CreateServer(directoryServerHostName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == $"/{knownDid}" && request.Host.Host == directoryServerHostName && request.IsHttps)
                {
                    response.StatusCode = 200;
                    DidDocument didDocument = new(
                        id: $"{knownDid}",
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services:
                        [
                            new(
                                id : "#atproto_pds",
                                type : "atprotopds",
                                serviceEndpoint : new Uri($"https://{expectedPdsHostName}")
                                )
                        ]);
                    await response.WriteAsJsonAsync(didDocument, _jsonSerializerOptions);
                }
                else if (request.Host.Host == directoryServerHostName && request.IsHttps)
                {
                    response.StatusCode = 400;
                }
                else if (request.IsHttps)
                {
                    response.StatusCode = 404;
                }
                else
                {
                    response.StatusCode = 500;
                }
            });

            using (var agent = new DirectoryAgent(
                new TestHttpClientFactory(testServer),
                new DirectoryAgentOptions()
                {
                    PlcDirectoryUri = new Uri($"https://{directoryServerHostName}")
                }))
            {
                AtProtoHttpResult<DidDocument> result = await agent.ResolveDidDocument(
                    did: new Did(knownDid),
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(result.Succeeded);
                Assert.Equal(new Did(knownDid), result.Result.Id);
            }
        }

        [Fact]
        public async Task AgentResolveDidDocumentSucceedsWithKnownWebDid()
        {
            const string expectedPdsHostName = "test.invalid";
            const string knownDid = $"did:web:{expectedPdsHostName}";

            TestServer testServer = TestServerBuilder.CreateServer(expectedPdsHostName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == $"/.well-known/did.json" && request.Host.Host == expectedPdsHostName && request.IsHttps)
                {
                    response.StatusCode = 200;
                    DidDocument didDocument = new(
                        id: $"{knownDid}",
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services:
                        [
                            new(
                                id : "#atproto_pds",
                                type : "atprotopds",
                                serviceEndpoint : new Uri($"https://{expectedPdsHostName}")
                                )
                        ]);
                    await response.WriteAsJsonAsync(didDocument, _jsonSerializerOptions);
                }
                else if (request.Host.Host == expectedPdsHostName && request.IsHttps)
                {
                    response.StatusCode = 404;
                }
                else if (request.IsHttps)
                {
                    response.StatusCode = 400;
                }
                else
                {
                    response.StatusCode = 500;
                }
            });

            using (var agent = new DirectoryAgent(
                new TestHttpClientFactory(testServer),
                new DirectoryAgentOptions()
                {
                    PlcDirectoryUri = new Uri($"https://directory.invalid")
                }))
            {
                AtProtoHttpResult<DidDocument> result = await agent.ResolveDidDocument(
                    did: new Did(knownDid),
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(result.Succeeded);
            }
        }

        [Fact]
        public async Task AgentResolveDidDocumentFailsWithWithUnknownDidType()
        {
            const string expectedPdsHostName = "test.invalid";
            const string did = $"did:invalid:{expectedPdsHostName}";

            TestServer testServer = TestServerBuilder.CreateServer(expectedPdsHostName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == $"/{did}" && request.Host.Host == expectedPdsHostName && request.IsHttps)
                {
                    response.StatusCode = 200;
                    DidDocument didDocument = new(
                        id: $"{did}",
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services:
                        [
                            new(
                                id : "#atproto_pds",
                                type : "atprotopds",
                                serviceEndpoint : new Uri($"https://{expectedPdsHostName}")
                                )
                        ]);
                    await response.WriteAsJsonAsync(didDocument, _jsonSerializerOptions);
                }
                else if (request.Host.Host == expectedPdsHostName && request.IsHttps)
                {
                    response.StatusCode = 404;
                }
                else if (request.IsHttps)
                {
                    response.StatusCode = 400;
                }
                else
                {
                    response.StatusCode = 500;
                }
            });

            using (var agent = new DirectoryAgent(
                new TestHttpClientFactory(testServer),
                new DirectoryAgentOptions()
                {
                    PlcDirectoryUri = new Uri($"https://directory.invalid")
                }))
            {

                await Assert.ThrowsAsync<ArgumentException>(async () =>
                {
                    _ = await agent.ResolveDidDocument(
                        did: new Did(did),
                        cancellationToken: TestContext.Current.CancellationToken);
                });
            }
        }

        [Fact]
        public async Task AgentResolveDidDocumentFailsWithUnknownPlcDid()
        {
            const string directoryServerHostName = "directory.invalid";
            const string expectedPdsHostName = "pds.invalid";
            const string knownDid = "did:plc:ec72yg6n2sydzjvtovvdlxrk";
            const string unknownDid = "did:plc:ec72yg6n2sydzjvtovvdlxrz";

            TestServer testServer = TestServerBuilder.CreateServer(directoryServerHostName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == $"/{knownDid}" && request.Host.Host == directoryServerHostName && request.IsHttps)
                {
                    response.StatusCode = 200;
                    DidDocument didDocument = new(
                        id: $"{knownDid}",
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services:
                        [
                            new(
                                id : "#atproto_pds",
                                type : "atprotopds",
                                serviceEndpoint : new Uri($"https://{expectedPdsHostName}")
                                )
                        ]);
                    await response.WriteAsJsonAsync(didDocument, _jsonSerializerOptions);
                }
                else if (request.Host.Host == directoryServerHostName && request.IsHttps)
                {
                    response.StatusCode = 404;
                }
                else if (request.IsHttps)
                {
                    response.StatusCode = 400;
                }
                else
                {
                    response.StatusCode = 500;
                }
            });

            using (var agent = new DirectoryAgent(
                new TestHttpClientFactory(testServer),
                new DirectoryAgentOptions()
                {
                    PlcDirectoryUri = new Uri($"https://{directoryServerHostName}")
                }))
            {
                AtProtoHttpResult<DidDocument> result = await agent.ResolveDidDocument(
                    did: new Did(unknownDid),
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.False(result.Succeeded);
                Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
            }
        }

        [Fact]
        public async Task AgentResolveDidDocumentFailsWithUnknownWebDid()
        {
            const string expectedPdsHostName = "test2.invalid";
            const string unknownDid = $"did:web:test2.invalid";

            TestServer testServer = TestServerBuilder.CreateServer(expectedPdsHostName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == $"/.well-known/did.json" && request.Host.Host == expectedPdsHostName && request.IsHttps)
                {
                    response.StatusCode = 404;
                    await response.WriteAsync("Not Found");

                }
                else if (request.IsHttps)
                {
                    response.StatusCode = 400;
                    await response.WriteAsync("Bad request");
                }
                else
                {
                    response.StatusCode = 500;
                }
            });

            using (var agent = new DirectoryAgent(
                new TestHttpClientFactory(testServer),
                new DirectoryAgentOptions()
                {
                    PlcDirectoryUri = new Uri($"https://directory.invalid")
                }))
            {
                AtProtoHttpResult<DidDocument> result = await agent.ResolveDidDocument(
                    did: new Did(unknownDid),
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.False(result.Succeeded);
                Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
            }
        }
    }
}
