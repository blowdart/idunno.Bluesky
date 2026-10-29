// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Embed.External;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Unspecced;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class LiveLexiconTests
{
    // Explicit tests deliberately contact live services; normal test runs only replay the checked-in captures.
    [Fact(Explicit = true)]
    public async Task PublicExploreEndpointDeserializesThroughTheSdk()
    {
        using HttpClient client = new();
#pragma warning disable BSKYUnspecced
        AtProtoHttpResult<AtmosphereExploreTab> result = await BlueskyServer.GetAtmosphereExploreTab(
            ["en"], null, null, new Uri("https://public.api.bsky.app"), null, client,
            cancellationToken: TestContext.Current.CancellationToken);
#pragma warning restore BSKYUnspecced

        Assert.True(result.Succeeded, $"Explore returned {result.StatusCode}: {result.AtErrorDetail}");
        Assert.NotEmpty(result.Result.Articles);
        Assert.NotEmpty(result.Result.Publications);
        Assert.NotEmpty(result.Result.Photos);
    }

    [Fact(Explicit = true)]
    public async Task PublicHydrationEndpointDeserializesThroughTheSdk()
    {
        using BlueskyAgent agent = new();
        AtProtoHttpResult<EmbeddedExternalViewResponse> result = await agent.GetEmbedExternalViewResponse(
            new Uri("https://blog.aster.place/3mwqgs7dp2s2a"),
            new AtUri("at://did:plc:ifn645rwvsuolxg7o3w7ouo4/site.standard.document/3mwqgs7dp2s2a"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, $"Hydration returned {result.StatusCode}: {result.AtErrorDetail}");
        Assert.NotNull(result.Result.DeserializeData(
            (JsonTypeInfo<ArticleView>)BlueskyServer.BlueskyJsonSerializerOptions.GetTypeInfo(typeof(ArticleView))));
    }

    [Fact(Explicit = true)]
    public async Task PublicListFeedExposesStartCursor()
    {
        using BlueskyAgent agent = new();
        AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>> result = await agent.GetListFeed(
            new AtUri("at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.list/3lbcrdr5d452n"),
            limit: 2, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, $"List feed returned {result.StatusCode}: {result.AtErrorDetail}");
        Assert.NotNull(result.Result.StartCursor);
    }

    [Fact(Explicit = true)]
    public async Task PublicStarterPacksEndpointDeserializesRicherResponse()
    {
        using BlueskyAgent agent = new();
#pragma warning disable BSKYUnspecced
        AtProtoHttpResult<RecommendationReadOnlyCollection<StarterPackView>> result = await agent.GetSuggestedStarterPacksWithRecommendation(
            limit: 1, cancellationToken: TestContext.Current.CancellationToken);
#pragma warning restore BSKYUnspecced

        Assert.True(result.Succeeded, $"Starter packs returned {result.StatusCode}: {result.AtErrorDetail}");
        Assert.NotEmpty(result.Result);
    }

    [Fact(Explicit = true)]
    public async Task PublicProfileEndpointDeserializesThroughTheSdk()
    {
        using BlueskyAgent agent = new();
        AtProtoHttpResult<ProfileViewDetailed> result = await agent.GetProfile(new Handle("bsky.app"), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, $"Profile returned {result.StatusCode}: {result.AtErrorDetail}");
        Assert.Equal("bsky.app", result.Result.Handle.ToString());
    }

    [Fact(Explicit = true)]
    public async Task AuthenticatedTimelinePreservesLiveStartCursorAcrossPages()
    {
        string? handle = Environment.GetEnvironmentVariable("_BlueskyHandle");
        string? password = Environment.GetEnvironmentVariable("_BlueskyPassword");
        Assert.False(string.IsNullOrWhiteSpace(handle), "Set _BlueskyHandle to run this explicit test.");
        Assert.False(string.IsNullOrWhiteSpace(password), "Set _BlueskyPassword to run this explicit test.");

        using TimelineResponseCapture capture = new();
        using BlueskyAgent agent = new(capture);
        AtProtoHttpResult<bool> login = await agent.Login(handle, password, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(login.Succeeded, $"Login returned HTTP {(int)login.StatusCode}.");

        string? cursor = null;
        for (int page = 0; page < 2; page++)
        {
            AtProtoHttpResult<Timeline> result = await agent.GetTimeline(
                limit: 2, cursor: cursor, cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded, $"Timeline returned HTTP {(int)result.StatusCode}.");
            Assert.True(capture.AuthenticatedRequest);
            Assert.False(string.IsNullOrEmpty(capture.StartCursor), "The live response did not supply startCursor.");
            Assert.Equal(capture.StartCursor, result.Result.StartCursor);
            Assert.Equal(capture.Cursor, result.Result.Cursor);
            Assert.Equal(capture.FeedCount, result.Result.Count);
            Assert.NotEmpty(result.Result);
            Assert.False(string.IsNullOrEmpty(result.Result.Cursor), "The live response did not supply a pagination cursor.");
            Assert.NotEqual(cursor, result.Result.Cursor);
            cursor = result.Result.Cursor;
        }
    }

    private sealed class TimelineResponseCapture() : DelegatingHandler(new HttpClientHandler()), IHttpClientFactory
    {
        internal string? StartCursor { get; private set; }

        internal string? Cursor { get; private set; }

        internal int FeedCount { get; private set; }

        internal bool AuthenticatedRequest { get; private set; }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (request.RequestUri?.AbsolutePath is "/xrpc/app.bsky.feed.getTimeline" && response.IsSuccessStatusCode)
            {
                AuthenticatedRequest = request.Headers.Authorization is not null;
                // Inspect only timeline metadata in memory; never retain login responses or personalized feed data.
                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                JsonElement root = document.RootElement;
                StartCursor = root.TryGetProperty("startCursor", out JsonElement startCursor) ? startCursor.GetString() : null;
                Cursor = root.TryGetProperty("cursor", out JsonElement cursor) ? cursor.GetString() : null;
                FeedCount = root.GetProperty("feed").GetArrayLength();
            }

            return response;
        }
    }
}
