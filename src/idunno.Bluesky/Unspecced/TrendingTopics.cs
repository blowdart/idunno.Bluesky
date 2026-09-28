// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using idunno.Bluesky.Unspecced.Model;

namespace idunno.Bluesky.Unspecced;

/// <summary>
/// Represents a collection of trending <paramref name="Topics" />, and <paramref name="Suggested" /> topics.
/// </summary>
/// <param name="Topics">A collection of trending topics.</param>
/// <param name="Suggested">A collection of suggested feeds.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Documentation", "CSENSE020:Potential ghost parameter reference in documentation", Justification = "Not a ghost reference at the end of summary.")]
public sealed record TrendingTopics(IReadOnlyCollection<TrendingTopic> Topics, IReadOnlyCollection<TrendingTopic> Suggested)
{
    internal TrendingTopics(GetTrendingTopicsResponse getTrendingTopicsResponse)
        : this(getTrendingTopicsResponse.Topics, getTrendingTopicsResponse.Suggested)
    {
    }

    /// <summary>
    /// A collection of trending topics.
    /// </summary>
    public IReadOnlyCollection<TrendingTopic> Topics
    {
        get;
        init => field = AsReadOnlyCopy(value);
    } = AsReadOnlyCopy(Topics);

    /// <summary>
    /// A collection of suggested topics.
    /// </summary>
    public IReadOnlyCollection<TrendingTopic> Suggested
    {
        get;
        init => field = AsReadOnlyCopy(value);
    } = AsReadOnlyCopy(Suggested);

    private static ReadOnlyCollection<TrendingTopic> AsReadOnlyCopy(IReadOnlyCollection<TrendingTopic>? value)
    {
        return value is null ? new List<TrendingTopic>().AsReadOnly() : new List<TrendingTopic>(value).AsReadOnly();
    }
}