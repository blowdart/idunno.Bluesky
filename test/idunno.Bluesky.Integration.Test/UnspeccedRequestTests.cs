// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Unspecced;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

#pragma warning disable BSKYUnspecced

[ExcludeFromCodeCoverage]
public class UnspeccedRequestTests
{
    private const string EmptyTrendsResponse = """{"trends":[]}""";

    private const string EmptyTrendingTopicsResponse = """{"topics":[],"suggested":[]}""";

    private const string EmptySuggestionsResponse = """{"suggestions":[]}""";

    private const string EmptySuggestedUsersResponse = """{"actors":[],"recIdStr":"a-snowflake"}""";

    private static readonly Did s_did = new("did:plc:hfgp6pj3akhqxntgqwramlbg");

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    private static BlueskyAgent CreateAgent(TestServer testServer) =>
        new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

    private static TestServer CreateCapturingServer(string path, string responseBody, Action<HttpRequest> captureRequest) =>
        TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                if (context.Request.Path == path)
                {
                    captureRequest(context.Request);

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(responseBody, TestContext.Current.CancellationToken);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            });

    [Fact]
    public async Task GetTrendingTopicsSendsTheAuthenticatedViewerAsTheViewerParameter()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.unspecced.getTrendingTopics",
            EmptyTrendingTopicsResponse,
            r => queryString = r.QueryString.Value ?? string.Empty);

        BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<TrendingTopics> result = await agent.GetTrendingTopics(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains($"viewer={Uri.EscapeDataString(s_did.ToString())}", queryString, StringComparison.Ordinal);
        Assert.DoesNotContain("did=", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTrendsDoesNotSendAnyQueryStringParametersWhenNoneAreSpecified()
    {
        string queryString = "not set";

        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.unspecced.getTrends",
            EmptyTrendsResponse,
            r => queryString = r.QueryString.Value ?? string.Empty);

        BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<RecommendationReadOnlyCollection<TrendView>> result = await agent.GetTrends(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(string.Empty, queryString);
    }

    [Theory]
    [InlineData("tag&limit=100", "value", "tag%26limit%3D100=value")]
    [InlineData("tag=x", "value", "tag%3Dx=value")]
    public async Task GetTaggedSuggestionsEncodesParameterKeys(string key, string value, string expected)
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.unspecced.getTaggedSuggestions",
            EmptySuggestionsResponse,
            r => queryString = r.QueryString.Value ?? string.Empty);

        BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<ICollection<Suggestion>> result = await agent.GetTaggedSuggestions(
            new List<KeyValuePair<string, object>> { new(key, value) },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains(expected, queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTaggedSuggestionsFormatsParameterValuesInvariantly()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.unspecced.getTaggedSuggestions",
            EmptySuggestionsResponse,
            r => queryString = r.QueryString.Value ?? string.Empty);

        BlueskyAgent agent = CreateAgent(testServer);

        using CultureScope cultureScope = new("de-DE");

        AtProtoHttpResult<ICollection<Suggestion>> result = await agent.GetTaggedSuggestions(
            new List<KeyValuePair<string, object>> { new("score", 3.5d) },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains("score=3.5", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTrendsSendsTheSubscribedLabelersItIsGiven()
    {
        string labelerHeader = string.Empty;

        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.unspecced.getTrends",
            EmptyTrendsResponse,
            r => labelerHeader = r.Headers["atproto-accept-labelers"].ToString());

        BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<RecommendationReadOnlyCollection<TrendView>> result = await agent.GetTrends(
            subscribedLabelers: [new Did("did:plc:ar7c4by46qjdydhdevvrndac")],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains("did:plc:ar7c4by46qjdydhdevvrndac", labelerHeader, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSuggestedUsersSurfacesTheRecommendationIdentifier()
    {
        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.unspecced.getSuggestedUsers",
            EmptySuggestedUsersResponse,
            _ => { });

        BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<RecommendationReadOnlyCollection<ProfileView>> result = await agent.GetSuggestedUsers(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("a-snowflake", result.Result.RecommendationId);
    }

    [Fact]
    public async Task GetPostThreadV2AllowsZeroLevelsBelowTheAnchor()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.unspecced.getPostThreadV2",
            """{"thread":[],"hasOtherReplies":false}""",
            r => queryString = r.QueryString.Value ?? string.Empty);

        BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<PostThreadV2> result = await agent.GetPostThreadV2(
            new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lbxjmrpmvs2s"),
            below: 0,
            branchingFactor: 0,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains("below=0", queryString, StringComparison.Ordinal);
        Assert.Contains("branchingFactor=0", queryString, StringComparison.Ordinal);
    }

    [ExcludeFromCodeCoverage]
    private sealed class CultureScope : IDisposable
    {
        private readonly System.Globalization.CultureInfo _previous = System.Globalization.CultureInfo.CurrentCulture;

        public CultureScope(string name)
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(name);
        }

        public void Dispose() => System.Globalization.CultureInfo.CurrentCulture = _previous;
    }
}
