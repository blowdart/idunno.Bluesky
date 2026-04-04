// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Unspecced;

/// <summary>
/// Encapsulates a trending topic.
/// </summary>
/// <param name="Topic">Gets the topic.</param>
/// <param name="DisplayName">Gets the display name for the <paramref name="Topic" />.</param>
/// <param name="Description">Gets a description of the <paramref name="Topic" />.</param>
/// <param name="Link">Gets a link for the <paramref name="Topic" />.</param>
/// <remarks>
/// <para>Functions and classes in the unspecced namespace are subject to change and may break without notice.</para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Documentation", "CSENSE020:Potential ghost parameter reference in documentation", Justification = "Not a ghost reference in summary")]
public sealed record TrendingTopic(string Topic, string DisplayName, string Description, string Link)
{
}
