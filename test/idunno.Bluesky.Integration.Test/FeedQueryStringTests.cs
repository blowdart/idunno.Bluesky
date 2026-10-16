// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky;
using idunno.Bluesky.Feed;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class FeedQueryStringTests
{
    [Fact]
    public async Task GetLikesSendsUriAndCidAsSeparateQueryParameters()
    {
        string queryString = string.Empty;
        AtUri uri = new("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/test");
        Cid cid = new("bafyreiaxqkvwjv5y45xq7huunf6mjvh7tf6ls44imkgjkqi3uthn57vjxu");
        string response = $$"""{"uri":"{{uri}}","cid":"{{cid}}","likes":[]}""";
        using TestServer testServer = CreateSearchServer("/xrpc/app.bsky.feed.getLikes", response, q => queryString = q);
        using BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        AtProtoHttpResult<LikesCollection> result = await agent.GetLikes(uri, cid, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains($"uri={Uri.EscapeDataString(uri.ToString())}&cid={cid}", queryString, StringComparison.Ordinal);
    }

    private const string EmptySearchV2Response = """{"posts":[]}""";

    private const string EmptySearchResponse = """{"posts":[]}""";

    private static readonly Did s_did = new("did:plc:hfgp6pj3akhqxntgqwramlbg");

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    private static TestServer CreateSearchServer(string path, string responseBody, Action<string> captureQueryString)
    {
        return TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                if (context.Request.Path == path)
                {
                    captureQueryString(context.Request.QueryString.Value ?? string.Empty);

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(responseBody, TestContext.Current.CancellationToken);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            });
    }

    [Fact]
    public async Task SearchPostsV2SendsEmbeddedAtUrisUnderTheLexiconParameterName()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer("/xrpc/app.bsky.feed.searchPostsV2", EmptySearchV2Response, q => queryString = q);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        await agent.SearchPostsV2(
            query: "test",
            embeddedAtUris: [new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x")],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("embeddedAtUris=", queryString, StringComparison.Ordinal);
        Assert.DoesNotContain("&embedded=", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchPostsV2SendsExcludeEmbeddedAtUrisUnderTheLexiconParameterName()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer("/xrpc/app.bsky.feed.searchPostsV2", EmptySearchV2Response, q => queryString = q);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        await agent.SearchPostsV2(
            query: "test",
            excludeEmbeddedAtUris: [new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x")],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("excludeEmbeddedAtUris=", queryString, StringComparison.Ordinal);
        Assert.DoesNotContain("excludeEmbedded=", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchPostsV2SendsReplyParentUriUnderTheLexiconParameterName()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer("/xrpc/app.bsky.feed.searchPostsV2", EmptySearchV2Response, q => queryString = q);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        await agent.SearchPostsV2(
            query: "test",
            replyParentUri: new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("replyParentUri=", queryString, StringComparison.Ordinal);
        Assert.DoesNotContain("&replyParent=", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchPostsV2SendsThreadRootUriUnderTheLexiconParameterName()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer("/xrpc/app.bsky.feed.searchPostsV2", EmptySearchV2Response, q => queryString = q);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        await agent.SearchPostsV2(
            query: "test",
            threadRootUri: new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("threadRootUri=", queryString, StringComparison.Ordinal);
        Assert.DoesNotContain("&threadRoot=", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchPostsV2SendsAllTimeAsItsOwnParameterRatherThanAsUntil()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer("/xrpc/app.bsky.feed.searchPostsV2", EmptySearchV2Response, q => queryString = q);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        await agent.SearchPostsV2(
            query: "test",
            allTime: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("allTime=true", queryString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("until=", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchPostsV2DoesNotEmitADanglingAmpersandWhenQueryIsNull()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer("/xrpc/app.bsky.feed.searchPostsV2", EmptySearchV2Response, q => queryString = q);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        await agent.SearchPostsV2(
            sort: SortOrder.Recent,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.DoesNotContain("?&", queryString, StringComparison.Ordinal);
        Assert.DoesNotContain("&&", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchPostsV2StillEmitsQueryWhenItIsSupplied()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer("/xrpc/app.bsky.feed.searchPostsV2", EmptySearchV2Response, q => queryString = q);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        await agent.SearchPostsV2(
            query: "hello",
            sort: SortOrder.Recent,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("query=hello", queryString, StringComparison.Ordinal);
        Assert.DoesNotContain("?&", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchPostsSendsAuthorUnderTheLexiconParameterName()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer("/xrpc/app.bsky.feed.searchPosts", EmptySearchResponse, q => queryString = q);
        using HttpClient httpClient = testServer.CreateClient();

        await BlueskyServer.SearchPosts(
            query: "test",
            searchOrder: SearchOrder.Latest,
            since: null,
            until: null,
            mentions: null,
            author: new Handle("test.invalid"),
            lang: null,
            domain: null,
            url: null,
            tags: null,
            limit: null,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("author=test.invalid", queryString, StringComparison.Ordinal);
        Assert.DoesNotContain("mentions=", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchPostsSendsBothAuthorAndMentionsWhenBothAreSupplied()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer("/xrpc/app.bsky.feed.searchPosts", EmptySearchResponse, q => queryString = q);
        using HttpClient httpClient = testServer.CreateClient();

        await BlueskyServer.SearchPosts(
            query: "test",
            searchOrder: SearchOrder.Latest,
            since: null,
            until: null,
            mentions: new Handle("mentioned.invalid"),
            author: new Handle("author.invalid"),
            lang: null,
            domain: null,
            url: null,
            tags: null,
            limit: null,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("author=author.invalid", queryString, StringComparison.Ordinal);
        Assert.Contains("mentions=mentioned.invalid", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchPostsV2RejectsAHashtagLongerThanTheMaximumInGraphemes()
    {
        using TestServer testServer = CreateSearchServer("/xrpc/app.bsky.feed.searchPostsV2", EmptySearchV2Response, _ => { });
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        string tooLong = new('a', Maximum.TagLengthInGraphemes + 1);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await agent.SearchPostsV2(query: "test", hashTags: [tooLong], cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchPostsV2RejectsAnExcludedHashtagLongerThanTheMaximumInBytes()
    {
        using TestServer testServer = CreateSearchServer("/xrpc/app.bsky.feed.searchPostsV2", EmptySearchV2Response, _ => { });
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        // A family emoji is one grapheme but 25 UTF-8 bytes, so this stays inside the grapheme limit and exceeds the byte limit.
        string tooManyBytes = string.Concat(Enumerable.Repeat("\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466", 30));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await agent.SearchPostsV2(query: "test", excludeHashTags: [tooManyBytes], cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchPostsDoesNotThrowWhenCredentialsAreNull()
    {
        using TestServer testServer = CreateSearchServer("/xrpc/app.bsky.feed.searchPosts", EmptySearchResponse, _ => { });
        using HttpClient httpClient = testServer.CreateClient();

        AtProtoHttpResult<SearchResults> result = await BlueskyServer.SearchPosts(
            query: "test",
            searchOrder: SearchOrder.Latest,
            since: null,
            until: null,
            mentions: null,
            author: null,
            lang: null,
            domain: null,
            url: null,
            tags: null,
            limit: null,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: null,
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task GetQuotesDoesNotThrowWhenCredentialsAreNull()
    {
        const string responseBody = """{"uri":"at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x","posts":[]}""";

        using TestServer testServer = CreateSearchServer("/xrpc/app.bsky.feed.getQuotes", responseBody, _ => { });
        using HttpClient httpClient = testServer.CreateClient();

        AtProtoHttpResult<QuotesCollection> result = await BlueskyServer.GetQuotes(
            uri: new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x"),
            cid: null,
            limit: null,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: null,
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task GetAuthorFeedUsesTheLexiconFilterNameAndVideoValue()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer(
            "/xrpc/app.bsky.feed.getAuthorFeed",
            """{"feed":[]}""",
            value => queryString = value);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Service = TestServerBuilder.DefaultUri };

        await agent.GetAuthorFeed(
            s_did,
            filter: FeedFilter.PostsWithVideo,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("filter=posts_with_video", queryString, StringComparison.Ordinal);
        Assert.DoesNotContain("feedFilter=", queryString, StringComparison.Ordinal);
    }

#pragma warning disable BSKYUnspecced // The unspecced endpoint is called to verify its lexicon query parameter.
    [Fact]
    public async Task GetPopularFeedGeneratorsUsesTheLexiconQueryName()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer(
            "/xrpc/app.bsky.unspecced.getPopularFeedGenerators",
            """{"feeds":[]}""",
            value => queryString = value);
        using HttpClient httpClient = testServer.CreateClient();

        await BlueskyServer.GetPopularFeedGenerators(
            query: "cats",
            limit: null,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: null,
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("query=cats", queryString, StringComparison.Ordinal);
        Assert.DoesNotContain("q=", queryString, StringComparison.Ordinal);
    }
#pragma warning restore BSKYUnspecced

    [Fact]
    public async Task GetUnreadCountsSendsFalseForExcludeGroupChats()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer(
            "/xrpc/chat.bsky.convo.getUnreadCounts",
            """{"unreadAcceptedConvos":0,"unreadRequestConvos":0}""",
            value => queryString = value);
        using HttpClient httpClient = testServer.CreateClient();

        await BlueskyServer.GetUnreadCounts(
            includeGroupChats: false,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("includeGroupChats=false", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSuggestedFeedsSendsPaginationParameters()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer(
            "/xrpc/app.bsky.feed.getSuggestedFeeds",
            """{"feeds":[]}""",
            value => queryString = value);
        using HttpClient httpClient = testServer.CreateClient();

        await BlueskyServer.GetSuggestedFeeds(
            limit: 10,
            cursor: "next page",
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            onCredentialsUpdated: null,
            loggerFactory: null,
            subscribedLabelers: null,
            maximumResponseSize: AtProtoHttpClient.DefaultMaximumResponseSize,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("limit=10", queryString, StringComparison.Ordinal);
        Assert.Contains("cursor=next%20page", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTimelineSendsSinceParameter()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer(
            "/xrpc/app.bsky.feed.getTimeline",
            """{"feed":[]}""",
            value => queryString = value);
        using HttpClient httpClient = testServer.CreateClient();

        await BlueskyServer.GetTimeline(
            algorithm: null,
            limit: null,
            cursor: null,
            since: "2024-01-01T00:00:00Z",
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            onCredentialsUpdated: null,
            loggerFactory: null,
            subscribedLabelers: null,
            maximumResponseSize: AtProtoHttpClient.DefaultMaximumResponseSize,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("since=2024-01-01T00%3A00%3A00Z", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetQuotesSendsSortParameter()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer(
            "/xrpc/app.bsky.feed.getQuotes",
            """{"uri":"at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x","posts":[]}""",
            value => queryString = value);
        using HttpClient httpClient = testServer.CreateClient();

        await BlueskyServer.GetQuotes(
            uri: new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x"),
            cid: null,
            limit: null,
            cursor: null,
            sort: "top",
            service: TestServerBuilder.DefaultUri,
            accessCredentials: null,
            httpClient: httpClient,
            onCredentialsUpdated: null,
            loggerFactory: null,
            subscribedLabelers: null,
            maximumResponseSize: AtProtoHttpClient.DefaultMaximumResponseSize,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("sort=top", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetListFeedSendsSinceParameter()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer(
            "/xrpc/app.bsky.feed.getListFeed",
            """{"feed":[]}""",
            value => queryString = value);
        using HttpClient httpClient = testServer.CreateClient();

        await BlueskyServer.GetListFeed(
            list: new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.graph.list/3lcf6ry7xy22x"),
            limit: null,
            cursor: null,
            since: "2024-01-01T00:00:00Z",
            service: TestServerBuilder.DefaultUri,
            accessCredentials: null,
            httpClient: httpClient,
            onCredentialsUpdated: null,
            loggerFactory: null,
            subscribedLabelers: null,
            maximumResponseSize: AtProtoHttpClient.DefaultMaximumResponseSize,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("since=2024-01-01T00%3A00%3A00Z", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetListsSendsPurposeFilters()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateSearchServer(
            "/xrpc/app.bsky.graph.getLists",
            """{"lists":[]}""",
            value => queryString = value);
        using HttpClient httpClient = testServer.CreateClient();

        await BlueskyServer.GetLists(
            actor: s_did,
            limit: null,
            cursor: null,
            purposes: ["curatelist", "modlist"],
            service: TestServerBuilder.DefaultUri,
            accessCredentials: null,
            httpClient: httpClient,
            onCredentialsUpdated: null,
            loggerFactory: null,
            subscribedLabelers: null,
            maximumResponseSize: AtProtoHttpClient.DefaultMaximumResponseSize,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("purposes=curatelist", queryString, StringComparison.Ordinal);
        Assert.Contains("purposes=modlist", queryString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task ExpandedFeedOverloadsRejectInvalidLimits(int limit)
    {
        using HttpClient httpClient = new();
        using BlueskyAgent agent = new() { Service = TestServerBuilder.DefaultUri, Credentials = CreateCredentials() };
        AtUri uri = new("at://did:plc:test/app.bsky.graph.list/abcdefghijklm");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => agent.GetSuggestedFeeds(limit, null, null, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BlueskyServer.GetSuggestedFeeds(
            limit, null, TestServerBuilder.DefaultUri, CreateCredentials(), httpClient,
            null, null, null, AtProtoHttpClient.DefaultMaximumResponseSize, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => agent.GetListFeed(uri, limit, null, null, null, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BlueskyServer.GetListFeed(
            uri, limit, null, null, TestServerBuilder.DefaultUri, null, httpClient,
            null, null, null, AtProtoHttpClient.DefaultMaximumResponseSize, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => agent.GetQuotes(uri, null, limit, null, null, null, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BlueskyServer.GetQuotes(
            uri, null, limit, null, null, TestServerBuilder.DefaultUri, null, httpClient,
            null, null, null, AtProtoHttpClient.DefaultMaximumResponseSize, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => agent.GetTimeline(null, limit, null, null, null, cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BlueskyServer.GetTimeline(
            null, limit, null, null, TestServerBuilder.DefaultUri, CreateCredentials(), httpClient,
            null, null, null, AtProtoHttpClient.DefaultMaximumResponseSize, cancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetListsRejectsInvalidPurposeEntries(string? purpose)
    {
        using HttpClient httpClient = new();
        using BlueskyAgent agent = new() { Service = TestServerBuilder.DefaultUri };
        string[] purposes = [purpose!];
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAnyAsync<ArgumentException>(() => agent.GetLists(s_did, null, null, null, purposes, cancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => BlueskyServer.GetLists(
            s_did, null, null, purposes, TestServerBuilder.DefaultUri, null, httpClient,
            null, null, null, AtProtoHttpClient.DefaultMaximumResponseSize, cancellationToken));
    }
}
