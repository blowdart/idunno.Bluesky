// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Bookmarks;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Chat.Group;
using idunno.Bluesky.Drafts;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Unspecced;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

/// <summary>
/// Covers the readers which skip <see langword="null"/> collection entries but had no test exercising that behaviour.
/// </summary>
/// <remarks>
/// <para>Neither <see cref="System.Text.Json.Serialization.JsonRequiredAttribute"/> nor
/// <see cref="System.Text.Json.JsonSerializerOptions.RespectNullableAnnotations"/> applies to a collection's element
/// type, so a service can return a <see langword="null"/> entry inside an otherwise well formed collection. Every
/// reader is expected to skip and log such an entry rather than hand it to the caller.</para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class ReaderNullEntryCoverageTests
{
    private const string Subject = """{"did":"did:plc:test","handle":"test.invalid"}""";

    private static readonly Did s_did = "did:plc:test";

    private static readonly AtUri s_feed = new("at://did:plc:test/app.bsky.feed.generator/1");

    private static readonly AtUri s_list = new("at://did:plc:test/app.bsky.graph.list/1");

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
    public async Task GetSuggestionsSkipsANullActorRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>> result = await BlueskyServer.GetSuggestions(
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"actors":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetBookmarksSkipsANullBookmarkRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<BookmarkView>> result = await BlueskyServer.GetBookmarks(
            cursor: null,
            limit: 25,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"bookmarks":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetConversationMembersSkipsANullMemberRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileViewBasic>> result = await BlueskyServer.GetConversationMembers(
            id: "convo",
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"members":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task ListConversationRequestsSkipsANullRequestRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ConversationViewBase>> result = await BlueskyServer.ListConversationRequests(
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"requests":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task SendMessageBatchSkipsANullItemRatherThanReturningIt()
    {
        AtProtoHttpResult<ICollection<MessageView>> result = await BlueskyServer.SendMessageBatch(
            batchedMessages: [new BatchedMessage("convo", new MessageInput("hello"))],
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"items":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task ListJoinGroupRequestsSkipsANullRequestRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<JoinRequestView>> result = await BlueskyServer.ListJoinGroupRequests(
            conversationId: "convo",
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"requests":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task ListMutualGroupsSkipsANullConversationRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ConversationView>> result = await BlueskyServer.ListMutualGroups(
            subject: s_did,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"convos":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetDraftsSkipsANullDraftRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<DraftView>> result = await BlueskyServer.GetDrafts(
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"drafts":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetActorFeedsSkipsANullFeedRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<GeneratorView>> result = await BlueskyServer.GetActorFeeds(
            actor: s_did,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"feeds":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetActorLikesSkipsANullPostRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>> result = await BlueskyServer.GetActorLikes(
            actor: s_did,
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
    public async Task GetAuthorFeedSkipsANullPostRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>> result = await BlueskyServer.GetAuthorFeed(
            actor: s_did,
            limit: 25,
            cursor: null,
            filter: null,
            includePins: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"feed":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetFeedSkipsANullPostRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>> result = await BlueskyServer.GetFeed(
            feed: s_feed,
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
    public async Task GetListFeedSkipsANullPostRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>> result = await BlueskyServer.GetListFeed(
            list: s_list,
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
    public async Task GetActorStarterPacksSkipsANullStarterPackRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<StarterPackViewBasic>> result = await BlueskyServer.GetActorStarterPacks(
            actor: s_did,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"starterPacks":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetBlocksSkipsANullBlockRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>> result = await BlueskyServer.GetBlocks(
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"blocks":[null]}"""),
            onCredentialsUpdated: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetFollowsSkipsANullFollowRatherThanReturningIt()
    {
        AtProtoHttpResult<Follows> result = await BlueskyServer.GetFollows(
            actor: s_did,
            limit: 25,
            cursor: null,
            sort: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient($$"""{"subject":{{Subject}},"follows":[null]}"""),
            onCredentialsUpdated: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetKnownFollowersSkipsANullFollowerRatherThanReturningIt()
    {
        AtProtoHttpResult<Followers> result = await BlueskyServer.GetKnownFollowers(
            actor: s_did,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient($$"""{"subject":{{Subject}},"followers":[null]}"""),
            onCredentialsUpdated: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetListBlocksSkipsANullListRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>> result = await BlueskyServer.GetListBlocks(
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"lists":[null]}"""),
            onCredentialsUpdated: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetListMutesSkipsANullListRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>> result = await BlueskyServer.GetListMutes(
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"lists":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetListsWithMembershipSkipsANullListRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ListWithMembership>> result = await BlueskyServer.GetListsWithMembership(
            actor: s_did,
            limit: 25,
            cursor: null,
            purposes: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"listsWithMembership":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetMutesSkipsANullMuteRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>> result = await BlueskyServer.GetMutes(
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"mutes":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetStarterPacksWithMembershipSkipsANullStarterPackRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<StarterPackWithMembership>> result = await BlueskyServer.GetStarterPacksWithMembership(
            actor: s_did,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"starterPacksWithMembership":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task SearchStarterPacksSkipsANullStarterPackRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<StarterPackViewBasic>> result = await BlueskyServer.SearchStarterPacks(
            q: "q",
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"starterPacks":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

#pragma warning disable BSKYUnspecced // Unspecced endpoints are called deliberately here to cover their null entry handling.

    [Fact]
    public async Task GetPopularFeedGeneratorsSkipsANullFeedRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<GeneratorView>> result = await BlueskyServer.GetPopularFeedGenerators(
            query: null,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"feeds":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetSuggestedStarterPacksSkipsANullStarterPackRatherThanReturningIt()
    {
        AtProtoHttpResult<ICollection<StarterPackView>> result = await BlueskyServer.GetSuggestedStarterPacks(
            limit: 25,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"starterPacks":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetTaggedSuggestionsSkipsANullSuggestionRatherThanReturningIt()
    {
        AtProtoHttpResult<ICollection<Suggestion>> result = await BlueskyServer.GetTaggedSuggestions(
            parameters: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"suggestions":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

#pragma warning restore BSKYUnspecced
}
