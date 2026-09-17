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
/// Covers readers whose response type declares its collection as a positional record parameter. Such a parameter needs
/// <c>[property: JsonRequired]</c> to be treated as required, and without it a response which omits the collection
/// entirely left the member <see langword="null"/>, which surfaced to the caller as a
/// <see cref="NullReferenceException"/> rather than as a failed result.
/// </summary>
/// <remarks>
/// <para><see cref="System.Text.Json.JsonSerializerOptions.RespectNullableAnnotations"/> rejects only an explicit
/// <see langword="null"/>, so it does not cover a missing property.</para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class MissingCollectionTests
{
    private static readonly Did s_did = "did:plc:test";

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
    public async Task GetConversationMembersReportsAMissingMembersCollectionAsAFailure()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileViewBasic>> result = await BlueskyServer.GetConversationMembers(
            id: "convo",
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task GetListsWithMembershipReportsAMissingListsCollectionAsAFailure()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ListWithMembership>> result = await BlueskyServer.GetListsWithMembership(
            actor: s_did,
            limit: 25,
            cursor: null,
            purposes: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task GetStarterPacksWithMembershipReportsAMissingStarterPacksCollectionAsAFailure()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<StarterPackWithMembership>> result = await BlueskyServer.GetStarterPacksWithMembership(
            actor: s_did,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task ListActivitySubscriptionsReportsAMissingSubscriptionsCollectionAsAFailure()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>> result = await BlueskyServer.ListActivitySubscriptions(
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task ListActivitySubscriptionsSkipsANullSubscriptionRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>> result = await BlueskyServer.ListActivitySubscriptions(
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"subscriptions":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

#pragma warning disable BSKYUnspecced // Unspecced endpoints are called deliberately here to cover their response handling.

    [Fact]
    public async Task GetPopularFeedGeneratorsReportsAMissingFeedsCollectionAsAFailure()
    {
        AtProtoHttpResult<PagedViewReadOnlyCollection<GeneratorView>> result = await BlueskyServer.GetPopularFeedGenerators(
            query: null,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task GetSuggestedStarterPacksReportsAMissingStarterPacksCollectionAsAFailure()
    {
        AtProtoHttpResult<ICollection<StarterPackView>> result = await BlueskyServer.GetSuggestedStarterPacks(
            limit: 25,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task GetTaggedSuggestionsReportsAMissingSuggestionsCollectionAsAFailure()
    {
        AtProtoHttpResult<ICollection<Suggestion>> result = await BlueskyServer.GetTaggedSuggestions(
            parameters: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task GetTrendsReportsAMissingTrendsCollectionAsAFailure()
    {
        AtProtoHttpResult<ICollection<TrendView>> result = await BlueskyServer.GetTrends(
            limit: 25,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

#pragma warning restore BSKYUnspecced

    [Fact]
    public async Task GetTimelineReportsAMissingFeedCollectionAsAFailure()
    {
        AtProtoHttpResult<Timeline> result = await BlueskyServer.GetTimeline(
            algorithm: null,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("{}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }
}
