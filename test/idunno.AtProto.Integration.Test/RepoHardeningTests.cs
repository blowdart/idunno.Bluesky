// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class RepoHardeningTests
{
    private static readonly Did s_testDid = "did:plc:test";
    private static readonly Nsid s_testCollection = "blue.idunno.test";

    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public RepoHardeningTests()
    {
        _jsonSerializerOptions = AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(JsonSerializerOptions.Default);
    }

    private sealed record UnsupportedOperation(Nsid Collection) : WriteOperation(Collection, null);

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_testDid, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    [Fact]
    public async Task ServerCallToApplyWritesThrowsArgumentExceptionOnAnUnsupportedOperationType()
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, context =>
        {
            context.Response.StatusCode = 500;
            return Task.CompletedTask;
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        ICollection<WriteOperation> operations = [new UnsupportedOperation(s_testCollection)];

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => AtProtoServer.ApplyWrites(
            operations: operations,
            repo: s_testDid,
            validate: true,
            cid: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("operations", exception.ParamName);
    }

    [Fact]
    public async Task ServerCallToApplyWritesWithJsonSerializerOptionsThrowsArgumentExceptionOnAnUnsupportedOperationType()
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, context =>
        {
            context.Response.StatusCode = 500;
            return Task.CompletedTask;
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        ICollection<WriteOperation> operations = [new UnsupportedOperation(s_testCollection)];

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => AtProtoServer.ApplyWrites(
            operations: operations,
            jsonSerializerOptions: _jsonSerializerOptions,
            repo: s_testDid,
            validate: true,
            cid: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("operations", exception.ParamName);
    }

    private static TestServer CreateListRecordsServer(string jsonReturnValue)
    {
        return TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpResponse response = context.Response;

            if (context.Request.Path == AtProtoServer.ListRecordsEndpoint)
            {
                response.StatusCode = 200;
                response.Headers.ContentType = "application/json";
                await response.WriteAsync(jsonReturnValue);
                return;
            }

            response.StatusCode = 404;
        });
    }

    // A page containing a null entry, a record missing its required uri, a record whose value does not match TRecord,
    // and a record whose uri is not a valid AT URI, interleaved with two records which deserialize correctly.
    private const string MalformedPageJson = """
        {
          "cursor": "cursor",
          "records": [
            {
              "uri": "at://did:plc:test/blue.idunno.test/rkey1",
              "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
              "value": { "testValue": "1" }
            },
            null,
            {
              "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
              "value": { "testValue": "2" }
            },
            {
              "uri": "at://did:plc:test/blue.idunno.test/rkey3",
              "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
              "value": { "testValue": 3 }
            },
            {
              "uri": "not-an-at-uri",
              "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
              "value": { "testValue": "4" }
            },
            {
              "uri": "at://did:plc:test/blue.idunno.test/rkey5",
              "cid": "bafyreih3stxgsbceqcredadhol7tlhhpbpjcssqnbzwiukexkqh3mjmblu",
              "value": { "testValue": "5" }
            }
          ]
        }
        """;

    [Fact]
    public async Task ServerCallToListRecordsSkipsRecordsWhichCannotBeDeserialized()
    {
        HttpClient httpClient = new TestHttpClientFactory(CreateListRecordsServer(MalformedPageJson)).CreateClient();

        AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<TestRecord>>> response = await AtProtoServer.ListRecords<TestRecord>(
            repo: s_testDid,
            collection: s_testCollection,
            limit: 10,
            cursor: null,
            reverse: false,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Succeeded);
        Assert.Equal(2, response.Result.Count);
        Assert.Equal("cursor", response.Result.Cursor);
        Assert.Equal("at://did:plc:test/blue.idunno.test/rkey1", response.Result[0].Uri);
        Assert.Equal("at://did:plc:test/blue.idunno.test/rkey5", response.Result[1].Uri);
    }

    [Fact]
    public async Task ServerCallToListRecordsWithJsonSerializerOptionsSkipsRecordsWhichCannotBeDeserialized()
    {
        HttpClient httpClient = new TestHttpClientFactory(CreateListRecordsServer(MalformedPageJson)).CreateClient();

        AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<TestRecord>>> response = await AtProtoServer.ListRecords<TestRecord>(
            repo: s_testDid,
            collection: s_testCollection,
            limit: 10,
            cursor: null,
            reverse: false,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            jsonSerializerOptions: _jsonSerializerOptions,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Succeeded);
        Assert.Equal(2, response.Result.Count);
        Assert.Equal("at://did:plc:test/blue.idunno.test/rkey1", response.Result[0].Uri);
        Assert.Equal("at://did:plc:test/blue.idunno.test/rkey5", response.Result[1].Uri);
    }

    [Theory]
    [InlineData("""{ "cursor": "cursor", "records": null }""")]
    [InlineData("""{ "cursor": "cursor" }""")]
    public async Task ServerCallToListRecordsWithNoRecordsReturnsAnEmptyCollection(string jsonReturnValue)
    {
        HttpClient httpClient = new TestHttpClientFactory(CreateListRecordsServer(jsonReturnValue)).CreateClient();

        AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<TestRecord>>> response = await AtProtoServer.ListRecords<TestRecord>(
            repo: s_testDid,
            collection: s_testCollection,
            limit: 10,
            cursor: null,
            reverse: false,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Succeeded);
        Assert.Empty(response.Result);
    }

    [Theory]
    [InlineData("image/jpeg\r\nX-Injected: true")]
    [InlineData("image/jpeg\nX-Injected: true")]
    [InlineData("image/jpeg\0")]
    [InlineData("image/jpeg\u007f")]
    [InlineData("/")]
    [InlineData("imagejpeg")]
    [InlineData("image/jpeg/extra")]
    public async Task ServerCallToUploadBlobThrowsArgumentExceptionOnAnInvalidMimeType(string mimeType)
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, context =>
        {
            context.Response.StatusCode = 500;
            return Task.CompletedTask;
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => AtProtoServer.UploadBlob(
            blob: [1, 2, 3, 4],
            mimeType: mimeType,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("mimeType", exception.ParamName);
    }

    [Theory]
    [InlineData("image/jpeg", "image/jpeg")]
    [InlineData(" image/jpeg", "image/jpeg")]
    [InlineData("image/jpeg ", "image/jpeg")]
    [InlineData("image /jpeg", "image/jpeg")]
    [InlineData("text/plain; charset=utf-8", "text/plain; charset=utf-8")]
    public async Task ServerCallToUploadBlobSendsTheNormalizedMimeType(string mimeType, string expectedContentType)
    {
        string? actualContentType = null;

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpResponse response = context.Response;

            if (context.Request.Path == AtProtoServer.UploadBlobEndpoint)
            {
                actualContentType = context.Request.ContentType;

                response.StatusCode = 200;

                await response.WriteAsJsonAsync(
                    new CreateBlobResponse(
                        new Blob(
                            reference: new CidLink("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"),
                            mimeType: expectedContentType,
                            size: 4)));
                return;
            }

            response.StatusCode = 404;
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        AtProtoHttpResult<Blob> response = await AtProtoServer.UploadBlob(
            blob: [1, 2, 3, 4],
            mimeType: mimeType,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Succeeded);
        Assert.Equal(expectedContentType, actualContentType);
    }

    [Fact]
    public void ListRecordsEndpointUsesTheLexiconCasing()
    {
        Assert.Equal("/xrpc/com.atproto.repo.listRecords", AtProtoServer.ListRecordsEndpoint);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ServerCallToPutRepositoryRecordSwapsOnTheRecordCidRatherThanTheCommitCid(bool useJsonSerializerOptions)
    {
        const string expectedCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

        string? requestBody = null;

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpResponse response = context.Response;

            if (context.Request.Path == AtProtoServer.PutRecordEndpoint)
            {
                using StreamReader reader = new(context.Request.Body);
                requestBody = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

                response.StatusCode = 200;
                response.Headers.ContentType = "application/json";
                await response.WriteAsync($$"""
                    {
                      "uri": "at://did:plc:test/blue.idunno.test/rkey1",
                      "cid": "{{expectedCid}}",
                      "commit": { "cid": "bafyreicypmumcyemtsrblhm4r4cawkjax744amgpzmb2fcksfut4g7rvya", "rev": "3lly43ogrzj2t" },
                      "validationStatus": "valid"
                    }
                    """);
                return;
            }

            response.StatusCode = 404;
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        AtProtoRepositoryRecord<TestRecord> repositoryRecord = new(
            uri: new AtUri($"at://{s_testDid}/{s_testCollection}/rkey1"),
            cid: expectedCid,
            value: new TestRecord() { TestValue = "testValue" });

        AtProtoHttpResult<PutRecordResult> response = useJsonSerializerOptions
            ? await AtProtoServer.PutRecord(
                repositoryRecord: repositoryRecord,
                jsonSerializerOptions: _jsonSerializerOptions,
                validate: true,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: CreateCredentials(),
                httpClient: httpClient,
                cancellationToken: TestContext.Current.CancellationToken)
            : await AtProtoServer.PutRecord(
                repositoryRecord: repositoryRecord,
                validate: true,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: CreateCredentials(),
                httpClient: httpClient,
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Succeeded);
        Assert.NotNull(requestBody);

        using JsonDocument document = JsonDocument.Parse(requestBody);

        Assert.True(document.RootElement.TryGetProperty("swapRecord", out JsonElement swapRecord));
        Assert.Equal(expectedCid, swapRecord.GetString());
        Assert.False(document.RootElement.TryGetProperty("swapCommit", out _));
    }

    [Fact]
    public async Task ServerCallToCreateRecordMapsAnInvalidSwapError()
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 400;
            context.Response.Headers.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"InvalidSwap","message":"Commit was at a different cid"}""");
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        AtProtoHttpResult<CreateRecordResult> response = await AtProtoServer.CreateRecord(
            record: new TestRecord() { TestValue = "testValue" },
            collection: s_testCollection,
            creator: s_testDid,
            rKey: null,
            validate: true,
            swapCommit: "bafyreicypmumcyemtsrblhm4r4cawkjax744amgpzmb2fcksfut4g7rvya",
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(response.Succeeded);
        Assert.IsType<InvalidSwap>(response.AtErrorDetail);
    }

    [Fact]
    public async Task ServerCallToDeleteRecordMapsAnInvalidSwapError()
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 400;
            context.Response.Headers.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"InvalidSwap","message":"Record was at a different cid"}""");
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        AtProtoHttpResult<DeleteResult> response = await AtProtoServer.DeleteRecord(
            repo: s_testDid,
            collection: s_testCollection,
            rKey: "rkey1",
            swapRecord: "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
            swapCommit: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(response.Succeeded);
        Assert.IsType<InvalidSwap>(response.AtErrorDetail);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task ServerCallToListRecordsReportsTheSuppliedLimitInItsExceptionMessage(int limit)
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, context =>
        {
            context.Response.StatusCode = 500;
            return Task.CompletedTask;
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        ArgumentOutOfRangeException exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => AtProtoServer.ListRecords<TestRecord>(
            repo: s_testDid,
            collection: s_testCollection,
            limit: limit,
            cursor: null,
            reverse: false,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("limit", exception.ParamName);
        Assert.Contains($"{limit} must be between 1 and 100.", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"commit":{"cid":"bafyreib2rxk3rh6kzwq6y7ug4eqhfhpqaqzqvuflstfpvgkzjt7b5yfkzy","rev":"3jx4mnqhtpm2b"}}""")]
    [InlineData("""{"commit":{"cid":"bafyreib2rxk3rh6kzwq6y7ug4eqhfhpqaqzqvuflstfpvgkzjt7b5yfkzy","rev":"3jx4mnqhtpm2b"},"results":null}""")]
    [InlineData("{}")]
    public async Task ServerCallToApplyWritesSucceedsWhenTheServiceOmitsResults(string responseBody)
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.Headers.ContentType = "application/json";
            await context.Response.WriteAsync(responseBody);
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        AtProtoHttpResult<ApplyWritesResults> response = await AtProtoServer.ApplyWrites(
            operations: [new DeleteOperation(s_testCollection, "rkey1")],
            repo: s_testDid,
            validate: true,
            cid: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Succeeded);
        Assert.Empty(response.Result.Results);
    }

    [Fact]
    public async Task ServerCallToApplyWritesSucceedsWithANullCommitWhenTheServiceOmitsTheCommit()
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.Headers.ContentType = "application/json";
            await context.Response.WriteAsync("""{"results":[]}""");
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        AtProtoHttpResult<ApplyWritesResults> response = await AtProtoServer.ApplyWrites(
            operations: [new DeleteOperation(s_testCollection, "rkey1")],
            repo: s_testDid,
            validate: true,
            cid: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Succeeded);
        Assert.Null(response.Result.Commit);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"commit":null}""")]
    public async Task ServerCallToDeleteRecordSucceedsWithANullCommitWhenTheServiceOmitsTheCommit(string responseBody)
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.Headers.ContentType = "application/json";
            await context.Response.WriteAsync(responseBody);
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        AtProtoHttpResult<DeleteResult> response = await AtProtoServer.DeleteRecord(
            repo: s_testDid,
            collection: s_testCollection,
            rKey: "rkey1",
            swapRecord: null,
            swapCommit: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Succeeded);
        Assert.Null(response.Result.Commit);
    }

    [Fact]
    public async Task ServerCallToDeleteRecordSucceedsWhenTheServiceReturnsNoContent()
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, context =>
        {
            context.Response.StatusCode = 204;
            return Task.CompletedTask;
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        AtProtoHttpResult<DeleteResult> response = await AtProtoServer.DeleteRecord(
            repo: s_testDid,
            collection: s_testCollection,
            rKey: "rkey1",
            swapRecord: null,
            swapCommit: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Succeeded);
        Assert.Null(response.Result.Commit);
    }

    [Fact]
    public async Task ServerCallToDeleteRecordReturnsTheCommitWhenTheServiceSuppliesOne()
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.Headers.ContentType = "application/json";
            await context.Response.WriteAsync("""{"commit":{"cid":"bafyreib2rxk3rh6kzwq6y7ug4eqhfhpqaqzqvuflstfpvgkzjt7b5yfkzy","rev":"3jx4mnqhtpm2b"}}""");
        });

        HttpClient httpClient = new TestHttpClientFactory(testServer).CreateClient();

        AtProtoHttpResult<DeleteResult> response = await AtProtoServer.DeleteRecord(
            repo: s_testDid,
            collection: s_testCollection,
            rKey: "rkey1",
            swapRecord: null,
            swapCommit: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Succeeded);
        Assert.NotNull(response.Result.Commit);
        Assert.Equal("3jx4mnqhtpm2b", response.Result.Commit.Rev, StringComparer.Ordinal);
    }
}
