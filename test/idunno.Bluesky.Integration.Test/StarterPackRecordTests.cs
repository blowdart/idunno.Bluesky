// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class StarterPackRecordTests
{
    private static readonly Did s_did = new("did:plc:test");
    private static readonly AtUri s_uri = new("at://did:plc:test/app.bsky.graph.starterpack/3lcf6ry7xy22x");
    private static readonly Cid s_cid = new("bafyreihbfkwjqz3hfyvnlwvbxbfqvwzqkqcfcjhbgvqjqvqmgqgqjqgqgq");

    private static StarterPack CreateValue() => new(
        name: "Example starter pack",
        description: "An example description",
        list: new AtUri("at://did:plc:test/app.bsky.graph.list/3lcf6ry7xy22y"),
        feeds: [new FeedItem(new AtUri("at://did:plc:test/app.bsky.feed.generator/example"))],
        createdAt: new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        updatedAt: new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero));

    private static BlueskyAgent CreateAgent(TestServer server) =>
        new(new TestHttpClientFactory(server))
        {
            Service = TestServerBuilder.DefaultUri,
            Credentials = new AccessCredentials(
                service: TestServerBuilder.DefaultUri,
                authenticationType: AuthenticationType.UsernamePassword,
                accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
                refreshToken: "refreshToken")
        };

    private static TestServer CreateServer(
        string method,
        ICollection<JsonElement> requests,
        string? error = null) =>
        TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                Assert.Equal("POST", context.Request.Method);
                Assert.Equal($"/xrpc/com.atproto.repo.{method}", context.Request.Path.Value);

                using JsonDocument document = await JsonDocument.ParseAsync(
                    context.Request.Body,
                    cancellationToken: TestContext.Current.CancellationToken);
                requests.Add(document.RootElement.Clone());
                context.Response.ContentType = "application/json";

                if (error is not null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    await context.Response.WriteAsync(
                        $"{{\"error\":\"{error}\",\"message\":\"The write was rejected.\"}}",
                        TestContext.Current.CancellationToken);
                    return;
                }

                string response = method == "deleteRecord"
                    ? $"{{\"commit\":{{\"cid\":\"{s_cid}\",\"rev\":\"3lcf6ry7xy22x\"}}}}"
                    : $"{{\"uri\":\"{s_uri}\",\"cid\":\"{s_cid}\",\"validationStatus\":\"valid\"}}";
                await context.Response.WriteAsync(response, TestContext.Current.CancellationToken);
            });

    [Theory]
    [InlineData("automatic")]
    [InlineData("keyed")]
    [InlineData("null-key")]
    public async Task CreateStarterPackWritesTheSuppliedRecord(string keyMode)
    {
        List<JsonElement> requests = [];
        using TestServer server = CreateServer("createRecord", requests);
        using BlueskyAgent agent = CreateAgent(server);
        StarterPack starterPack = CreateValue();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        AtProtoHttpResult<CreateRecordResult> result = keyMode switch
        {
            "automatic" => await agent.CreateStarterPack(starterPack, cancellationToken),
            "keyed" => await agent.CreateStarterPack(s_uri.RecordKey, starterPack, cancellationToken),
            _ => await agent.CreateStarterPack(null, starterPack, cancellationToken)
        };

        Assert.True(result.Succeeded);
        Assert.Equal(s_uri, result.Result.Uri);
        Assert.Equal(s_cid, result.Result.Cid);
        JsonElement request = Assert.Single(requests);
        AssertRecord(request, starterPack);
        Assert.False(request.TryGetProperty("swapRecord", out _));
        if (keyMode == "keyed")
        {
            Assert.Equal(s_uri.RecordKey!.Value, request.GetProperty("rkey").GetString());
        }
        else
        {
            Assert.False(request.TryGetProperty("rkey", out _));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateStarterPackWritesTheSuppliedRecordAndPreservesConcurrencyChecks(bool conditional)
    {
        List<JsonElement> requests = [];
        using TestServer server = CreateServer("putRecord", requests);
        using BlueskyAgent agent = CreateAgent(server);
        StarterPack starterPack = CreateValue();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        AtProtoHttpResult<PutRecordResult> result = conditional
            ? await agent.UpdateStarterPack(new AtProtoRepositoryRecord<StarterPack>(s_uri, s_cid, starterPack), cancellationToken)
            : await agent.UpdateStarterPack(s_uri, starterPack, cancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(s_uri, result.Result.Uri);
        Assert.Equal(s_cid, result.Result.Cid);
        JsonElement request = Assert.Single(requests);
        AssertRecord(request, starterPack);
        Assert.Equal(s_uri.RecordKey!.Value, request.GetProperty("rkey").GetString());
        if (conditional)
        {
            Assert.Equal(s_cid.Value, request.GetProperty("swapRecord").GetString());
        }
        else
        {
            Assert.False(request.TryGetProperty("swapRecord", out _));
        }
    }

    [Fact]
    public async Task DeleteStarterPackDeletesOnlyTheSpecifiedRecord()
    {
        List<JsonElement> requests = [];
        using TestServer server = CreateServer("deleteRecord", requests);
        using BlueskyAgent agent = CreateAgent(server);

        AtProtoHttpResult<DeleteResult> result = await agent.DeleteStarterPack(s_uri, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(s_cid, result.Result.Commit!.Cid);
        JsonElement request = Assert.Single(requests);
        Assert.Equal(s_did.Value, request.GetProperty("repo").GetString());
        Assert.Equal("app.bsky.graph.starterpack", request.GetProperty("collection").GetString());
        Assert.Equal(s_uri.RecordKey!.Value, request.GetProperty("rkey").GetString());
        Assert.False(request.TryGetProperty("record", out _));
    }

    [Theory]
    [InlineData("createRecord", "InvalidSwap")]
    [InlineData("createRecord", "InvalidRecord")]
    [InlineData("createRecord", "RecordAlreadyExists")]
    [InlineData("putRecord", "InvalidSwap")]
    [InlineData("putRecord", "InvalidRecord")]
    [InlineData("deleteRecord", "InvalidSwap")]
    public async Task StarterPackHelpersReturnWriteErrors(string method, string error)
    {
        List<JsonElement> requests = [];
        using TestServer server = CreateServer(method, requests, error);
        using BlueskyAgent agent = CreateAgent(server);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        switch (method)
        {
            case "createRecord":
                AssertFailure(await agent.CreateStarterPack(s_uri.RecordKey, CreateValue(), cancellationToken), error);
                break;
            case "putRecord":
                AssertFailure(await agent.UpdateStarterPack(new AtProtoRepositoryRecord<StarterPack>(s_uri, s_cid, CreateValue()), cancellationToken), error);
                break;
            default:
                AssertFailure(await agent.DeleteStarterPack(s_uri, cancellationToken), error);
                break;
        }

        Assert.Single(requests);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("create-keyed")]
    [InlineData("update")]
    [InlineData("update-record")]
    [InlineData("delete")]
    public async Task StarterPackHelpersRequireAuthentication(string operation)
    {
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => ExecuteOperation(agent, operation, s_uri));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("create-keyed")]
    [InlineData("update")]
    [InlineData("update-record")]
    [InlineData("delete")]
    public async Task StarterPackHelpersHonorCancellation(string operation)
    {
        List<JsonElement> requests = [];
        using TestServer server = CreateServer("unused", requests);
        using BlueskyAgent agent = CreateAgent(server);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExecuteOperation(agent, operation, s_uri, cancellation.Token));

        Assert.Empty(requests);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("update-record")]
    [InlineData("delete")]
    public async Task StarterPackHelpersRejectOtherOwnersWithoutWriting(string operation)
    {
        List<JsonElement> requests = [];
        using TestServer server = CreateServer("unused", requests);
        using BlueskyAgent agent = CreateAgent(server);
        AtUri otherUri = new("at://did:plc:other/app.bsky.graph.starterpack/3lcf6ry7xy22x");

        await Assert.ThrowsAsync<ArgumentException>(() => ExecuteOperation(agent, operation, otherUri));

        Assert.Empty(requests);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("update-record")]
    [InlineData("delete")]
    public async Task StarterPackHelpersValidateUrisBeforeAuthentication(string operation)
    {
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<ArgumentNullException>(() => ExecuteOperation(agent, operation, new AtUri("at://did:plc:test")));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ExecuteOperation(agent, operation, new AtUri("at://did:plc:test/app.bsky.graph.starterpack")));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ExecuteOperation(agent, operation, new AtUri("at://did:plc:test/app.bsky.graph.list/3lcf6ry7xy22x")));
    }

    [Fact]
    public async Task StarterPackHelpersRejectNullArgumentsBeforeAuthentication()
    {
        using BlueskyAgent agent = new();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentNullException>(() => agent.CreateStarterPack(null!, cancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => agent.CreateStarterPack(s_uri.RecordKey, null!, cancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => agent.UpdateStarterPack(null!, CreateValue(), cancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => agent.UpdateStarterPack(s_uri, null!, cancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => agent.UpdateStarterPack((AtProtoRepositoryRecord<StarterPack>)null!, cancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => agent.UpdateStarterPack(new AtProtoRepositoryRecord<StarterPack>(s_uri, s_cid, null!), cancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => agent.DeleteStarterPack(null!, cancellationToken));
    }

    private static Task ExecuteOperation(BlueskyAgent agent, string operation, AtUri uri, CancellationToken? cancellationToken = null)
    {
        CancellationToken token = cancellationToken ?? TestContext.Current.CancellationToken;

        return operation switch
        {
            "create" => agent.CreateStarterPack(CreateValue(), token),
            "create-keyed" => agent.CreateStarterPack(s_uri.RecordKey, CreateValue(), token),
            "update" => agent.UpdateStarterPack(uri, CreateValue(), token),
            "update-record" => agent.UpdateStarterPack(new AtProtoRepositoryRecord<StarterPack>(uri, s_cid, CreateValue()), token),
            "delete" => agent.DeleteStarterPack(uri, token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }

    private static void AssertRecord(JsonElement request, StarterPack starterPack)
    {
        Assert.Equal(s_did.Value, request.GetProperty("repo").GetString());
        Assert.Equal("app.bsky.graph.starterpack", request.GetProperty("collection").GetString());
        Assert.True(request.GetProperty("validate").GetBoolean());
        JsonElement record = request.GetProperty("record");
        Assert.Equal("app.bsky.graph.starterpack", record.GetProperty("$type").GetString());
        Assert.Equal(starterPack.Name, record.GetProperty("name").GetString());
        Assert.Equal(starterPack.Description, record.GetProperty("description").GetString());
        Assert.Equal(starterPack.List.ToString(), record.GetProperty("list").GetString());
        Assert.Equal(starterPack.Feeds![0].Uri.ToString(), Assert.Single(record.GetProperty("feeds").EnumerateArray()).GetProperty("uri").GetString());
        Assert.Equal(starterPack.CreatedAt, record.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(starterPack.UpdatedAt, record.GetProperty("updatedAt").GetDateTimeOffset());
    }

    private static void AssertFailure<T>(AtProtoHttpResult<T> result, string error)
    {
        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.NotNull(result.AtErrorDetail);
        Assert.Equal(error, result.AtErrorDetail.Error);
        Assert.Equal("The write was rejected.", result.AtErrorDetail.Message);
    }
}
