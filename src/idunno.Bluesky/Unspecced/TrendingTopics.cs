// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Unspecced.Model;

namespace idunno.Bluesky.Unspecced
{
    /// <summary>
    /// Represents a collection of trending topics, and suggested topics.
    /// </summary>
    /// <param name="Topics">A collection of trending topics.</param>
    /// <param name="Suggested">A collection of suggested feeds.</param>
    public sealed record TrendingTopics(ICollection<TrendingTopic> Topics, ICollection<TrendingTopic> Suggested)
    {
        internal TrendingTopics(GetTrendingTopicsResponse getTrendingTopicsResponse)
            : this(
                getTrendingTopicsResponse is not null ? getTrendingTopicsResponse.Topics : throw new ArgumentNullException(nameof(getTrendingTopicsResponse)),
                getTrendingTopicsResponse.Suggested)
        {
        }
    }
}
