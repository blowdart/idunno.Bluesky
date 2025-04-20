// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Actor.Model;
using idunno.AtProto.Authentication;

namespace idunno.Bluesky
{
    public static partial class BlueskyServer
    {
        // https://docs.bsky.app/docs/api/app-bsky-actor-get-profile
        private const string GetProfileEndpoint = "/xrpc/app.bsky.actor.getProfile";

        // https://docs.bsky.app/docs/api/app-bsky-actor-get-profiles
        private const string GetProfilesEndpoint = "/xrpc/app.bsky.actor.getProfiles";

        // https://docs.bsky.app/docs/api/app-bsky-actor-get-preferences
        private const string GetPreferencesEndpoint = "/xrpc/app.bsky.actor.getPreferences";

        // https://docs.bsky.app/docs/api/app-bsky-actor-get-suggestions
        private const string GetSuggestionsEndpoint = "/xrpc/app.bsky.actor.getSuggestions";

        // https://docs.bsky.app/docs/api/app-bsky-actor-put-preferences
        private const string PutPreferencesEndpoint = "/xrpc/app.bsky.actor.putPreferences";

        // https://docs.bsky.app/docs/api/app-bsky-actor-search-actors
        private const string SearchActorsEndpoint = "/xrpc/app.bsky.actor.searchActors";

        // https://docs.bsky.app/docs/api/app-bsky-actor-search-actors-typeahead
        private const string SearchActorsTypeAheadEndpoint = "/xrpc/app.bsky.actor.searchActorsTypeahead";


        /// <summary>
        /// Get a detailed profile view of an account. Does not require authentication,
        /// but if an access token is provided will contain relevant metadata about the requesting
        /// actor's relationship to the requested account.
        /// </summary>
        /// <param name="actor">The handle or DID of the account to fetch profile of.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<ProfileViewDetailed>> GetProfile(
            AtIdentifier actor,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<ProfileViewDetailed> request = new(AppViewProxy, loggerFactory);

            return await request.Get(
                service,
                $"{GetProfileEndpoint}?actor={Uri.EscapeDataString(actor.ToString())}",
                credentials: accessCredentials,
                onCredentialsUpdated: onCredentialsUpdated,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                subscribedLabelers : subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a detailed profile view of accounts. Does not require authentication,
        /// but if an access token is provided will contain relevant metadata about the requesting
        /// actor's relationship to the requested account.
        /// </summary>
        /// <param name="actors">The handle or DID of the accounts to fetch profiles for.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actors"/>, <paramref name="service"/> or <paramref name="httpClient" /> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="actors"/> is an empty collection or if it contains &gt;25 handles.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<IReadOnlyCollection<ProfileViewDetailed>>> GetProfiles(
            IEnumerable<AtIdentifier> actors,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actors);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            var actorList = new List<AtIdentifier>(actors);

            if (actorList.Count == 0 || actorList.Count > 25)
            {
                ArgumentOutOfRangeException.ThrowIfZero(actorList.Count);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(actorList.Count, 25);
            }

            string queryString = string.Join("&", actorList.Select(uri => $"actors={Uri.EscapeDataString(uri.ToString())}"));

            AtProtoHttpClient<GetProfilesResponse> request = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetProfilesResponse> response =  await request.Get(
                service,
                $"{GetProfilesEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers : subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<IReadOnlyCollection<ProfileViewDetailed>>(
                    new Collection<ProfileViewDetailed>(response.Result.Profiles).AsReadOnly(), 
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<IReadOnlyCollection<ProfileViewDetailed>>(
                    new Collection<ProfileViewDetailed>().AsReadOnly(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Gets the preferences for the requesting account.
        /// </summary>
        /// <param name="includeBlueskyModerationLabeler">Flag indicating whether the Bluesky moderation service should be returned as part of the labeler subscriptions.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>,<paramref name="accessCredentials"/> or <paramref name="httpClient"/> are null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<Preferences>> GetPreferences(
            bool includeBlueskyModerationLabeler,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<GetPreferencesResponse> client = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<GetPreferencesResponse> response = await client.Get(
                service,
                GetPreferencesEndpoint,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<Preferences>(
                    new Preferences(response.Result.Preferences, includeBlueskyModerationLabeler),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Preferences>(new Preferences(), response.StatusCode, response.HttpResponseHeaders, response.AtErrorDetail,response.RateLimit);
            }
        }

        /// <summary>
        /// Get a list of suggested actors for the authenticator users. The expected use is discovery of accounts to follow during new account onboarding.
        /// </summary>
        /// <param name="limit">The number of suggested actors to return.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt;=0 or &gt;100.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> GetSuggestions(
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            int limitValue = limit ?? 50;

            ArgumentOutOfRangeException.ThrowIfNegative(limitValue);
            ArgumentOutOfRangeException.ThrowIfZero(limitValue);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limitValue, Maximum.SuggestedActors);

            AtProtoHttpClient<GetSuggestionsResponse> request = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<GetSuggestionsResponse> response = await request.Get(
                service,
                $"{GetSuggestionsEndpoint}?cursor={cursor}&limit={limit}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers : subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>(
                    new PagedViewReadOnlyCollection<ProfileView>(new List<ProfileView>(response.Result.Actors).AsReadOnly(), response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>(
                    new PagedViewReadOnlyCollection<ProfileView>(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Gets the preferences for the requesting account.
        /// </summary>
        /// <param name="preferences">The preferences to update</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>,<paramref name="accessCredentials"/> or <paramref name="httpClient"/> are null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<EmptyResponse>> PutPreferences(
            IList<Preference> preferences,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<EmptyResponse> client = new(AppViewProxy, loggerFactory);

            PutPreferencesRequest request = new(preferences);

            AtProtoHttpResult<EmptyResponse> response = await client.Post(
                service,
                PutPreferencesEndpoint,
                request,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions : BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return response;
        }

        /// <summary>
        /// Find actors (profiles) matching search criteria. Does not require authentication.
        /// </summary>
        /// <param name="q">The search query string. Syntax, phrase, Boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
        /// <param name="limit">The number of suggested actors to return. Defaults to 50 if null.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="q"/>, <paramref name="service"/>, or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt;=0 or &gt;100.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> SearchActors(
            string q,
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
            ArgumentException.ThrowIfNullOrWhiteSpace(q);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            int limitValue = limit ?? 25;

            ArgumentOutOfRangeException.ThrowIfNegative(limitValue);
            ArgumentOutOfRangeException.ThrowIfZero(limitValue);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limitValue, 100);

            AtProtoHttpClient<SearchActorsResponse> request = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<SearchActorsResponse> response = await request.Get(
                service,
                $"{SearchActorsEndpoint}?q={Uri.EscapeDataString(q)}&limit={limit}&cursor={cursor}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>(
                    new PagedViewReadOnlyCollection<ProfileView>(new List<ProfileView>(response.Result.Actors).AsReadOnly(), response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>(
                    new PagedViewReadOnlyCollection<ProfileView>(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Find actor suggestions for a prefix search term. Expected use is for auto-completion during text field entry. Does not require authentication.
        /// </summary>
        /// <param name="q">"Search query prefix; not a full query string.</param>
        /// <param name="limit">The number of suggested actors to return. Defaults to 50 if null.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="q"/> is null.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt;=0 or &gt;100.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileViewBasic>>> SearchActorsTypeahead(
            string q,
            int? limit,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(q);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            int limitValue = limit ?? 10;

            ArgumentOutOfRangeException.ThrowIfNegative(limitValue);
            ArgumentOutOfRangeException.ThrowIfZero(limitValue);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limitValue, 100);

            AtProtoHttpClient<SearchActorsResponse> request = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<SearchActorsResponse> response = await request.Get(
                service,
                $"{SearchActorsTypeAheadEndpoint}?q={Uri.EscapeDataString(q)}&limit={limit}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions : BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileViewBasic>>(
                    new PagedViewReadOnlyCollection<ProfileViewBasic>(new List<ProfileViewBasic>(response.Result.Actors).AsReadOnly()),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileViewBasic>>(
                    new PagedViewReadOnlyCollection<ProfileViewBasic>(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }
    }
}
