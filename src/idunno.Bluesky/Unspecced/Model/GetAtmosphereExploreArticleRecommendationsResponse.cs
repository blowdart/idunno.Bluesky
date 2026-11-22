// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Unspecced.Model;

/// <summary>
/// The wire format of the response from <c>app.bsky.unspecced.getAtmosphereExploreArticleRecommendations</c>.
/// </summary>
/// <param name="Articles">The recommended articles.</param>
/// <param name="RecIdStr">The snowflake identifier for this recommendation.</param>
[SuppressMessage("Performance", "CA1812", Justification = "Used in GetAtmosphereExploreArticleRecommendations.")]
internal sealed record GetAtmosphereExploreArticleRecommendationsResponse(
    [property: JsonRequired] ICollection<AtmosphereExploreArticle> Articles,
    [property: JsonRequired] string RecIdStr);
