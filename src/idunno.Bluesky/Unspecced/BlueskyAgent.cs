// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Unspecced;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Get an unspecced, paged list of views over globally popular feed generators.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
        public async Task<AtProtoHttpResult<AgeAssuranceState>> GetAgeAssuranceState(
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

#pragma warning disable BSKYUnspecced
            return await BlueskyServer.GetAgeAssuranceState(
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore BSKYUnspecced
        }

        /// <summary>
        /// Get an unspecced, paged list of views over globally popular feed generators.
        /// </summary>
        /// <param name="query">Search query string; syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
        /// <param name="limit">The maximum number of feed generators to return.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt;100.</exception>
        [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<GeneratorView>>> GetPopularFeedGenerators(
            string? query = null,
            int? limit = null,
            string? cursor = null,
            CancellationToken cancellationToken = default)
        {
            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.PopularFeedGenerators);
            }

#pragma warning disable BSKYUnspecced
            return await BlueskyServer.GetPopularFeedGenerators(
                query,
                limit,
                cursor,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore BSKYUnspecced
        }

        /// <summary>
        /// Get a <see cref="ICollection{T}"/> of <see cref="StarterPackView"/>s of suggested starter packs.
        /// </summary>
        /// <param name="limit">The number of starter packs to return. Must be between 1 and 50.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt;50.</exception>
        [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
        public async Task<AtProtoHttpResult<ICollection<StarterPackView>>> GetSuggestedStarterPacks(
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.SuggestedStarterPacks);
            }

#pragma warning disable BSKYUnspecced
            return await BlueskyServer.GetSuggestedStarterPacks(
                limit,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore BSKYUnspecced
        }

        /// <summary>
        /// Get a <see cref="PagedReadOnlyCollection{T}"/> of <see cref="ProfileView"/>s of suggested actors.
        /// </summary>
        /// <param name="category">An optional category of users to get suggestions for.</param>
        /// <param name="limit">The number of topics to return. Must be between 1 and 50.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt;50.</exception>
        [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
        public async Task<AtProtoHttpResult<ICollection<ProfileView>>> GetSuggestedUsers(
            string? category = null,
            int? limit = null,
            CancellationToken cancellationToken = default)
        {
            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.SuggestedUsers);
            }

#pragma warning disable BSKYUnspecced
            return await BlueskyServer.GetSuggestedUsers(
                category,
                limit,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore BSKYUnspecced
        }

        /// <summary>
        /// Get a collection of tagged suggestions.
        /// </summary>
        /// <param name="parameters">Any parameters to send to the endpoint. Parameter values will automatically be query string encoded.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
        public async Task<AtProtoHttpResult<ICollection<Suggestion>>> GetTaggedSuggestions(
            ICollection<KeyValuePair<string, object>>? parameters = null,
            CancellationToken cancellationToken = default)
        {
#pragma warning disable BSKYUnspecced
            return await BlueskyServer.GetTaggedSuggestions(
                parameters,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore BSKYUnspecced
        }

        /// <summary>
        /// Get a collection of trending topics
        /// </summary>
        /// <param name="limit">The number of topics to return. Must be between 1 and 25.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt;25.</exception>
        [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
        public async Task<AtProtoHttpResult<TrendingTopics>> GetTrendingTopics(int? limit = null, CancellationToken cancellationToken = default)
        {
            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.TrendingTopics);
            }

#pragma warning disable BSKYUnspecced
            return await BlueskyServer.GetTrendingTopics(
                limit,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore BSKYUnspecced
        }

        /// <summary>
        /// Get a collection of trends.
        /// </summary>
        /// <param name="limit">The number of topics to return. Must be between 1 and 25.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt;25.</exception>
        [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
        public async Task<AtProtoHttpResult<ICollection<TrendView>>> GetTrends(int? limit = null, CancellationToken cancellationToken = default)
        {
            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.Trends);
            }

#pragma warning disable BSKYUnspecced
            return await BlueskyServer.GetTrends(
                limit,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore BSKYUnspecced
        }
    }
}
