// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Unspecced;
using idunno.Bluesky.Unspecced.Model;

namespace idunno.Bluesky
{
    public static partial class BlueskyServer
    {
        private const string GetAgeAssuranceStateEndpoint = "/xrpc/app.bsky.unspecced.getAgeAssuranceState";

        private const string GetPopularFeedGeneratorsEndpoint = "/xrpc/app.bsky.unspecced.getPopularFeedGenerators";

        private const string GetSuggestedStarterPacksEndpoint = "/xrpc/app.bsky.unspecced.getSuggestedStarterPacks";

        private const string GetSuggestedUsersEndpoint = "/xrpc/app.bsky.unspecced.getSuggestedUsers";

        private const string GetTaggedSuggestionsEndpoint = "/xrpc/app.bsky.unspecced.getTaggedSuggestions";

        private const string GetTrendingTopicEndpoint = "/xrpc/app.bsky.unspecced.getTrendingTopics";

        private const string GetTrendsEndpoint = "/xrpc/app.bsky.unspecced.getTrends";

        /// <summary>
        /// Gets the age assurance status for the current user.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient" /> are <see langword="null"/>.</exception>
        [UnconditionalSuppressMessage("Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
        public static async Task<AtProtoHttpResult<AgeAssuranceState>> GetAgeAssuranceState(
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<GetAgeAssuranceStateResponse> request = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<GetAgeAssuranceStateResponse> response = await request.Get(
                service,
                $"{GetAgeAssuranceStateEndpoint}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<AgeAssuranceState>(
                    new AgeAssuranceState(response.Result.LastInitiatedAt, response.Result.Status),
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<AgeAssuranceState>(
                    null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Get an unspecced, paged list of views over globally popular feed generators.
        /// </summary>
        /// <param name="query">Search query string; syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
        /// <param name="limit">The maximum number of feed generators to return.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient" /> are <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt;100.</exception>
        [UnconditionalSuppressMessage("Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<GeneratorView>>> GetPopularFeedGenerators(
            string? query,
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.PopularFeedGenerators);
            }

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            StringBuilder queryStringBuilder = new();
            if (query is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"q={Uri.EscapeDataString(query)}");
            }
            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }

            string queryString = queryStringBuilder.ToString();
            queryString = queryString.TrimStart('&');

            AtProtoHttpClient<GetPopularFeedGeneratorsResponse> request = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<GetPopularFeedGeneratorsResponse> response = await request.Get(
                service,
                $"{GetPopularFeedGeneratorsEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<GeneratorView>>(
                    new PagedViewReadOnlyCollection<GeneratorView>(response.Result.Feeds, response.Result.Cursor),
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<GeneratorView>>(
                    null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Get a collection of suggested <see cref="StarterPackView"/>s.
        /// </summary>
        /// <param name="limit">The number of starter packs to return. Must be between 1 and 50.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient" /> are <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt;50.</exception>
        [UnconditionalSuppressMessage("Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
        public static async Task<AtProtoHttpResult<ICollection<StarterPackView>>> GetSuggestedStarterPacks(
            int? limit,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.SuggestedStarterPacks);
            }

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            string queryString = string.Empty;

            if (limit is not null)
            {
                queryString += $"limit={limit}";
            }

            AtProtoHttpClient<GetSuggestedStarterPacksResponse> request = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<GetSuggestedStarterPacksResponse> response = await request.Get(
                service,
                $"{GetSuggestedStarterPacksEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<ICollection<StarterPackView>>(
                    response.Result.StarterPacks,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ICollection<StarterPackView>>(
                    null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Get a <see cref="PagedReadOnlyCollection{T}"/> of <see cref="ProfileView"/>s of suggested actors.
        /// </summary>
        /// <param name="category">An optional category of users to get suggestions for.</param>
        /// <param name="limit">The number of topics to return. Must be between 1 and 50.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient" /> are <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt;50.</exception>
        [UnconditionalSuppressMessage("Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
        public static async Task<AtProtoHttpResult<ICollection<ProfileView>>> GetSuggestedUsers(
            string? category,
            int? limit,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.SuggestedUsers);
            }

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            string queryString = string.Empty;

            if (!string.IsNullOrEmpty(category))
            {
                queryString += $"category={Uri.EscapeDataString(category)}";
            }

            if (limit is not null)
            {
                queryString += $"&limit={limit}";
            }

            queryString = queryString.TrimStart('&');

            AtProtoHttpClient<GetSuggestedUsersResponse> request = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<GetSuggestedUsersResponse> response = await request.Get(
                service,
                $"{GetSuggestedUsersEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<ICollection<ProfileView>>(
                    response.Result.Actors,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ICollection<ProfileView>>(
                    null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Get a collection of tagged suggestions.
        /// </summary>
        /// <param name="parameters">Any parameters to send to the endpoint. Parameter values will automatically be query string encoded.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient" /> are <see langword="null"/>.</exception>
        [UnconditionalSuppressMessage("Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
        public static async Task<AtProtoHttpResult<ICollection<Suggestion>>> GetTaggedSuggestions(
            ICollection<KeyValuePair<string, object>>? parameters,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            StringBuilder queryStringBuilder = new ();
            string? queryString = string.Empty;

            if (parameters != null && parameters.Count > 0)
            {
                foreach (KeyValuePair<string, object> parameter in parameters)
                {
                    if (parameter.Value is not null && parameter.Value.ToString() is not null)
                    {
                        queryStringBuilder.Append(CultureInfo.InvariantCulture, $"{parameter.Key}={Uri.EscapeDataString(parameter.Value.ToString()!)}&");
                    }
                    else
                    {
                        queryStringBuilder.Append(CultureInfo.InvariantCulture, $"{parameter.Key}&");
                    }
                }

                queryStringBuilder.Length--;

                queryString = queryStringBuilder.ToString();
            }

            AtProtoHttpClient<GetTaggedSuggestionsResponse> request = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<GetTaggedSuggestionsResponse> response = await request.Get(
                service,
                $"{GetTaggedSuggestionsEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<ICollection<Suggestion>>(
                    response.Result.Suggestions,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ICollection<Suggestion>>(
                    null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Get a collection of trending topics
        /// </summary>
        /// <param name="limit">The number of topics to return. Must be between 1 and 25.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient" /> are <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt;25.</exception>
        [UnconditionalSuppressMessage("Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
        public static async Task<AtProtoHttpResult<TrendingTopics>> GetTrendingTopics(
            int? limit,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.TrendingTopics);
            }

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            string queryString = string.Empty;

            if (limit is not null)
            {
                queryString = $"limit={limit}";
            }

            if (accessCredentials != null)
            {
                queryString += $"&did={accessCredentials.Did}";
            }

            queryString = queryString.TrimStart('&');

            AtProtoHttpClient<GetTrendingTopicsResponse> request = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<GetTrendingTopicsResponse> response = await request.Get(
                service,
                $"{GetTrendingTopicEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<TrendingTopics>(
                    new TrendingTopics(response.Result),
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<TrendingTopics>(
                    null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Get a collection of trends
        /// </summary>
        /// <param name="limit">The number of trends to return. Must be between 1 and 25.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient" /> are <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt;25.</exception>
        [UnconditionalSuppressMessage("Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
        public static async Task<AtProtoHttpResult<ICollection<TrendView>>> GetTrends(
            int? limit,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.Trends);
            }

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            string queryString = string.Empty;

            if (limit is not null)
            {
                queryString = $"limit={limit}";
            }

            AtProtoHttpClient<GetTrendsResponse> request = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<GetTrendsResponse> response = await request.Get(
                service,
                $"{GetTrendsEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<ICollection<TrendView>>(
                    response.Result.Trends,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ICollection<TrendView>>(
                    null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }
    }
}
