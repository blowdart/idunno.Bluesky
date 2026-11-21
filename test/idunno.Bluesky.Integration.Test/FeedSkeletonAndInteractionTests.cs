// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Feed;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class FeedSkeletonAndInteractionTests
{
    private static readonly Did s_did = new("did:plc:hfgp6pj3akhqxntgqwramlbg");

    private static readonly Did s_feedGeneratorDid = new("did:web:feeds.example.com");

    private static readonly AtUri s_feed = new("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.generator/test");

    private static readonly AtUri s_post = new("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x");

    private static readonly AtUri s_repost = new("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.repost/3lcf6ry7xy22y");

    private const string SkeletonResponse = """
        {
          "cursor": "next",
          "reqId": "request-1",
          "feed": [
            { "post": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x", "feedContext": "ctx-1" },
            { "post": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x", "reason": { "$type": "app.bsky.feed.defs#skeletonReasonRepost", "repost": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.repost/3lcf6ry7xy22y" } },
            { "post": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x", "reason": { "$type": "app.bsky.feed.defs#skeletonReasonPin" } },
            { "post": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x", "reason": { "$type": "app.bsky.feed.defs#skeletonReasonFuture", "extra": 1 } },
            null
          ]
        }
        """;

    private const string FeedGeneratorResponse = """
        {
          "view": {
            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.generator/test",
            "cid": "bafyreiaxqkvwjv5y45xq7huunf6mjvh7tf6ls44imkgjkqi3uthn57vjxu",
            "did": "did:web:feeds.example.com",
            "creator": { "did": "did:plc:hfgp6pj3akhqxntgqwramlbg", "handle": "creator.test" },
            "displayName": "Test feed",
            "indexedAt": "2026-01-01T00:00:00.000Z"
          },
          "isOnline": true,
          "isValid": true
        }
        """;

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    private static BlueskyAgent CreateAgent(TestServer testServer) =>
        new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

    private sealed record CapturedRequest(string Path, string Query, string? Proxy, string Body);

    private static TestServer CreateServer(
        List<CapturedRequest> captured,
        Func<string, (HttpStatusCode, string)?> respond) =>
        TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                using StreamReader reader = new(context.Request.Body);
                string body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

                lock (captured)
                {
                    captured.Add(new CapturedRequest(
                        context.Request.Path.Value ?? string.Empty,
                        context.Request.QueryString.Value ?? string.Empty,
                        context.Request.Headers["atproto-proxy"],
                        body));
                }

                (HttpStatusCode, string)? response = respond(context.Request.Path.Value ?? string.Empty);
                if (response is null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                context.Response.StatusCode = (int)response.Value.Item1;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(response.Value.Item2, TestContext.Current.CancellationToken);
            });

    [Fact]
    public async Task GetFeedSkeletonSendsTheLexiconQueryAndProxiesToTheSuppliedFeedGenerator()
    {
        List<CapturedRequest> captured = [];
        using TestServer testServer = CreateServer(
            captured,
            path => path == "/xrpc/app.bsky.feed.getFeedSkeleton" ? (HttpStatusCode.OK, SkeletonResponse) : null);
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<FeedSkeleton> result = await agent.GetFeedSkeleton(
            s_feed,
            feedGenerator: s_feedGeneratorDid,
            limit: 30,
            cursor: "a&b",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        CapturedRequest request = Assert.Single(captured);
        Assert.Equal($"?feed={Uri.EscapeDataString(s_feed.ToString())}&limit=30&cursor=a%26b", request.Query);
        Assert.Equal("did:web:feeds.example.com#bsky_fg", request.Proxy);

        Assert.Equal("next", result.Result.Cursor);
        Assert.Equal("request-1", result.Result.RequestId);
        Assert.Equal(4, result.Result.Count);

        Assert.All(result.Result, p => Assert.Equal(s_post, p.Post));
        Assert.Equal("ctx-1", result.Result[0].FeedContext);
        Assert.Null(result.Result[0].Reason);

        SkeletonReasonRepost repost = Assert.IsType<SkeletonReasonRepost>(result.Result[1].Reason);
        Assert.Equal(s_repost, repost.Repost);
        Assert.IsType<SkeletonReasonPin>(result.Result[2].Reason);

        SkeletonReason unknown = Assert.IsType<SkeletonReason>(result.Result[3].Reason);
        Assert.NotNull(unknown.ExtensionData);
        Assert.True(unknown.ExtensionData.ContainsKey("extra"));
    }

    [Fact]
    public async Task GetFeedSkeletonLooksUpTheFeedGeneratorServiceWhenNotSupplied()
    {
        List<CapturedRequest> captured = [];
        using TestServer testServer = CreateServer(
            captured,
            path => path switch
            {
                "/xrpc/app.bsky.feed.getFeedGenerator" => (HttpStatusCode.OK, FeedGeneratorResponse),
                "/xrpc/app.bsky.feed.getFeedSkeleton" => (HttpStatusCode.OK, SkeletonResponse),
                _ => null
            });
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<FeedSkeleton> result = await agent.GetFeedSkeleton(s_feed, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Collection(
            captured,
            lookup => Assert.Equal("/xrpc/app.bsky.feed.getFeedGenerator", lookup.Path),
            skeleton =>
            {
                Assert.Equal("/xrpc/app.bsky.feed.getFeedSkeleton", skeleton.Path);
                Assert.Equal("did:web:feeds.example.com#bsky_fg", skeleton.Proxy);
            });
    }

    [Fact]
    public async Task GetFeedSkeletonReturnsTheFailedFeedGeneratorLookup()
    {
        List<CapturedRequest> captured = [];
        using TestServer testServer = CreateServer(
            captured,
            path => path == "/xrpc/app.bsky.feed.getFeedGenerator"
                ? (HttpStatusCode.BadRequest, """{"error":"InvalidRequest","message":"could not find feed"}""")
                : null);
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<FeedSkeleton> result = await agent.GetFeedSkeleton(s_feed, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("InvalidRequest", result.AtErrorDetail?.Error);
        Assert.DoesNotContain(captured, r => r.Path == "/xrpc/app.bsky.feed.getFeedSkeleton");
    }

    [Fact]
    public async Task GetFeedSkeletonSurfacesTheUnknownFeedError()
    {
        List<CapturedRequest> captured = [];
        using TestServer testServer = CreateServer(
            captured,
            path => path == "/xrpc/app.bsky.feed.getFeedSkeleton"
                ? (HttpStatusCode.BadRequest, """{"error":"UnknownFeed","message":"Unknown feed"}""")
                : null);
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<FeedSkeleton> result = await agent.GetFeedSkeleton(
            s_feed,
            feedGenerator: s_feedGeneratorDid,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("UnknownFeed", result.AtErrorDetail?.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetFeedSkeletonRejectsAnOutOfRangeLimit(int limit)
    {
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => agent.GetFeedSkeleton(
            s_feed,
            feedGenerator: s_feedGeneratorDid,
            limit: limit,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetFeedSkeletonCanBeCalledWithoutAuthentication()
    {
        List<CapturedRequest> captured = [];
        using TestServer testServer = CreateServer(
            captured,
            path => path == "/xrpc/app.bsky.feed.getFeedSkeleton" ? (HttpStatusCode.OK, SkeletonResponse) : null);
        using BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Service = TestServerBuilder.DefaultUri };

        AtProtoHttpResult<FeedSkeleton> result = await agent.GetFeedSkeleton(
            s_feed,
            feedGenerator: s_feedGeneratorDid,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("did:web:feeds.example.com#bsky_fg", Assert.Single(captured).Proxy);
    }

    [Theory]
    [InlineData(true, "did:web:feeds.example.com#bsky_fg")]
    [InlineData(false, BlueskyServer.AppViewProxy)]
    public async Task SendInteractionsPostsTheLexiconBodyToTheExpectedService(bool supplyFeedGenerator, string expectedProxy)
    {
        List<CapturedRequest> captured = [];
        using TestServer testServer = CreateServer(
            captured,
            path => path == "/xrpc/app.bsky.feed.sendInteractions" ? (HttpStatusCode.OK, "{}") : null);
        using BlueskyAgent agent = CreateAgent(testServer);

        using JsonDocument payload = JsonDocument.Parse("""{"position":3}""");

        AtProtoHttpResult<EmptyResponse> result = await agent.SendInteractions(
            [
                new Interaction(
                    item: s_post,
                    @event: InteractionEvent.InteractionLike,
                    viewContext: InteractionViewContext.Thread,
                    payload: payload.RootElement.Clone(),
                    feedContext: "ctx-1",
                    requestId: "request-1"),
                new Interaction(@event: InteractionEvent.RequestLess)
            ],
            feed: s_feed,
            feedGenerator: supplyFeedGenerator ? s_feedGeneratorDid : null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        CapturedRequest request = Assert.Single(captured);
        Assert.Equal(expectedProxy, request.Proxy);

        using JsonDocument body = JsonDocument.Parse(request.Body);
        Assert.Equal(s_feed.ToString(), body.RootElement.GetProperty("feed").GetString());

        JsonElement[] interactions = [.. body.RootElement.GetProperty("interactions").EnumerateArray()];
        Assert.Equal(2, interactions.Length);

        Assert.Equal(s_post.ToString(), interactions[0].GetProperty("item").GetString());
        Assert.Equal("app.bsky.feed.defs#interactionLike", interactions[0].GetProperty("event").GetString());
        Assert.Equal("thread", interactions[0].GetProperty("viewContext").GetString());
        Assert.Equal(3, interactions[0].GetProperty("payload").GetProperty("position").GetInt32());
        Assert.Equal("ctx-1", interactions[0].GetProperty("feedContext").GetString());
        Assert.Equal("request-1", interactions[0].GetProperty("reqId").GetString());
        Assert.False(interactions[0].TryGetProperty("requestId", out _));

        Assert.Equal("app.bsky.feed.defs#requestLess", interactions[1].GetProperty("event").GetString());
        Assert.False(interactions[1].TryGetProperty("item", out _));
        Assert.False(interactions[1].TryGetProperty("viewContext", out _));
    }

    [Fact]
    public async Task SendInteractionsOmitsAnUnspecifiedFeed()
    {
        List<CapturedRequest> captured = [];
        using TestServer testServer = CreateServer(
            captured,
            path => path == "/xrpc/app.bsky.feed.sendInteractions" ? (HttpStatusCode.OK, "{}") : null);
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<EmptyResponse> result = await agent.SendInteractions(
            [new Interaction(item: s_post, @event: InteractionEvent.InteractionSeen)],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        using JsonDocument body = JsonDocument.Parse(Assert.Single(captured).Body);
        Assert.False(body.RootElement.TryGetProperty("feed", out _));
    }

    public static TheoryData<int, Type> InvalidInteractions => new()
    {
        { 0, typeof(ArgumentNullException) },
        { 1, typeof(ArgumentException) },
        { 2, typeof(ArgumentException) },
        { 3, typeof(ArgumentException) },
        { 4, typeof(ArgumentException) },
        { 5, typeof(ArgumentException) },
    };

    private static Interaction[] GetInvalidInteractions(int caseNumber) => caseNumber switch
    {
        0 => [null!],
        1 => [new Interaction(@event: InteractionEvent.Unknown)],
        2 => [new Interaction(viewContext: InteractionViewContext.Unknown)],
        3 => [new Interaction(feedContext: new string('a', Interaction.MaximumFeedContextLength + 1))],
        4 => [new Interaction(feedContext: new string('\u00e9', (Interaction.MaximumFeedContextLength / 2) + 1))],
        5 => [new Interaction(requestId: new string('a', Interaction.MaximumRequestIdLength + 1))],
        _ => throw new ArgumentOutOfRangeException(nameof(caseNumber))
    };

    [Theory]
    [MemberData(nameof(InvalidInteractions))]
    public async Task SendInteractionsRejectsInvalidInteractionsBeforeSending(int caseNumber, Type expectedException)
    {
        List<CapturedRequest> captured = [];
        using TestServer testServer = CreateServer(captured, _ => (HttpStatusCode.OK, "{}"));
        using BlueskyAgent agent = CreateAgent(testServer);

        Exception exception = await Assert.ThrowsAnyAsync<ArgumentException>(() => agent.SendInteractions(
            GetInvalidInteractions(caseNumber),
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.IsType(expectedException, exception);
        Assert.Empty(captured);
    }

    [Fact]
    public async Task SendInteractionsAcceptsStringsAtTheLexiconLimits()
    {
        List<CapturedRequest> captured = [];
        using TestServer testServer = CreateServer(
            captured,
            path => path == "/xrpc/app.bsky.feed.sendInteractions" ? (HttpStatusCode.OK, "{}") : null);
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<EmptyResponse> result = await agent.SendInteractions(
            [new Interaction(
                feedContext: new string('a', Interaction.MaximumFeedContextLength),
                requestId: new string('a', Interaction.MaximumRequestIdLength))],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Single(captured);
    }

    [Fact]
    public async Task SendInteractionsAllowsAnEmptyInteractionArray()
    {
        List<CapturedRequest> captured = [];
        using TestServer testServer = CreateServer(
            captured,
            path => path == "/xrpc/app.bsky.feed.sendInteractions" ? (HttpStatusCode.OK, "{}") : null);
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<EmptyResponse> result = await agent.SendInteractions(
            [],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        using JsonDocument body = JsonDocument.Parse(Assert.Single(captured).Body);
        Assert.Empty(body.RootElement.GetProperty("interactions").EnumerateArray());
    }

    [Fact]
    public async Task SendInteractionsCanBeCalledWithoutAuthentication()
    {
        List<CapturedRequest> captured = [];
        using TestServer testServer = CreateServer(
            captured,
            path => path == "/xrpc/app.bsky.feed.sendInteractions" ? (HttpStatusCode.OK, "{}") : null);
        using BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Service = TestServerBuilder.DefaultUri };

        AtProtoHttpResult<EmptyResponse> result = await agent.SendInteractions(
            [new Interaction(@event: InteractionEvent.InteractionSeen)],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(BlueskyServer.AppViewProxy, Assert.Single(captured).Proxy);
    }
}
