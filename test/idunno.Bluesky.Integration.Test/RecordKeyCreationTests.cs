// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.Graph;
using idunno.Bluesky.RichText;

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

    private static BlueskyAgent CreateAgent(TestServer testServer, IFacetExtractor? facetExtractor = null) =>
        new(new TestHttpClientFactory(testServer), new BlueskyAgentOptions { FacetExtractor = facetExtractor })
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

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t\t")]
    [InlineData("\n\n")]
    [InlineData(" \t\r\n ")]
    public async Task PostingOverloadsPreserveWhitespaceOnlyText(string text)
    {
            List<JsonElement> requests = [];
            using TestServer testServer = CreateRecordServer(requests);
            using BlueskyAgent agent = CreateAgent(testServer);

            Blob blob = new(new CidLink("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpeg", 1024);
            EmbeddedImage image = new(blob, "alt text");
            EmbeddedVideo video = new(blob, altText: "video");
            EmbeddedExternal externalCard = new("https://example.com", "Example", "An example card");
            RecordKey key = TimestampIdentifier.Next();
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;

            Assert.True((await agent.Post(text, cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(key, text, cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(text, "en", cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(key, text, "en", cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(text, image, cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(key, text, image, cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(text, new[] { image }, cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(key, text, new[] { image }, cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(text, video, cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(key, text, video, cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(text, externalCard, cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(key, text, externalCard, cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(new Post(text), cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(key, new Post(text), cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(new PostBuilder(text), cancellationToken)).Succeeded);
            Assert.True((await agent.Post(key, new PostBuilder(text), cancellationToken)).Succeeded);
            PostBuilder builder = new();
            builder.WithText(text);
            builder.EmbedRecord(externalCard);
            Assert.True((await agent.Post(builder, cancellationToken)).Succeeded);
            Assert.True((await agent.Post(key, builder, cancellationToken)).Succeeded);

            Assert.Equal(18, requests.Count);
            for (int index = 0; index < requests.Count; index++)
            {
                JsonElement request = requests[index];
                Assert.Equal("app.bsky.feed.post", request.GetProperty("collection").GetString());
                JsonElement record = request.GetProperty("record");
                Assert.Equal("app.bsky.feed.post", record.GetProperty("$type").GetString());
                Assert.Equal(text, record.GetProperty("text").GetString());
                Assert.Equal(index is >= 4 and <= 11 or >= 16, record.TryGetProperty("embed", out _));

                if (index % 2 == 1)
                {
                    Assert.Equal(key.Value, request.GetProperty("rkey").GetString());
                }
            }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task TextPostingOverloadsStillRejectNullOrEmptyText(string? text)
    {
            List<JsonElement> requests = [];
            using TestServer testServer = CreateRecordServer(requests);
            using BlueskyAgent agent = CreateAgent(testServer);
            RecordKey key = TimestampIdentifier.Next();
            Blob blob = new(new CidLink("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpeg", 1024);
            EmbeddedImage image = new(blob, "alt text");
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;

            if (text is null)
            {
                await Assert.ThrowsAsync<ArgumentNullException>(() => agent.Post(text!, cancellationToken: cancellationToken));
                await Assert.ThrowsAsync<ArgumentNullException>(() => agent.Post(key, text!, cancellationToken: cancellationToken));
                await Assert.ThrowsAsync<ArgumentNullException>(() => agent.Post(text!, image, cancellationToken: cancellationToken));
                await Assert.ThrowsAsync<ArgumentNullException>(() => agent.Post(key, text!, image, cancellationToken: cancellationToken));
            }
            else
            {
                await Assert.ThrowsAsync<ArgumentException>(() => agent.Post(text, cancellationToken: cancellationToken));
                await Assert.ThrowsAsync<ArgumentException>(() => agent.Post(key, text, cancellationToken: cancellationToken));
                await Assert.ThrowsAsync<ArgumentException>(() => agent.Post(text, image, cancellationToken: cancellationToken));
                await Assert.ThrowsAsync<ArgumentException>(() => agent.Post(key, text, image, cancellationToken: cancellationToken));
            }

            Assert.Empty(requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostingStillEnforcesTextLengthLimits(bool testBytes)
    {
            List<JsonElement> requests = [];
            using TestServer testServer = CreateRecordServer(requests);
            using BlueskyAgent agent = CreateAgent(testServer);
            RecordKey key = TimestampIdentifier.Next();
            string atLimit = testBytes
                ? "a" + new string('\u0301', (Maximum.PostLengthInBytes - 2) / 2) + "b"
                : new string(' ', Maximum.PostLengthInGraphemes);
            string overLimit = atLimit + (testBytes ? "c" : " ");
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;

            Assert.True((await agent.Post(atLimit, cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(key, atLimit, cancellationToken: cancellationToken)).Succeeded);
            Assert.True((await agent.Post(new PostBuilder(atLimit), cancellationToken)).Succeeded);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => agent.Post(overLimit, cancellationToken: cancellationToken));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => agent.Post(key, overLimit, cancellationToken: cancellationToken));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PostBuilder(overLimit));
            Assert.Equal(3, requests.Count);
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
    public async Task KeyedPostAndQuoteOverloadsForwardTheirKeys()
    {
        List<JsonElement> requests = [];
        using TestServer testServer = CreateRecordServer(requests);
        using BlueskyAgent agent = CreateAgent(testServer);

        Blob blob = new(new CidLink("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpeg", 1024);
        EmbeddedImage image = new(blob, "alt text");
        EmbeddedVideo video = new(blob, altText: "video");
        EmbeddedExternal externalCard = new("https://example.com", "Example", "An example card");
        StrongReference quoteReference = new(s_postUri, s_cid);
        RecordKey[] keys = Enumerable.Range(0, 8).Select(_ => (RecordKey)TimestampIdentifier.Next()).ToArray();

        Assert.True((await agent.Post(keys[0], "language post", "en", extractFacets: false, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.Post(keys[1], "image post", image, extractFacets: false, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.Post(keys[2], "images post", new[] { image }, extractFacets: false, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.Post(keys[3], "video post", video, extractFacets: false, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.Post(keys[4], "card post", externalCard, extractFacets: false, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.Quote(keys[5], quoteReference, "quoted text", cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.Quote(keys[6], quoteReference, "quoted image", image, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.Quote(keys[7], quoteReference, "quoted images", new[] { image }, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);

        Assert.Equal(keys.Length, requests.Count);
        for (int index = 0; index < keys.Length; index++)
        {
            Assert.Equal(keys[index].Value, requests[index].GetProperty("rkey").GetString());
        }

        Assert.Equal("en", requests[0].GetProperty("record").GetProperty("langs")[0].GetString());
        Assert.True(requests[1].GetProperty("record").TryGetProperty("embed", out _));
        Assert.True(requests[2].GetProperty("record").TryGetProperty("embed", out _));
        Assert.True(requests[3].GetProperty("record").TryGetProperty("embed", out _));
        Assert.True(requests[4].GetProperty("record").TryGetProperty("embed", out _));
        Assert.Equal(s_postUri.ToString(), requests[5].GetProperty("record").GetProperty("embed").GetProperty("record").GetProperty("uri").GetString());
        Assert.True(requests[6].GetProperty("record").TryGetProperty("embed", out _));
        Assert.True(requests[7].GetProperty("record").TryGetProperty("embed", out _));
    }

    [Fact]
    public async Task KeyedExternalCardPostForwardsItsKey()
    {
        List<JsonElement> requests = [];
        using TestServer testServer = CreateApplyWritesServer(requests);
        using BlueskyAgent agent = CreateAgent(testServer);

        RecordKey key = TimestampIdentifier.Next();
        EmbeddedExternal externalCard = new("https://example.com", "Example", "An example card");
        AtProtoHttpResult<CreateRecordResult> result = await agent.Post(
            key,
            externalCard,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        JsonElement operation = Assert.Single(requests).GetProperty("writes")[0];
        Assert.Equal("app.bsky.feed.post", operation.GetProperty("collection").GetString());
        Assert.Equal(key.Value, operation.GetProperty("rkey").GetString());
        Assert.Equal($"at://{s_did}/app.bsky.feed.post/{key}", result.Result.Uri.ToString());
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

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task VideoAndCardQuotesPreserveMediaAndReference(bool useVideo, bool hasText, bool useKey)
    {
        List<JsonElement> requests = [];
        using TestServer testServer = hasText ? CreateRecordServer(requests) : CreateApplyWritesServer(requests);
        using BlueskyAgent agent = CreateAgent(testServer);
        StrongReference reference = new(s_postUri, s_cid);
        RecordKey key = TimestampIdentifier.Next();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        EmbeddedExternal card = new("https://example.com", "Card title", "Card description");
        EmbeddedVideo video = new(
            new Blob(new CidLink(s_cid.Value), "video/mp4", 1024),
            altText: "Video description");
        AtProtoHttpResult<CreateRecordResult> result;

        if (useVideo)
        {
            result = (hasText, useKey) switch
            {
                (true, true) => await agent.Quote(key, reference, "Quote text", video, ["tag"], cancellationToken),
                (true, false) => await agent.Quote(reference, "Quote text", video, ["tag"], cancellationToken),
                (false, true) => await agent.Quote(key, reference, video, ["tag"], cancellationToken),
                (false, false) => await agent.Quote(reference, video, ["tag"], cancellationToken)
            };
        }
        else
        {
            result = (hasText, useKey) switch
            {
                (true, true) => await agent.Quote(key, reference, "Quote text", card, ["tag"], cancellationToken),
                (true, false) => await agent.Quote(reference, "Quote text", card, ["tag"], cancellationToken),
                (false, true) => await agent.Quote(key, reference, card, ["tag"], cancellationToken),
                (false, false) => await agent.Quote(reference, card, ["tag"], cancellationToken)
            };
        }

        Assert.True(result.Succeeded);
        JsonElement request = Assert.Single(requests);
        JsonElement operation = hasText ? request : Assert.Single(request.GetProperty("writes").EnumerateArray());
        Assert.Equal("app.bsky.feed.post", operation.GetProperty("collection").GetString());
        if (useKey)
        {
            Assert.Equal(key.Value, operation.GetProperty("rkey").GetString());
            Assert.Equal($"at://{s_did}/app.bsky.feed.post/{key}", result.Result.Uri.ToString());
        }
        else if (hasText)
        {
            Assert.False(operation.TryGetProperty("rkey", out _));
        }

        JsonElement post = operation.GetProperty(hasText ? "record" : "value");
        Assert.Equal(hasText ? "Quote text" : string.Empty, post.GetProperty("text").GetString());
        Assert.Equal("tag", post.GetProperty("tags")[0].GetString());
        JsonElement embed = post.GetProperty("embed");
        Assert.Equal("app.bsky.embed.recordWithMedia", embed.GetProperty("$type").GetString());
        JsonElement quotedRecord = embed.GetProperty("record").GetProperty("record");
        Assert.Equal(s_postUri.ToString(), quotedRecord.GetProperty("uri").GetString());
        Assert.Equal(s_cid.Value, quotedRecord.GetProperty("cid").GetString());
        JsonElement media = embed.GetProperty("media");
        Assert.Equal(useVideo ? "app.bsky.embed.video" : "app.bsky.embed.external", media.GetProperty("$type").GetString());
        if (useVideo)
        {
            Assert.Equal("Video description", media.GetProperty("alt").GetString());
        }
        else
        {
            Assert.Equal("https://example.com", media.GetProperty("external").GetProperty("uri").GetString());
            Assert.Equal("Card title", media.GetProperty("external").GetProperty("title").GetString());
        }
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

    [Theory]
    [MemberData(nameof(ReplyFacetCases))]
    public async Task ReplyToForwardsItsKeyAndKeepsTheResolvedRootAndParent(int variant, bool useKey, int mode)
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
                    string rKey = body.TryGetProperty("rkey", out JsonElement suppliedKey) ? suppliedKey.GetString()! : "3lcf6ry7xy22x";
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(
                        $"{{\"uri\":\"at://{s_did}/{collection}/{rKey}\",\"cid\":\"{s_cid}\"}}",
                        TestContext.Current.CancellationToken);
                    return;
                }

                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            });

        RecordingFacetExtractor extractor = new();
        using BlueskyAgent agent = CreateAgent(testServer, extractor);
        RecordKey key = TimestampIdentifier.Next();
        const string text = "https://example.com @example.test";
        StrongReference parent = new(parentUri, s_cid);
        EmbeddedImage image = new(new Blob(new CidLink(s_cid.Value), "image/jpeg", 1024), "Image");
        CancellationToken token = TestContext.Current.CancellationToken;
        bool extractFacets = mode != 2;
        AtProtoHttpResult<CreateRecordResult> result = mode == 0
            ? (variant, useKey) switch
            {
                (0, false) => await agent.ReplyTo(parent, text, cancellationToken: token),
                (0, true) => await agent.ReplyTo(key, parent, text, cancellationToken: token),
                (1, false) => await agent.ReplyTo(parent, text, image, cancellationToken: token),
                (1, true) => await agent.ReplyTo(key, parent, text, image, cancellationToken: token),
                (2, false) => await agent.ReplyTo(parent, text, images: [image], cancellationToken: token),
                (2, true) => await agent.ReplyTo(key, parent, text, images: [image], cancellationToken: token),
                _ => throw new InvalidOperationException()
            }
            : (variant, useKey) switch
            {
                (0, false) => await agent.ReplyTo(parent, text, extractFacets: extractFacets, cancellationToken: token),
                (0, true) => await agent.ReplyTo(key, parent, text, extractFacets: extractFacets, cancellationToken: token),
                (1, false) => await agent.ReplyTo(parent, text, image, extractFacets: extractFacets, cancellationToken: token),
                (1, true) => await agent.ReplyTo(key, parent, text, image, extractFacets: extractFacets, cancellationToken: token),
                (2, false) => await agent.ReplyTo(parent, text, images: [image], extractFacets: extractFacets, cancellationToken: token),
                (2, true) => await agent.ReplyTo(key, parent, text, images: [image], extractFacets: extractFacets, cancellationToken: token),
                _ => throw new InvalidOperationException()
            };

        Assert.True(result.Succeeded);
        Assert.Equal(mode == 2 ? 0 : 1, extractor.CallCount);
        JsonElement record = Assert.Single(createRequests).GetProperty("record");
        Assert.Equal(useKey, createRequests[0].TryGetProperty("rkey", out JsonElement rKey));
        if (useKey)
        {
            Assert.Equal(key.Value, rKey.GetString());
        }
        Assert.Equal(text, record.GetProperty("text").GetString());
        Assert.Equal(mode != 2, record.TryGetProperty("facets", out _));
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
    public static TheoryData<int, bool, int> ReplyFacetCases =>
        new(QuoteFacetCases.Where(testCase => testCase.Data.Item1 < 3));

    public static TheoryData<int, bool, int> QuoteFacetCases
    {
        get
        {
            TheoryData<int, bool, int> cases = [];
            for (int variant = 0; variant < 5; variant++)
            {
                foreach (bool useKey in new[] { false, true })
                {
                    for (int mode = 0; mode < 3; mode++)
                    {
                        cases.Add(variant, useKey, mode);
                    }
                }
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(QuoteFacetCases))]
    public async Task QuoteHelpersControlFacetExtraction(int variant, bool useKey, int mode)
    {
        List<JsonElement> requests = [];
        using TestServer server = CreateRecordServer(requests);
        RecordingFacetExtractor extractor = new();
        using BlueskyAgent agent = CreateAgent(server, extractor);
        StrongReference reference = new(s_postUri, s_cid);
        RecordKey key = TimestampIdentifier.Next();
        const string text = "https://example.com @example.test";
        CancellationToken token = TestContext.Current.CancellationToken;
        Blob blob = new(new CidLink(s_cid.Value), "image/jpeg", 1024);
        EmbeddedImage image = new(blob, "Image");
        EmbeddedVideo video = new(blob, altText: "Video");
        EmbeddedExternal card = new("https://example.com", "Title", "Description");
        bool extractFacets = mode != 2;
        AtProtoHttpResult<CreateRecordResult> result;

        if (mode == 0)
        {
            result = (variant, useKey) switch
            {
                (0, false) => await agent.Quote(reference, text, null, token),
                (0, true) => await agent.Quote(key, reference, text, null, token),
                (1, false) => await agent.Quote(reference, text, image, null, token),
                (1, true) => await agent.Quote(key, reference, text, image, null, token),
                (2, false) => await agent.Quote(reference, text, [image], null, token),
                (2, true) => await agent.Quote(key, reference, text, [image], null, token),
                (3, false) => await agent.Quote(reference, text, video, null, token),
                (3, true) => await agent.Quote(key, reference, text, video, null, token),
                (4, false) => await agent.Quote(reference, text, card, null, token),
                (4, true) => await agent.Quote(key, reference, text, card, null, token),
                _ => throw new InvalidOperationException()
            };
        }
        else
        {
            result = (variant, useKey) switch
            {
                (0, false) => await agent.Quote(reference, text: text, extractFacets: extractFacets, cancellationToken: token),
                (0, true) => await agent.Quote(key, reference, text: text, extractFacets: extractFacets, cancellationToken: token),
                (1, false) => await agent.Quote(reference, text: text, image: image, extractFacets: extractFacets, cancellationToken: token),
                (1, true) => await agent.Quote(key, reference, text: text, image: image, extractFacets: extractFacets, cancellationToken: token),
                (2, false) => await agent.Quote(reference, text: text, images: [image], extractFacets: extractFacets, cancellationToken: token),
                (2, true) => await agent.Quote(key, reference, text: text, images: [image], extractFacets: extractFacets, cancellationToken: token),
                (3, false) => await agent.Quote(reference, text: text, video: video, extractFacets: extractFacets, cancellationToken: token),
                (3, true) => await agent.Quote(key, reference, text: text, video: video, extractFacets: extractFacets, cancellationToken: token),
                (4, false) => await agent.Quote(reference, text: text, externalCard: card, extractFacets: extractFacets, cancellationToken: token),
                (4, true) => await agent.Quote(key, reference, text: text, externalCard: card, extractFacets: extractFacets, cancellationToken: token),
                _ => throw new InvalidOperationException()
            };
        }

        Assert.True(result.Succeeded);
        JsonElement request = Assert.Single(requests);
        Assert.Equal(useKey, request.TryGetProperty("rkey", out JsonElement suppliedKey));
        if (useKey)
        {
            Assert.Equal(key.Value, suppliedKey.GetString());
        }
        JsonElement post = request.GetProperty("record");
        Assert.Equal(text, post.GetProperty("text").GetString());
        if (variant is 3 or 4)
        {
            Assert.Equal(Thread.CurrentThread.CurrentUICulture.Name, post.GetProperty("langs")[0].GetString());
        }
        Assert.Equal(extractFacets ? 1 : 0, extractor.CallCount);
        Assert.Equal(extractFacets, post.TryGetProperty("facets", out JsonElement facets));
        if (extractFacets)
        {
            Assert.Equal(text, extractor.Text);
            Assert.Equal(token, extractor.CancellationToken);
            Assert.Equal(2, facets.GetArrayLength());
            Assert.Equal("https://example.com", facets[0].GetProperty("features")[0].GetProperty("uri").GetString());
            Assert.Equal(s_did.Value, facets[1].GetProperty("features")[0].GetProperty("did").GetString());
        }
        JsonElement embed = post.GetProperty("embed");
        JsonElement quoted = variant == 0 ? embed.GetProperty("record") : embed.GetProperty("record").GetProperty("record");
        Assert.Equal(s_postUri.ToString(), quoted.GetProperty("uri").GetString());
        Assert.Equal(s_cid.Value, quoted.GetProperty("cid").GetString());
    }

    [Theory]
    [MemberData(nameof(QuoteFacetCases))]
    public async Task PostHelpersControlFacetExtraction(int variant, bool useKey, int mode)
    {
        List<JsonElement> requests = [];
        using TestServer server = CreateRecordServer(requests);
        RecordingFacetExtractor extractor = new();
        using BlueskyAgent agent = CreateAgent(server, extractor);
        RecordKey key = TimestampIdentifier.Next();
        const string text = "https://example.com @example.test";
        CancellationToken token = TestContext.Current.CancellationToken;
        Blob blob = new(new CidLink(s_cid.Value), "image/jpeg", 1024);
        EmbeddedImage image = new(blob, "Image");
        EmbeddedVideo video = new(blob, altText: "Video");
        EmbeddedExternal card = new("https://example.com", "Title", "Description");
        bool extractFacets = mode != 2;
        AtProtoHttpResult<CreateRecordResult> result;

        if (mode == 0)
        {
            result = (variant, useKey) switch
            {
                (0, false) => await agent.Post(text, cancellationToken: token),
                (0, true) => await agent.Post(key, text, cancellationToken: token),
                (1, false) => await agent.Post(text, image, cancellationToken: token),
                (1, true) => await agent.Post(key, text, image, cancellationToken: token),
                (2, false) => await agent.Post(text, images: [image], cancellationToken: token),
                (2, true) => await agent.Post(key, text, images: [image], cancellationToken: token),
                (3, false) => await agent.Post(text, video, cancellationToken: token),
                (3, true) => await agent.Post(key, text, video, cancellationToken: token),
                (4, false) => await agent.Post(text, card, cancellationToken: token),
                (4, true) => await agent.Post(key, text, card, cancellationToken: token),
                _ => throw new InvalidOperationException()
            };
        }
        else
        {
            result = (variant, useKey) switch
            {
                (0, false) => await agent.Post(text, extractFacets: extractFacets, cancellationToken: token),
                (0, true) => await agent.Post(key, text, extractFacets: extractFacets, cancellationToken: token),
                (1, false) => await agent.Post(text, image, extractFacets: extractFacets, cancellationToken: token),
                (1, true) => await agent.Post(key, text, image, extractFacets: extractFacets, cancellationToken: token),
                (2, false) => await agent.Post(text, images: [image], extractFacets: extractFacets, cancellationToken: token),
                (2, true) => await agent.Post(key, text, images: [image], extractFacets: extractFacets, cancellationToken: token),
                (3, false) => await agent.Post(text, video, extractFacets: extractFacets, cancellationToken: token),
                (3, true) => await agent.Post(key, text, video, extractFacets: extractFacets, cancellationToken: token),
                (4, false) => await agent.Post(text, card, extractFacets: extractFacets, cancellationToken: token),
                (4, true) => await agent.Post(key, text, card, extractFacets: extractFacets, cancellationToken: token),
                _ => throw new InvalidOperationException()
            };
        }

        Assert.True(result.Succeeded);
        JsonElement request = Assert.Single(requests);
        Assert.Equal(useKey, request.TryGetProperty("rkey", out JsonElement suppliedKey));
        if (useKey)
        {
            Assert.Equal(key.Value, suppliedKey.GetString());
        }
        JsonElement post = request.GetProperty("record");
        Assert.Equal(text, post.GetProperty("text").GetString());
        Assert.Equal(extractFacets ? 1 : 0, extractor.CallCount);
        Assert.Equal(extractFacets, post.TryGetProperty("facets", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BuilderPublishingKeepsPreparedFacetsWithoutExtraction(bool useKey)
    {
        List<JsonElement> requests = [];
        using TestServer server = CreateRecordServer(requests);
        RecordingFacetExtractor extractor = new();
        using BlueskyAgent agent = CreateAgent(server, extractor);
        RecordKey key = TimestampIdentifier.Next();
        PostBuilder builder = new("https://example.com", facets:
        [
            new(new ByteSlice(0, 19), [new LinkFacetFeature("https://example.com")])
        ]);
        AtProtoHttpResult<CreateRecordResult> result = useKey
            ? await agent.Post(key, builder, TestContext.Current.CancellationToken)
            : await agent.Post(builder, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(0, extractor.CallCount);
        JsonElement facets = Assert.Single(requests).GetProperty("record").GetProperty("facets");
        Assert.Equal(1, facets.GetArrayLength());
        Assert.Equal("https://example.com", facets[0].GetProperty("features")[0].GetProperty("uri").GetString());
    }

    [Theory]
    [MemberData(nameof(QuoteFacetCases))]
    public async Task TextlessQuotesDoNotExtractFacets(int variant, bool useKey, int mode)
    {
        List<JsonElement> requests = [];
        using TestServer server = CreateApplyWritesServer(requests);
        RecordingFacetExtractor extractor = new();
        using BlueskyAgent agent = CreateAgent(server, extractor);
        StrongReference reference = new(s_postUri, s_cid);
        RecordKey key = TimestampIdentifier.Next();
        CancellationToken token = TestContext.Current.CancellationToken;
        Blob blob = new(new CidLink(s_cid.Value), "image/jpeg", 1024);
        EmbeddedImage image = new(blob, "Image");
        EmbeddedVideo video = new(blob, altText: "Video");
        EmbeddedExternal card = new("https://example.com", "Title", "Description");
        bool extractFacets = mode != 2;
        AtProtoHttpResult<CreateRecordResult> result = (variant, useKey) switch
        {
            (0, false) => await agent.Quote(reference, extractFacets: extractFacets, cancellationToken: token),
            (0, true) => await agent.Quote(key, reference, extractFacets: extractFacets, cancellationToken: token),
            (1, false) => await agent.Quote(reference, image: image, extractFacets: extractFacets, cancellationToken: token),
            (1, true) => await agent.Quote(key, reference, image: image, extractFacets: extractFacets, cancellationToken: token),
            (2, false) => await agent.Quote(reference, images: [image], extractFacets: extractFacets, cancellationToken: token),
            (2, true) => await agent.Quote(key, reference, images: [image], extractFacets: extractFacets, cancellationToken: token),
            (3, false) => await agent.Quote(reference, video: video, extractFacets: extractFacets, cancellationToken: token),
            (3, true) => await agent.Quote(key, reference, video: video, extractFacets: extractFacets, cancellationToken: token),
            (4, false) => await agent.Quote(reference, externalCard: card, extractFacets: extractFacets, cancellationToken: token),
            (4, true) => await agent.Quote(key, reference, externalCard: card, extractFacets: extractFacets, cancellationToken: token),
            _ => throw new InvalidOperationException()
        };

        Assert.True(result.Succeeded);
        Assert.Equal(0, extractor.CallCount);
        JsonElement operation = Assert.Single(Assert.Single(requests).GetProperty("writes").EnumerateArray());
        Assert.Equal(string.Empty, operation.GetProperty("value").GetProperty("text").GetString());
        Assert.False(operation.GetProperty("value").TryGetProperty("facets", out _));
        Assert.False(operation.GetProperty("value").TryGetProperty("langs", out _));
        if (useKey)
        {
            Assert.Equal(key.Value, operation.GetProperty("rkey").GetString());
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CardOnlyPostsAcceptFacetControl(bool useKey, bool extractFacets)
    {
        List<JsonElement> requests = [];
        using TestServer server = CreateApplyWritesServer(requests);
        RecordingFacetExtractor extractor = new();
        using BlueskyAgent agent = CreateAgent(server, extractor);
        RecordKey key = TimestampIdentifier.Next();
        EmbeddedExternal card = new("https://example.com", "Title", "Description");
        AtProtoHttpResult<CreateRecordResult> result = useKey
            ? await agent.Post(key, card, extractFacets: extractFacets, cancellationToken: TestContext.Current.CancellationToken)
            : await agent.Post(card, extractFacets: extractFacets, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(0, extractor.CallCount);
        JsonElement operation = Assert.Single(Assert.Single(requests).GetProperty("writes").EnumerateArray());
        Assert.Equal(string.Empty, operation.GetProperty("value").GetProperty("text").GetString());
        Assert.False(operation.GetProperty("value").TryGetProperty("facets", out _));
    }

    private sealed class RecordingFacetExtractor : IFacetExtractor
    {
        public int CallCount { get; private set; }

        public string? Text { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<IList<Facet>> ExtractFacets(string text, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Text = text;
            CancellationToken = cancellationToken;

            return Task.FromResult<IList<Facet>>(
            [
                new(new ByteSlice(0, 19), [new LinkFacetFeature("https://example.com")]),
                new(new ByteSlice(20, 33), [new MentionFacetFeature(s_did)])
            ]);
        }
    }
}
