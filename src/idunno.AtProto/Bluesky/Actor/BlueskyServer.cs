// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using idunno.AtProto.Bluesky.Actor;
using idunno.AtProto.Bluesky.Feed;

namespace idunno.AtProto.Bluesky
{
    internal partial class BlueskyServer
    {
        // https://docs.bsky.app/docs/api/app-bsky-actor-get-profile
        private const string GetProfileEndpoint = "/xrpc/app.bsky.actor.getProfile";

        // https://docs.bsky.app/docs/api/app-bsky-actor-get-profiles
        private const string GetProfilesEndpoint = "/xrpc/app.bsky.actor.getProfiles";

        // https://docs.bsky.app/docs/api/app-bsky-actor-get-suggestions
        private const string GetSuggestionsEndpoint = "/xrpc/app.bsky.actor.getSuggestions";

        // https://docs.bsky.app/docs/api/app-bsky-actor-search-actors
        private const string SearchActorsEndpoint = "/xrpc/app.bsky.actor.searchActors";

        /// <summary>
        /// Get detailed profile view of an actor. Does not require authentication, but contains relevant metadata with authentication.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor to fetch the profile of.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessToken">The access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="ActorProfile"/> containing information for the specified <paramref name="actor"/>.</returns>
        public static async Task<HttpResult<ActorProfile>> GetProfile(
            AtIdentifier actor,
            Uri service,
            string? accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken = default)
        {
            AtProtoRequest<ActorProfile> request = new();

            return await request.Get(
                service,
                $"{GetProfileEndpoint}?actor={Uri.EscapeDataString(actor.ToString())}",
                accessToken,
                httpClientHandler,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get detailed profile view of multiple actors. Does not require authentication, but contains relevant metadata with authentication.
        /// </summary>
        /// <param name="actors">The <see cref="AtIdentifier"/>s of the actors to fetch the profiles of.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessToken">The access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="ActorProfile"/> containing information for the specified <paramref name="actor"/>.</returns>
        public static async Task<HttpResult<IReadOnlyList<ActorProfile>?>> GetProfiles(
            AtIdentifier[] identifiers,
            Uri service,
            string? accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            string identifiersAsQueryString = string.Join("&", identifiers.Select(atId => $"actors={Uri.EscapeDataString(atId.ToString())}"));

            AtProtoRequest<FeedProfiles> request = new();

            HttpResult<FeedProfiles> result = await request.Get(
                service,
                $"{GetProfilesEndpoint}?{identifiersAsQueryString}",
                accessToken,
                httpClientHandler,
                cancellationToken).ConfigureAwait(false);

            // Flatten the result a little for ease of use.

            HttpResult<IReadOnlyList<ActorProfile>?> profilesAsArray = new()
            {
                StatusCode = result.StatusCode
            };

            if (result.Succeeded && result.Result is not null && result.Result.Profiles is not null)
            {
                profilesAsArray.Result = result.Result.Profiles;
            }

            if (result.Error is not null)
            {
                profilesAsArray.Error = result.Error;
            }

            return profilesAsArray;
        }

        /// <summary>
        /// Retrieves a list of suggested actors for the user associated with the accessToken.
        /// </summary>
        /// <param name="service">The service to retrieve the timeline from.</param>
        /// <param name="limit">The maximum number of entries to retrieve.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="accessToken">The access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A view of the suggested actors, and a cursor, from the <paramref name="service"/>.</returns>
        public static async Task<HttpResult<ActorSuggestions>> GetSuggestions(
            Uri service,
            int? limit,
            string? cursor,
            string accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            StringBuilder queryString = new();

            if (limit is not null)
            {
                queryString.Append(CultureInfo.InvariantCulture, $"limit={limit}&");
            }

            if (cursor is not null)
            {
                queryString.Append(CultureInfo.InvariantCulture, $"cursor={Uri.EscapeDataString(cursor)}&");
            }

            if (queryString.Length > 0 && queryString[queryString.Length - 1] == '&')
            {
                queryString.Remove(queryString.Length - 1, 1);
            }

            AtProtoRequest<ActorSuggestions> request = new();

            return await request.Get(
                service,
                $"{GetSuggestionsEndpoint}?{queryString}",
                accessToken,
                httpClientHandler,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Find actors matching the specified search criteria.
        /// </summary>
        /// <param name="service">The service to retrieve the timeline from.</param>
        /// <param name="q">Search query string. Syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
        /// <param name="limit">The maximum number of entries to retrieve.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="accessToken">The access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A view of any actors matching the search criteria, the number of which are limited by the <paramref name="limit"/> and a cursor for pagination.</returns>
        public static async Task<HttpResult<ActorSearchResults>> SearchActors(
            Uri service,
            string q,
            int? limit,
            string? cursor,
            string? accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            StringBuilder queryString = new();

            queryString.Append(CultureInfo.InvariantCulture, $"term={Uri.EscapeDataString(q)}&");

            if (limit is not null)
            {
                queryString.Append(CultureInfo.InvariantCulture, $"limit={limit}&");
            }

            if (cursor is not null)
            {
                queryString.Append(CultureInfo.InvariantCulture, $"cursor={Uri.EscapeDataString(cursor)}&");
            }

            if (queryString.Length > 0 && queryString[queryString.Length - 1] == '&')
            {
                queryString.Remove(queryString.Length - 1, 1);
            }

            AtProtoRequest<ActorSearchResults> request = new();

            return await request.Get(
                service,
                $"{SearchActorsEndpoint}?{queryString}",
                accessToken,
                httpClientHandler,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
