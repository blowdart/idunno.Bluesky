// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Feed;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class FeedResponseMetadataTests
{
    private static readonly Did s_did = new("did:plc:test");

    private static readonly AtUri s_list = new("at://did:plc:test/app.bsky.graph.list/test");

    [Fact]
    public void ExistingPublicCollectionConstructorsKeepTheirCursorBehavior()
    {
        IList<FeedViewPost> list = [];
        ICollection<FeedViewPost> collection = [];
        PagedViewReadOnlyCollection<FeedViewPost>[] feeds =
        [
            new(),
            new(list, "older"),
            new(collection, "older")
        ];

        Assert.All(feeds, feed => Assert.Null(feed.StartCursor));
        Assert.Null(feeds[0].Cursor);
        Assert.Equal("older", feeds[1].Cursor);
        Assert.Equal("older", feeds[2].Cursor);
    }

    [Theory]
    [InlineData(true, true, "\"newest&cursor=1\"", "newest&cursor=1")]
    [InlineData(true, false, "\"newest&cursor=1\"", "newest&cursor=1")]
    [InlineData(false, true, "\"newest&cursor=1\"", "newest&cursor=1")]
    [InlineData(false, false, "\"newest&cursor=1\"", "newest&cursor=1")]
    [InlineData(true, true, "\"\"", "")]
    [InlineData(false, true, "\"\"", "")]
    [InlineData(true, true, null, null)]
    [InlineData(false, true, null, null)]
    public async Task SyntheticFeedResponsePreservesStartCursor(bool timeline, bool useAgent, string? cursorJson, string? expected)
    {
        string metadata = cursorJson is null ? string.Empty : $",\"startCursor\":{cursorJson}";
        string body = $$"""
            {
                "feed":[null,{
                    "post":{
                        "uri":"at://did:plc:test/app.bsky.feed.post/test",
                        "cid":"bafyreiaxqkvwjv5y45xq7huunf6mjvh7tf6ls44imkgjkqi3uthn57vjxu",
                        "author":{"did":"did:plc:test","handle":"test.invalid"},
                        "record":{
                            "$type":"app.bsky.feed.post",
                            "text":"Synthetic feed response, not a live response.",
                            "createdAt":"2026-01-01T00:00:00Z"
                        },
                        "indexedAt":"2026-01-01T00:00:00Z"
                    }
                },null],
                "cursor":"older"
                {{metadata}}
            }
            """;
        string endpoint = timeline ? "getTimeline" : "getListFeed";
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal($"/xrpc/app.bsky.feed.{endpoint}", context.Request.Path.ToString());
            Assert.Equal("5", context.Request.Query["limit"].ToString());
            if (!timeline)
            {
                Assert.Equal(s_list.ToString(), context.Request.Query["list"].ToString());
            }

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body, TestContext.Current.CancellationToken);
        });
        AccessCredentials credentials = new(
            TestServerBuilder.DefaultUri,
            AuthenticationType.UsernamePassword,
            JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
            "refreshToken");
        using BlueskyAgent agent = new(new TestHttpClientFactory(server)) { Credentials = credentials, Service = TestServerBuilder.DefaultUri };
        using HttpClient client = server.CreateClient();
        PagedViewReadOnlyCollection<FeedViewPost> feed;
        if (timeline)
        {
            AtProtoHttpResult<Timeline> result = useAgent
                ? await agent.GetTimeline(limit: 5, cancellationToken: TestContext.Current.CancellationToken)
                : await BlueskyServer.GetTimeline(
                    algorithm: null,
                    limit: 5,
                    cursor: null,
                    service: TestServerBuilder.DefaultUri,
                    accessCredentials: credentials,
                    httpClient: client,
                    cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded);
            feed = result.Result;
        }
        else
        {
            AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>> result = useAgent
                ? await agent.GetListFeed(s_list, limit: 5, cancellationToken: TestContext.Current.CancellationToken)
                : await BlueskyServer.GetListFeed(
                    list: s_list,
                    limit: 5,
                    cursor: null,
                    service: TestServerBuilder.DefaultUri,
                    accessCredentials: credentials,
                    httpClient: client,
                    cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded);
            feed = result.Result;
        }

        Assert.Equal(expected, feed.StartCursor);
        Assert.Equal("older", feed.Cursor);
        FeedViewPost item = Assert.Single(feed);
        Assert.Equal("at://did:plc:test/app.bsky.feed.post/test", item.Post.Uri.ToString());
    }

    [Fact]
    public async Task CapturedPublicListFeedReplaysWithStartCursor()
    {
        // Replays an unauthenticated public AppView capture from 2026-10-15; this test makes no live call.
        // Public unauthenticated capture: HTTP 200, 2026-10-15T20:12:07Z.
        // Source: https://public.api.bsky.app/xrpc/app.bsky.feed.getListFeed?list=at%3A%2F%2Fdid%3Aplc%3Av7gngzva22rilmcp6fiypowf%2Fapp.bsky.graph.list%2F3lbcrdr5d452n&limit=2
        // Observed: Two feed items and startCursor.
        const string body = """
            {"feed":[{"post":{"uri":"at://did:plc:wbj65xnocn6xy5iax65snujj/app.bsky.feed.post/3mxwufbmztk2i","cid":"bafyreialudz4ecqfx46gawrnge32wqd7kecrrqh75pqtnrgtxwkuzirq6a","author":{"did":"did:plc:wbj65xnocn6xy5iax65snujj","handle":"craigmcg.bsky.social","displayName":"Craig McGowan","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:wbj65xnocn6xy5iax65snujj/bafkreicikcrz7ckxfd2365e4mscjms2j4ff35d7djqnbejybjsfwnaipyy","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-11-21T00:50:43.665Z"},"record":{"$type":"app.bsky.feed.post","createdAt":"2026-10-15T20:06:48.670Z","langs":["en"],"reply":{"parent":{"cid":"bafyreiaolixpb3ug65xoxzenzgproih3rqltovvzuatkr5ru57jfgdzu34","uri":"at://did:plc:lqwihsassnzkba6w2xamrga6/app.bsky.feed.post/3mxudniefps27"},"root":{"cid":"bafyreidy6seg3jmopwz3zuyqhjfetw2im622ayvb2fh3ffy544fgev4ot4","uri":"at://did:plc:wbj65xnocn6xy5iax65snujj/app.bsky.feed.post/3mxtxwdgsqs27"}},"text":"Yes agree, thank you Carolannie!"},"bookmarkCount":0,"replyCount":0,"repostCount":0,"likeCount":0,"quoteCount":0,"indexedAt":"2026-10-15T20:06:49.656Z","labels":[]},"reply":{"root":{"uri":"at://did:plc:wbj65xnocn6xy5iax65snujj/app.bsky.feed.post/3mxtxwdgsqs27","cid":"bafyreidy6seg3jmopwz3zuyqhjfetw2im622ayvb2fh3ffy544fgev4ot4","author":{"did":"did:plc:wbj65xnocn6xy5iax65snujj","handle":"craigmcg.bsky.social","displayName":"Craig McGowan","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:wbj65xnocn6xy5iax65snujj/bafkreicikcrz7ckxfd2365e4mscjms2j4ff35d7djqnbejybjsfwnaipyy","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-11-21T00:50:43.665Z"},"record":{"$type":"app.bsky.feed.post","createdAt":"2026-10-14T16:32:02.998Z","langs":["en"],"text":"Hello Blueskiers...\n\nSorry I have been missing many posts, plus not uploading recently. I am far away from home and unfortunately been quite sick. So the devices have been sitting alongside me idle in my hotel room. \n\nHopefully back soon! 🤞🤞🤞"},"bookmarkCount":0,"replyCount":11,"repostCount":0,"likeCount":18,"quoteCount":0,"indexedAt":"2026-10-14T16:32:05.780Z","labels":[],"$type":"app.bsky.feed.defs#postView"},"parent":{"uri":"at://did:plc:lqwihsassnzkba6w2xamrga6/app.bsky.feed.post/3mxudniefps27","cid":"bafyreiaolixpb3ug65xoxzenzgproih3rqltovvzuatkr5ru57jfgdzu34","author":{"did":"did:plc:lqwihsassnzkba6w2xamrga6","handle":"carolannie.bsky.social","displayName":"Carolannie","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:lqwihsassnzkba6w2xamrga6/bafkreia767z3lxbg5nnohprtbvgkuoleikz7ewszmpejhvhm67a7i4me6e","associated":{"chat":{"allowIncoming":"following","allowGroupInvites":"following"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[{"src":"did:plc:lqwihsassnzkba6w2xamrga6","uri":"at://did:plc:lqwihsassnzkba6w2xamrga6/app.bsky.actor.profile/self","cid":"bafyreiavs6gztwhuv2ccdrkhxxbpxunms5qidswos5sw5yfepezpfj3u3e","val":"!no-unauthenticated","cts":"1970-01-01T00:00:00.000Z"}],"createdAt":"2023-08-08T01:50:20.793Z"},"record":{"$type":"app.bsky.feed.post","createdAt":"2026-10-14T20:01:51.076Z","langs":["en"],"reply":{"parent":{"cid":"bafyreidy6seg3jmopwz3zuyqhjfetw2im622ayvb2fh3ffy544fgev4ot4","uri":"at://did:plc:wbj65xnocn6xy5iax65snujj/app.bsky.feed.post/3mxtxwdgsqs27"},"root":{"cid":"bafyreidy6seg3jmopwz3zuyqhjfetw2im622ayvb2fh3ffy544fgev4ot4","uri":"at://did:plc:wbj65xnocn6xy5iax65snujj/app.bsky.feed.post/3mxtxwdgsqs27"}},"text":"Take care of yourself first"},"bookmarkCount":0,"replyCount":1,"repostCount":0,"likeCount":1,"quoteCount":0,"indexedAt":"2026-10-14T20:01:51.736Z","labels":[],"$type":"app.bsky.feed.defs#postView"},"grandparentAuthor":{"did":"did:plc:wbj65xnocn6xy5iax65snujj","handle":"craigmcg.bsky.social","displayName":"Craig McGowan","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:wbj65xnocn6xy5iax65snujj/bafkreicikcrz7ckxfd2365e4mscjms2j4ff35d7djqnbejybjsfwnaipyy","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-11-21T00:50:43.665Z"}}},{"post":{"uri":"at://did:plc:wbj65xnocn6xy5iax65snujj/app.bsky.feed.post/3mxwudz2cbc2i","cid":"bafyreibihtyxyk42pevzt2jwqljb5a6jrqusok4kp6kyhwz4lwudd46lrq","author":{"did":"did:plc:wbj65xnocn6xy5iax65snujj","handle":"craigmcg.bsky.social","displayName":"Craig McGowan","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:wbj65xnocn6xy5iax65snujj/bafkreicikcrz7ckxfd2365e4mscjms2j4ff35d7djqnbejybjsfwnaipyy","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-11-21T00:50:43.665Z"},"record":{"$type":"app.bsky.feed.post","createdAt":"2026-10-15T20:06:06.114Z","langs":["en"],"reply":{"parent":{"cid":"bafyreietdgwqb776ilofsgtymdbrc7jxj2s6hslwvhdtykjx3fclffjo3i","uri":"at://did:plc:pdem6qfpcajfcxxe6nqibov5/app.bsky.feed.post/3mxuqkejlms2z"},"root":{"cid":"bafyreidy6seg3jmopwz3zuyqhjfetw2im622ayvb2fh3ffy544fgev4ot4","uri":"at://did:plc:wbj65xnocn6xy5iax65snujj/app.bsky.feed.post/3mxtxwdgsqs27"}},"text":"Thank you for this! 🙏"},"bookmarkCount":0,"replyCount":0,"repostCount":0,"likeCount":0,"quoteCount":0,"indexedAt":"2026-10-15T20:06:07.061Z","labels":[]},"reply":{"root":{"uri":"at://did:plc:wbj65xnocn6xy5iax65snujj/app.bsky.feed.post/3mxtxwdgsqs27","cid":"bafyreidy6seg3jmopwz3zuyqhjfetw2im622ayvb2fh3ffy544fgev4ot4","author":{"did":"did:plc:wbj65xnocn6xy5iax65snujj","handle":"craigmcg.bsky.social","displayName":"Craig McGowan","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:wbj65xnocn6xy5iax65snujj/bafkreicikcrz7ckxfd2365e4mscjms2j4ff35d7djqnbejybjsfwnaipyy","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-11-21T00:50:43.665Z"},"record":{"$type":"app.bsky.feed.post","createdAt":"2026-10-14T16:32:02.998Z","langs":["en"],"text":"Hello Blueskiers...\n\nSorry I have been missing many posts, plus not uploading recently. I am far away from home and unfortunately been quite sick. So the devices have been sitting alongside me idle in my hotel room. \n\nHopefully back soon! 🤞🤞🤞"},"bookmarkCount":0,"replyCount":11,"repostCount":0,"likeCount":18,"quoteCount":0,"indexedAt":"2026-10-14T16:32:05.780Z","labels":[],"$type":"app.bsky.feed.defs#postView"},"parent":{"uri":"at://did:plc:pdem6qfpcajfcxxe6nqibov5/app.bsky.feed.post/3mxuqkejlms2z","cid":"bafyreietdgwqb776ilofsgtymdbrc7jxj2s6hslwvhdtykjx3fclffjo3i","author":{"did":"did:plc:pdem6qfpcajfcxxe6nqibov5","handle":"danskhansen.bsky.social","displayName":"danskhansen","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:pdem6qfpcajfcxxe6nqibov5/bafkreigood7bibz2qtpkusv542pi3zskgk7c2su37ggbx2y2hgluzwgdzu","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2025-02-22T20:55:18.545Z"},"record":{"$type":"app.bsky.feed.post","createdAt":"2026-10-14T23:52:45.032Z","langs":["en"],"reply":{"parent":{"cid":"bafyreidy6seg3jmopwz3zuyqhjfetw2im622ayvb2fh3ffy544fgev4ot4","uri":"at://did:plc:wbj65xnocn6xy5iax65snujj/app.bsky.feed.post/3mxtxwdgsqs27"},"root":{"cid":"bafyreidy6seg3jmopwz3zuyqhjfetw2im622ayvb2fh3ffy544fgev4ot4","uri":"at://did:plc:wbj65xnocn6xy5iax65snujj/app.bsky.feed.post/3mxtxwdgsqs27"}},"text":"Hope you feel better soon!"},"bookmarkCount":0,"replyCount":1,"repostCount":0,"likeCount":1,"quoteCount":0,"indexedAt":"2026-10-14T23:52:45.759Z","labels":[],"$type":"app.bsky.feed.defs#postView"},"grandparentAuthor":{"did":"did:plc:wbj65xnocn6xy5iax65snujj","handle":"craigmcg.bsky.social","displayName":"Craig McGowan","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:wbj65xnocn6xy5iax65snujj/bafkreicikcrz7ckxfd2365e4mscjms2j4ff35d7djqnbejybjsfwnaipyy","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-11-21T00:50:43.665Z"}}}],"cursor":"2026-10-15T20:06:06.114Z","startCursor":"2026-10-15T20:06:48.67Z"}
            """;
        using JsonDocument document = JsonDocument.Parse(body);
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal("/xrpc/app.bsky.feed.getListFeed", context.Request.Path.ToString());
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body, TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();

        AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>> result = await BlueskyServer.GetListFeed(
            list: s_list,
            limit: 2,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: null,
            httpClient: client,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(document.RootElement.GetProperty("startCursor").GetString(), result.Result.StartCursor);
        Assert.Equal(document.RootElement.GetProperty("feed").GetArrayLength(), result.Result.Count);
        Assert.NotEmpty(result.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SyntheticEmptyFeedKeepsStartCursorIndependentOfPaginationCursor(bool timeline)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"feed":[],"startCursor":"newest"}""", TestContext.Current.CancellationToken);
        });
        AccessCredentials credentials = new(
            TestServerBuilder.DefaultUri,
            AuthenticationType.UsernamePassword,
            JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
            "refreshToken");
        using BlueskyAgent agent = new(new TestHttpClientFactory(server)) { Credentials = credentials, Service = TestServerBuilder.DefaultUri };
        PagedViewReadOnlyCollection<FeedViewPost> feed;
        if (timeline)
        {
            AtProtoHttpResult<Timeline> result = await agent.GetTimeline(cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded);
            feed = result.Result;
        }
        else
        {
            AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>> result = await agent.GetListFeed(
                s_list, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded);
            feed = result.Result;
        }

        Assert.Empty(feed);
        Assert.Null(feed.Cursor);
        Assert.Equal("newest", feed.StartCursor);
    }
}
