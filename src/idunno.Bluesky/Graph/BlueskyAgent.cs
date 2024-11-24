// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Graph;

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
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="actor"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="uris"/> does not contain any <see cref="AtUri"/>s or has &gt; 25 <see cref="AtUri"/>s.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> GetBlocks(
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.GetBlocks(
                limit,
                cursor,
                Service,
                AccessToken,
                HttpClient,
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
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="actor"/> is null.</exception>
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
                AuthenticatedOrUnauthenticatedServiceUri,
                AccessToken,
                HttpClient,
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
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<Followers>> GetFollowers(
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await GetFollowers(
                Session.Did,
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
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="actor"/> is null.</exception>
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
                AuthenticatedOrUnauthenticatedServiceUri,
                AccessToken,
                HttpClient,
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
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<Follows>> GetFollows(
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.GetFollows(
                Session.Did,
                limit,
                cursor,
                AuthenticatedOrUnauthenticatedServiceUri,
                AccessToken,
                HttpClient,
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
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<Followers>> GetKnownFollowers(
            AtIdentifier actor,
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            ArgumentNullException.ThrowIfNull(actor);

            return await BlueskyServer.GetKnownFollowers(
                actor,
                limit,
                cursor,
                Service,
                AccessToken,
                HttpClient,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a paged list of mod lists that the requesting account (actor) is blocking. Requires authentication.
        /// </summary>
        /// <param name="limit">The maximum number of lists that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>> GetListBlocks(
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.GetListBlocks(
                limit,
                cursor,
                Service,
                AccessToken,
                HttpClient,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a paged list of mod lists that the requesting account (actor) is muting. Requires authentication.
        /// </summary>
        /// <param name="limit">The maximum number of lists that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>> GetListMutes(
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.GetListMutes(
                limit,
                cursor,
                Service,
                AccessToken,
                HttpClient,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }


        /// <summary>
        /// Get a <see cref="ListView"/> of a Bluesky list and a paginated collection of <see cref="ListItemView"/>s of its items.
        /// </summary>
        /// <param name="list">The <see cref="AtUri"/> of the list to get.</param>
        /// <param name="limit">The maximum number of lists that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="list"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        public async Task<AtProtoHttpResult<ListViewWithItems>> GetList(
            AtUri list,
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(list);

            return await BlueskyServer.GetList(
                list,
                limit,
                cursor,
                AuthenticatedOrUnauthenticatedServiceUri,
                AccessToken,
                HttpClient,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a paged list of lists that created by the specified <paramref name="actor"/>.
        /// </summary>
        /// <param name="actor">The actor whose lists to enumerate.</param>
        /// <param name="limit">The maximum number of lists that should be return in a page.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessToken">An access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="actor"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
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
                AuthenticatedOrUnauthenticatedServiceUri,
                AccessToken,
                HttpClient,
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
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> GetMutes(
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.GetMutes(
                limit,
                cursor,
                Service,
                AccessToken,
                HttpClient,
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
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="actor"/> or <paramref name="others"/> is null.</exception>
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
                AuthenticatedOrUnauthenticatedServiceUri,
                AccessToken,
                HttpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerates public relationships between the current account, and a list of other accounts.
        /// </summary>
        /// <param name="others">A list of other accounts to be related back to <paramref name="actor"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="actor"/> or <paramref name="others"/> is null.</exception>
        public async Task<AtProtoHttpResult<ActorRelationships>> GetRelationships(
            ICollection<Did> others,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            ArgumentNullException.ThrowIfNull(others);

            return await BlueskyServer.GetRelationships(
                Did,
                others,
                AuthenticatedOrUnauthenticatedServiceUri,
                AccessToken,
                HttpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a view of a starter pack.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the starter pack to view.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        public async Task<AtProtoHttpResult<StarterPackView>> GetStarterPack(
            AtUri uri,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            return await BlueskyServer.GetStarterPack(
                uri,
                AuthenticatedOrUnauthenticatedServiceUri,
                AccessToken,
                HttpClient,
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
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="uris"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        public async Task<AtProtoHttpResult<IReadOnlyList<StarterPackViewBasic>>> GetStarterPacks(
            ICollection<AtUri> uris,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uris);

            return await BlueskyServer.GetStarterPacks(
                uris,
                AuthenticatedOrUnauthenticatedServiceUri,
                AccessToken,
                HttpClient,
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
        public async Task<AtProtoHttpResult<SuggestedActors>> GetSuggestedFollowsByActor(
            AtIdentifier actor,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            return await BlueskyServer.GetSuggestedFollowsByActor(
                actor,
                AuthenticatedOrUnauthenticatedServiceUri,
                AccessToken,
                HttpClient,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a mute relationship for the specified list of accounts. Requires authentication.
        /// </summary>
        /// <param name="listUri">The <see cref="AtUri"/> of the list of actors to mute.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="listUri"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> MuteActorList(
            AtUri listUri,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(listUri);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.MuteActorList(
                listUri,
                Service,
                AccessToken,
                HttpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a mute relationship for the specified account. Requires authentication.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor to mute.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="actor"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> MuteActor(
            AtIdentifier actor,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.MuteActor(
                actor,
                Service,
                AccessToken,
                HttpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Mutes a thread preventing notifications from the thread and any of its children. Requires authentication.
        /// </summary>
        /// <param name="rootUri">The <see cref="AtUri"/> of the thread to mute</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="rootUri"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> MuteThread(
            AtUri rootUri,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(rootUri);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.MuteThread(
                rootUri,
                Service,
                AccessToken,
                HttpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Unmutes the specified list of accounts. Requires authentication.
        /// </summary>
        /// <param name="listUri">The <see cref="AtUri"/> of the list of actors to unmute.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="listUri"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> UnmuteActorList(
            AtUri listUri,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(listUri);

            if (Session is null || string.IsNullOrWhiteSpace(AccessToken))
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.UnmuteActorList(
                listUri,
                Service,
                AccessToken,
                HttpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Unmutes the specified account. Requires authentication.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor to unmute</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="actor"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> UnmuteActor(
            AtIdentifier actor,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.UnmuteActor(
                actor,
                Service,
                AccessToken,
                HttpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Unmutes a thread. Requires authentication.
        /// </summary>
        /// <param name="rootUri">The <see cref="AtUri"/> of the thread to unmute</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="rootUri"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the agent is unauthenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> UnmuteThread(
            AtUri rootUri,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(rootUri);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.UnmuteThread(
                rootUri,
                Service,
                AccessToken,
                HttpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
