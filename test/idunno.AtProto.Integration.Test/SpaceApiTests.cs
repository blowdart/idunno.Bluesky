// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable BSKYSpaces

using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.AtProto.Space;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.AtProto.Integration.Test;

public class SpaceApiTests
{
    /// <summary>
    /// Replays sanitized response bodies captured from the Spaces alpha PDS through the implemented write APIs.
    /// </summary>
    /// <remarks>
    /// <para>The request assertions use reconstructed inputs; the original requests were not captured.</para>
    /// </remarks>
    [Fact]
    public async Task SpaceWritesAcceptCapturedAlphaResponses()
    {
        const string repository = "did:plc:writer";
        const string collection = "app.bsky.feed.post";
        const string spaceUri = "at:space/did:plc:fixture/app.bsky.bookmark/fixture";
        const string recordKey = "3k4duaz5vfs2b";
        const string recordUri = spaceUri + "/" + repository + "/" + collection + "/" + recordKey;
        const string createdCid = "bafyreig32pwtd7tcunqgzycd7hnquetiovko6jfbcwfwtj22gqvic7qqd4";
        const string updatedCid = "bafyreihdcj4pqoq37d3z6h6k7ed5iki5csu22ykeciks2dcbwfpdkrn7fe";

        using Stream fixtureStream = typeof(SpaceApiTests).Assembly.GetManifestResourceStream(
            "idunno.AtProto.Integration.Test.TestData.SpaceAlphaResponses.json") ??
            throw new InvalidOperationException("The Spaces alpha response fixture is missing.");
        using JsonDocument fixture = await JsonDocument.ParseAsync(
            fixtureStream,
            cancellationToken: TestContext.Current.CancellationToken);

        Dictionary<string, JsonElement> requestBodies = [];
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal(HttpMethods.Post, context.Request.Method);
            string path = context.Request.Path;
            string endpoint = path switch
            {
                "/xrpc/com.atproto.space.createSpace" => "createSpace",
                "/xrpc/com.atproto.space.createRecord" => "createRecord",
                "/xrpc/com.atproto.space.putRecord" => "putRecord",
                "/xrpc/com.atproto.space.deleteRecord" => "deleteRecord",
                _ => throw new InvalidOperationException("Unexpected Spaces endpoint.")
            };

            using JsonDocument body = await JsonDocument.ParseAsync(context.Request.Body);
            requestBodies[path] = body.RootElement.Clone();
            JsonElement response = fixture.RootElement.GetProperty(endpoint);
            context.Response.StatusCode = response.GetProperty("statusCode").GetInt32();
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(response.GetProperty("body").GetRawText());
        });

        Uri service = TestServerBuilder.DefaultUri;
        using AtProtoAgent agent = new(service, new TestHttpClientFactory(testServer));
        agent.Credentials = new AccessCredentials(
            service: service,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt(new Did(repository), service.ToString()),
            refreshToken: "refreshToken");

        AtProtoHttpResult<SpaceAtUri> createSpace = await agent.CreateSpace(
            spaceType: "app.bsky.bookmark",
            policy: new PersonalSpacePolicy(),
            skey: "fixture",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(createSpace.Succeeded);
        Assert.Equal(spaceUri, createSpace.Result.ToString());

        Dictionary<string, JsonElement> recordFields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """{"$type":"app.bsky.feed.post","text":"Spaces alpha fixture record","createdAt":"2026-01-01T00:00:00Z"}""") ??
            throw new JsonException("The test record could not be parsed.");
        AtProtoRecord record = new() { ExtensionData = recordFields };
        AtProtoHttpResult<CreateRecordResult> createRecord = await agent.CreateSpaceRecord(
            space: createSpace.Result,
            record: record,
            collection: collection,
            validate: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(createRecord.Succeeded);
        Assert.Equal(recordUri, createRecord.Result.Uri.ToString());
        Assert.Equal(createdCid, createRecord.Result.Cid.ToString());
        Assert.Equal(ValidationStatus.Valid, createRecord.Result.ValidationStatus);
        SpaceAtUri createdRecordUri = Assert.IsType<SpaceAtUri>(createRecord.Result.Uri);
        RecordKey generatedRecordKey = Assert.IsType<RecordKey>(createdRecordUri.RecordKey);
        Assert.Equal(recordKey, generatedRecordKey.ToString());

        recordFields["text"] = JsonSerializer.SerializeToElement("Spaces alpha fixture record updated");
        AtProtoHttpResult<PutRecordResult> putRecord = await agent.PutSpaceRecord(
            space: createSpace.Result,
            record: record,
            collection: collection,
            rkey: generatedRecordKey,
            validate: true,
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<DeleteResult> deleteRecord = await agent.DeleteSpaceRecord(
            space: createSpace.Result,
            collection: collection,
            rkey: generatedRecordKey,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(putRecord.Succeeded);
        Assert.Equal(recordUri, putRecord.Result.Uri.ToString());
        Assert.Equal(updatedCid, putRecord.Result.Cid.ToString());
        Assert.Equal(ValidationStatus.Valid, putRecord.Result.ValidationStatus);
        Assert.True(deleteRecord.Succeeded);

        JsonElement spaceRequest = requestBodies["/xrpc/com.atproto.space.createSpace"];
        JsonElement createRequest = requestBodies["/xrpc/com.atproto.space.createRecord"];
        JsonElement putRequest = requestBodies["/xrpc/com.atproto.space.putRecord"];
        JsonElement deleteRequest = requestBodies["/xrpc/com.atproto.space.deleteRecord"];
        Assert.Equal("app.bsky.bookmark", spaceRequest.GetProperty("spaceType").GetString());
        Assert.Equal("fixture", spaceRequest.GetProperty("skey").GetString());
        Assert.Equal("com.atproto.personalspace.policy", spaceRequest.GetProperty("policy").GetProperty("$type").GetString());
        Assert.Equal(spaceUri, createRequest.GetProperty("space").GetString());
        Assert.Equal(repository, createRequest.GetProperty("repo").GetString());
        Assert.Equal(collection, createRequest.GetProperty("collection").GetString());
        Assert.False(createRequest.TryGetProperty("rkey", out _));
        Assert.True(createRequest.GetProperty("validate").GetBoolean());
        Assert.Equal(collection, createRequest.GetProperty("record").GetProperty("$type").GetString());
        Assert.Equal("Spaces alpha fixture record", createRequest.GetProperty("record").GetProperty("text").GetString());
        Assert.Equal(spaceUri, putRequest.GetProperty("space").GetString());
        Assert.Equal(repository, putRequest.GetProperty("repo").GetString());
        Assert.Equal(collection, putRequest.GetProperty("collection").GetString());
        Assert.Equal(recordKey, putRequest.GetProperty("rkey").GetString());
        Assert.True(putRequest.GetProperty("validate").GetBoolean());
        Assert.Equal("Spaces alpha fixture record updated", putRequest.GetProperty("record").GetProperty("text").GetString());
        Assert.Equal(spaceUri, deleteRequest.GetProperty("space").GetString());
        Assert.Equal(repository, deleteRequest.GetProperty("repo").GetString());
        Assert.Equal(collection, deleteRequest.GetProperty("collection").GetString());
        Assert.Equal(recordKey, deleteRequest.GetProperty("rkey").GetString());
    }

    [Fact]
    public async Task AgentCanCreateSpaceAndWriteSpaceRecords()
    {
        const string repository = "did:plc:test";
        const string collection = "app.bsky.feed.post";
        const string spaceUri = "at:space/did:plc:owner/app.bsky.bookmark/space-key";
        const string recordUri = "at:space/did:plc:owner/app.bsky.bookmark/space-key/did:plc:test/app.bsky.feed.post/record-key";
        Cid cid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
        Uri service = TestServerBuilder.DefaultUri;
        AccessCredentials credentials = new(
            service: service,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt(new Did(repository), service.ToString()),
            refreshToken: "refreshToken");

        Dictionary<string, JsonElement> requestBodies = [];
        TestServer testServer = TestServerBuilder.CreateServer(service, async context =>
        {
            string path = context.Request.Path;
            using JsonDocument body = await JsonDocument.ParseAsync(context.Request.Body);
            requestBodies[path] = body.RootElement.Clone();
            context.Response.ContentType = "application/json";

            if (path == "/xrpc/com.atproto.space.createSpace")
            {
                await context.Response.WriteAsJsonAsync(new { space = spaceUri });
            }
            else if (path is "/xrpc/com.atproto.space.createRecord" or "/xrpc/com.atproto.space.putRecord")
            {
                await context.Response.WriteAsJsonAsync(new
                {
                    uri = recordUri,
                    cid = cid.ToString(),
                    validationStatus = "valid"
                });
            }
            else
            {
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("{}");
            }
        });

        using AtProtoAgent agent = new(service, new TestHttpClientFactory(testServer));
        agent.Credentials = credentials;

        PersonalSpacePolicy policy = new();
        AtProtoHttpResult<SpaceAtUri> createSpaceResult = await agent.CreateSpace(
            spaceType: "app.bsky.bookmark",
            policy: policy,
            skey: "space-key",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(createSpaceResult.Succeeded);
        Assert.Equal(spaceUri, createSpaceResult.Result.ToString());
        Assert.Equal("app.bsky.bookmark", requestBodies["/xrpc/com.atproto.space.createSpace"].GetProperty("spaceType").GetString());
        Assert.Equal("space-key", requestBodies["/xrpc/com.atproto.space.createSpace"].GetProperty("skey").GetString());
        Assert.Equal("com.atproto.personalspace.policy", requestBodies["/xrpc/com.atproto.space.createSpace"].GetProperty("policy").GetProperty("$type").GetString());

        SpaceAtUri space = new(spaceUri);
        TestRecord record = new() { TestValue = "space record" };
        JsonSerializerOptions jsonSerializerOptions = AtProtoJsonSerializerOptions.Create(
            options => options.TypeInfoResolverChain.Add(SourceGenerationContext.Default));
        AtProtoHttpResult<CreateRecordResult> createRecordResult = await agent.CreateSpaceRecord(
            space: space,
            record: record,
            jsonSerializerOptions: jsonSerializerOptions,
            collection: collection,
            rkey: "record-key",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<PutRecordResult> putRecordResult = await agent.PutSpaceRecord(
            space: space,
            record: record,
            jsonSerializerOptions: jsonSerializerOptions,
            collection: collection,
            rkey: "record-key",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<DeleteResult> deleteRecordResult = await agent.DeleteSpaceRecord(
            space: space,
            collection: collection,
            rkey: "record-key",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(createRecordResult.Succeeded);
        Assert.Equal(recordUri, createRecordResult.Result.Uri.ToString());
        Assert.Equal(cid, createRecordResult.Result.Cid);
        Assert.True(putRecordResult.Succeeded);
        Assert.Equal(recordUri, putRecordResult.Result.Uri.ToString());
        Assert.True(deleteRecordResult.Succeeded);

        JsonElement createRequest = requestBodies["/xrpc/com.atproto.space.createRecord"];
        JsonElement putRequest = requestBodies["/xrpc/com.atproto.space.putRecord"];
        JsonElement deleteRequest = requestBodies["/xrpc/com.atproto.space.deleteRecord"];

        Assert.Equal(spaceUri, createRequest.GetProperty("space").GetString());
        Assert.Equal(repository, createRequest.GetProperty("repo").GetString());
        Assert.Equal(collection, createRequest.GetProperty("collection").GetString());
        Assert.Equal("record-key", createRequest.GetProperty("rkey").GetString());
        Assert.Equal("space record", createRequest.GetProperty("record").GetProperty("testValue").GetString());

        Assert.Equal(spaceUri, putRequest.GetProperty("space").GetString());
        Assert.Equal(repository, putRequest.GetProperty("repo").GetString());
        Assert.Equal(collection, putRequest.GetProperty("collection").GetString());
        Assert.Equal("record-key", putRequest.GetProperty("rkey").GetString());
        Assert.Equal(spaceUri, deleteRequest.GetProperty("space").GetString());
        Assert.Equal(repository, deleteRequest.GetProperty("repo").GetString());
        Assert.Equal(collection, deleteRequest.GetProperty("collection").GetString());
        Assert.Equal("record-key", deleteRequest.GetProperty("rkey").GetString());
        Assert.False(deleteRequest.TryGetProperty("record", out _));
    }

    [Fact]
    public async Task SpaceEndpointErrorsAreReturnedInAtErrorDetail()
    {
        Dictionary<string, int> requestCounts = [];
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            string path = context.Request.Path;
            requestCounts.TryGetValue(path, out int requestCount);
            requestCounts[path] = ++requestCount;

            string error = path switch
            {
                "/xrpc/com.atproto.space.createSpace" when requestCount == 1 => "SpaceAlreadyExists",
                "/xrpc/com.atproto.space.createSpace" => "UnsupportedPolicy",
                "/xrpc/com.atproto.space.createRecord" when requestCount == 1 => "RecordAlreadyExists",
                "/xrpc/com.atproto.space.createRecord" => "SpaceNotFound",
                "/xrpc/com.atproto.space.getSpace" when requestCount == 1 => "SpaceNotFound",
                "/xrpc/com.atproto.space.getSpace" => "NotAuthorized",
                "/xrpc/com.atproto.space.listSpaces" => "InvalidCursor",
                "/xrpc/com.atproto.space.updateSpace" when requestCount == 1 => "SpaceNotFound",
                "/xrpc/com.atproto.space.updateSpace" when requestCount == 2 => "NotAuthorized",
                "/xrpc/com.atproto.space.updateSpace" => "UnsupportedPolicy",
                "/xrpc/com.atproto.space.deleteSpace" when requestCount == 1 => "SpaceNotFound",
                "/xrpc/com.atproto.space.deleteSpace" => "NotAuthorized",
                _ => "SpaceNotFound"
            };

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error, message = "Mock endpoint error." });
        });

        using AtProtoAgent agent = new(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer));
        Did did = "did:plc:test";
        agent.Credentials = new AccessCredentials(
            service: TestServerBuilder.DefaultUri,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt(did, TestServerBuilder.DefaultUri.ToString()),
            refreshToken: "refreshToken");

        SpaceAtUri space = new("at:space/did:plc:owner/app.bsky.bookmark/space-key");
        PersonalSpacePolicy policy = new();
        TestRecord record = new() { TestValue = "space record" };

        AtProtoHttpResult<SpaceAtUri> alreadyExists = await agent.CreateSpace(
            spaceType: "app.bsky.bookmark",
            policy: policy,
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<SpaceAtUri> unsupportedPolicy = await agent.CreateSpace(
            spaceType: "app.bsky.bookmark",
            policy: policy,
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<CreateRecordResult> recordAlreadyExists = await agent.CreateSpaceRecord(
            space: space,
            record: record,
            collection: "app.bsky.feed.post",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<CreateRecordResult> missingSpaceOnCreate = await agent.CreateSpaceRecord(
            space: space,
            record: record,
            collection: "app.bsky.feed.post",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<PutRecordResult> missingSpaceOnPut = await agent.PutSpaceRecord(
            space: space,
            record: record,
            collection: "app.bsky.feed.post",
            rkey: "record-key",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<DeleteResult> missingSpaceOnDelete = await agent.DeleteSpaceRecord(
            space: space,
            collection: "app.bsky.feed.post",
            rkey: "record-key",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<SpaceDescription> missingSpaceOnGet = await agent.GetSpace(space, cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<SpaceDescription> unauthorizedSpaceOnGet = await agent.GetSpace(space, cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<PagedReadOnlyCollection<SpaceView>> invalidCursor = await agent.ListSpaces(
            cursor: "invalid",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<EmptyResponse> missingSpaceOnUpdate = await agent.UpdateSpace(
            space,
            policy,
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<EmptyResponse> unauthorizedSpaceOnUpdate = await agent.UpdateSpace(
            space,
            policy,
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<EmptyResponse> unsupportedSpacePolicy = await agent.UpdateSpace(
            space,
            policy,
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<DeleteResult> missingSpaceOnDeleteSpace = await agent.DeleteSpace(space, cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<DeleteResult> unauthorizedSpaceOnDelete = await agent.DeleteSpace(space, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("SpaceAlreadyExists", alreadyExists.AtErrorDetail?.Error);
        Assert.Equal("UnsupportedPolicy", unsupportedPolicy.AtErrorDetail?.Error);
        Assert.Equal("RecordAlreadyExists", recordAlreadyExists.AtErrorDetail?.Error);
        Assert.Equal("SpaceNotFound", missingSpaceOnCreate.AtErrorDetail?.Error);
        Assert.Equal("SpaceNotFound", missingSpaceOnPut.AtErrorDetail?.Error);
        Assert.Equal("SpaceNotFound", missingSpaceOnDelete.AtErrorDetail?.Error);
        Assert.Equal("SpaceNotFound", missingSpaceOnGet.AtErrorDetail?.Error);
        Assert.Equal("NotAuthorized", unauthorizedSpaceOnGet.AtErrorDetail?.Error);
        Assert.Equal("InvalidCursor", invalidCursor.AtErrorDetail?.Error);
        Assert.Equal("SpaceNotFound", missingSpaceOnUpdate.AtErrorDetail?.Error);
        Assert.Equal("NotAuthorized", unauthorizedSpaceOnUpdate.AtErrorDetail?.Error);
        Assert.Equal("UnsupportedPolicy", unsupportedSpacePolicy.AtErrorDetail?.Error);
        Assert.Equal("SpaceNotFound", missingSpaceOnDeleteSpace.AtErrorDetail?.Error);
        Assert.Equal("NotAuthorized", unauthorizedSpaceOnDelete.AtErrorDetail?.Error);
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status401Unauthorized)]
    public async Task AppPasswordRejectionIsReturnedInAtErrorDetail(int statusCode)
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(new { error = "InvalidToken", message = "Bad token scope" });
        });

        using AtProtoAgent agent = new(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer));
        agent.Credentials = new AccessCredentials(
            service: TestServerBuilder.DefaultUri,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:test"), TestServerBuilder.DefaultUri.ToString()),
            refreshToken: "refreshToken");

        AtProtoHttpResult<SpaceAtUri> result = await agent.CreateSpace(
            spaceType: "app.bsky.bookmark",
            policy: new PersonalSpacePolicy(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(statusCode, (int)result.StatusCode);
        Assert.Equal("InvalidToken", result.AtErrorDetail?.Error);
        Assert.Equal("Bad token scope", result.AtErrorDetail?.Message);
    }

    [Theory]
    [InlineData(StatusCodes.Status200OK)]
    [InlineData(StatusCodes.Status204NoContent)]
    public async Task AgentCanReadListUpdateAndDeleteSpaces(int statusCode)
    {
        const string spaceUri = "at:space/did:plc:owner/app.bsky.bookmark/space-key";
        const string getQuery = "?space=at%3Aspace%2Fdid%3Aplc%3Aowner%2Fapp.bsky.bookmark%2Fspace-key";
        string? listQuery = null;
        Dictionary<string, JsonElement> requestBodies = [];
        Uri service = TestServerBuilder.DefaultUri;
        TestServer testServer = TestServerBuilder.CreateServer(service, async context =>
        {
            string path = context.Request.Path;
            if (context.Request.Method == HttpMethods.Get && path == "/xrpc/com.atproto.space.getSpace")
            {
                Assert.Equal(getQuery, context.Request.QueryString.Value);
                await context.Response.WriteAsJsonAsync(new
                {
                    space = spaceUri,
                    policy = JsonNode.Parse("""{"$type":"com.atproto.memberspace.policy","publicRead":true}""")
                });
            }
            else if (context.Request.Method == HttpMethods.Get && path == "/xrpc/com.atproto.space.listSpaces")
            {
                listQuery = context.Request.QueryString.Value;
                await context.Response.WriteAsJsonAsync(new
                {
                    spaces = new object[]
                    {
                        new
                        {
                            space = spaceUri,
                            repoRev = "3k4duaz5vfs2b",
                            data = JsonNode.Parse("""{"$bytes":"AQID"}"""),
                            spaceRev = "3k4duaz5vfs2b"
                        },
                        new
                        {
                            space = "at:space/did:plc:owner/app.bsky.bookmark/other"
                        }
                    },
                    cursor = "next"
                });
            }
            else if (context.Request.Method == HttpMethods.Post &&
                path is "/xrpc/com.atproto.space.updateSpace" or "/xrpc/com.atproto.space.deleteSpace")
            {
                using JsonDocument body = await JsonDocument.ParseAsync(context.Request.Body);
                requestBodies[path] = body.RootElement.Clone();
                context.Response.StatusCode = statusCode;
            }
            else
            {
                throw new InvalidOperationException("Unexpected Spaces endpoint.");
            }
        });

        using AtProtoAgent agent = new(service, new TestHttpClientFactory(testServer));
        agent.Credentials = new AccessCredentials(
            service: service,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:owner"), service.ToString()),
            refreshToken: "refreshToken");

        SpaceAtUri space = new(spaceUri);
        AtProtoHttpResult<SpaceDescription> getResult = await agent.GetSpace(space, cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<PagedReadOnlyCollection<SpaceView>> listResult = await agent.ListSpaces(
            spaceDid: "did:plc:owner",
            spaceType: "app.bsky.bookmark",
            limit: 25,
            cursor: "after/+",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<EmptyResponse> updateResult = await agent.UpdateSpace(
            space,
            new ManagedSpacePolicy { ManagingApp = "did:plc:app#app" },
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<DeleteResult> deleteResult = await agent.DeleteSpace(space, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(getResult.Succeeded);
        Assert.Equal(spaceUri, getResult.Result.Space.ToString());
        MemberSpacePolicy memberPolicy = Assert.IsType<MemberSpacePolicy>(getResult.Result.Policy);
        Assert.True(memberPolicy.PublicRead);
        Assert.True(listResult.Succeeded);
        Assert.Equal("next", listResult.Result.Cursor);
        Assert.Equal(2, listResult.Result.Count);
        SpaceView listedSpace = listResult.Result[0];
        Assert.Equal(new byte[] { 1, 2, 3 }, listedSpace.Data?.ToBytes());
        Assert.Null(listResult.Result[1].RepoRev);
        Assert.Null(listResult.Result[1].Data);
        Assert.Null(listResult.Result[1].SpaceRev);
        Assert.Equal("?spaceDid=did%3Aplc%3Aowner&spaceType=app.bsky.bookmark&limit=25&cursor=after%2F%2B", listQuery);
        Assert.True(updateResult.Succeeded);
        Assert.True(deleteResult.Succeeded);
        Assert.Equal(spaceUri, requestBodies["/xrpc/com.atproto.space.updateSpace"].GetProperty("space").GetString());
        Assert.Equal("com.atproto.managedspace.policy", requestBodies["/xrpc/com.atproto.space.updateSpace"].GetProperty("policy").GetProperty("$type").GetString());
        Assert.Equal("did:plc:app#app", requestBodies["/xrpc/com.atproto.space.updateSpace"].GetProperty("policy").GetProperty("managingApp").GetString());
        Assert.Equal(spaceUri, requestBodies["/xrpc/com.atproto.space.deleteSpace"].GetProperty("space").GetString());
    }

    [Fact]
    public void SpacePoliciesSerializeWithLexiconDiscriminators()
    {
        SpacePolicy[] policies =
        [
            new PersonalSpacePolicy(),
            new MemberSpacePolicy { PublicRead = true, AllowedApps = [new Uri("https://example.com/app")] },
            new ManagedSpacePolicy { ManagingApp = "did:plc:app#app", PublishPolicy = true }
        ];
        string[] discriminators =
        [
            "com.atproto.personalspace.policy",
            "com.atproto.memberspace.policy",
            "com.atproto.managedspace.policy"
        ];

        for (int i = 0; i < policies.Length; i++)
        {
            string json = JsonSerializer.Serialize(policies[i], policies[i].GetType(), AtProtoJsonSerializerOptions.Default);
            using JsonDocument document = JsonDocument.Parse(json);
            Assert.Equal(discriminators[i], document.RootElement.GetProperty("$type").GetString());

            SpacePolicy? deserialized = JsonSerializer.Deserialize<SpacePolicy>(json, AtProtoJsonSerializerOptions.Default);
            Assert.Equal(policies[i].GetType(), deserialized?.GetType());
            if (deserialized is MemberSpacePolicy memberPolicy)
            {
                Assert.Equal(new Uri("https://example.com/app"), Assert.Single(memberPolicy.AllowedApps!));
            }
        }
    }

    [Theory]
    [InlineData("""{"$type":"org.example.space.policy","setting":{"enabled":true}}""")]
    [InlineData("""{"setting":{"enabled":true},"$type":"org.example.space.policy"}""")]
    public void UnknownSpacePoliciesPreserveTheirContents(string json)
    {
        SpacePolicy? policy = JsonSerializer.Deserialize<SpacePolicy>(json, AtProtoJsonSerializerOptions.Default);
        UnknownSpacePolicy unknown = Assert.IsType<UnknownSpacePolicy>(policy);
        Assert.Equal("org.example.space.policy", unknown.Value.GetProperty("$type").GetString());
        Assert.True(unknown.Value.GetProperty("setting").GetProperty("enabled").GetBoolean());

        string serialized = JsonSerializer.Serialize(policy, AtProtoJsonSerializerOptions.Default);
        using JsonDocument document = JsonDocument.Parse(serialized);
        Assert.Equal("org.example.space.policy", document.RootElement.GetProperty("$type").GetString());
        Assert.True(document.RootElement.GetProperty("setting").GetProperty("enabled").GetBoolean());

        string direct = JsonSerializer.Serialize(unknown, AtProtoJsonSerializerOptions.Default);
        using JsonDocument directDocument = JsonDocument.Parse(direct);
        Assert.Equal("org.example.space.policy", directDocument.RootElement.GetProperty("$type").GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"$type":1}""")]
    [InlineData("""{"$type":""}""")]
    public void SpacePolicyRejectsMissingOrInvalidDiscriminator(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SpacePolicy>(json, AtProtoJsonSerializerOptions.Default));
    }

    [Fact]
    public async Task UnknownPolicyCanBeReadAndWrittenThroughSpaceEndpoints()
    {
        const string policyJson = """{"$type":"org.example.space.policy","setting":{"enabled":true}}""";
        const string spaceUri = "at:space/did:plc:owner/app.bsky.bookmark/space-key";
        JsonElement? writtenPolicy = null;
        Uri service = TestServerBuilder.DefaultUri;
        TestServer testServer = TestServerBuilder.CreateServer(service, async context =>
        {
            if (context.Request.Method == HttpMethods.Get)
            {
                await context.Response.WriteAsync($$"""{"space":"{{spaceUri}}","policy":{{policyJson}}}""");
            }
            else
            {
                using JsonDocument body = await JsonDocument.ParseAsync(context.Request.Body);
                writtenPolicy = body.RootElement.GetProperty("policy").Clone();
                context.Response.StatusCode = StatusCodes.Status204NoContent;
            }
        });

        using AtProtoAgent agent = new(service, new TestHttpClientFactory(testServer));
        agent.Credentials = new AccessCredentials(
            service: service,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:test"), service.ToString()),
            refreshToken: "refreshToken");

        SpaceAtUri space = new(spaceUri);
        AtProtoHttpResult<SpaceDescription> get = await agent.GetSpace(space, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(get.Succeeded);
        Assert.IsType<UnknownSpacePolicy>(get.Result.Policy);
        AtProtoHttpResult<EmptyResponse> update = await agent.UpdateSpace(space, get.Result.Policy, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(update.Succeeded);
        Assert.True(writtenPolicy.HasValue);
        Assert.Equal("org.example.space.policy", writtenPolicy.Value.GetProperty("$type").GetString());
        Assert.True(writtenPolicy.Value.GetProperty("setting").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void SpaceDefinitionsRoundTripThroughSourceGeneratedMetadata()
    {
        SpaceAtUri space = new("at:space/did:plc:owner/app.bsky.bookmark/space-key");
        TimestampIdentifier revision = "3k4duaz5vfs2b";
        SpaceCommit commit = new()
        {
            Ver = 1,
            Repo = "did:plc:owner",
            Space = space,
            Rev = revision,
            Data = new byte[] { 1, 2, 3 }
        };
        SpaceRepoWrite write = new()
        {
            Repo = "did:plc:owner",
            RepoRev = revision,
            Data = new byte[] { 4, 5, 6 },
            SpaceRev = revision
        };
        SpaceRepoRemoved removed = new()
        {
            Repo = "did:plc:owner",
            SpaceRev = revision
        };

        string commitJson = JsonSerializer.Serialize(commit, AtProtoJsonSerializerOptions.Default);
        string writeJson = JsonSerializer.Serialize(write, AtProtoJsonSerializerOptions.Default);
        string removedJson = JsonSerializer.Serialize(removed, AtProtoJsonSerializerOptions.Default);
        using JsonDocument commitDocument = JsonDocument.Parse(commitJson);
        using JsonDocument writeDocument = JsonDocument.Parse(writeJson);
        Assert.Equal("AQID", commitDocument.RootElement.GetProperty("data").GetProperty("$bytes").GetString());
        Assert.Equal("BAUG", writeDocument.RootElement.GetProperty("data").GetProperty("$bytes").GetString());
        SpaceCommit? commitRoundTrip = JsonSerializer.Deserialize<SpaceCommit>(commitJson, AtProtoJsonSerializerOptions.Default);
        SpaceRepoWrite? writeRoundTrip = JsonSerializer.Deserialize<SpaceRepoWrite>(writeJson, AtProtoJsonSerializerOptions.Default);
        SpaceRepoRemoved? removedRoundTrip = JsonSerializer.Deserialize<SpaceRepoRemoved>(removedJson, AtProtoJsonSerializerOptions.Default);

        Assert.Equal(commit.Data, commitRoundTrip?.Data);
        Assert.Equal(write.Data, writeRoundTrip?.Data);
        Assert.Equal(removed.SpaceRev, removedRoundTrip?.SpaceRev);
    }

    [Fact]
    public async Task SpaceRecordUriCannotBeUsedAsASpaceReference()
    {
        int requestCount = 0;
        Uri service = TestServerBuilder.DefaultUri;
        TestServer testServer = TestServerBuilder.CreateServer(service, _ =>
        {
            requestCount++;
            return Task.CompletedTask;
        });
        Did did = "did:plc:test";

        using AtProtoAgent agent = new(service, new TestHttpClientFactory(testServer));
        agent.Credentials = new AccessCredentials(
            service: service,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt(did, service.ToString()),
            refreshToken: "refreshToken");

        SpaceAtUri spaceRecord = new("at:space/did:plc:owner/app.bsky.bookmark/space-key/did:plc:test/app.bsky.feed.post/record-key");
        TestRecord record = new() { TestValue = "space record" };

        await Assert.ThrowsAsync<ArgumentException>(() => agent.CreateSpaceRecord(
            space: spaceRecord,
            record: record,
            collection: "app.bsky.feed.post",
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.PutSpaceRecord(
            space: spaceRecord,
            record: record,
            collection: "app.bsky.feed.post",
            rkey: "record-key",
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.DeleteSpaceRecord(
            space: spaceRecord,
            collection: "app.bsky.feed.post",
            rkey: "record-key",
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.GetSpace(
            space: spaceRecord,
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.UpdateSpace(
            space: spaceRecord,
            policy: new PersonalSpacePolicy(),
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.DeleteSpace(
            space: spaceRecord,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(0, requestCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SpaceReadRejectsRecordUriInResponse(bool getSpace)
    {
        const string recordUri = "at:space/did:plc:owner/app.bsky.bookmark/space-key/did:plc:test/app.bsky.feed.post/record-key";
        Uri service = TestServerBuilder.DefaultUri;
        TestServer testServer = TestServerBuilder.CreateServer(service, async context =>
        {
            context.Response.ContentType = "application/json";
            string response = getSpace
                ? $$$"""{"space":"{{{recordUri}}}","policy":{"$type":"com.atproto.personalspace.policy"}}"""
                : $$$"""{"spaces":[{"space":"{{{recordUri}}}"}]}""";
            await context.Response.WriteAsync(response);
        });

        using AtProtoAgent agent = new(service, new TestHttpClientFactory(testServer));
        agent.Credentials = new AccessCredentials(
            service: service,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:test"), service.ToString()),
            refreshToken: "refreshToken");

        if (getSpace)
        {
            await Assert.ThrowsAsync<JsonException>(() => agent.GetSpace(
                new SpaceAtUri("at:space/did:plc:owner/app.bsky.bookmark/space-key"),
                cancellationToken: TestContext.Current.CancellationToken));
        }
        else
        {
            await Assert.ThrowsAsync<JsonException>(() => agent.ListSpaces(cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task ListSpacesRejectsLimitsOutsideLexiconRange(int limit)
    {
        int requestCount = 0;
        Uri service = TestServerBuilder.DefaultUri;
        TestServer testServer = TestServerBuilder.CreateServer(service, _ =>
        {
            requestCount++;
            return Task.CompletedTask;
        });

        using AtProtoAgent agent = new(service, new TestHttpClientFactory(testServer));
        agent.Credentials = new AccessCredentials(
            service: service,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:test"), service.ToString()),
            refreshToken: "refreshToken");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => agent.ListSpaces(
            limit: limit,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(0, requestCount);
    }
}
