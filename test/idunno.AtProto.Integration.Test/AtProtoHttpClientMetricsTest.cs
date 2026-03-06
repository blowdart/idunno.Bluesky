// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Duende.IdentityModel.OidcClient.DPoP;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Primitives;


namespace idunno.AtProto.Integration.Test
{
    public class AtProtoHttpClientMetricsTest
    {
        // https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#test-custom-metrics

        [Fact]
        public async Task CreateRequestIncrementsIncrementsMetrics()
        {
            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var requestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total");
            var responseCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total");
            var successfulCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.successful");
            var failureCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.failure");
            var dPoPRetriesCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.dpop_retry");
            var deserializationFailuresCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total.deserialization_failure");
            var xrpcRequestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.xrpc_request");

            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Next();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = CreateTestServer(
                serverUri: TestServerBuilder.DefaultUri,
                expectedCredentials: expectedCredentials,
                expectedDid: expectedDid,
                expectedCollection: expectedCollection,
                expectedRecordKey: expectedRecordKey,
                expectedAtUri: expectedAtUri,
                expectedCid: expectedCid);

            JsonNode record = JsonSerializer.SerializeToNode(new TestRecord { TestValue = "test" })!;
            var createRequest = new CreateRecordRequest(
                 record: record,
                 repo: expectedDid,
                 collection: expectedCollection,
                 rKey: expectedRecordKey);

            string body = JsonSerializer.Serialize(createRequest, AtProtoServer.AtProtoJsonSerializerOptions);

            AtProtoHttpClient atProtoHttpClient = new(serviceProxy: null, loggerFactory: null, meterFactory: meterFactory);
            AtProtoHttpResult<string> result = await atProtoHttpClient.Post(
                service: TestServerBuilder.DefaultUri,
                endpoint: AtProtoServer.CreateRecordEndpoint,
                body: body,
                credentials: expectedCredentials,
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = requestCollector.GetMeasurementSnapshot();
            Assert.Single(requestMeasurements);
            Assert.True(requestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "POST",
                requestMeasurements[0]!.Tags["http_method"]);
            Assert.True(requestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                requestMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> responseMeasurements = responseCollector.GetMeasurementSnapshot();
            Assert.Single(responseMeasurements);
            Assert.True(responseMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> successfulMeasurements = successfulCollector.GetMeasurementSnapshot();
            Assert.Single(successfulMeasurements);
            Assert.True(successfulMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                successfulMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> failureMeasurements = failureCollector.GetMeasurementSnapshot();
            Assert.Empty(failureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> dPoPMeasurements = dPoPRetriesCollector.GetMeasurementSnapshot();
            Assert.Empty(dPoPMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> deserializationFailureMeasurements = deserializationFailuresCollector.GetMeasurementSnapshot();
            Assert.Empty(deserializationFailureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> xrpcRequestMeasurements = xrpcRequestCollector.GetMeasurementSnapshot();
            Assert.Single(xrpcRequestMeasurements);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.createRecord",
                xrpcRequestMeasurements[0]!.Tags["xrpc_endpoint"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                xrpcRequestMeasurements[0]!.Tags["server"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "POST",
                xrpcRequestMeasurements[0]!.Tags["http_method"]);
        }

        [Fact]
        public async Task GetRecordRequestIncrementsMetrics()
        {
            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var requestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total");
            var responseCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total");
            var successfulCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.successful");
            var failureCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.failure");
            var dPoPRetriesCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.dpop_retry");
            var deserializationFailuresCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total.deserialization_failure");
            var xrpcRequestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.xrpc_request");

            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Next();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = CreateTestServer(
                serverUri: TestServerBuilder.DefaultUri,
                expectedCredentials: expectedCredentials,
                expectedDid: expectedDid,
                expectedCollection: expectedCollection,
                expectedRecordKey: expectedRecordKey,
                expectedAtUri: expectedAtUri,
                expectedCid: expectedCid);

            AtProtoHttpClient atProtoHttpClient = new(serviceProxy: null, loggerFactory: null, meterFactory: meterFactory);
            AtProtoHttpResult<string> result = await atProtoHttpClient.Get(
                service: TestServerBuilder.DefaultUri,
                endpoint: $"{AtProtoServer.GetRecordEndpoint}?repo={expectedDid}&collection={expectedCollection}&rkey={expectedRecordKey}",
                credentials: null,
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = requestCollector.GetMeasurementSnapshot();
            Assert.Single(requestMeasurements);
            Assert.True(requestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                requestMeasurements[0]!.Tags["http_method"]);
            Assert.True(requestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                requestMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> responseMeasurements = responseCollector.GetMeasurementSnapshot();
            Assert.Single(responseMeasurements);
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> successfulMeasurements = successfulCollector.GetMeasurementSnapshot();
            Assert.Single(successfulMeasurements);
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                successfulMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> failureMeasurements = failureCollector.GetMeasurementSnapshot();
            Assert.Empty(failureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> dPoPMeasurements = dPoPRetriesCollector.GetMeasurementSnapshot();
            Assert.Empty(dPoPMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> deserializationFailureMeasurements = deserializationFailuresCollector.GetMeasurementSnapshot();
            Assert.Empty(deserializationFailureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> xrpcRequestMeasurements = xrpcRequestCollector.GetMeasurementSnapshot();
            Assert.Single(xrpcRequestMeasurements);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.getRecord",
                xrpcRequestMeasurements[0]!.Tags["xrpc_endpoint"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                xrpcRequestMeasurements[0]!.Tags["server"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                xrpcRequestMeasurements[0]!.Tags["http_method"]);
        }

        [Fact]
        public async Task GetRecordFollowedByCreateRecordIncrementsMetrics()
        {
            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var requestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total");
            var responseCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total");
            var successfulCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.successful");
            var failureCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.failure");
            var dPoPRetriesCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.dpop_retry");
            var deserializationFailuresCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total.deserialization_failure");
            var xrpcRequestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.xrpc_request");

            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Next();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
            string[] expectedVerbs = ["GET", "POST"];
            string[] expectedXrpcEndpoints = ["com.atproto.repo.getRecord", "com.atproto.repo.createRecord"];

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = CreateTestServer(
                serverUri: TestServerBuilder.DefaultUri,
                expectedCredentials: expectedCredentials,
                expectedDid: expectedDid,
                expectedCollection: expectedCollection,
                expectedRecordKey: expectedRecordKey,
                expectedAtUri: expectedAtUri,
                expectedCid: expectedCid);

            AtProtoHttpClient atProtoHttpClient = new(serviceProxy: null, loggerFactory: null, meterFactory: meterFactory);
            AtProtoHttpResult<string> getResult = await atProtoHttpClient.Get(
                service: TestServerBuilder.DefaultUri,
                endpoint: $"{AtProtoServer.GetRecordEndpoint}?repo={expectedDid}&collection={expectedCollection}&rkey={expectedRecordKey}",
                credentials: null,
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

            JsonNode record = JsonSerializer.SerializeToNode(new TestRecord { TestValue = "test" })!;
            var createRequest = new CreateRecordRequest(
                 record: record,
                 repo: expectedDid,
                 collection: expectedCollection,
                 rKey: expectedRecordKey);

            string body = JsonSerializer.Serialize(createRequest, AtProtoServer.AtProtoJsonSerializerOptions);

            AtProtoHttpResult<string> postResult = await atProtoHttpClient.Post(
                service: TestServerBuilder.DefaultUri,
                endpoint: AtProtoServer.CreateRecordEndpoint,
                body: body,
                credentials: expectedCredentials,
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(getResult.Succeeded);
            Assert.True(postResult.Succeeded);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = requestCollector.GetMeasurementSnapshot();
            Assert.Equal(2, requestMeasurements.Count);
            foreach (var measurement in requestMeasurements)
            {
                Assert.True(measurement!.ContainsTags("http_method"));
                Assert.Contains(
                    measurement.Tags["http_method"],
                    expectedVerbs);
                Assert.True(measurement.ContainsTags("server"));
                Assert.Equal(
                    TestServerBuilder.DefaultUri.Host,
                    measurement.Tags["server"]);
            }

            IReadOnlyList<CollectedMeasurement<long>> responseMeasurements = responseCollector.GetMeasurementSnapshot();
            Assert.Equal(2, responseMeasurements.Count);
            foreach (var measurement in requestMeasurements)
            {
                Assert.True(measurement.ContainsTags("server"));
                Assert.Equal(
                    TestServerBuilder.DefaultUri.Host,
                    measurement.Tags["server"]);
            }

            IReadOnlyList<CollectedMeasurement<long>> successfulMeasurements = successfulCollector.GetMeasurementSnapshot();
            Assert.Equal(2, successfulMeasurements.Count);

            IReadOnlyList<CollectedMeasurement<long>> failureMeasurements = failureCollector.GetMeasurementSnapshot();
            Assert.Empty(failureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> dPoPMeasurements = dPoPRetriesCollector.GetMeasurementSnapshot();
            Assert.Empty(dPoPMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> deserializationFailureMeasurements = deserializationFailuresCollector.GetMeasurementSnapshot();
            Assert.Empty(deserializationFailureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> xrpcRequestMeasurements = xrpcRequestCollector.GetMeasurementSnapshot();
            Assert.Equal(2, xrpcRequestMeasurements.Count);
            for (int i = 0; i < xrpcRequestMeasurements.Count; i++)
            {
                CollectedMeasurement<long> measurement = xrpcRequestMeasurements[i];
                Assert.True(measurement.ContainsTags("server"));
                Assert.Equal(
                    TestServerBuilder.DefaultUri.Host,
                    measurement.Tags["server"]);

                Assert.True(measurement.ContainsTags("xrpc_endpoint"));
                Assert.Contains(
                    measurement.Tags["xrpc_endpoint"],
                    expectedXrpcEndpoints);

                Assert.True(measurement.ContainsTags("http_method"));
                Assert.Contains(
                    measurement.Tags["http_method"],
                    expectedVerbs);
            }

            foreach (var measurement in requestMeasurements)
            {
                Assert.True(measurement.ContainsTags("http_method"));
                Assert.Contains(
                    measurement.Tags["http_method"],
                    expectedVerbs);

                Assert.True(measurement.ContainsTags("server"));
                Assert.Equal(
                    TestServerBuilder.DefaultUri.Host,
                    measurement.Tags["server"]);

                Assert.True(measurement.ContainsTags("xrpc_endpoint"));
                Assert.Contains(
                    measurement.Tags["xrpc_endpoint"],
                    expectedXrpcEndpoints);
            }
        }

        [Fact]
        public async Task FailedGetIncrementsMetrics()
        {
            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var requestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total");
            var responseCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total");
            var successfulCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.successful");
            var failureCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.failure");
            var dPoPRetriesCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.dpop_retry");
            var deserializationFailuresCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total.deserialization_failure");
            var xrpcRequestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.xrpc_request");

            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Next();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = CreateTestServer(
                serverUri: TestServerBuilder.DefaultUri,
                expectedCredentials: expectedCredentials,
                expectedDid: expectedDid,
                expectedCollection: expectedCollection,
                expectedRecordKey: expectedRecordKey,
                expectedAtUri: expectedAtUri,
                expectedCid: expectedCid);

            AtProtoHttpClient atProtoHttpClient = new(serviceProxy: null, loggerFactory: null, meterFactory: meterFactory);
            AtProtoHttpResult<string> result = await atProtoHttpClient.Get(
                service: TestServerBuilder.DefaultUri,
                endpoint: $"{AtProtoServer.GetRecordEndpoint}?repo={expectedDid}&collection={expectedCollection}&rkey={TimestampIdentifier.Next()}",
                credentials: null,
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = requestCollector.GetMeasurementSnapshot();
            Assert.Single(requestMeasurements);
            Assert.True(requestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                requestMeasurements[0]!.Tags["http_method"]);
            Assert.True(requestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                requestMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> responseMeasurements = responseCollector.GetMeasurementSnapshot();
            Assert.Single(responseMeasurements);
            Assert.True(responseMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> successfulMeasurements = successfulCollector.GetMeasurementSnapshot();
            Assert.Empty(successfulMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> failureMeasurements = failureCollector.GetMeasurementSnapshot();
            Assert.Single(failureMeasurements);
            Assert.True(failureMeasurements[0]!.ContainsTags("http_status_code"));
            Assert.Equal(
                404,
                failureMeasurements[0]!.Tags["http_status_code"]);
            Assert.True(failureMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                failureMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> dPoPMeasurements = dPoPRetriesCollector.GetMeasurementSnapshot();
            Assert.Empty(dPoPMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> deserializationFailureMeasurements = deserializationFailuresCollector.GetMeasurementSnapshot();
            Assert.Empty(deserializationFailureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> xrpcRequestMeasurements = xrpcRequestCollector.GetMeasurementSnapshot();
            Assert.Single(xrpcRequestMeasurements);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.getRecord",
                xrpcRequestMeasurements[0]!.Tags["xrpc_endpoint"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                xrpcRequestMeasurements[0]!.Tags["server"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                xrpcRequestMeasurements[0]!.Tags["http_method"]);
        }

        [Fact]
        public async Task DPoPFailAndRetryIncrementsMetrics()
        {
            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var requestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total");
            var responseCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total");
            var successfulCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.successful");
            var failureCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.failure");
            var dPoPRetriesCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.dpop_retry");
            var deserializationFailuresCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total.deserialization_failure");
            var xrpcRequestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.xrpc_request");

            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Next();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            string dPoProofKey = JsonWebKeys.CreateRsaJson();

            DPoPAccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken",
                    dPoPProofKey: dPoProofKey,
                    dPoPNonce: "initialNonce");

            TestServer testServer = CreateTestServer(
                serverUri: TestServerBuilder.DefaultUri,
                expectedCredentials: expectedCredentials,
                expectedDid: expectedDid,
                expectedCollection: expectedCollection,
                expectedRecordKey: expectedRecordKey,
                expectedAtUri: expectedAtUri,
                expectedCid: expectedCid,
                triggerDPoPRetry: true);

            AtProtoHttpClient atProtoHttpClient = new(serviceProxy: null, loggerFactory: null, meterFactory: meterFactory);
            AtProtoHttpResult<string> result = await atProtoHttpClient.Get(
                service: TestServerBuilder.DefaultUri,
                endpoint: $"{AtProtoServer.GetRecordEndpoint}?repo={expectedDid}&collection={expectedCollection}&rkey={expectedRecordKey}",
                credentials: expectedCredentials,
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = requestCollector.GetMeasurementSnapshot();
            Assert.Equal(2, requestMeasurements.Count);
            Assert.True(requestMeasurements[0].ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                requestMeasurements[0].Tags["http_method"]);
            Assert.True(requestMeasurements[0].ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                requestMeasurements[0].Tags["server"]);

            Assert.True(requestMeasurements[1].ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                requestMeasurements[1].Tags["http_method"]);
            Assert.True(requestMeasurements[1].ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                requestMeasurements[1].Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> responseMeasurements = responseCollector.GetMeasurementSnapshot();
            Assert.Equal(2, responseMeasurements.Count);
            Assert.True(responseMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);
            Assert.True(responseMeasurements[1]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[1]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> successfulMeasurements = successfulCollector.GetMeasurementSnapshot();
            Assert.Single(successfulMeasurements);
            Assert.True(responseMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> failureMeasurements = failureCollector.GetMeasurementSnapshot();
            Assert.Single(failureMeasurements);
            Assert.True(responseMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> dPoPMeasurements = dPoPRetriesCollector.GetMeasurementSnapshot();
            Assert.Single(dPoPMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> deserializationFailureMeasurements = deserializationFailuresCollector.GetMeasurementSnapshot();
            Assert.Empty(deserializationFailureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> xrpcRequestMeasurements = xrpcRequestCollector.GetMeasurementSnapshot();
            Assert.Equal(2, xrpcRequestMeasurements.Count);
            Assert.True(xrpcRequestMeasurements[0].ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.getRecord",
                xrpcRequestMeasurements[0]!.Tags["xrpc_endpoint"]);
            Assert.True(xrpcRequestMeasurements[0].ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                xrpcRequestMeasurements[0]!.Tags["server"]);
            Assert.True(xrpcRequestMeasurements[0].ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                xrpcRequestMeasurements[0]!.Tags["http_method"]);

            Assert.True(xrpcRequestMeasurements[1].ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.getRecord",
                xrpcRequestMeasurements[1].Tags["xrpc_endpoint"]);
            Assert.True(xrpcRequestMeasurements[1].ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                xrpcRequestMeasurements[1].Tags["server"]);
            Assert.True(xrpcRequestMeasurements[1].ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                xrpcRequestMeasurements[1].Tags["http_method"]);
        }

        [Fact]
        public async Task UploadBlobIncrementsMetrics()
        {
            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var requestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total");
            var responseCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total");
            var successfulCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.successful");
            var failureCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.failure");
            var dPoPRetriesCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.dpop_retry");
            var deserializationFailuresCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total.deserialization_failure");
            var xrpcRequestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.xrpc_request");

            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Next();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = CreateTestServer(
                serverUri: TestServerBuilder.DefaultUri,
                expectedCredentials: expectedCredentials,
                expectedDid: expectedDid,
                expectedCollection: expectedCollection,
                expectedRecordKey: expectedRecordKey,
                expectedAtUri: expectedAtUri,
                expectedCid: expectedCid);

            AtProtoHttpClient<CreateBlobResponse> atProtoHttpClient = new(
                serviceProxy: null,
                requestHeaders: null,
                loggerFactory: null,
                meterFactory: meterFactory);

            Random rnd = new();
            byte[] blobContent = new byte[1024];
            rnd.NextBytes(blobContent);

            AtProtoHttpResult<CreateBlobResponse> result = await atProtoHttpClient.PostBlob(
                service: TestServerBuilder.DefaultUri,
                endpoint: AtProtoServer.UploadBlobEndpoint,
                blob: blobContent,
                requestHeaders: null,
                contentHeaders: null,
                credentials: expectedCredentials,
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = requestCollector.GetMeasurementSnapshot();
            Assert.Single(requestMeasurements);
            Assert.Single(requestMeasurements);
            Assert.True(requestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "POST",
                requestMeasurements[0]!.Tags["http_method"]);
            Assert.True(requestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                requestMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> responseMeasurements = responseCollector.GetMeasurementSnapshot();
            Assert.Single(responseMeasurements);
            Assert.True(responseMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> successfulMeasurements = successfulCollector.GetMeasurementSnapshot();
            Assert.Single(successfulMeasurements);
            Assert.True(successfulMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                successfulMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> failureMeasurements = failureCollector.GetMeasurementSnapshot();
            Assert.Empty(failureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> dPoPMeasurements = dPoPRetriesCollector.GetMeasurementSnapshot();
            Assert.Empty(dPoPMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> deserializationFailureMeasurements = deserializationFailuresCollector.GetMeasurementSnapshot();
            Assert.Empty(deserializationFailureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> xrpcRequestMeasurements = xrpcRequestCollector.GetMeasurementSnapshot();
            Assert.Single(xrpcRequestMeasurements);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.uploadBlob",
                xrpcRequestMeasurements[0]!.Tags["xrpc_endpoint"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                xrpcRequestMeasurements[0]!.Tags["server"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "POST",
                xrpcRequestMeasurements[0]!.Tags["http_method"]);
        }

        [Fact]
        public async Task SerializationFailureGetRecordRequestIncrementsMetrics()
        {
            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var requestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total");
            var responseCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total");
            var successfulCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.successful");
            var failureCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.failure");
            var dPoPRetriesCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.dpop_retry");
            var deserializationFailuresCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total.deserialization_failure");
            var xrpcRequestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.xrpc_request");

            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Next();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = CreateTestServer(
                serverUri: TestServerBuilder.DefaultUri,
                expectedCredentials: expectedCredentials,
                expectedDid: expectedDid,
                expectedCollection: expectedCollection,
                expectedRecordKey: expectedRecordKey,
                expectedAtUri: expectedAtUri,
                expectedCid: expectedCid,
                returnBadGetResult: true);

            AtProtoHttpClient<TestRecord> atProtoHttpClient = new(
                serviceProxy: null,
                requestHeaders: null,
                loggerFactory: null,
                meterFactory: meterFactory);

            AtProtoHttpResult<TestRecord> result = await atProtoHttpClient.Get(
                service: TestServerBuilder.DefaultUri,
                endpoint: $"{AtProtoServer.GetRecordEndpoint}?repo={expectedDid}&collection={expectedCollection}&rkey={expectedRecordKey}",
                credentials: null,
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = requestCollector.GetMeasurementSnapshot();
            Assert.Single(requestMeasurements);
            Assert.True(requestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                requestMeasurements[0]!.Tags["http_method"]);
            Assert.True(requestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                requestMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> responseMeasurements = responseCollector.GetMeasurementSnapshot();
            Assert.Single(responseMeasurements);
            Assert.True(responseMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> successfulMeasurements = successfulCollector.GetMeasurementSnapshot();
            Assert.Single(successfulMeasurements);
            Assert.True(successfulMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                successfulMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> failureMeasurements = failureCollector.GetMeasurementSnapshot();
            Assert.Empty(failureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> dPoPMeasurements = dPoPRetriesCollector.GetMeasurementSnapshot();
            Assert.Empty(dPoPMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> deserializationFailureMeasurements = deserializationFailuresCollector.GetMeasurementSnapshot();
            Assert.Single(deserializationFailureMeasurements);
            Assert.True(deserializationFailureMeasurements[0]!.ContainsTags("type"));
            Assert.Equal(
                "idunno.AtProto.Integration.Test.TestRecord",
                deserializationFailureMeasurements[0]!.Tags["type"]);
            Assert.True(deserializationFailureMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                deserializationFailureMeasurements[0]!.Tags["server"]);
            Assert.True(deserializationFailureMeasurements[0]!.ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.getRecord",
                deserializationFailureMeasurements[0]!.Tags["xrpc_endpoint"]);
            Assert.True(deserializationFailureMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                deserializationFailureMeasurements[0]!.Tags["http_method"]);

            IReadOnlyList<CollectedMeasurement<long>> xrpcRequestMeasurements = xrpcRequestCollector.GetMeasurementSnapshot();
            Assert.Single(xrpcRequestMeasurements);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.getRecord",
                xrpcRequestMeasurements[0]!.Tags["xrpc_endpoint"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                xrpcRequestMeasurements[0]!.Tags["server"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                xrpcRequestMeasurements[0]!.Tags["http_method"]);
        }

        [Fact]
        public async Task CreateRequestWithSerializerOptionsIncrementsIncrementsMetrics()
        {
            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var requestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total");
            var responseCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total");
            var successfulCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.successful");
            var failureCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.failure");
            var dPoPRetriesCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.dpop_retry");
            var deserializationFailuresCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total.deserialization_failure");
            var xrpcRequestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.xrpc_request");

            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Next();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = CreateTestServer(
                serverUri: TestServerBuilder.DefaultUri,
                expectedCredentials: expectedCredentials,
                expectedDid: expectedDid,
                expectedCollection: expectedCollection,
                expectedRecordKey: expectedRecordKey,
                expectedAtUri: expectedAtUri,
                expectedCid: expectedCid);

            JsonNode record = JsonSerializer.SerializeToNode(new TestRecord { TestValue = "test" })!;
            var createRequest = new CreateRecordRequest(
                 record: record,
                 repo: expectedDid,
                 collection: expectedCollection,
                 rKey: expectedRecordKey);

            AtProtoHttpClient<string> atProtoHttpClient = new(serviceProxy: null, requestHeaders: null, loggerFactory: null, meterFactory: meterFactory);
            AtProtoHttpResult<string> result = await atProtoHttpClient.Post(
                service: TestServerBuilder.DefaultUri,
                endpoint: AtProtoServer.CreateRecordEndpoint,
                record: createRequest,
                credentials: expectedCredentials,
                httpClient: testServer.CreateClient(),
                jsonSerializerOptions: AtProtoServer.AtProtoJsonSerializerOptions,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = requestCollector.GetMeasurementSnapshot();
            Assert.Single(requestMeasurements);
            Assert.True(requestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "POST",
                requestMeasurements[0]!.Tags["http_method"]);
            Assert.True(requestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                requestMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> responseMeasurements = responseCollector.GetMeasurementSnapshot();
            Assert.Single(responseMeasurements);
            Assert.True(responseMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> successfulMeasurements = successfulCollector.GetMeasurementSnapshot();
            Assert.Single(successfulMeasurements);
            Assert.True(successfulMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                successfulMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> failureMeasurements = failureCollector.GetMeasurementSnapshot();
            Assert.Empty(failureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> dPoPMeasurements = dPoPRetriesCollector.GetMeasurementSnapshot();
            Assert.Empty(dPoPMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> deserializationFailureMeasurements = deserializationFailuresCollector.GetMeasurementSnapshot();
            Assert.Empty(deserializationFailureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> xrpcRequestMeasurements = xrpcRequestCollector.GetMeasurementSnapshot();
            Assert.Single(xrpcRequestMeasurements);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.createRecord",
                xrpcRequestMeasurements[0]!.Tags["xrpc_endpoint"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                xrpcRequestMeasurements[0]!.Tags["server"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "POST",
                xrpcRequestMeasurements[0]!.Tags["http_method"]);
        }

        [Fact]
        public async Task GetRecordRequestWithSerializerOptionsIncrementsMetrics()
        {
            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var requestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total");
            var responseCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total");
            var successfulCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.successful");
            var failureCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.failure");
            var dPoPRetriesCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.dpop_retry");
            var deserializationFailuresCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total.deserialization_failure");
            var xrpcRequestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.xrpc_request");

            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Next();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = CreateTestServer(
                serverUri: TestServerBuilder.DefaultUri,
                expectedCredentials: expectedCredentials,
                expectedDid: expectedDid,
                expectedCollection: expectedCollection,
                expectedRecordKey: expectedRecordKey,
                expectedAtUri: expectedAtUri,
                expectedCid: expectedCid);

            AtProtoHttpClient<AtProtoRepositoryRecord<TestRecord>> atProtoHttpClient = new(serviceProxy: null, requestHeaders: null, loggerFactory: null, meterFactory: meterFactory);
            AtProtoHttpResult<AtProtoRepositoryRecord<TestRecord>> result = await atProtoHttpClient.Get(
                service: TestServerBuilder.DefaultUri,
                endpoint: $"{AtProtoServer.GetRecordEndpoint}?repo={expectedDid}&collection={expectedCollection}&rkey={expectedRecordKey}",
                credentials: null,
                httpClient: testServer.CreateClient(),
                jsonSerializerOptions: SourceGenerationContext.Default.Options,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = requestCollector.GetMeasurementSnapshot();
            Assert.Single(requestMeasurements);
            Assert.True(requestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                requestMeasurements[0]!.Tags["http_method"]);
            Assert.True(requestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                requestMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> responseMeasurements = responseCollector.GetMeasurementSnapshot();
            Assert.Single(responseMeasurements);
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> successfulMeasurements = successfulCollector.GetMeasurementSnapshot();
            Assert.Single(successfulMeasurements);
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                successfulMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> failureMeasurements = failureCollector.GetMeasurementSnapshot();
            Assert.Empty(failureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> dPoPMeasurements = dPoPRetriesCollector.GetMeasurementSnapshot();
            Assert.Empty(dPoPMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> deserializationFailureMeasurements = deserializationFailuresCollector.GetMeasurementSnapshot();
            Assert.Empty(deserializationFailureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> xrpcRequestMeasurements = xrpcRequestCollector.GetMeasurementSnapshot();
            Assert.Single(xrpcRequestMeasurements);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.getRecord",
                xrpcRequestMeasurements[0]!.Tags["xrpc_endpoint"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                xrpcRequestMeasurements[0]!.Tags["server"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                xrpcRequestMeasurements[0]!.Tags["http_method"]);
        }

        [Fact]
        public async Task GetRecordFollowedByCreateRecordWithSerializerOptionsIncrementsMetrics()
        {
            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var requestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total");
            var responseCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total");
            var successfulCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.successful");
            var failureCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.failure");
            var dPoPRetriesCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.dpop_retry");
            var deserializationFailuresCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total.deserialization_failure");
            var xrpcRequestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.xrpc_request");

            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Next();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
            string[] expectedVerbs = ["GET", "POST"];
            string[] expectedXrpcEndpoints = ["com.atproto.repo.getRecord", "com.atproto.repo.createRecord"];

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = CreateTestServer(
                serverUri: TestServerBuilder.DefaultUri,
                expectedCredentials: expectedCredentials,
                expectedDid: expectedDid,
                expectedCollection: expectedCollection,
                expectedRecordKey: expectedRecordKey,
                expectedAtUri: expectedAtUri,
                expectedCid: expectedCid);

            AtProtoHttpClient<AtProtoRepositoryRecord<TestRecord>> getOperationHttpClient = new(serviceProxy: null, requestHeaders: null, loggerFactory: null, meterFactory: meterFactory);
            AtProtoHttpResult<AtProtoRepositoryRecord<TestRecord>> getResult = await getOperationHttpClient.Get(
                service: TestServerBuilder.DefaultUri,
                endpoint: $"{AtProtoServer.GetRecordEndpoint}?repo={expectedDid}&collection={expectedCollection}&rkey={expectedRecordKey}",
                credentials: null,
                httpClient: testServer.CreateClient(),
                jsonSerializerOptions: SourceGenerationContext.Default.Options,
                cancellationToken: TestContext.Current.CancellationToken);

            JsonNode record = JsonSerializer.SerializeToNode(new TestRecord { TestValue = "test" })!;
            var createRequest = new CreateRecordRequest(
                 record: record,
                 repo: expectedDid,
                 collection: expectedCollection,
                 rKey: expectedRecordKey);

            AtProtoHttpClient<CreateRecordResponse> createRecordHttpClient = new(serviceProxy: null, requestHeaders: null, loggerFactory: null, meterFactory: meterFactory);

            AtProtoHttpResult<CreateRecordResponse> postResult = await createRecordHttpClient.Post(
                record: createRequest,
                service: TestServerBuilder.DefaultUri,
                endpoint: AtProtoServer.CreateRecordEndpoint,
                credentials: expectedCredentials,
                httpClient: testServer.CreateClient(),
                jsonSerializerOptions: AtProtoServer.AtProtoJsonSerializerOptions,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(getResult.Succeeded);
            Assert.True(postResult.Succeeded);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = requestCollector.GetMeasurementSnapshot();
            Assert.Equal(2, requestMeasurements.Count);
            foreach (var measurement in requestMeasurements)
            {
                Assert.True(measurement!.ContainsTags("http_method"));
                Assert.Contains(
                    measurement.Tags["http_method"],
                    expectedVerbs);
                Assert.True(measurement.ContainsTags("server"));
                Assert.Equal(
                    TestServerBuilder.DefaultUri.Host,
                    measurement.Tags["server"]);
            }

            IReadOnlyList<CollectedMeasurement<long>> responseMeasurements = responseCollector.GetMeasurementSnapshot();
            Assert.Equal(2, responseMeasurements.Count);
            foreach (var measurement in requestMeasurements)
            {
                Assert.True(measurement.ContainsTags("server"));
                Assert.Equal(
                    TestServerBuilder.DefaultUri.Host,
                    measurement.Tags["server"]);
            }

            IReadOnlyList<CollectedMeasurement<long>> successfulMeasurements = successfulCollector.GetMeasurementSnapshot();
            Assert.Equal(2, successfulMeasurements.Count);

            IReadOnlyList<CollectedMeasurement<long>> failureMeasurements = failureCollector.GetMeasurementSnapshot();
            Assert.Empty(failureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> dPoPMeasurements = dPoPRetriesCollector.GetMeasurementSnapshot();
            Assert.Empty(dPoPMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> deserializationFailureMeasurements = deserializationFailuresCollector.GetMeasurementSnapshot();
            Assert.Empty(deserializationFailureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> xrpcRequestMeasurements = xrpcRequestCollector.GetMeasurementSnapshot();
            Assert.Equal(2, xrpcRequestMeasurements.Count);
            for (int i = 0; i < xrpcRequestMeasurements.Count; i++)
            {
                CollectedMeasurement<long> measurement = xrpcRequestMeasurements[i];
                Assert.True(measurement.ContainsTags("server"));
                Assert.Equal(
                    TestServerBuilder.DefaultUri.Host,
                    measurement.Tags["server"]);

                Assert.True(measurement.ContainsTags("xrpc_endpoint"));
                Assert.Contains(
                    measurement.Tags["xrpc_endpoint"],
                    expectedXrpcEndpoints);

                Assert.True(measurement.ContainsTags("http_method"));
                Assert.Contains(
                    measurement.Tags["http_method"],
                    expectedVerbs);
            }
        }

        [Fact]
        public async Task FailedGetWithSerializerOptionsIncrementsMetrics()
        {
            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var requestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total");
            var responseCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total");
            var successfulCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.successful");
            var failureCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.failure");
            var dPoPRetriesCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.dpop_retry");
            var deserializationFailuresCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total.deserialization_failure");
            var xrpcRequestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.xrpc_request");

            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Next();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = CreateTestServer(
                serverUri: TestServerBuilder.DefaultUri,
                expectedCredentials: expectedCredentials,
                expectedDid: expectedDid,
                expectedCollection: expectedCollection,
                expectedRecordKey: expectedRecordKey,
                expectedAtUri: expectedAtUri,
                expectedCid: expectedCid);

            AtProtoHttpClient<AtProtoRepositoryRecord<TestRecord>> atProtoHttpClient = new(serviceProxy: null, requestHeaders: null, loggerFactory: null, meterFactory: meterFactory);
            AtProtoHttpResult<AtProtoRepositoryRecord<TestRecord>> result = await atProtoHttpClient.Get(
                service: TestServerBuilder.DefaultUri,
                endpoint: $"{AtProtoServer.GetRecordEndpoint}?repo={expectedDid}&collection={expectedCollection}&rkey={TimestampIdentifier.Next()}",
                credentials: null,
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = requestCollector.GetMeasurementSnapshot();
            Assert.Single(requestMeasurements);
            Assert.True(requestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                requestMeasurements[0]!.Tags["http_method"]);
            Assert.True(requestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                requestMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> responseMeasurements = responseCollector.GetMeasurementSnapshot();
            Assert.Single(responseMeasurements);
            Assert.True(responseMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> successfulMeasurements = successfulCollector.GetMeasurementSnapshot();
            Assert.Empty(successfulMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> failureMeasurements = failureCollector.GetMeasurementSnapshot();
            Assert.Single(failureMeasurements);
            Assert.True(failureMeasurements[0]!.ContainsTags("http_status_code"));
            Assert.Equal(
                404,
                failureMeasurements[0]!.Tags["http_status_code"]);
            Assert.True(failureMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                failureMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> dPoPMeasurements = dPoPRetriesCollector.GetMeasurementSnapshot();
            Assert.Empty(dPoPMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> deserializationFailureMeasurements = deserializationFailuresCollector.GetMeasurementSnapshot();
            Assert.Empty(deserializationFailureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> xrpcRequestMeasurements = xrpcRequestCollector.GetMeasurementSnapshot();
            Assert.Single(xrpcRequestMeasurements);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.getRecord",
                xrpcRequestMeasurements[0]!.Tags["xrpc_endpoint"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                xrpcRequestMeasurements[0]!.Tags["server"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                xrpcRequestMeasurements[0]!.Tags["http_method"]);
        }

        [Fact]
        public async Task DPoPFailAndRetryWithSerializerOptionsIncrementsMetrics()
        {
            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var requestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total");
            var responseCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total");
            var successfulCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.successful");
            var failureCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.failure");
            var dPoPRetriesCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.dpop_retry");
            var deserializationFailuresCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total.deserialization_failure");
            var xrpcRequestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.xrpc_request");

            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Next();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            string dPoProofKey = JsonWebKeys.CreateRsaJson();

            DPoPAccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken",
                    dPoPProofKey: dPoProofKey,
                    dPoPNonce: "initialNonce");

            TestServer testServer = CreateTestServer(
                serverUri: TestServerBuilder.DefaultUri,
                expectedCredentials: expectedCredentials,
                expectedDid: expectedDid,
                expectedCollection: expectedCollection,
                expectedRecordKey: expectedRecordKey,
                expectedAtUri: expectedAtUri,
                expectedCid: expectedCid,
                triggerDPoPRetry: true);

            AtProtoHttpClient<AtProtoRepositoryRecord<TestRecord>> atProtoHttpClient = new(serviceProxy: null, requestHeaders: null, loggerFactory: null, meterFactory: meterFactory);
            AtProtoHttpResult<AtProtoRepositoryRecord<TestRecord>> result = await atProtoHttpClient.Get(
                service: TestServerBuilder.DefaultUri,
                endpoint: $"{AtProtoServer.GetRecordEndpoint}?repo={expectedDid}&collection={expectedCollection}&rkey={expectedRecordKey}",
                credentials: expectedCredentials,
                httpClient: testServer.CreateClient(),
                jsonSerializerOptions: SourceGenerationContext.Default.Options,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = requestCollector.GetMeasurementSnapshot();
            Assert.Equal(2, requestMeasurements.Count);
            Assert.True(requestMeasurements[0].ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                requestMeasurements[0].Tags["http_method"]);
            Assert.True(requestMeasurements[0].ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                requestMeasurements[0].Tags["server"]);

            Assert.True(requestMeasurements[1].ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                requestMeasurements[1].Tags["http_method"]);
            Assert.True(requestMeasurements[1].ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                requestMeasurements[1].Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> responseMeasurements = responseCollector.GetMeasurementSnapshot();
            Assert.Equal(2, responseMeasurements.Count);
            Assert.True(responseMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);
            Assert.True(responseMeasurements[1]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[1]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> successfulMeasurements = successfulCollector.GetMeasurementSnapshot();
            Assert.Single(successfulMeasurements);
            Assert.True(responseMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> failureMeasurements = failureCollector.GetMeasurementSnapshot();
            Assert.Single(failureMeasurements);
            Assert.True(responseMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> dPoPMeasurements = dPoPRetriesCollector.GetMeasurementSnapshot();
            Assert.Single(dPoPMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> deserializationFailureMeasurements = deserializationFailuresCollector.GetMeasurementSnapshot();
            Assert.Empty(deserializationFailureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> xrpcRequestMeasurements = xrpcRequestCollector.GetMeasurementSnapshot();
            Assert.Equal(2, xrpcRequestMeasurements.Count);
            Assert.True(xrpcRequestMeasurements[0].ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.getRecord",
                xrpcRequestMeasurements[0]!.Tags["xrpc_endpoint"]);
            Assert.True(xrpcRequestMeasurements[0].ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                xrpcRequestMeasurements[0]!.Tags["server"]);
            Assert.True(xrpcRequestMeasurements[0].ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                xrpcRequestMeasurements[0]!.Tags["http_method"]);

            Assert.True(xrpcRequestMeasurements[1].ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.getRecord",
                xrpcRequestMeasurements[1].Tags["xrpc_endpoint"]);
            Assert.True(xrpcRequestMeasurements[1].ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                xrpcRequestMeasurements[1].Tags["server"]);
            Assert.True(xrpcRequestMeasurements[1].ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                xrpcRequestMeasurements[1].Tags["http_method"]);
        }

        [Fact]
        public async Task SerializationFailureGetRecordRequestWithSerializerOptionsIncrementsMetrics()
        {
            IServiceProvider services = CreateServiceProvider();
            IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
            var requestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total");
            var responseCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total");
            var successfulCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.successful");
            var failureCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.failure");
            var dPoPRetriesCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.dpop_retry");
            var deserializationFailuresCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.responses.total.deserialization_failure");
            var xrpcRequestCollector = new MetricCollector<long>(meterFactory, AtProtoHttpClientMetrics.MeterName, "idunno.atproto.atprotohttpclient.requests.total.xrpc_request");

            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Next();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = CreateTestServer(
                serverUri: TestServerBuilder.DefaultUri,
                expectedCredentials: expectedCredentials,
                expectedDid: expectedDid,
                expectedCollection: expectedCollection,
                expectedRecordKey: expectedRecordKey,
                expectedAtUri: expectedAtUri,
                expectedCid: expectedCid,
                returnBadGetResult: true);

            AtProtoHttpClient<AtProtoRepositoryRecord<TestRecord>> atProtoHttpClient = new(
                serviceProxy: null,
                requestHeaders: null,
                loggerFactory: null,
                meterFactory: meterFactory);

            AtProtoHttpResult<AtProtoRepositoryRecord<TestRecord>> result = await atProtoHttpClient.Get(
                service: TestServerBuilder.DefaultUri,
                endpoint: $"{AtProtoServer.GetRecordEndpoint}?repo={expectedDid}&collection={expectedCollection}&rkey={expectedRecordKey}",
                credentials: null,
                httpClient: testServer.CreateClient(),
                jsonSerializerOptions: SourceGenerationContext.Default.Options,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);

            IReadOnlyList<CollectedMeasurement<long>> requestMeasurements = requestCollector.GetMeasurementSnapshot();
            Assert.Single(requestMeasurements);
            Assert.True(requestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                requestMeasurements[0]!.Tags["http_method"]);
            Assert.True(requestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                requestMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> responseMeasurements = responseCollector.GetMeasurementSnapshot();
            Assert.Single(responseMeasurements);
            Assert.True(responseMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                responseMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> successfulMeasurements = successfulCollector.GetMeasurementSnapshot();
            Assert.Single(successfulMeasurements);
            Assert.True(successfulMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                successfulMeasurements[0]!.Tags["server"]);

            IReadOnlyList<CollectedMeasurement<long>> failureMeasurements = failureCollector.GetMeasurementSnapshot();
            Assert.Empty(failureMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> dPoPMeasurements = dPoPRetriesCollector.GetMeasurementSnapshot();
            Assert.Empty(dPoPMeasurements);

            IReadOnlyList<CollectedMeasurement<long>> deserializationFailureMeasurements = deserializationFailuresCollector.GetMeasurementSnapshot();
            Assert.Single(deserializationFailureMeasurements);
            Assert.True(deserializationFailureMeasurements[0]!.ContainsTags("type"));
            Assert.Equal(
                "idunno.AtProto.Repo.AtProtoRepositoryRecord`1[[idunno.AtProto.Integration.Test.TestRecord, idunno.AtProto.Integration.Test, Version=1.0.0.0, Culture=neutral, PublicKeyToken=5289fcbcf27e814d]]",
                deserializationFailureMeasurements[0]!.Tags["type"]);
            Assert.True(deserializationFailureMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                deserializationFailureMeasurements[0]!.Tags["server"]);
            Assert.True(deserializationFailureMeasurements[0]!.ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.getRecord",
                deserializationFailureMeasurements[0]!.Tags["xrpc_endpoint"]);
            Assert.True(deserializationFailureMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                deserializationFailureMeasurements[0]!.Tags["http_method"]);

            IReadOnlyList<CollectedMeasurement<long>> xrpcRequestMeasurements = xrpcRequestCollector.GetMeasurementSnapshot();
            Assert.Single(xrpcRequestMeasurements);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("xrpc_endpoint"));
            Assert.Equal(
                "com.atproto.repo.getRecord",
                xrpcRequestMeasurements[0]!.Tags["xrpc_endpoint"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("server"));
            Assert.Equal(
                TestServerBuilder.DefaultUri.Host,
                xrpcRequestMeasurements[0]!.Tags["server"]);
            Assert.True(xrpcRequestMeasurements[0]!.ContainsTags("http_method"));
            Assert.Equal(
                "GET",
                xrpcRequestMeasurements[0]!.Tags["http_method"]);
        }

        private static TestServer CreateTestServer(
            Uri serverUri,
            IAccessCredential expectedCredentials,
            Did expectedDid,
            Nsid expectedCollection,
            RecordKey expectedRecordKey,
            AtUri expectedAtUri,
            Cid expectedCid,
            bool triggerDPoPRetry = false,
            bool returnBadGetResult = false)
        {
            bool dPoPRotationSent = false;

            return TestServerBuilder.CreateServer(serverUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (triggerDPoPRetry && !dPoPRotationSent)
                {
                    if (request.Headers.Authorization.Count != 1)
                    {
                        response.StatusCode = 401;
                        return;
                    }

                    dPoPRotationSent = true;
                    response.StatusCode = 401;

                    AtErrorDetail errorDetail = new("use_dpop_nonce", null);
                    response.Headers.Append(
                        key: "DPoP-Nonce",
                        value: new StringValues("eyJ7S_zG.eyJH0-Z.HX4w-7v"));

                    await response.WriteAsJsonAsync(errorDetail);

                    return;
                }

                if (request.Path == AtProtoServer.CreateRecordEndpoint)
                {
                    if (request.Headers.Authorization.Count != 1)
                    {
                        response.StatusCode = 401;
                        return;
                    }

                    if (request.Headers.Authorization.ToString() != $"Bearer {expectedCredentials.AccessJwt}")
                    {
                        response.StatusCode = 403;
                        return;
                    }

                    JsonDocument? bodyAsJson = await JsonSerializer.DeserializeAsync<JsonDocument>(request.Body);

                    if (bodyAsJson is null || !bodyAsJson.RootElement.TryGetProperty("record", out _))
                    {
                        response.StatusCode = 400;
                        return;
                    }

                    if (!bodyAsJson.RootElement.TryGetProperty("repo", out JsonElement repo) || repo.GetString() != expectedDid.ToString())
                    {
                        response.StatusCode = 500;
                        return;
                    }

                    if (!bodyAsJson.RootElement.TryGetProperty("rkey", out JsonElement rkey) || rkey.GetString() != expectedRecordKey.ToString())
                    {
                        response.StatusCode = 500;
                        return;
                    }

                    response.StatusCode = 200;
                    var createRecordResponse = new CreateRecordResponse(expectedAtUri, expectedCid)
                    {
                        Commit = new(expectedCid, "revision"),
                        ValidationStatus = "valid"
                    };

                    await response.WriteAsJsonAsync(createRecordResponse);
                    return;
                }
                else if (request.Path == AtProtoServer.GetRecordEndpoint)
                {
                    string? repo = request.Query["repo"].FirstOrDefault();
                    string? collection = request.Query["collection"].FirstOrDefault();
                    string? rkey = request.Query["rkey"].FirstOrDefault();

                    if (repo is null || collection is null || rkey is null)
                    {
                        response.StatusCode = 400;
                        return;
                    }
                    else if (repo != expectedDid || collection != expectedCollection || rkey != expectedRecordKey)
                    {
                        response.StatusCode = 404;
                    }
                    else
                    {
                        response.StatusCode = 200;

                        if (!returnBadGetResult)
                        {
                            var record = new TestRecord { TestValue = "test" };
                            var getRecordResponse = new AtProtoRepositoryRecord<TestRecord>(
                                uri: new AtUri($"at://{repo}/{collection}/{rkey}"),
                                cid: expectedCid,
                                value: record);
                            await response.WriteAsJsonAsync(getRecordResponse, options: SourceGenerationContext.Default.Options);
                        }
                        else
                        {
                            JsonObject badObject = new()
                            {
                                ["unexpected"] = "value"
                            };

                            var getRecordResponse = new AtProtoRepositoryRecord(
                                uri: new AtUri($"at://{repo}/{collection}/{rkey}"),
                                cid: expectedCid,
                                value: badObject);

                            await response.WriteAsJsonAsync(getRecordResponse);
                        }

                        return;
                    }
                }
                else if (request.Path == AtProtoServer.UploadBlobEndpoint)
                {
                    response.StatusCode = 200;

                    var blob = new Blob(
                        reference: new BlobReference(expectedCid),
                        mimeType: "text/plain",
                        size: 1234);

                    var uploadBlobResponse = new CreateBlobResponse(blob);

                    await response.WriteAsJsonAsync(uploadBlobResponse);
                    return;
                }
                else
                {
                    response.StatusCode = 404;
                    return;
                }
            });
        }

        private static ServiceProvider CreateServiceProvider()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddMetrics();
            serviceCollection.AddSingleton<AtProtoHttpClient>();
            return serviceCollection.BuildServiceProvider();
        }
    }
}
