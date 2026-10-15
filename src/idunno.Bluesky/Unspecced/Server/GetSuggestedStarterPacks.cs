// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Unspecced;
using idunno.Bluesky.Unspecced.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Get a collection of suggested <see cref="StarterPackView"/>s.
    /// </summary>
    /// <param name="limit">The number of starter packs to return. Must be between 1 and 25.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Func{T1, T2, TResult}" /> to await if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient" /> are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is less than 1 or greater than 25, or <paramref name="maximumResponseSize"/> is not positive.</exception>
    [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
    public static async Task<AtProtoHttpResult<ICollection<StarterPackView>>> GetSuggestedStarterPacks(
        int? limit,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        IEnumerable<Did>? subscribedLabelers = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        AtProtoHttpResult<RecommendationReadOnlyCollection<StarterPackView>> response = await GetSuggestedStarterPacksWithRecommendation(
            limit,
            service,
            accessCredentials,
            httpClient,
            onCredentialsUpdated,
            loggerFactory,
            subscribedLabelers,
            maximumResponseSize,
            cancellationToken).ConfigureAwait(false);

        return new AtProtoHttpResult<ICollection<StarterPackView>>(
            response.Result is null ? null : new List<StarterPackView>(response.Result),
            statusCode: response.StatusCode,
            httpResponseHeaders: response.HttpResponseHeaders,
            atErrorDetail: response.AtErrorDetail,
            rateLimit: response.RateLimit);
    }

    /// <summary>
    /// Gets suggested starter packs and the identifier for the recommendation which produced them.
    /// </summary>
    /// <param name="limit">The number of starter packs to return. Must be between 1 and 25.</param>
    /// <param name="service">The service to retrieve the starter packs from.</param>
    /// <param name="accessCredentials">The credentials used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An HTTP client to use for the request.</param>
    /// <param name="onCredentialsUpdated">An optional callback to await if the credentials need updating.</param>
    /// <param name="loggerFactory">An optional factory for creating loggers.</param>
    /// <param name="subscribedLabelers">An optional list of labeler DIDs to retrieve labels from.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>A result containing the suggested starter packs and their optional recommendation identifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is less than 1 or greater than 25, or <paramref name="maximumResponseSize"/> is not positive.</exception>
    [UnconditionalSuppressMessage("Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
    public static async Task<AtProtoHttpResult<RecommendationReadOnlyCollection<StarterPackView>>> GetSuggestedStarterPacksWithRecommendation(
        int? limit,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        IEnumerable<Did>? subscribedLabelers = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.SuggestedStarterPacks);
        }

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        StringBuilder queryStringBuilder = new();

        if (limit is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"limit={limit}&");
        }

        string queryString = BuildQueryString(queryStringBuilder);

        BlueskyHttpClient<GetSuggestedStarterPacksResponse> request = new(AppViewProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };

        AtProtoHttpResult<GetSuggestedStarterPacksResponse> response = await request.Get(
            service,
            $"/xrpc/app.bsky.unspecced.getSuggestedStarterPacks{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<RecommendationReadOnlyCollection<StarterPackView>>(
                new RecommendationReadOnlyCollection<StarterPackView>(
                    WithoutNullEntries(response.Result.StarterPacks, service, nameof(response.Result.StarterPacks), loggerFactory),
                    response.Result.RecIdStr),
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<RecommendationReadOnlyCollection<StarterPackView>>(
                null,
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }
    }
}
