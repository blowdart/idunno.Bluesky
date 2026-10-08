// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.Graph;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class RecordKeyCreationTests
{
    private static readonly Did s_did = new("did:plc:test");

    private static readonly Cid s_cid = new("bafyreihbfkwjqz3hfyvnlwvbxbfqvwzqkqcfcjhbgvqjqvqmgqgqjqgqgq");

    private static readonly AtUri s_postUri = new("at://did:plc:test/app.bsky.feed.post/3lcf6ry7xy22x");

    private static readonly AtUri s_listUri = new("at://did:plc:test/app.bsky.graph.list/3lcf6ry7xy22x");

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    private static BlueskyAgent CreateAgent(TestServer testServer) =>
        new(new TestHttpClientFactory(testServer))
        {
            Credentials = CreateCredentials(),
            Service = TestServerBuilder.DefaultUri
        };

    private static TestServer CreateRecordServer(
        ICollection<JsonElement> requests,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                if (context.Request.Path != "/xrpc/com.atproto.repo.createRecord")
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                using JsonDocument request = await JsonDocument.ParseAsync(
                    context.Request.Body,
                    cancellationToken: TestContext.Current.CancellationToken);
                requests.Add(request.RootElement.Clone());
                context.Response.StatusCode = (int)statusCode;
                context.Response.ContentType = "application/json";

                if (statusCode != HttpStatusCode.OK)
                {
                    await context.Response.WriteAsync(
                        """{"error":"RecordAlreadyExists","message":"record already exists"}""",
                        TestContext.Current.CancellationToken);
                    return;
                }

                JsonElement body = request.RootElement;
                string collection = body.GetProperty("collection").GetString()!;
                string rKey = body.TryGetProperty("rkey", out JsonElement key)
                    ? key.GetString()!
                    : "3lcf6ry7xy22x";
                string response = $"{{\"uri\":\"at://{s_did}/{collection}/{rKey}\",\"cid\":\"{s_cid}\",\"validationStatus\":\"valid\"}}";
                await context.Response.WriteAsync(response, TestContext.Current.CancellationToken);
            });
    }

    private static TestServer CreateApplyWritesServer(ICollection<JsonElement> requests)
    {
        return TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                if (context.Request.Path != "/xrpc/com.atproto.repo.applyWrites")
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                using JsonDocument request = await JsonDocument.ParseAsync(
                    context.Request.Body,
                    cancellationToken: TestContext.Current.CancellationToken);
                requests.Add(request.RootElement.Clone());

                List<string> results = [];
                foreach (JsonElement operation in request.RootElement.GetProperty("writes").EnumerateArray())
                {
                    string collection = operation.GetProperty("collection").GetString()!;
                    string rKey = operation.GetProperty("rkey").GetString()!;
                    results.Add(
                        $"{{\"$type\":\"com.atproto.repo.applyWrites#createResult\",\"uri\":\"at://{s_did}/{collection}/{rKey}\",\"cid\":\"{s_cid}\",\"validationStatus\":\"valid\"}}");
                }

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    $"{{\"commit\":{{\"cid\":\"{s_cid}\",\"rev\":\"3lcf6ry7xy22x\"}},\"results\":[{string.Join(",", results)}]}}",
                    TestContext.Current.CancellationToken);
            });
    }

    [Fact]
    public async Task SupportedCreationHelpersForwardTheirRecordKeys()
    {
        List<JsonElement> requests = [];
        using TestServer testServer = CreateRecordServer(requests);
        using BlueskyAgent agent = CreateAgent(testServer);

        StrongReference postReference = new(s_postUri, s_cid);
        RecordKey likeKey = TimestampIdentifier.Next();
        RecordKey repostKey = TimestampIdentifier.Next();
        RecordKey followKey = TimestampIdentifier.Next();
        RecordKey blockKey = TimestampIdentifier.Next();
        RecordKey listKey = TimestampIdentifier.Next();
        RecordKey listItemKey = TimestampIdentifier.Next();
        RecordKey listBlockKey = TimestampIdentifier.Next();
        RecordKey optOutKey = TimestampIdentifier.Next();
        RecordKey postKey = TimestampIdentifier.Next();

        Assert.True((await agent.Like(likeKey, postReference, TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.Repost(repostKey, postReference, TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.Follow(followKey, s_did, TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.Block(blockKey, s_did, TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.CreateList(
            listKey,
            new List("test list", ListPurpose.CurateList, description: null),
            TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.AddToList(
            listItemKey,
            s_listUri,
            s_did,
            TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.BlockModList(
            listBlockKey,
            s_listUri,
            TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.CreateReferenceListOptOut(
            optOutKey,
            s_listUri,
            TestContext.Current.CancellationToken)).Succeeded);

        DateTimeOffset createdAt = new(2025, 1, 2, 3, 4, 5, TimeSpan.Zero);
        StrongReference root = new(s_postUri, s_cid);
        StrongReference parent = new(new AtUri("at://did:plc:test/app.bsky.feed.post/3lcf6ry7xy23x"), s_cid);
        ReplyReferences reply = new(parent, root);
        Post post = new("fixed post text", createdAt, reply: reply);
        Assert.True((await agent.Post(
            postKey,
            post,
            extractFacets: false,
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded);

        Dictionary<string, RecordKey> expectedKeys = new(StringComparer.Ordinal)
        {
            ["app.bsky.feed.like"] = likeKey,
            ["app.bsky.feed.repost"] = repostKey,
            ["app.bsky.graph.follow"] = followKey,
            ["app.bsky.graph.block"] = blockKey,
            ["app.bsky.graph.list"] = listKey,
            ["app.bsky.graph.listitem"] = listItemKey,
            ["app.bsky.graph.listblock"] = listBlockKey,
            ["app.bsky.graph.referencelistoptout"] = optOutKey,
            ["app.bsky.feed.post"] = postKey,
        };

        Assert.Equal(expectedKeys.Count, requests.Count);

        foreach (JsonElement request in requests)
        {
            string collection = request.GetProperty("collection").GetString()!;
            Assert.Equal(expectedKeys[collection].Value, request.GetProperty("rkey").GetString());
        }

        JsonElement Record(string collection) => requests.Single(request =>
            request.GetProperty("collection").GetString() == collection).GetProperty("record");

        Assert.Equal(s_postUri.ToString(), Record("app.bsky.feed.like").GetProperty("subject").GetProperty("uri").GetString());
        Assert.Equal(s_postUri.ToString(), Record("app.bsky.feed.repost").GetProperty("subject").GetProperty("uri").GetString());
        Assert.Equal(s_did.ToString(), Record("app.bsky.graph.follow").GetProperty("subject").GetString());
        Assert.Equal(s_did.ToString(), Record("app.bsky.graph.block").GetProperty("subject").GetString());
        Assert.Equal("test list", Record("app.bsky.graph.list").GetProperty("name").GetString());
        Assert.Equal(s_listUri.ToString(), Record("app.bsky.graph.listitem").GetProperty("list").GetString());
        Assert.Equal(s_did.ToString(), Record("app.bsky.graph.listitem").GetProperty("subject").GetString());
        Assert.Equal(s_listUri.ToString(), Record("app.bsky.graph.listblock").GetProperty("subject").GetString());
        Assert.Equal(s_listUri.ToString(), Record("app.bsky.graph.referencelistoptout").GetProperty("subject").GetString());

        JsonElement postRecord = requests.Single(request =>
            request.GetProperty("collection").GetString() == "app.bsky.feed.post").GetProperty("record");
        Assert.Equal("fixed post text", postRecord.GetProperty("text").GetString());
        Assert.Equal(createdAt, postRecord.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(root.Uri.ToString(), postRecord.GetProperty("reply").GetProperty("root").GetProperty("uri").GetString());
        Assert.Equal(parent.Uri.ToString(), postRecord.GetProperty("reply").GetProperty("parent").GetProperty("uri").GetString());
        Assert.Equal(createdAt, post.CreatedAt);
        Assert.Equal(reply, post.Reply);
    }

    [Fact]
    public async Task SingletonStatusKeepsItsProtocolDefinedKey()
    {
        List<JsonElement> requests = [];
        using TestServer testServer = CreateRecordServer(requests);
        using BlueskyAgent agent = CreateAgent(testServer);

        Assert.True((await agent.CreateStatus(
            new Status(KnownStatusValues.Live),
            TestContext.Current.CancellationToken)).Succeeded);

        JsonElement request = Assert.Single(requests);
        Assert.Equal("app.bsky.actor.status", request.GetProperty("collection").GetString());
        Assert.Equal("self", request.GetProperty("rkey").GetString());
    }

    [Fact]
    public async Task KeyedListItemStillValidatesItsListUriBeforeSending()
    {
        using BlueskyAgent agent = new();
        RecordKey key = TimestampIdentifier.Next();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => agent.AddToList(
            key,
            s_postUri,
            s_did,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OmittedRecordKeyKeepsServerGeneratedKeyBehavior()
    {
        List<JsonElement> requests = [];
        using TestServer testServer = CreateRecordServer(requests);
        using BlueskyAgent agent = CreateAgent(testServer);

        Assert.True((await agent.Follow(s_did, TestContext.Current.CancellationToken)).Succeeded);

        Assert.Single(requests);
        Assert.False(requests[0].TryGetProperty("rkey", out _));
    }

    [Fact]
    public async Task ExistingRecordFailureIsReturnedWithoutRetry()
    {
        List<JsonElement> requests = [];
        using TestServer testServer = CreateRecordServer(requests, HttpStatusCode.BadRequest);
        using BlueskyAgent agent = CreateAgent(testServer);

        RecordKey key = TimestampIdentifier.Next();
        AtProtoHttpResult<CreateRecordResult> result = await agent.Like(
            key,
            new StrongReference(s_postUri, s_cid),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("RecordAlreadyExists", result.AtErrorDetail?.Error);
        Assert.Single(requests);
        Assert.Equal(key.Value, requests[0].GetProperty("rkey").GetString());
    }

    [Fact]
    public async Task PostGateUsesTheSameKeyAndUriAsItsPost()
    {
        List<JsonElement> requests = [];
        using TestServer testServer = CreateApplyWritesServer(requests);
        using BlueskyAgent agent = CreateAgent(testServer);

        RecordKey key = TimestampIdentifier.Next();
        AtProtoHttpResult<CreateRecordResult> result = await agent.Post(
            key,
            "post with gate",
            postGateRules: Array.Empty<PostGateRule>(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal($"at://{s_did}/app.bsky.feed.post/{key}", result.Result.Uri.ToString());

        JsonElement writes = Assert.Single(requests).GetProperty("writes");
        Assert.Equal(2, writes.GetArrayLength());
        Assert.All(writes.EnumerateArray(), operation => Assert.Equal(key.Value, operation.GetProperty("rkey").GetString()));
        Assert.Equal("app.bsky.feed.post", writes[0].GetProperty("collection").GetString());
        Assert.Equal("app.bsky.feed.postgate", writes[1].GetProperty("collection").GetString());
        Assert.Equal(
            result.Result.Uri.ToString(),
            writes[1].GetProperty("value").GetProperty("post").GetString());
    }

    [Fact]
    public async Task QuoteUsesTheSuppliedKeyInItsApplyWritesCreation()
    {
        List<JsonElement> requests = [];
        using TestServer testServer = CreateApplyWritesServer(requests);
        using BlueskyAgent agent = CreateAgent(testServer);

        RecordKey key = TimestampIdentifier.Next();
        AtProtoHttpResult<CreateRecordResult> result = await agent.Quote(
            key,
            new StrongReference(s_postUri, s_cid),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        JsonElement operation = Assert.Single(requests).GetProperty("writes")[0];
        Assert.Equal("app.bsky.feed.post", operation.GetProperty("collection").GetString());
        Assert.Equal(key.Value, operation.GetProperty("rkey").GetString());
        Assert.Equal($"at://{s_did}/app.bsky.feed.post/{key}", result.Result.Uri.ToString());
    }

    [Fact]
    public async Task PostBuilderForwardsTheSuppliedKey()
    {
        List<JsonElement> requests = [];
        using TestServer testServer = CreateRecordServer(requests);
        using BlueskyAgent agent = CreateAgent(testServer);

        RecordKey key = TimestampIdentifier.Next();
        PostBuilder builder = new("builder text", lang: "en");
        AtProtoHttpResult<CreateRecordResult> result = await agent.Post(
            key,
            builder,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        JsonElement request = Assert.Single(requests);
        Assert.Equal(key.Value, request.GetProperty("rkey").GetString());
        Assert.Equal("builder text", request.GetProperty("record").GetProperty("text").GetString());
    }

    [Fact]
    public async Task ReplyToForwardsItsKeyAndKeepsTheResolvedRootAndParent()
    {
        List<JsonElement> createRequests = [];
        AtUri parentUri = new("at://did:plc:test/app.bsky.feed.post/3lcf6ry7xy23x");
        string getRecordResponse = $$"""
            {
              "uri": "{{parentUri}}",
              "cid": "{{s_cid}}",
              "value": {
                "$type": "app.bsky.feed.post",
                "text": "parent post",
                "createdAt": "2025-01-01T00:00:00Z",
                "reply": {
                  "root": {
                    "uri": "{{s_postUri}}",
                    "cid": "{{s_cid}}"
                  },
                  "parent": {
                    "uri": "{{parentUri}}",
                    "cid": "{{s_cid}}"
                  }
                }
              }
            }
            """;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                if (context.Request.Path == "/xrpc/com.atproto.repo.getRecord")
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(getRecordResponse, TestContext.Current.CancellationToken);
                    return;
                }

                if (context.Request.Path == "/xrpc/com.atproto.repo.createRecord")
                {
                    using JsonDocument request = await JsonDocument.ParseAsync(
                        context.Request.Body,
                        cancellationToken: TestContext.Current.CancellationToken);
                    createRequests.Add(request.RootElement.Clone());
                    JsonElement body = request.RootElement;
                    string collection = body.GetProperty("collection").GetString()!;
                    string rKey = body.GetProperty("rkey").GetString()!;
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(
                        $"{{\"uri\":\"at://{s_did}/{collection}/{rKey}\",\"cid\":\"{s_cid}\"}}",
                        TestContext.Current.CancellationToken);
                    return;
                }

                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            });

        using BlueskyAgent agent = CreateAgent(testServer);
        RecordKey key = TimestampIdentifier.Next();
        AtProtoHttpResult<CreateRecordResult> result = await agent.ReplyTo(
            key,
            new StrongReference(parentUri, s_cid),
            "reply text",
            extractFacets: false,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        JsonElement record = Assert.Single(createRequests).GetProperty("record");
        Assert.Equal(key.Value, createRequests[0].GetProperty("rkey").GetString());
        Assert.Equal("reply text", record.GetProperty("text").GetString());
        Assert.Equal(s_postUri.ToString(), record.GetProperty("reply").GetProperty("root").GetProperty("uri").GetString());
        Assert.Equal(parentUri.ToString(), record.GetProperty("reply").GetProperty("parent").GetProperty("uri").GetString());
    }

    [Fact]
    public async Task ExistingOverloadsStillAcceptPositionalAndNamedCancellationTokens()
    {
        using BlueskyAgent agent = new();
        StrongReference reference = new(s_postUri, s_cid);
        PostBuilder builder = new("existing post", lang: "en");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.Like(reference, cancellationToken));
        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.Follow(s_did, cancellationToken));
        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.Post(builder, cancellationToken));
        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.ReplyTo(reference, "reply text", null, true, cancellationToken));
        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.Quote(strongReference: reference, cancellationToken: cancellationToken));
        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.Like(strongReference: reference, cancellationToken: cancellationToken));
        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.Post(postBuilder: builder, cancellationToken: cancellationToken));
    }
}
