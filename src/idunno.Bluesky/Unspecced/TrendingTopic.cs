// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Unspecced
{
    /// <summary>
    /// Encapsulates a trending topic.
    /// </summary>
    /// <param name="Topic">Gets the topic.</param>
    /// <param name="DisplayName">Gets the display name for the topic.</param>
    /// <param name="Description">Gets a description of the topic.</param>
    /// <param name="Link">Gets a link for the topic.</param>
    /// <remarks>
    /// <para>Functions and classes in the unspecced namespace are subject to change and may break without notice.</para>
    /// </remarks>
    public sealed record TrendingTopic(string Topic, string DisplayName, string Description, string Link)
    {
    }
}
