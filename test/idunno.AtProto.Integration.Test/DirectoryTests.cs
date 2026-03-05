// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

using idunno.AtProto.DidPlcDirectory;
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

        [Fact]
        public async Task DirectServerResolveDidDocumentSucceedsWithKnownPlcDid()
        {
            const string directoryServerHostName = "directory.invalid";
            const string expectedPdsHostName = "pds.invalid";
            const string knownDid = "did:plc:ec72yg6n2sydzjvtovvdlxrk";

            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var totalRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total");
            var totalFailedRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.failed");
            var totalSucceededRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.succeeded");
            var requestDurationCollector = new MetricCollector<double>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.request.duration");

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
                meterFactory: meterFactory,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);
            Assert.Equal(new Did(knownDid), result.Result.Id);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = totalRequestsCollector.GetMeasurementSnapshot();
            Assert.Single(requestMeasurements);
            Assert.True(requestMeasurements[0]!.ContainsTags("did.type"));
            Assert.Equal(
                "plc",
                requestMeasurements[0]!.Tags["did.type"]);

            IReadOnlyList<CollectedMeasurement<long>> succeededMeasurements = totalSucceededRequestsCollector.GetMeasurementSnapshot();
            Assert.Single(succeededMeasurements);
            Assert.True(succeededMeasurements[0]!.ContainsTags("did.type"));
            Assert.Equal(
                "plc",
                succeededMeasurements[0]!.Tags["did.type"]);

            IReadOnlyList<CollectedMeasurement<long>> failedMeasurements = totalFailedRequestsCollector.GetMeasurementSnapshot();
            Assert.Empty(failedMeasurements);

            IReadOnlyList <CollectedMeasurement<double>> durationMeasurements = requestDurationCollector.GetMeasurementSnapshot();
            Assert.Single(durationMeasurements);
        }

        [Fact]
        public async Task DirectServerResolveDidDocumentSucceedsWithKnownWebDid()
        {
            const string expectedPdsHostName = "test.invalid";
            const string knownDid = $"did:web:{expectedPdsHostName}";

            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var totalRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total");
            var totalFailedRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.failed");
            var totalSucceededRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.succeeded");
            var requestDurationCollector = new MetricCollector<double>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.request.duration");

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
                meterFactory: meterFactory,
                TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);
            Assert.Equal(new Did(knownDid), result.Result.Id);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = totalRequestsCollector.GetMeasurementSnapshot();
            Assert.Single(requestMeasurements);
            Assert.True(requestMeasurements[0]!.ContainsTags("did.type"));
            Assert.Equal(
                "web",
                requestMeasurements[0]!.Tags["did.type"]);

            IReadOnlyList<CollectedMeasurement<long>> succeededMeasurements = totalSucceededRequestsCollector.GetMeasurementSnapshot();
            Assert.Single(succeededMeasurements);
            Assert.True(succeededMeasurements[0]!.ContainsTags("did.type"));
            Assert.Equal(
                "web",
                succeededMeasurements[0]!.Tags["did.type"]);

            IReadOnlyList<CollectedMeasurement<long>> failedMeasurements = totalFailedRequestsCollector.GetMeasurementSnapshot();
            Assert.Empty(failedMeasurements);

            IReadOnlyList<CollectedMeasurement<double>> durationMeasurements = requestDurationCollector.GetMeasurementSnapshot();
            Assert.Single(durationMeasurements);
        }

        [Fact]
        public async Task DirectServerResolveDidDocumentFailsWithWithUnknownDidType()
        {
            const string expectedPdsHostName = "test.invalid";
            const string did = $"did:invalid:{expectedPdsHostName}";

            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var totalRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total");
            var totalFailedRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.failed");
            var totalSucceededRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.succeeded");
            var requestDurationCollector = new MetricCollector<double>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.request.duration");

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
                    meterFactory: meterFactory,
                    cancellationToken: TestContext.Current.CancellationToken);
            });

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = totalRequestsCollector.GetMeasurementSnapshot();
            Assert.Single(requestMeasurements);
            Assert.True(requestMeasurements[0]!.ContainsTags("did.type"));
            Assert.Equal(
                "unknown",
                requestMeasurements[0]!.Tags["did.type"]);

            IReadOnlyList<CollectedMeasurement<long>> succeededMeasurements = totalSucceededRequestsCollector.GetMeasurementSnapshot();
            Assert.Empty(succeededMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> failedMeasurements = totalFailedRequestsCollector.GetMeasurementSnapshot();
            Assert.Single(failedMeasurements);
            Assert.True(failedMeasurements[0]!.ContainsTags("did.type"));
            Assert.Equal(
                "unknown",
                failedMeasurements[0]!.Tags["did.type"]);
            IReadOnlyList<CollectedMeasurement<double>> durationMeasurements = requestDurationCollector.GetMeasurementSnapshot();
            Assert.Single(durationMeasurements);
        }

        [Fact]
        public async Task DirectServerResolveDidDocumentFailsWithUnknownPlcDid()
        {
            const string directoryServerHostName = "directory.invalid";
            const string expectedPdsHostName = "pds.invalid";
            const string knownDid = "did:plc:ec72yg6n2sydzjvtovvdlxrk";
            const string unknownDid = "did:plc:ec72yg6n2sydzjvtovvdlxrz";

            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var totalRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total");
            var totalFailedRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.failed");
            var totalSucceededRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.succeeded");
            var requestDurationCollector = new MetricCollector<double>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.request.duration");

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
                meterFactory: meterFactory,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = totalRequestsCollector.GetMeasurementSnapshot();
            Assert.Single(requestMeasurements);
            Assert.True(requestMeasurements[0]!.ContainsTags("did.type"));
            Assert.Equal(
                "plc",
                requestMeasurements[0]!.Tags["did.type"]);

            IReadOnlyList<CollectedMeasurement<long>> succeededMeasurements = totalSucceededRequestsCollector.GetMeasurementSnapshot();
            Assert.Empty(succeededMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> failedMeasurements = totalFailedRequestsCollector.GetMeasurementSnapshot();
            Assert.Single(failedMeasurements);
            Assert.True(failedMeasurements[0]!.ContainsTags("did.type"));
            Assert.Equal(
                "plc",
                failedMeasurements[0]!.Tags["did.type"]);
            Assert.True(failedMeasurements[0]!.ContainsTags("http_status_code"));
            Assert.Equal(
                404,
                failedMeasurements[0]!.Tags["http_status_code"]);

            IReadOnlyList<CollectedMeasurement<double>> durationMeasurements = requestDurationCollector.GetMeasurementSnapshot();
            Assert.Single(durationMeasurements);
        }

        [Fact]
        public async Task DirectServerResolveDidDocumentFailsWithUnknownWebDid()
        {
            const string expectedPdsHostName = "test2.invalid";
            const string unknownDid = $"did:web:test2.invalid";

            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var totalRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total");
            var totalFailedRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.failed");
            var totalSucceededRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.succeeded");
            var requestDurationCollector = new MetricCollector<double>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.request.duration");

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
                meterFactory: meterFactory,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = totalRequestsCollector.GetMeasurementSnapshot();
            Assert.Single(requestMeasurements);
            Assert.True(requestMeasurements[0]!.ContainsTags("did.type"));
            Assert.Equal(
                "web",
                requestMeasurements[0]!.Tags["did.type"]);

            IReadOnlyList<CollectedMeasurement<long>> succeededMeasurements = totalSucceededRequestsCollector.GetMeasurementSnapshot();
            Assert.Empty(succeededMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> failedMeasurements = totalFailedRequestsCollector.GetMeasurementSnapshot();
            Assert.Single(failedMeasurements);
            Assert.True(failedMeasurements[0]!.ContainsTags("did.type"));
            Assert.Equal(
                "web",
                failedMeasurements[0]!.Tags["did.type"]);
            Assert.True(failedMeasurements[0]!.ContainsTags("http_status_code"));
            Assert.Equal(
                404,
                failedMeasurements[0]!.Tags["http_status_code"]);

            IReadOnlyList<CollectedMeasurement<double>> durationMeasurements = requestDurationCollector.GetMeasurementSnapshot();
            Assert.Single(durationMeasurements);
        }

        [Fact]
        public async Task DirectoryAgentResolveDidDocumentSucceedsWithKnownPlcDid()
        {
            const string directoryServerHostName = "directory.invalid";
            const string expectedPdsHostName = "pds.invalid";
            const string knownDid = "did:plc:ec72yg6n2sydzjvtovvdlxrk";

            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var totalRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total");
            var totalFailedRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.failed");
            var totalSucceededRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.succeeded");
            var requestDurationCollector = new MetricCollector<double>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.request.duration");

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
                    PlcDirectoryUri = new Uri($"https://{directoryServerHostName}"),
                    MeterFactory = meterFactory,
                }))
            {
                AtProtoHttpResult<DidDocument> result = await agent.ResolveDidDocument(
                    did: new Did(knownDid),
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(result.Succeeded);
                Assert.Equal(new Did(knownDid), result.Result.Id);

                IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = totalRequestsCollector.GetMeasurementSnapshot();
                Assert.Single(requestMeasurements);
                Assert.True(requestMeasurements[0]!.ContainsTags("did.type"));
                Assert.Equal(
                    "plc",
                    requestMeasurements[0]!.Tags["did.type"]);

                IReadOnlyList<CollectedMeasurement<long>> succeededMeasurements = totalSucceededRequestsCollector.GetMeasurementSnapshot();
                Assert.Single(succeededMeasurements);
                Assert.True(succeededMeasurements[0]!.ContainsTags("did.type"));
                Assert.Equal(
                    "plc",
                    succeededMeasurements[0]!.Tags["did.type"]);

                IReadOnlyList<CollectedMeasurement<long>> failedMeasurements = totalFailedRequestsCollector.GetMeasurementSnapshot();
                Assert.Empty(failedMeasurements);

                IReadOnlyList<CollectedMeasurement<double>> durationMeasurements = requestDurationCollector.GetMeasurementSnapshot();
                Assert.Single(durationMeasurements);
            }
        }

        [Fact]
        public async Task DirectoryAgentResolveDidDocumentSucceedsWithKnownWebDid()
        {
            const string expectedPdsHostName = "test.invalid";
            const string knownDid = $"did:web:{expectedPdsHostName}";

            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var totalRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total");
            var totalFailedRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.failed");
            var totalSucceededRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.succeeded");
            var requestDurationCollector = new MetricCollector<double>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.request.duration");

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
                    PlcDirectoryUri = new Uri($"https://directory.invalid"),
                    MeterFactory = meterFactory,
                }))
            {
                AtProtoHttpResult<DidDocument> result = await agent.ResolveDidDocument(
                    did: new Did(knownDid),
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(result.Succeeded);

                IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = totalRequestsCollector.GetMeasurementSnapshot();
                Assert.Single(requestMeasurements);
                Assert.True(requestMeasurements[0]!.ContainsTags("did.type"));
                Assert.Equal(
                    "web",
                    requestMeasurements[0]!.Tags["did.type"]);

                IReadOnlyList<CollectedMeasurement<long>> succeededMeasurements = totalSucceededRequestsCollector.GetMeasurementSnapshot();
                Assert.Single(succeededMeasurements);
                Assert.True(succeededMeasurements[0]!.ContainsTags("did.type"));
                Assert.Equal(
                    "web",
                    succeededMeasurements[0]!.Tags["did.type"]);

                IReadOnlyList<CollectedMeasurement<long>> failedMeasurements = totalFailedRequestsCollector.GetMeasurementSnapshot();
                Assert.Empty(failedMeasurements);

                IReadOnlyList<CollectedMeasurement<double>> durationMeasurements = requestDurationCollector.GetMeasurementSnapshot();
                Assert.Single(durationMeasurements);
            }
        }

        [Fact]
        public async Task DirectoryAgentResolveDidDocumentFailsWithWithUnknownDidType()
        {
            const string expectedPdsHostName = "test.invalid";
            const string did = $"did:invalid:{expectedPdsHostName}";

            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var totalRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total");
            var totalFailedRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.failed");
            var totalSucceededRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.succeeded");
            var requestDurationCollector = new MetricCollector<double>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.request.duration");

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
                    PlcDirectoryUri = new Uri($"https://directory.invalid"),
                    MeterFactory = meterFactory,
                }))
            {

                await Assert.ThrowsAsync<ArgumentException>(async () =>
                {
                    _ = await agent.ResolveDidDocument(
                        did: new Did(did),
                        cancellationToken: TestContext.Current.CancellationToken);
                });

                IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = totalRequestsCollector.GetMeasurementSnapshot();
                Assert.Single(requestMeasurements);
                Assert.True(requestMeasurements[0]!.ContainsTags("did.type"));
                Assert.Equal(
                    "unknown",
                    requestMeasurements[0]!.Tags["did.type"]);

                IReadOnlyList<CollectedMeasurement<long>> succeededMeasurements = totalSucceededRequestsCollector.GetMeasurementSnapshot();
                Assert.Empty(succeededMeasurements);

                IReadOnlyList<CollectedMeasurement<long>> failedMeasurements = totalFailedRequestsCollector.GetMeasurementSnapshot();
                Assert.Single(failedMeasurements);
                Assert.True(failedMeasurements[0]!.ContainsTags("did.type"));
                Assert.Equal(
                    "unknown",
                    failedMeasurements[0]!.Tags["did.type"]);
                IReadOnlyList<CollectedMeasurement<double>> durationMeasurements = requestDurationCollector.GetMeasurementSnapshot();
                Assert.Single(durationMeasurements);
            }
        }

        [Fact]
        public async Task DirectoryAgentResolveDidDocumentFailsWithUnknownPlcDid()
        {
            const string directoryServerHostName = "directory.invalid";
            const string expectedPdsHostName = "pds.invalid";
            const string knownDid = "did:plc:ec72yg6n2sydzjvtovvdlxrk";
            const string unknownDid = "did:plc:ec72yg6n2sydzjvtovvdlxrz";

            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var totalRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total");
            var totalFailedRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.failed");
            var totalSucceededRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.succeeded");
            var requestDurationCollector = new MetricCollector<double>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.request.duration");

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
                    PlcDirectoryUri = new Uri($"https://{directoryServerHostName}"),
                    MeterFactory = meterFactory
                }))
            {
                AtProtoHttpResult<DidDocument> result = await agent.ResolveDidDocument(
                    did: new Did(unknownDid),
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.False(result.Succeeded);
                Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);

                IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = totalRequestsCollector.GetMeasurementSnapshot();
                Assert.Single(requestMeasurements);
                Assert.True(requestMeasurements[0]!.ContainsTags("did.type"));
                Assert.Equal(
                    "plc",
                    requestMeasurements[0]!.Tags["did.type"]);

                IReadOnlyList<CollectedMeasurement<long>> succeededMeasurements = totalSucceededRequestsCollector.GetMeasurementSnapshot();
                Assert.Empty(succeededMeasurements);

                IReadOnlyList<CollectedMeasurement<long>> failedMeasurements = totalFailedRequestsCollector.GetMeasurementSnapshot();
                Assert.Single(failedMeasurements);
                Assert.True(failedMeasurements[0]!.ContainsTags("did.type"));
                Assert.Equal(
                    "plc",
                    failedMeasurements[0]!.Tags["did.type"]);
                Assert.True(failedMeasurements[0]!.ContainsTags("http_status_code"));
                Assert.Equal(
                    404,
                    failedMeasurements[0]!.Tags["http_status_code"]);
                IReadOnlyList<CollectedMeasurement<double>> durationMeasurements = requestDurationCollector.GetMeasurementSnapshot();
                Assert.Single(durationMeasurements);
            }
        }

        [Fact]
        public async Task DirectoryAgentResolveDidDocumentFailsWithUnknownWebDid()
        {
            const string expectedPdsHostName = "test2.invalid";
            const string unknownDid = $"did:web:test2.invalid";

            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var totalRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total");
            var totalFailedRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.failed");
            var totalSucceededRequestsCollector = new MetricCollector<long>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.requests.total.succeeded");
            var requestDurationCollector = new MetricCollector<double>(meterFactory, DirectoryMetrics.MeterName, "idunno.atproto.directory.request.duration");

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
                    PlcDirectoryUri = new Uri($"https://directory.invalid"),
                    MeterFactory = meterFactory
                }))
            {
                AtProtoHttpResult<DidDocument> result = await agent.ResolveDidDocument(
                    did: new Did(unknownDid),
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.False(result.Succeeded);
                Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);

                IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = totalRequestsCollector.GetMeasurementSnapshot();
                Assert.Single(requestMeasurements);
                Assert.True(requestMeasurements[0]!.ContainsTags("did.type"));
                Assert.Equal(
                    "web",
                    requestMeasurements[0]!.Tags["did.type"]);

                IReadOnlyList<CollectedMeasurement<long>> succeededMeasurements = totalSucceededRequestsCollector.GetMeasurementSnapshot();
                Assert.Empty(succeededMeasurements);

                IReadOnlyList<CollectedMeasurement<long>> failedMeasurements = totalFailedRequestsCollector.GetMeasurementSnapshot();
                Assert.Single(failedMeasurements);
                Assert.True(failedMeasurements[0]!.ContainsTags("did.type"));
                Assert.Equal(
                    "web",
                    failedMeasurements[0]!.Tags["did.type"]);
                Assert.True(failedMeasurements[0]!.ContainsTags("http_status_code"));
                Assert.Equal(
                    404,
                    failedMeasurements[0]!.Tags["http_status_code"]);
                IReadOnlyList<CollectedMeasurement<double>> durationMeasurements = requestDurationCollector.GetMeasurementSnapshot();
                Assert.Single(durationMeasurements);
            }
        }

        private static ServiceProvider CreateServiceProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddMetrics();
            return serviceCollection.BuildServiceProvider();
        }
    }
}
