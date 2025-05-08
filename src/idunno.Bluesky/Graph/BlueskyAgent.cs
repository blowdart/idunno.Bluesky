// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Record;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Enumerates which accounts the requesting account is currently blocking. Requires authentication.
        /// </summary>
        /// <param name="limit">The maximum number of followers that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> GetBlocks(
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.GetBlocks(
                limit,
                cursor,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a paged list of followers for the specified <paramref name="actor"/>.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose followers should be retrieved.</param>
        /// <param name="limit">The maximum number of followers that will be included in the paged results.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is null.</exception>
        public async Task<AtProtoHttpResult<Followers>> GetFollowers(
            AtIdentifier actor,
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            return await BlueskyServer.GetFollowers(
                actor,
                limit,
                cursor,
                service: AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a paged list of followers for the current user. Requires an authenticated session.
        /// </summary>
        /// <param name="limit">The maximum number of followers that will be included in the paged results.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<Followers>> GetFollowers(
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await GetFollowers(
                Credentials.Did,
                limit,
                cursor,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a paged list of accounts whom an actor follows.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose follows should be retrieved.</param>
        /// <param name="limit">The maximum number of follows that will be included in the paged results.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is null.</exception>
        public async Task<AtProtoHttpResult<Follows>> GetFollows(
            AtIdentifier actor,
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            return await BlueskyServer.GetFollows(
                actor,
                limit,
                cursor,
                service: AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a paged list of accounts whom the current user follows. Requires an authenticated session.
        /// </summary>
        /// <param name="limit">The maximum number of follows that will be included in the paged results.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<Follows>> GetFollows(
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await GetFollows(
                Credentials.Did,
                limit,
                cursor,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a paged list of accounts which follow a specified account (actor) and are followed by the viewer. Requires authentication.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose follows should be enumerated.</param>
        /// <param name="limit">The maximum number of followers that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<Followers>> GetKnownFollowers(
            AtIdentifier actor,
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            ArgumentNullException.ThrowIfNull(actor);

            return await BlueskyServer.GetKnownFollowers(
                actor,
                limit,
                cursor,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a paged list of mod lists that the requesting account (actor) is blocking. Requires authentication.
        /// </summary>
        /// <param name="limit">The maximum number of lists that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>> GetListBlocks(
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.GetListBlocks(
                limit,
                cursor,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a paged list of mod lists that the requesting account (actor) is muting. Requires authentication.
        /// </summary>
        /// <param name="limit">The maximum number of lists that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>> GetListMutes(
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.GetListMutes(
                limit,
                cursor,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }


        /// <summary>
        /// Get a <see cref="ListView"/> of a Bluesky list and a paginated collection of <see cref="ListItemView"/>s of its items.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the list to get.</param>
        /// <param name="limit">The maximum number of lists that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        public async Task<AtProtoHttpResult<ListViewWithItems>> GetList(
            AtUri uri,
            int? limit = 50,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            return await BlueskyServer.GetList(
                uri,
                limit,
                cursor,
                service: AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the referenced record for a list.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the list record to get.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null or the uri collection property is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/> does not point to a list record.</exception>
        public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<BlueskyList>>> GetListRecord(
            AtUri uri,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(uri);

            ArgumentNullException.ThrowIfNull(uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.List);

            return await GetBlueskyRecord<BlueskyList>(
                uri: uri,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a paged list of lists that created by the specified <paramref name="actor"/>.
        /// </summary>
        /// <param name="actor">The actor whose lists to enumerate.</param>
        /// <param name="limit">The maximum number of lists that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>> GetLists(
            AtIdentifier actor,
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            return await BlueskyServer.GetLists(
                actor,
                limit,
                cursor,
                service: AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a paged list of muted profiles for the current user. Requires authentication.
        /// </summary>
        /// <param name="limit">The maximum number of lists that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> GetMutes(
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.GetMutes(
                limit,
                cursor,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerates public relationships between one account, and a list of other accounts.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose follows should be enumerated.</param>
        /// <param name="others">A list of other accounts to be related back to <paramref name="actor"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> or <paramref name="others"/> is null.</exception>
        public async Task<AtProtoHttpResult<ActorRelationships>> GetRelationships(
            Did actor,
            ICollection<Did> others,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);
            ArgumentNullException.ThrowIfNull(others);

            return await BlueskyServer.GetRelationships(
                actor,
                others,
                service: AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerates public relationships between the current account, and a list of other accounts.
        /// </summary>
        /// <param name="others">A list of other accounts to be related back to current actor.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="others"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<ActorRelationships>> GetRelationships(
            ICollection<Did> others,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            ArgumentNullException.ThrowIfNull(others);

            return await GetRelationships(
                Did,
                others,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a view of a starter pack.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the starter pack to view.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null</exception>
        public async Task<AtProtoHttpResult<StarterPackView>> GetStarterPack(
            AtUri uri,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            return await BlueskyServer.GetStarterPack(
                uri,
                service: AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a basic view of the specified starter packs.
        /// </summary>
        /// <param name="uris">A collection of <see cref="AtUri"/>s for the starter packs to view.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uris"/> is null.</exception>
        public async Task<AtProtoHttpResult<IReadOnlyList<StarterPackViewBasic>>> GetStarterPacks(
            ICollection<AtUri> uris,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uris);

            return await BlueskyServer.GetStarterPacks(
                uris,
                service: AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerates follows similar to a given account (<paramref name="actor"/>). Expected use is to recommend additional accounts immediately after following one account.
        /// </summary>
        /// <param name="actor">The account to enumerate suggested follows for.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is null.</exception>
        public async Task<AtProtoHttpResult<SuggestedActors>> GetSuggestedFollowsByActor(
            AtIdentifier actor,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            return await BlueskyServer.GetSuggestedFollowsByActor(
                actor,
                service: AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a mute relationship for the specified account. Requires authentication.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor to mute.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> Mute(
            AtIdentifier actor,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.MuteActor(
                actor,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a mute relationship for the specified account. Requires authentication.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor to mute.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> MuteActor(
            AtIdentifier actor,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await Mute(
                actor,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a mute relationship for the specified list of accounts. Requires authentication.
        /// </summary>
        /// <param name="listUri">The <see cref="AtUri"/> of the list of actors to mute.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="listUri"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> MuteActorList(
            AtUri listUri,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(listUri);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.MuteActorList(
                listUri,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a mute relationship for the specified moderation list. Requires authentication.
        /// </summary>
        /// <param name="listUri">The <see cref="AtUri"/> of the moderation list of actors to mute.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="listUri"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> MuteModList(
            AtUri listUri,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(listUri);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await MuteActorList(listUri, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Mutes a thread preventing notifications from the thread and any of its children. Requires authentication.
        /// </summary>
        /// <param name="rootUri">The <see cref="AtUri"/> of the thread to mute</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rootUri"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> MuteThread(
            AtUri rootUri,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(rootUri);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.MuteThread(
                rootUri,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Unmutes the specified list of accounts. Requires authentication.
        /// </summary>
        /// <param name="listUri">The <see cref="AtUri"/> of the list of actors to unmute.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="listUri"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> UnmuteActorList(
            AtUri listUri,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(listUri);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.UnmuteActorList(
                listUri,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Unmutes the specified moderation list. Requires authentication.
        /// </summary>
        /// <param name="listUri">The <see cref="AtUri"/> of the list of actors to unmute.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="listUri"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> UnmuteModeList(
            AtUri listUri,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(listUri);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await UnmuteActorList(listUri, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Unmutes the specified account. Requires authentication.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor to unmute</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> Unmute(
            AtIdentifier actor,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.UnmuteActor(
                actor,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Unmutes the specified account. Requires authentication.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor to unmute</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> UnmuteActor(
            AtIdentifier actor,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await Unmute(actor, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Unmutes a thread. Requires authentication.
        /// </summary>
        /// <param name="rootUri">The <see cref="AtUri"/> of the thread to unmute</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rootUri"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> UnmuteThread(
            AtUri rootUri,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(rootUri);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.UnmuteThread(
                rootUri,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
