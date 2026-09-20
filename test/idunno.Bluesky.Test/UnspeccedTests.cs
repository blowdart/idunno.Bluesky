// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Unspecced;

namespace idunno.Bluesky.Test;

#pragma warning disable BSKYUnspecced

[ExcludeFromCodeCoverage]
public class UnspeccedTests
{
    private static readonly AtUri s_anchor = new("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lbxjmrpmvs2s");

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public async Task GetPostThreadV2ThrowsWhenBelowIsOutOfRange(int below)
    {
        using BlueskyAgent agent = new();

        ArgumentOutOfRangeException exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => agent.GetPostThreadV2(s_anchor, below: below, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("below", exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task GetPostThreadV2ThrowsWhenBranchingFactorIsOutOfRange(int branchingFactor)
    {
        using BlueskyAgent agent = new();

        ArgumentOutOfRangeException exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => agent.GetPostThreadV2(s_anchor, branchingFactor: branchingFactor, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("branchingFactor", exception.ParamName);
    }

    [Fact]
    public async Task GetPostThreadV2DoesNotRejectZeroLevelsBelowTheAnchor()
    {
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.GetPostThreadV2(s_anchor, below: 0, branchingFactor: 0, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TrendViewTakesADefensiveCopyOfTheActorsItIsGiven()
    {
        List<ProfileViewBasic> actors = [];

        TrendView trendView = new(
            "topic",
            "display name",
            "link",
            DateTimeOffset.UtcNow,
            10,
            null,
            null,
            actors,
            null);

        actors.Add(new ProfileViewBasic(new Did("did:plc:ar7c4by46qjdydhdevvrndac"), new Handle("example.invalid"), null, null, null, null, null, null, null, null, null, null));

        Assert.Empty(trendView.Actors);
    }

    [Fact]
    public void TrendingTopicsTakesADefensiveCopyOfTheCollectionsItIsGiven()
    {
        List<TrendingTopic> topics = [];
        List<TrendingTopic> suggested = [];

        TrendingTopics trendingTopics = new(topics, suggested);

        topics.Add(new TrendingTopic("topic", "display name", null, "https://example.invalid/"));
        suggested.Add(new TrendingTopic("suggested", "display name", null, "https://example.invalid/"));

        Assert.Empty(trendingTopics.Topics);
        Assert.Empty(trendingTopics.Suggested);
    }

    [Fact]
    public void RecommendationReadOnlyCollectionTakesADefensiveCopyOfTheCollectionItIsGiven()
    {
        List<TrendView> trends = [];

        RecommendationReadOnlyCollection<TrendView> collection = new(trends, "a-snowflake");

        trends.Add(new TrendView("topic", "display name", "link", DateTimeOffset.UtcNow, 1, null, null, [], null));

        Assert.Empty(collection);
        Assert.Equal("a-snowflake", collection.RecommendationId);
    }
}
