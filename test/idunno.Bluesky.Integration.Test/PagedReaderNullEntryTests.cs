// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Unspecced;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

/// <summary>
/// Neither <see cref="System.Text.Json.Serialization.JsonRequiredAttribute"/> nor
/// <see cref="System.Text.Json.JsonSerializerOptions.RespectNullableAnnotations"/> apply to the element type of a
/// collection, so a service returning a null entry inside an otherwise well formed collection used to hand that null
/// straight through to the caller. Paged readers now skip and log null entries instead.
/// </summary>
[ExcludeFromCodeCoverage]
public class PagedReaderNullEntryTests
{
    private static readonly Did s_did = "did:plc:test";

    private static readonly AtUri s_starterPack = new("at://did:plc:test/app.bsky.graph.starterpack/1");

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
    public async Task SearchActorsSkipsANullActorRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>> result = await BlueskyServer.SearchActors(
            q: "q",
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
    public async Task GetListsSkipsANullListRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>> result = await BlueskyServer.GetLists(
            actor: s_did,
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
    public async Task GetProfilesSkipsANullProfileRatherThanReturningIt()
    {
        AtProtoHttpResult<IReadOnlyCollection<ProfileViewDetailed>> result = await BlueskyServer.GetProfiles(
            actors: [s_did],
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"profiles":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetStarterPacksSkipsANullStarterPackRatherThanReturningIt()
    {
        AtProtoHttpResult<IReadOnlyList<StarterPackViewBasic>> result = await BlueskyServer.GetStarterPacks(
            uris: [s_starterPack],
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"starterPacks":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetFollowersSkipsANullFollowerRatherThanReturningIt()
    {
        AtProtoHttpResult<Followers> result = await BlueskyServer.GetFollowers(
            actor: s_did,
            limit: 25,
            cursor: null,
            sort: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"subject":{"did":"did:plc:test","handle":"test.invalid"},"followers":[null]}"""),
            onCredentialsUpdated: null,
            loggerFactory: null,
            subscribedLabelers: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetFollowersReportsAMalformedPageAsAFailureRatherThanAnEmptyPage()
    {
        AtProtoHttpResult<Followers> result = await BlueskyServer.GetFollowers(
            actor: s_did,
            limit: 25,
            cursor: null,
            sort: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            onCredentialsUpdated: null,
            loggerFactory: null,
            subscribedLabelers: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task GetStarterPacksReportsAMalformedPageAsAFailureRatherThanAnEmptyPage()
    {
        AtProtoHttpResult<IReadOnlyList<StarterPackViewBasic>> result = await BlueskyServer.GetStarterPacks(
            uris: [s_starterPack],
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task GetPreferencesReportsAMalformedResponseAsAFailureRatherThanEmptyPreferences()
    {
        AtProtoHttpResult<Preferences> result = await BlueskyServer.GetPreferences(
            includeBlueskyModerationLabeler: false,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task UpdateAllReadReportsAMalformedResponseAsAFailureRatherThanAZeroCount()
    {
        AtProtoHttpResult<ulong?> result = await BlueskyServer.UpdateAllRead(
            status: "request",
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task GetNotificationUnreadCountReportsAMalformedResponseAsAFailureRatherThanACount()
    {
        AtProtoHttpResult<int?> result = await BlueskyServer.GetNotificationUnreadCount(
            seenAt: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task UpdateAllReadReturnsTheCountForAWellFormedResponse()
    {
        AtProtoHttpResult<ulong?> result = await BlueskyServer.UpdateAllRead(
            status: "request",
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"updatedCount":3}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(3UL, result.Result);
    }

    [Fact]
    public async Task GetNotificationUnreadCountReturnsZeroForAWellFormedEmptyResponse()
    {
        AtProtoHttpResult<int?> result = await BlueskyServer.GetNotificationUnreadCount(
            seenAt: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"count":0}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public async Task GetLabelerServicesReportsAMalformedResponseAsAFailureRatherThanAnEmptyCollection()
    {
        AtProtoHttpResult<ICollection<LabelerView>> result = await BlueskyServer.GetLabelerServices(
            dids: [s_did],
            getDetailedViews: false,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task GetPostsSkipsANullPostRatherThanReturningIt()
    {
        AtProtoHttpResult<IReadOnlyCollection<PostView>> result = await BlueskyServer.GetPosts(
            uris: [s_starterPack],
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"posts":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

#pragma warning disable BSKYUnspecced // Unspecced endpoints are called deliberately here to cover their null entry handling.

    [Fact]
    public async Task GetTrendsSkipsANullTrendRatherThanReturningIt()
    {
        AtProtoHttpResult<RecommendationReadOnlyCollection<TrendView>> result = await BlueskyServer.GetTrends(
            limit: 25,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"trends":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetSuggestedUsersSkipsANullActorRatherThanReturningIt()
    {
        AtProtoHttpResult<RecommendationReadOnlyCollection<ProfileView>> result = await BlueskyServer.GetSuggestedUsers(
            category: null,
            limit: 25,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"actors":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task GetSuggestedUsersAcceptsAResponseWithoutARecommendationIdentifier()
    {
        AtProtoHttpResult<RecommendationReadOnlyCollection<ProfileView>> result = await BlueskyServer.GetSuggestedUsers(
            category: null,
            limit: 25,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"actors":[]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

#pragma warning restore BSKYUnspecced
}
