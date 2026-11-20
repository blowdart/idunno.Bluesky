// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Unspecced;
using idunno.Bluesky.Unspecced.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Gets an unhydrated list of articles recommended for Atmosphere Explore.
    /// </summary>
    /// <param name="viewer">The optional <see cref="Did"/> of the account making the request. Omit for public or unauthenticated queries.</param>
    /// <param name="limit">The maximum number of articles to return. Must be between 1 and 100.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the recommendations from.</param>
    /// <param name="accessCredentials">Optional <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Func{T1, T2, TResult}" /> to await if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
    /// <remarks>
    /// <para>
    /// The lexicon describes this endpoint as a skeleton intended to be called and hydrated by
    /// <see cref="GetAtmosphereExploreTab"/>. Most applications should call that method instead.
    /// </para>
    /// <para>
    /// This overload uses the caller-supplied <see cref="HttpClient"/> to contact <paramref name="service"/>. If that URI can be
    /// influenced by untrusted input, the client must enforce SSRF protection at connection time: resolve the destination and block
    /// loopback, private, link-local and other non-public IPv4 and IPv6 addresses, and disable redirects or apply the same checks to
    /// every redirect target. URI-only validation does not prevent DNS rebinding.
    /// </para>
    /// </remarks>
    [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<RecommendationReadOnlyCollection<AtmosphereExploreArticle>>> GetAtmosphereExploreArticleRecommendations(
        Did? viewer,
        int? limit,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.AtmosphereExploreArticleRecommendations);
        }

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        StringBuilder queryStringBuilder = new();

        if (viewer is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"viewer={Uri.EscapeDataString(viewer.ToString())}&");
        }

        if (limit is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"limit={limit}&");
        }

        BlueskyHttpClient<GetAtmosphereExploreArticleRecommendationsResponse> request = new(AppViewProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };

        AtProtoHttpResult<GetAtmosphereExploreArticleRecommendationsResponse> response = await request.Get(
            service,
            $"/xrpc/app.bsky.unspecced.getAtmosphereExploreArticleRecommendations{BuildQueryString(queryStringBuilder)}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<RecommendationReadOnlyCollection<AtmosphereExploreArticle>>(
                new RecommendationReadOnlyCollection<AtmosphereExploreArticle>(
                    WithoutNullEntries(response.Result.Articles, service, nameof(response.Result.Articles), loggerFactory),
                    response.Result.RecIdStr),
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<RecommendationReadOnlyCollection<AtmosphereExploreArticle>>(
                null,
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }
    }
}
