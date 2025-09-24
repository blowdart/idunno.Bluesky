// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Unspecced
{
    /// <summary>
    /// Represents a view model for a trending topic, including its metadata, associated actors, and status.
    /// </summary>
    /// <param name="Topic">The topic for the trend.</param>
    /// <param name="DisplayName">The display name for the trend.</param>
    /// <param name="Link">A link to the trend.</param>
    /// <param name="StartedAt">The <see cref="DateTimeOffset"/> the trend started at.</param>
    /// <param name="PostCount">The post count for the topic.</param>
    /// <param name="Status">The status of topic.</param>
    /// <param name="Category">The category of the topic</param>
    /// <param name="Actors">A collection of actors contributing to the topic.</param>
    public sealed record TrendView(
        string Topic,
        string DisplayName,
        string Link,
        DateTimeOffset StartedAt,
        int PostCount,
        string? Status,
        string Category,
        ICollection<ProfileViewBasic> Actors) : View
    {
    }
}
