// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.Bluesky.Unspecced;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets an unhydrated list of articles recommended for Atmosphere Explore.
    /// </summary>
    /// <param name="limit">The maximum number of articles to return. Must be between 1 and 100.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
    /// <remarks>
    /// <para>
    /// When the agent is authenticated its <see cref="Did"/> is sent as the viewer. The lexicon describes this endpoint as a skeleton
    /// intended to be called and hydrated by <see cref="GetAtmosphereExploreTab"/>. Most applications should call that method instead.
    /// </para>
    /// </remarks>
    [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
    public async Task<AtProtoHttpResult<RecommendationReadOnlyCollection<AtmosphereExploreArticle>>> GetAtmosphereExploreArticleRecommendations(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.AtmosphereExploreArticleRecommendations);
        }

#pragma warning disable BSKYUnspecced
        return await BlueskyServer.GetAtmosphereExploreArticleRecommendations(
            viewer: IsAuthenticated ? Did : null,
            limit: limit,
            service: AuthenticatedOrUnauthenticatedServiceUri,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore BSKYUnspecced
    }
}
