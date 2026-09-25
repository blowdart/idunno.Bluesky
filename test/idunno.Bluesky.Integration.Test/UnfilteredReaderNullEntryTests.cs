// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Unspecced;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

/// <summary>
/// Covers the readers which were returning a collection straight from the wire without filtering out
/// <see langword="null"/> entries.
/// </summary>
/// <remarks>
/// <para>Neither <see cref="System.Text.Json.Serialization.JsonRequiredAttribute"/> nor
/// <see cref="System.Text.Json.JsonSerializerOptions.RespectNullableAnnotations"/> applies to a collection's element
/// type, so a service can return a <see langword="null"/> entry inside an otherwise well formed collection. Every
/// reader is expected to skip and log such an entry rather than hand it to the caller.</para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class UnfilteredReaderNullEntryTests
{
    private const string ListView = """
        {
            "uri": "at://did:plc:test/app.bsky.graph.list/1",
            "cid": "bafyreib2rxk3rh6kzwq6y7ug4eqhfhpqaqzqvuflstfpvgkzjt7b5yfkzy",
            "creator": { "did": "did:plc:test", "handle": "test.invalid" },
            "name": "Test",
            "purpose": "app.bsky.graph.defs#curatelist"
        }
        """;

    private static readonly Did s_did = "did:plc:test";

    private static readonly AtUri s_post = new("at://did:plc:test/app.bsky.feed.post/1");

    private static readonly AtUri s_list = new("at://did:plc:test/app.bsky.graph.list/1");

    private static readonly AtUri s_feed = new("at://did:plc:test/app.bsky.feed.generator/1");

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    private static HttpClient CreateClient(string body) =>
        TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body);
        }).CreateClient();

    [Fact]
    public async Task GetConversationLogSkipsANullLogEntryRatherThanReturningIt()
    {
        AtProtoHttpResult<Logs> result = await BlueskyServer.GetConversationLog(
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"logs":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetMessagesSkipsANullMessageRatherThanReturningIt()
    {
        AtProtoHttpResult<Messages> result = await BlueskyServer.GetMessages(
            id: "convo",
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"messages":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task ListConversationsSkipsANullConversationRatherThanReturningIt()
    {
        AtProtoHttpResult<Conversations> result = await BlueskyServer.ListConversations(
            limit: 25,
            cursor: null,
            readState: null,
            status: null,
            kind: null,
            lockStatus: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"convos":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetListSkipsANullListItemRatherThanReturningIt()
    {
        AtProtoHttpResult<ListViewWithItems> result = await BlueskyServer.GetList(
            list: s_list,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient($$"""{"list":{{ListView}},"items":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetFeedGeneratorsSkipsANullGeneratorRatherThanReturningIt()
    {
        AtProtoHttpResult<IReadOnlyCollection<GeneratorView>> result = await BlueskyServer.GetFeedGenerators(
            feeds: [s_feed],
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"feeds":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetLikesSkipsANullLikeRatherThanReturningIt()
    {
        AtProtoHttpResult<LikesCollection> result = await BlueskyServer.GetLikes(
            uri: s_post,
            cid: null,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient($$"""{"uri":"{{s_post}}","likes":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetQuotesSkipsANullPostRatherThanReturningIt()
    {
        AtProtoHttpResult<QuotesCollection> result = await BlueskyServer.GetQuotes(
            uri: s_post,
            cid: null,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient($$"""{"uri":"{{s_post}}","posts":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetRepostedBySkipsANullProfileRatherThanReturningIt()
    {
        AtProtoHttpResult<RepostedBy> result = await BlueskyServer.GetRepostedBy(
            uri: s_post,
            cid: null,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient($$"""{"uri":"{{s_post}}","repostedBy":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetSuggestedFeedsSkipsANullGeneratorRatherThanReturningIt()
    {
        AtProtoHttpResult<SuggestedFeeds> result = await BlueskyServer.GetSuggestedFeeds(
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"feeds":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetTimelineSkipsANullFeedEntryRatherThanReturningIt()
    {
        AtProtoHttpResult<Timeline> result = await BlueskyServer.GetTimeline(
            algorithm: null,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"feed":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task SearchPostsSkipsANullPostRatherThanReturningIt()
    {
        AtProtoHttpResult<SearchResults> result = await BlueskyServer.SearchPosts(
            query: "test",
            searchOrder: null,
            since: null,
            until: null,
            mentions: null,
            author: null,
            lang: null,
            domain: null,
            url: null,
            tags: null,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"posts":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task SearchPostsV2SkipsANullPostRatherThanReturningIt()
    {
        AtProtoHttpResult<SearchV2Results> result = await SearchPostsV2("""{"posts":[null]}""");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task SearchPostsV2SkipsANullDetectedQueryLanguageRatherThanReturningIt()
    {
        AtProtoHttpResult<SearchV2Results> result = await SearchPostsV2("""{"posts":[],"detectedQueryLanguages":[null]}""");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.NotNull(result.Result.DetectedQueryLanguages);
        Assert.Empty(result.Result.DetectedQueryLanguages);
    }

    [Fact]
    public async Task GetPostThreadOtherV2SkipsANullThreadItemRatherThanReturningIt()
    {
#pragma warning disable BSKYUnspecced
        AtProtoHttpResult<IReadOnlyCollection<ThreadItem>> result = await BlueskyServer.GetPostThreadOtherV2(
            anchor: s_post,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"thread":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);
#pragma warning restore BSKYUnspecced

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    private static Task<AtProtoHttpResult<SearchV2Results>> SearchPostsV2(string body) =>
        BlueskyServer.SearchPostsV2(
            cursor: null,
            limit: 25,
            query: "test",
            sort: null,
            authors: null,
            mentions: null,
            domains: null,
            urls: null,
            embeddedAtUris: null,
            hashTags: null,
            excludeAuthors: null,
            excludeMentions: null,
            excludeDomains: null,
            excludeUrls: null,
            excludeEmbeddedAtUris: null,
            excludeHashTags: null,
            since: null,
            until: null,
            allTime: null,
            languages: null,
            excludeLanguages: null,
            hasMedia: null,
            hasVideo: null,
            replyParentUri: null,
            threadRootUri: null,
            excludeReplies: null,
            repliesOnly: null,
            following: null,
            queryLanguage: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient(body),
            cancellationToken: TestContext.Current.CancellationToken);
}
