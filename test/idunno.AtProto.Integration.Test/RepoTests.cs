// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;

namespace idunno.AtProto.Integration.Test
{
    [ExcludeFromCodeCoverage]
    public class RepoTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public RepoTests()
        {
            _jsonSerializerOptions = AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(JsonSerializerOptions.Default);
        }

        [Fact]
        public async Task DescribeRepoReturnsDescription()
        {
            const string domainName = "test.invalid";

            const string expectedHandle = "handle.test.invalid";
            const string expectedDid = "did:web:test.invalid";
            const bool expectedHandleIsCorrect = true;

            Nsid[] expectedCollections =
            [
                new Nsid("com.atproto.posts"),
                new Nsid("invalid.handle.beans"),
            ];
            DidDocument expectedDidDoc = new(
                id: $"{expectedDid}",
                context: ["https://www.w3.org/ns/did/v1"],
                alsoKnownAs: null,
                verificationMethods: null,
                services:
                [
                    new(
                        id : "#atproto_pds",
                        type : "atprotopds",
                        serviceEndpoint : new Uri($"https://{domainName}"))
                ]);

            TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.DescribeRepoEndpoint &&
                    request.QueryString.HasValue &&
                    request.Query["repo"].FirstOrDefault() == expectedHandle)
                {
                    response.StatusCode = 200;
                    response.ContentType = "text/plain";

                    var responseBody = new RepoDescription(handle: expectedHandle, did: expectedDid, didDoc: expectedDidDoc, collections: expectedCollections, handleIsCorrect: expectedHandleIsCorrect);

                    await response.WriteAsJsonAsync(value: responseBody, jsonTypeInfo: AtProto.SourceGenerationContext.Default.RepoDescription);
                }
            });

            using (var agent = new AtProtoAgent(
                new Uri($"https://{domainName}"),
                new TestHttpClientFactory(testServer)))
            {
                AtProtoHttpResult<RepoDescription> describeRepoResult = await agent.DescribeRepo(AtIdentifier.Create(expectedHandle), cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(describeRepoResult.Succeeded);

                Assert.Equal(expectedHandle, describeRepoResult.Result.Handle);
                Assert.Equal(expectedDid, describeRepoResult.Result.Did);
                Assert.Equal(expectedDidDoc.Id, describeRepoResult.Result.DidDoc.Id);
                Assert.Equal(expectedDidDoc.Context, describeRepoResult.Result.DidDoc.Context);
                Assert.Equal(expectedDidDoc.Services, describeRepoResult.Result.DidDoc.Services);
                Assert.Empty(expectedDidDoc.AlsoKnownAs);
                Assert.Empty(expectedDidDoc.VerificationMethods);
                Assert.NotEmpty(describeRepoResult.Result.DidDoc.Services);
                Assert.Equal(expectedCollections, describeRepoResult.Result.Collections);
                Assert.Equal(expectedHandleIsCorrect, describeRepoResult.Result.HandleIsCorrect);
            }
        }

        [Fact]
        public async Task NotFoundRepoOnDescribeRepoReturnsCorrectError()
        {
            const string domainName = "test.invalid";
            const string expectedHandle = "handle.test.invalid";

            TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.DescribeRepoEndpoint &&
                    request.QueryString.HasValue &&
                    request.Query["repo"].FirstOrDefault() == expectedHandle)
                {
                    response.StatusCode = 404;

                    AtErrorDetail errorDetail = new() { Error = "NotFound", Message = "Repo not found" };

                    await response.WriteAsJsonAsync(value: errorDetail, jsonTypeInfo: AtProto.SourceGenerationContext.Default.AtErrorDetail);
                }
            });

            using (var agent = new AtProtoAgent(
                new Uri($"https://{domainName}"),
                new TestHttpClientFactory(testServer)))
            {
                AtProtoHttpResult<RepoDescription> describeRepoResult = await agent.DescribeRepo(AtIdentifier.Create(expectedHandle), cancellationToken: TestContext.Current.CancellationToken);

                Assert.False(describeRepoResult.Succeeded);
                Assert.Equal(HttpStatusCode.NotFound, describeRepoResult.StatusCode);
                Assert.NotNull(describeRepoResult.AtErrorDetail);
                Assert.Equal("NotFound", describeRepoResult.AtErrorDetail.Error);
                Assert.Equal("Repo not found", describeRepoResult.AtErrorDetail.Message);
            }
        }

        [Fact]
        public async Task ServerCallToCreateRecordSerializesTheRecordWithNoConfiguredTypeResolverCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Generate();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            JsonElement capturedRecordValue = default ;

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

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

                    if (bodyAsJson is null || !bodyAsJson.RootElement.TryGetProperty("record", out capturedRecordValue))
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
                    var serverDescription = new CreateRecordResponse(expectedAtUri, expectedCid, new Commit(expectedCid, "revision"), "valid");
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });


            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            TestRecordValue recordValue = new() { TestValue = "test" };

            AtProtoHttpResult<CreateRecordResult> response = await AtProtoServer.CreateRecord(
                record: recordValue,
                collection: expectedCollection,
                creator: expectedDid,
                rKey: expectedRecordKey,
                validate: true,
                swapCommit : null,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: expectedCredentials,
                httpClient : httpClient,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(response.Succeeded);

            Assert.Equal(expectedAtUri, response.Result.Uri);
            Assert.Equal(expectedCid, response.Result.Cid);
            Assert.NotNull(response.Result.Commit);
            Assert.Equal(expectedCid, response.Result.Commit.Cid);
            Assert.Equal("revision", response.Result.Commit.Rev);

            Assert.Equal(ValidationStatus.Valid, response.Result.ValidationStatus);

            Assert.Equal("{\"testValue\":\"test\"}", capturedRecordValue.ToString());
        }

        [Fact]
        public async Task ServerCallToCreateRecordSerializesTheRecordWithConfiguredTypeResolverCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Generate();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            JsonElement capturedRecordValue = default;

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

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

                    if (bodyAsJson is null || !bodyAsJson.RootElement.TryGetProperty("record", out capturedRecordValue))
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
                    var serverDescription = new CreateRecordResponse(expectedAtUri, expectedCid, new Commit(expectedCid, "revision"), "valid");
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });


            JsonSerializerOptions jsonSerializerOptions = AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(SourceGenerationContext.Default);

            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            TestRecordValue recordValue = new() { TestValue = "test" };

            AtProtoHttpResult<CreateRecordResult> response = await AtProtoServer.CreateRecord(
                record: recordValue,
                collection: expectedCollection,
                creator: expectedDid,
                rKey: expectedRecordKey,
                validate: true,
                swapCommit: null,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: expectedCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(response.Succeeded);

            Assert.Equal(expectedAtUri, response.Result.Uri);
            Assert.Equal(expectedCid, response.Result.Cid);
            Assert.NotNull(response.Result.Commit);
            Assert.Equal(expectedCid, response.Result.Commit.Cid);
            Assert.Equal("revision", response.Result.Commit.Rev);

            Assert.Equal(ValidationStatus.Valid, response.Result.ValidationStatus);

            Assert.Equal("{\"testValue\":\"test\"}", capturedRecordValue.ToString());
        }

        [Fact]
        public async Task AgentCallToCreateRecordSerializesTheRecordWithNoConfiguredTypeResolverCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{TimestampIdentifier.Generate()}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            JsonElement capturedRecordValue = default;

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

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

                    if (bodyAsJson is null || !bodyAsJson.RootElement.TryGetProperty("record", out capturedRecordValue))
                    {
                        response.StatusCode = 400;
                        return;
                    }

                    if (!bodyAsJson.RootElement.TryGetProperty("repo", out JsonElement repo) || repo.GetString() != expectedDid.ToString())
                    {
                        response.StatusCode = 500;
                        return;
                    }

                    response.StatusCode = 200;
                    var serverDescription = new CreateRecordResponse(expectedAtUri, expectedCid, new Commit(expectedCid, "revision"), "valid");
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });

            using (var agent = new AtProtoAgent(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer)))
            {
                agent.Credentials = expectedCredentials;

                TestRecordValue recordValue = new() { TestValue = "test" };

                AtProtoHttpResult<CreateRecordResult> response = await agent.CreateRecord(
                    record: recordValue,
                    collection: expectedCollection,
                    validate: true,
                    swapCommit: null,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(response.Succeeded);

                Assert.Equal(expectedAtUri, response.Result.Uri);
                Assert.Equal(expectedCid, response.Result.Cid);
                Assert.NotNull(response.Result.Commit);
                Assert.Equal(expectedCid, response.Result.Commit.Cid);
                Assert.Equal("revision", response.Result.Commit.Rev);

                Assert.Equal(ValidationStatus.Valid, response.Result.ValidationStatus);

                Assert.Equal("{\"testValue\":\"test\"}", capturedRecordValue.ToString());
            }
        }

        [Fact]
        public async Task AgentCallToCreateRecordSerializesTheRecordWithConfiguredTypeResolverCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{TimestampIdentifier.Generate()}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            JsonElement capturedRecordValue = default;

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

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

                    if (bodyAsJson is null || !bodyAsJson.RootElement.TryGetProperty("record", out capturedRecordValue))
                    {
                        response.StatusCode = 400;
                        return;
                    }

                    if (!bodyAsJson.RootElement.TryGetProperty("repo", out JsonElement repo) || repo.GetString() != expectedDid.ToString())
                    {
                        response.StatusCode = 500;
                        return;
                    }

                    response.StatusCode = 200;
                    var serverDescription = new CreateRecordResponse(expectedAtUri, expectedCid, new Commit(expectedCid, "revision"), "valid");
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });

            JsonOptions httpJsonOptions = new();
            httpJsonOptions.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);

            using (var agent = new AtProtoAgent(
                service: TestServerBuilder.DefaultUri,
                httpClientFactory: new TestHttpClientFactory(testServer),
                options: new()
                {
                    HttpJsonOptions = httpJsonOptions
                }))
            {
                agent.Credentials = expectedCredentials;

                TestRecordValue recordValue = new() { TestValue = "test" };

                AtProtoHttpResult<CreateRecordResult> response = await agent.CreateRecord(
                    record: recordValue,
                    collection: expectedCollection,
                    validate: true,
                    swapCommit: null,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(response.Succeeded);

                Assert.Equal(expectedAtUri, response.Result.Uri);
                Assert.Equal(expectedCid, response.Result.Cid);
                Assert.NotNull(response.Result.Commit);
                Assert.Equal(expectedCid, response.Result.Commit.Cid);
                Assert.Equal("revision", response.Result.Commit.Rev);

                Assert.Equal(ValidationStatus.Valid, response.Result.ValidationStatus);

                Assert.Equal("{\"testValue\":\"test\"}", capturedRecordValue.ToString());
            }
        }

        [Fact]
        public async Task UnauthenticatedAgentCallToCreateRecordThrowsAuthenticationRequiredException()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{TimestampIdentifier.Generate()}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.CreateRecordEndpoint)
                {
                    response.StatusCode = 200;
                    var serverDescription = new CreateRecordResponse(expectedAtUri, expectedCid, new Commit(expectedCid, "revision"), "valid");
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });

            using (var agent = new AtProtoAgent(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer)))
            {
                TestRecordValue recordValue = new() { TestValue = "test" };

                await Assert.ThrowsAsync<AuthenticationRequiredException>(() => agent.CreateRecord(
                    record: recordValue,
                    collection: expectedCollection,
                    validate: true,
                    swapCommit: null,
                    cancellationToken: TestContext.Current.CancellationToken));
            }
        }

        [Fact]
        public async Task ServerCallToPutRecordValueWithNoConfiguredTypeResolverSerializesTheRecordValueCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Generate();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            JsonElement capturedRecordValue = default;

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.PutRecordEndpoint)
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

                    if (bodyAsJson is null || !bodyAsJson.RootElement.TryGetProperty("record", out capturedRecordValue))
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
                    var serverDescription = new CreateRecordResponse(expectedAtUri, expectedCid, new Commit(expectedCid, "revision"), "valid");
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });


            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            TestRecordValue recordValue = new() { TestValue = "test" };

            AtProtoHttpResult<PutRecordResult> response = await AtProtoServer.PutRecord(
                recordValue: recordValue,
                collection: expectedCollection,
                creator: expectedDid,
                rKey: expectedRecordKey,
                validate: true,
                swapCommit: null,
                swapRecord: null,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: expectedCredentials,
                httpClient: httpClient,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(response.Succeeded);

            Assert.Equal(expectedAtUri, response.Result.Uri);
            Assert.Equal(expectedCid, response.Result.Cid);
            Assert.NotNull(response.Result.Commit);
            Assert.Equal(expectedCid, response.Result.Commit.Cid);
            Assert.Equal("revision", response.Result.Commit.Rev);

            Assert.Equal(ValidationStatus.Valid, response.Result.ValidationStatus);

            Assert.Equal("{\"testValue\":\"test\"}", capturedRecordValue.ToString());
        }

        [Fact]
        public async Task ServerCallToPutRecordValueWithConfiguredTypeResolverSerializesTheRecordValueCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Generate();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            JsonElement capturedRecordValue = default;

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.PutRecordEndpoint)
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

                    if (bodyAsJson is null || !bodyAsJson.RootElement.TryGetProperty("record", out capturedRecordValue))
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
                    var serverDescription = new CreateRecordResponse(expectedAtUri, expectedCid, new Commit(expectedCid, "revision"), "valid");
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });


            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            JsonSerializerOptions jsonSerializerOptions = AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(SourceGenerationContext.Default);
            TestRecordValue recordValue = new() { TestValue = "test" };

            AtProtoHttpResult<PutRecordResult> response = await AtProtoServer.PutRecord(
                recordValue: recordValue,
                collection: expectedCollection,
                creator: expectedDid,
                rKey: expectedRecordKey,
                validate: true,
                swapCommit: null,
                swapRecord: null,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: expectedCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(response.Succeeded);

            Assert.Equal(expectedAtUri, response.Result.Uri);
            Assert.Equal(expectedCid, response.Result.Cid);
            Assert.NotNull(response.Result.Commit);
            Assert.Equal(expectedCid, response.Result.Commit.Cid);
            Assert.Equal("revision", response.Result.Commit.Rev);

            Assert.Equal(ValidationStatus.Valid, response.Result.ValidationStatus);

            Assert.Equal("{\"testValue\":\"test\"}", capturedRecordValue.ToString());
        }

        [Fact]
        public async Task AgentCallToPutRecordValueSerializesTheRecordValueWithNoConfiguredTypeResolverCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Generate();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            JsonElement capturedRecordValue = default;

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.PutRecordEndpoint)
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

                    if (bodyAsJson is null || !bodyAsJson.RootElement.TryGetProperty("record", out capturedRecordValue))
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
                    var serverDescription = new CreateRecordResponse(expectedAtUri, expectedCid, new Commit(expectedCid, "revision"), "valid");
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });

            using (var agent = new AtProtoAgent(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer)))
            {
                agent.Credentials = expectedCredentials;

                TestRecordValue recordValue = new() { TestValue = "test" };

                AtProtoHttpResult<PutRecordResult> response = await agent.PutRecord(
                    recordValue: recordValue,
                    collection: expectedCollection,
                    rKey: expectedRecordKey,
                    validate: true,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(response.Succeeded);

                Assert.Equal(expectedAtUri, response.Result.Uri);
                Assert.Equal(expectedCid, response.Result.Cid);
                Assert.NotNull(response.Result.Commit);
                Assert.Equal(expectedCid, response.Result.Commit.Cid);
                Assert.Equal("revision", response.Result.Commit.Rev);

                Assert.Equal(ValidationStatus.Valid, response.Result.ValidationStatus);

                Assert.Equal("{\"testValue\":\"test\"}", capturedRecordValue.ToString());
            }
        }

        [Fact]
        public async Task AgentCallToPutRecordValueSerializesTheRecordValueWithConfiguredTypeResolverCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Generate();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            JsonElement capturedRecordValue = default;

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.PutRecordEndpoint)
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

                    if (bodyAsJson is null || !bodyAsJson.RootElement.TryGetProperty("record", out capturedRecordValue))
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
                    var serverDescription = new CreateRecordResponse(expectedAtUri, expectedCid, new Commit(expectedCid, "revision"), "valid");
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });

            JsonOptions httpJsonOptions = new();
            httpJsonOptions.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);

            using (var agent = new AtProtoAgent(
                service: TestServerBuilder.DefaultUri,
                httpClientFactory: new TestHttpClientFactory(testServer),
                options: new()
                {
                    HttpJsonOptions = httpJsonOptions
                }))
            {
                agent.Credentials = expectedCredentials;

                TestRecordValue recordValue = new() { TestValue = "test" };

                AtProtoHttpResult<PutRecordResult> response = await agent.PutRecord(
                    recordValue: recordValue,
                    collection: expectedCollection,
                    rKey: expectedRecordKey,
                    validate: true,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(response.Succeeded);

                Assert.Equal(expectedAtUri, response.Result.Uri);
                Assert.Equal(expectedCid, response.Result.Cid);
                Assert.NotNull(response.Result.Commit);
                Assert.Equal(expectedCid, response.Result.Commit.Cid);
                Assert.Equal("revision", response.Result.Commit.Rev);

                Assert.Equal(ValidationStatus.Valid, response.Result.ValidationStatus);

                Assert.Equal("{\"testValue\":\"test\"}", capturedRecordValue.ToString());
            }
        }

        [Fact]
        public async Task ServerCallToPutTypedRecordSerializesTheRecordValueWithNoConfiguredTypeResolverCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Generate();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            JsonElement capturedRecordValue = default;

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.PutRecordEndpoint)
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

                    if (bodyAsJson is null || !bodyAsJson.RootElement.TryGetProperty("record", out capturedRecordValue))
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
                    var serverDescription = new CreateRecordResponse(expectedAtUri, expectedCid, new Commit(expectedCid, "revision"), "valid");
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });


            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            TestRecordValue recordValue = new() { TestValue = "test" };
            TestRecord record = new(expectedAtUri, expectedCid, recordValue);

            AtProtoHttpResult<PutRecordResult> response = await AtProtoServer.PutRecord(
                record: record,
                validate: true,
                swapCommit: null,
                swapRecord: null,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: expectedCredentials,
                httpClient: httpClient,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(response.Succeeded);

            Assert.Equal(expectedAtUri, response.Result.Uri);
            Assert.Equal(expectedCid, response.Result.Cid);
            Assert.NotNull(response.Result.Commit);
            Assert.Equal(expectedCid, response.Result.Commit.Cid);
            Assert.Equal("revision", response.Result.Commit.Rev);

            Assert.Equal(ValidationStatus.Valid, response.Result.ValidationStatus);

            Assert.Equal("{\"testValue\":\"test\"}", capturedRecordValue.ToString());
        }

        [Fact]
        public async Task ServerCallToPutTypedRecordSerializesTheRecordValueWithConfiguredTypeResolverCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Generate();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            JsonElement capturedRecordValue = default;

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.PutRecordEndpoint)
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

                    if (bodyAsJson is null || !bodyAsJson.RootElement.TryGetProperty("record", out capturedRecordValue))
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
                    var serverDescription = new CreateRecordResponse(expectedAtUri, expectedCid, new Commit(expectedCid, "revision"), "valid");
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });


            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            JsonSerializerOptions jsonSerializerOptions = AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(SourceGenerationContext.Default);

            TestRecordValue recordValue = new() { TestValue = "test" };
            TestRecord record = new(expectedAtUri, expectedCid, recordValue);

            AtProtoHttpResult<PutRecordResult> response = await AtProtoServer.PutRecord(
                record: record,
                validate: true,
                swapCommit: null,
                swapRecord: null,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: expectedCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(response.Succeeded);

            Assert.Equal(expectedAtUri, response.Result.Uri);
            Assert.Equal(expectedCid, response.Result.Cid);
            Assert.NotNull(response.Result.Commit);
            Assert.Equal(expectedCid, response.Result.Commit.Cid);
            Assert.Equal("revision", response.Result.Commit.Rev);

            Assert.Equal(ValidationStatus.Valid, response.Result.ValidationStatus);

            Assert.Equal("{\"testValue\":\"test\"}", capturedRecordValue.ToString());
        }

        [Fact]
        public async Task UnauthenticatedAgentCallToPutRecordThrowsAuthenticationRequiredException()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{TimestampIdentifier.Generate()}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.CreateRecordEndpoint)
                {
                    response.StatusCode = 200;
                    var serverDescription = new CreateRecordResponse(expectedAtUri, expectedCid, new Commit(expectedCid, "revision"), "valid");
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });

            using (var agent = new AtProtoAgent(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer)))
            {
                TestRecordValue recordValue = new() { TestValue = "test" };
                TestRecord record = new(expectedAtUri, expectedCid, recordValue);

                await Assert.ThrowsAsync<AuthenticationRequiredException>(() => agent.PutRecord(
                    record: record,
                    validate: true,
                    swapCommit: null,
                    cancellationToken: TestContext.Current.CancellationToken));
            }
        }

        [Fact]
        public async Task UnauthenticatedAgentCallToPutRecordValueThrowsAuthenticationRequiredException()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = TimestampIdentifier.Generate();
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.CreateRecordEndpoint)
                {
                    response.StatusCode = 200;
                    var serverDescription = new CreateRecordResponse(expectedAtUri, expectedCid, new Commit(expectedCid, "revision"), "valid");
                    await response.WriteAsJsonAsync(serverDescription);
                }
            });


            using (var agent = new AtProtoAgent(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer)))
            {
                TestRecordValue recordValue = new() { TestValue = "test" };
                TestRecord record = new(expectedAtUri, expectedCid, recordValue);

                await Assert.ThrowsAsync<AuthenticationRequiredException>(() => agent.PutRecord(
                    recordValue: recordValue,
                    collection: expectedCollection,
                    rKey: expectedRecordKey,
                    validate: true,
                    cancellationToken: TestContext.Current.CancellationToken));
            }
        }

        [Fact]
        public async Task ServerCallToListRecordsWithResolverChainDeserializesCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            const string jsonReturnValue = """
            {
              "cursor": "cursor",
              "records": [
                {
                  "uri": "at://did:plc:test/blue.idunno.test/rkey1",
                  "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
                  "value": {
                      "testValue": "1"
                  }
                },
                {
                  "uri": "at://did:plc:test/blue.idunno.test/rkey2",
                  "cid": "bafyreih3stxgsbceqcredadhol7tlhhpbpjcssqnbzwiukexkqh3mjmblu",
                  "value": {
                      "testValue": "2"
                  }
                }
              ]
            }
            """;

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.ListRecordsEndpoint)
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

                    if (!request.QueryString.HasValue)
                    {
                        response.StatusCode = 400;
                        return;
                    }

                    if (request.Query["repo"].ToString() != expectedDid)
                    {
                        response.StatusCode = 404;
                        return;
                    }

                    if (request.Query["collection"].ToString() != expectedCollection)
                    {
                        response.StatusCode = 404;
                        return;
                    }

                    response.StatusCode = 200;
                    response.Headers.ContentType = "application/json";
                    await response.WriteAsync(jsonReturnValue);
                    return;
                }
            });

            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRecord<TestRecordValue>>> response = await AtProtoServer.ListRecords<TestRecordValue>(
                repo: expectedDid,
                collection: expectedCollection,
                limit: 10,
                cursor: "cursor",
                reverse: true,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: expectedCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: _jsonSerializerOptions,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(response.Succeeded);

            Assert.Equal(2, response.Result.Count);
            Assert.Equal("cursor", response.Result.Cursor);

            Assert.Equal("at://did:plc:test/blue.idunno.test/rkey1", response.Result[0].Uri);
            Assert.Equal("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4", response.Result[0].Cid);
            Assert.NotNull(response.Result[0].Value);
            Assert.Equal("1", response.Result[0].Value.TestValue);
            Assert.Empty(response.Result[0].Value.ExtensionData);

            Assert.Equal("at://did:plc:test/blue.idunno.test/rkey2", response.Result[1].Uri);
            Assert.Equal("bafyreih3stxgsbceqcredadhol7tlhhpbpjcssqnbzwiukexkqh3mjmblu", response.Result[1].Cid);
            Assert.NotNull(response.Result[1].Value);
            Assert.Equal("2", response.Result[1].Value.TestValue);
            Assert.Empty(response.Result[1].Value.ExtensionData);
        }

        [Fact]
        public async Task AgentCallToListRecordsWithResolverChainConfiguredDeserializesCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            const string jsonReturnValue = """
            {
              "cursor": "cursor",
              "records": [
                {
                  "uri": "at://did:plc:test/blue.idunno.test/rkey1",
                  "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
                  "value": {
                      "testValue": "1"
                  }
                },
                {
                  "uri": "at://did:plc:test/blue.idunno.test/rkey2",
                  "cid": "bafyreih3stxgsbceqcredadhol7tlhhpbpjcssqnbzwiukexkqh3mjmblu",
                  "value": {
                      "testValue": "2"
                  }
                }
              ]
            }
            """;

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.ListRecordsEndpoint)
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

                    if (!request.QueryString.HasValue)
                    {
                        response.StatusCode = 400;
                        return;
                    }

                    if (request.Query["repo"].ToString() != expectedDid)
                    {
                        response.StatusCode = 404;
                        return;
                    }

                    if (request.Query["collection"].ToString() != expectedCollection)
                    {
                        response.StatusCode = 404;
                        return;
                    }

                    response.StatusCode = 200;
                    response.Headers.ContentType = "application/json";
                    await response.WriteAsync(jsonReturnValue);
                    return;
                }
            });

            JsonOptions httpJsonOptions = new();
            httpJsonOptions.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);

            using (var agent = new AtProtoAgent(
                service: TestServerBuilder.DefaultUri,
                httpClientFactory: new TestHttpClientFactory(testServer),
                options: new ()
                {
                    HttpJsonOptions = httpJsonOptions
                }))
            {
                agent.Credentials = expectedCredentials;

                AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRecord<TestRecordValue>>> response = await agent.ListRecords<TestRecordValue>(
                    collection: expectedCollection,
                    cursor: "cursor",
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(response.Succeeded);

                Assert.Equal(2, response.Result.Count);
                Assert.Equal("cursor", response.Result.Cursor);

                Assert.Equal("at://did:plc:test/blue.idunno.test/rkey1", response.Result[0].Uri);
                Assert.Equal("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4", response.Result[0].Cid);
                Assert.NotNull(response.Result[0].Value);
                Assert.Equal("1", response.Result[0].Value.TestValue);
                Assert.Empty(response.Result[0].Value.ExtensionData);

                Assert.Equal("at://did:plc:test/blue.idunno.test/rkey2", response.Result[1].Uri);
                Assert.Equal("bafyreih3stxgsbceqcredadhol7tlhhpbpjcssqnbzwiukexkqh3mjmblu", response.Result[1].Cid);
                Assert.NotNull(response.Result[1].Value);
                Assert.Equal("2", response.Result[1].Value.TestValue);
                Assert.Empty(response.Result[1].Value.ExtensionData);
            }
        }

        [Fact]
        public async Task AgentCallToListRecordsWithNoResolverChainConfiguredDeserializesCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            const string jsonReturnValue = """
            {
              "cursor": "cursor",
              "records": [
                {
                  "uri": "at://did:plc:test/blue.idunno.test/rkey1",
                  "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
                  "value": {
                      "testValue": "1"
                  }
                },
                {
                  "uri": "at://did:plc:test/blue.idunno.test/rkey2",
                  "cid": "bafyreih3stxgsbceqcredadhol7tlhhpbpjcssqnbzwiukexkqh3mjmblu",
                  "value": {
                      "testValue": "2"
                  }
                }
              ]
            }
            """;

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.ListRecordsEndpoint)
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

                    if (!request.QueryString.HasValue)
                    {
                        response.StatusCode = 400;
                        return;
                    }

                    if (request.Query["repo"].ToString() != expectedDid)
                    {
                        response.StatusCode = 404;
                        return;
                    }

                    if (request.Query["collection"].ToString() != expectedCollection)
                    {
                        response.StatusCode = 404;
                        return;
                    }

                    response.StatusCode = 200;
                    response.Headers.ContentType = "application/json";
                    await response.WriteAsync(jsonReturnValue);
                    return;
                }
            });

            using (var agent = new AtProtoAgent(
                service: TestServerBuilder.DefaultUri,
                httpClientFactory: new TestHttpClientFactory(testServer)))
            {
                agent.Credentials = expectedCredentials;

                AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRecord<TestRecordValue>>> response = await agent.ListRecords<TestRecordValue>(
                    collection: expectedCollection,
                    cursor: "cursor",
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(response.Succeeded);

                Assert.Equal(2, response.Result.Count);
                Assert.Equal("cursor", response.Result.Cursor);

                Assert.Equal("at://did:plc:test/blue.idunno.test/rkey1", response.Result[0].Uri);
                Assert.Equal("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4", response.Result[0].Cid);
                Assert.NotNull(response.Result[0].Value);
                Assert.Equal("1", response.Result[0].Value.TestValue);
                Assert.Empty(response.Result[0].Value.ExtensionData);

                Assert.Equal("at://did:plc:test/blue.idunno.test/rkey2", response.Result[1].Uri);
                Assert.Equal("bafyreih3stxgsbceqcredadhol7tlhhpbpjcssqnbzwiukexkqh3mjmblu", response.Result[1].Cid);
                Assert.NotNull(response.Result[1].Value);
                Assert.Equal("2", response.Result[1].Value.TestValue);
                Assert.Empty(response.Result[1].Value.ExtensionData);
            }
        }

        [Fact]
        public async Task ServerCallToListRecordsWithNoResolverChainDeserializesCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            const string jsonReturnValue = """
            {
              "cursor": "cursor",
              "records": [
                {
                  "uri": "at://did:plc:test/blue.idunno.test/rkey1",
                  "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
                  "value": {
                      "testValue": "1"
                  }
                },
                {
                  "uri": "at://did:plc:test/blue.idunno.test/rkey2",
                  "cid": "bafyreih3stxgsbceqcredadhol7tlhhpbpjcssqnbzwiukexkqh3mjmblu",
                  "value": {
                      "testValue": "2"
                  }
                }
              ]
            }
            """;

            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.ListRecordsEndpoint)
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

                    if (!request.QueryString.HasValue)
                    {
                        response.StatusCode = 400;
                        return;
                    }

                    if (request.Query["repo"].ToString() != expectedDid)
                    {
                        response.StatusCode = 404;
                        return;
                    }

                    if (request.Query["collection"].ToString() != expectedCollection)
                    {
                        response.StatusCode = 404;
                        return;
                    }

                    response.StatusCode = 200;
                    response.Headers.ContentType = "application/json";
                    await response.WriteAsync(jsonReturnValue);
                    return;
                }
            });

            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRecord<TestRecordValue>>> response = await AtProtoServer.ListRecords<TestRecordValue>(
                repo: expectedDid,
                collection: expectedCollection,
                limit: 10,
                cursor: "cursor",
                reverse: true,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: expectedCredentials,
                httpClient: httpClient,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(response.Succeeded);

            Assert.Equal(2, response.Result.Count);
            Assert.Equal("cursor", response.Result.Cursor);

            Assert.Equal("at://did:plc:test/blue.idunno.test/rkey1", response.Result[0].Uri);
            Assert.Equal("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4", response.Result[0].Cid);
            Assert.NotNull(response.Result[0].Value);
            Assert.Equal("1", response.Result[0].Value.TestValue);
            Assert.Empty(response.Result[0].Value.ExtensionData);

            Assert.Equal("at://did:plc:test/blue.idunno.test/rkey2", response.Result[1].Uri);
            Assert.Equal("bafyreih3stxgsbceqcredadhol7tlhhpbpjcssqnbzwiukexkqh3mjmblu", response.Result[1].Cid);
            Assert.NotNull(response.Result[1].Value);
            Assert.Equal("2", response.Result[1].Value.TestValue);
            Assert.Empty(response.Result[1].Value.ExtensionData);
        }

        [Fact]
        public async Task AnonymousServerCallToGetRecordSerializesTheRecordWithNoConfiguredTypeResolverCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = "rkey1";
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            const string jsonReturnValue = """
            {
              "uri": "at://did:plc:test/blue.idunno.test/rkey1",
              "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
              "value": {
                  "testValue": "test"
              }
            }
            """;

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.GetRecordEndpoint)
                {
                    if (!request.QueryString.HasValue)
                    {
                        response.StatusCode = 400;
                        return;
                    }

                    if (request.Query["repo"].ToString() != expectedDid)
                    {
                        response.StatusCode = 404;
                        return;
                    }

                    if (request.Query["collection"].ToString() != expectedCollection)
                    {
                        response.StatusCode = 404;
                        return;
                    }

                    if (request.Query["rkey"].ToString() != expectedRecordKey)
                    {
                        response.StatusCode = 404;
                        return;
                    }

                    response.StatusCode = 200;
                    response.Headers.ContentType = "application/json";
                    await response.WriteAsync(jsonReturnValue);
                }
            });

            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            AtProtoHttpResult<TestRecord> response = await AtProtoServer.GetRecord<TestRecord>(
                repo: expectedDid,
                collection: expectedCollection,
                rKey: expectedRecordKey,
                cid: null,
                service: TestServerBuilder.DefaultUri,
                httpClient: httpClient,
                accessCredentials: null,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(response.Succeeded);

            Assert.Equal(expectedAtUri, response.Result.Uri);
            Assert.Equal(expectedCid, response.Result.Cid);

            Assert.Equal("test", response.Result.Value.TestValue);
        }

        [Fact]
        public async Task AnonymousServerCallToGetRecordSerializesTheRecordWithConfiguredTypeResolverCorrectly()
        {
            Did expectedDid = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";
            RecordKey expectedRecordKey = "rkey1";
            AtUri expectedAtUri = new($"at://{expectedDid}/{expectedCollection}/{expectedRecordKey}");
            Cid expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            const string jsonReturnValue = """
            {
              "uri": "at://did:plc:test/blue.idunno.test/rkey1",
              "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
              "value": {
                  "testValue": "test"
              }
            }
            """;

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.GetRecordEndpoint)
                {
                    if (!request.QueryString.HasValue)
                    {
                        response.StatusCode = 400;
                        return;
                    }

                    if (request.Query["repo"].ToString() != expectedDid)
                    {
                        response.StatusCode = 404;
                        return;
                    }

                    if (request.Query["collection"].ToString() != expectedCollection)
                    {
                        response.StatusCode = 404;
                        return;
                    }

                    if (request.Query["rkey"].ToString() != expectedRecordKey)
                    {
                        response.StatusCode = 404;
                        return;
                    }

                    response.StatusCode = 200;
                    response.Headers.ContentType = "application/json";
                    await response.WriteAsync(jsonReturnValue);
                }
            });

            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            AtProtoHttpResult<TestRecord> response = await AtProtoServer.GetRecord<TestRecord>(
                repo: expectedDid,
                collection: expectedCollection,
                rKey: expectedRecordKey,
                cid: null,
                service: TestServerBuilder.DefaultUri,
                httpClient: httpClient,
                accessCredentials: null,
                jsonSerializerOptions: _jsonSerializerOptions,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(response.Succeeded);

            Assert.Equal(expectedAtUri, response.Result.Uri);
            Assert.Equal(expectedCid, response.Result.Cid);

            Assert.Equal("test", response.Result.Value.TestValue);
        }

        [Fact]
        public async Task ServerCallToApplyWritesSerializesTheBatchWithConfiguredTypeResolverCorrectly()
        {
            Did expectedRepo = "did:plc:test";
            Nsid expectedCollection = "blue.idunno.test";

            const string jsonReturnValue = """
                {
                    "commit": {
                        "cid": "bafyreicypmumcyemtsrblhm4r4cawkjax744amgpzmb2fcksfut4g7rvya",
                        "rev": "3lly43ogrzj2t"
                    },
                    "results": [
                        {
                            "$type": "com.atproto.repo.applyWrites#createResult",
                            "cid": "bafyreihkmnqyhbk3u6lsbfuiaqsyg3rkchhpcfuhughxpqijkc66qih7zy",
                            "uri": "at://did:plc:test/blue.idunno.test/3lly43pdas22n",
                            "validationStatus": "valid"
                        }
                    ]
                }
                """;


            RecordKey expectedDeleteRKey = TimestampIdentifier.Generate();
            AccessCredentials expectedCredentials = new(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(expectedRepo, TestServerBuilder.DefaultUri.ToString()),
                    refreshToken: "refreshToken");

            ICollection<WriteOperation> operations = [];
            operations.Add(new CreateOperation(expectedCollection, new TestRecordValue() { TestValue = "testValue" }));

            TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == AtProtoServer.ApplyWritesEndpoint)
                {
                    response.StatusCode = 200;
                    response.Headers.ContentType = "application/json";
                    await response.WriteAsync(jsonReturnValue);
                    return;
                }
            });

            HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

            await AtProtoServer.ApplyWrites(
                operations: operations,
                jsonSerializerOptions: _jsonSerializerOptions,
                repo: expectedRepo,
                validate: true,
                cid: null,
                service: TestServerBuilder.DefaultUri,
                httpClient: httpClient,
                accessCredentials:expectedCredentials,
                cancellationToken: TestContext.Current.CancellationToken);
        }
    }
}
