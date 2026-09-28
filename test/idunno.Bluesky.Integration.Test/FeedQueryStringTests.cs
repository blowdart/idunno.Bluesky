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
}
