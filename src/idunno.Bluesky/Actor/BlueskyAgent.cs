// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Gets a <see cref="ProfileViewDetailed"/> for the specified <paramref name="actor"/>.
        /// </summary>
        /// <param name="actor">The actor to retrieve the <see cref="ProfileViewDetailed"/> for.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is null.</exception>
        public async Task<AtProtoHttpResult<ProfileViewDetailed>> GetProfile(
            AtIdentifier actor,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            return await BlueskyServer.GetProfile(
                actor,
                AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets <see cref="ProfileViewDetailed"/>s for the specified <paramref name="actors"/>.
        /// </summary>
        /// <param name="actors">A collection of <see cref="AtIdentifier"/>s of the actors to return <see cref="ProfileViewDetailed"/>s for.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actors"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="actors"/> is an empty collection or if it contains &gt;25 handles.</exception>
        public async Task<AtProtoHttpResult<IReadOnlyCollection<ProfileViewDetailed>>> GetProfiles(
            IEnumerable<AtIdentifier> actors,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actors);

            var actorList = new List<AtIdentifier>(actors);

            if (actorList.Count == 0 || actorList.Count > 25)
            {
                ArgumentOutOfRangeException.ThrowIfZero(actorList.Count);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(actorList.Count, 25);
            }

            return await BlueskyServer.GetProfiles(
                actorList,
                AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the preferences for the current user.
        /// </summary>
        /// <param name="includeBlueskyModerationLabeler">Flag indicating whether labeler subscriptions should include the default Bluesky moderation labeler.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Preferences>> GetPreferences(bool includeBlueskyModerationLabeler = true, CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.GetPreferences(
                includeBlueskyModerationLabeler,
                Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a list of suggested actors for the authenticator users. The expected use is discovery of accounts to follow during new account onboarding.
        /// </summary>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> GetSuggestions(
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await GetSuggestions(50, cursor, subscribedLabelers, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a list of suggested actors for the authenticator users. The expected use is discovery of accounts to follow during new account onboarding.
        /// </summary>
        /// <param name="limit">The number of suggested actors to return. Defaults to 50 if null.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt;=0 or &gt;100.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> GetSuggestions(
            int? limit,
            string? cursor,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            int limitValue = limit ?? 50;

            ArgumentOutOfRangeException.ThrowIfNegative(limitValue);
            ArgumentOutOfRangeException.ThrowIfZero(limitValue);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limitValue, 100);

            return await BlueskyServer.GetSuggestions(
                limit,
                cursor,
                Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Find actors (profiles) matching search criteria. Does not require authentication.
        /// </summary>
        /// <param name="q">The search query string. Syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
        /// <param name="limit">The number of suggested actors to return. Defaults to 50 if null.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="q" /> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt;=0 or &gt;100.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> SearchActors(
            string q,
            int? limit = 50,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(q);

            if (limit is not null)
            {
                int limitValue = (int)limit;

                ArgumentOutOfRangeException.ThrowIfNegative(limitValue);
                ArgumentOutOfRangeException.ThrowIfZero(limitValue);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(limitValue, 100);
            }

            return await BlueskyServer.SearchActors(
                q,
                limit,
                cursor,
                AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Find actor suggestions for a prefix search term. Expected use is for auto-completion during text field entry. Does not require authentication.
        /// </summary>
        /// <param name="q">Search query prefix; not a full query string.</param>
        /// <param name="limit">The number of suggested actors to return. Defaults to 50 if null.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="q" /> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt;=0 or &gt;100.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileViewBasic>>> SearchActorsTypeahead(
            string q,
            int? limit = 10,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(q);

            if (limit is not null)
            {
                int limitValue = (int)limit;

                ArgumentOutOfRangeException.ThrowIfNegative(limitValue);
                ArgumentOutOfRangeException.ThrowIfZero(limitValue);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(limitValue, 100);
            }

            return await BlueskyServer.SearchActorsTypeahead(
                q,
                limit,
                AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
