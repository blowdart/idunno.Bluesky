// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Bluesky;
using idunno.AtProto.Bluesky.Actor;
using idunno.AtProto.Bluesky.Feed;
using idunno.AtProto.Bluesky.Notifications;
using idunno.AtProto.Repo;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides a class for sending requests to and receiving responses from an Bluesky service, identified by its service URI.
    /// </summary>
    public class BlueskyAgent : AtProtoAgent
    {
        /// <summary>
        /// Creates a new instance of <see cref="BlueskyAgent"/>.
        /// </summary>
        public BlueskyAgent() : base()
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/>.
        /// </summary>
        /// <param name="enableTokenRefresh">A flag indicating if the agent should handle token refreshing automatically.</param>
        public BlueskyAgent(bool enableTokenRefresh = true) : base(enableTokenRefresh)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/> which will default to the specified <paramref name="service"/>.
        /// </summary>
        /// <param name="service">The service to connect to if no service is specified.</param>
        public BlueskyAgent(Uri service) : base(service)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyAgent"/> which will default to the specified <paramref name="service"/>.
        /// </summary>
        /// <param name="service">The service to connect to if no service is specified.</param>
        /// <param name="enableTokenRefresh">A flag indicating if the agent should handle token refreshing automatically.</param>
        public BlueskyAgent(Uri service, bool enableTokenRefresh = true) : base(service, enableTokenRefresh)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/> which will default to the specified <paramref name="service"/>
        /// and will use the specified <paramref name="httpClientHandler"/> when HttpClients are created to make requests.
        /// </summary>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making requests.</param>
        public BlueskyAgent(HttpClientHandler httpClientHandler) : base(httpClientHandler, true)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyAgent"/> which will default to the specified <paramref name="service"/>
        /// and will use the specified <paramref name="httpClientHandler"/> when HttpClients are created to make requests.
        /// </summary>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making requests.</param>
        public BlueskyAgent(HttpClientHandler httpClientHandler, bool enableTokenRefresh = true) : base(httpClientHandler, enableTokenRefresh)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyAgent"/> which will default to the specified <paramref name="service"/>
        /// and will use the specified <paramref name="httpClientHandler"/> when HttpClients are created to make requests.
        /// </summary>
        /// <param name="service">The service to connect to if no service is specified.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making requests.</param>
        public BlueskyAgent(Uri service, HttpClientHandler httpClientHandler) : base(service, httpClientHandler, true)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="BlueskyAgent"/> which will default to the specified <paramref name="service"/>
        /// and will use the specified <paramref name="httpClientHandler"/> when HttpClients are created to make requests.
        /// </summary>
        /// <param name="service">The service to connect to if no service is specified.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making requests.</param>
        public BlueskyAgent(Uri service, HttpClientHandler httpClientHandler, bool enableTokenRefresh = true) : base(service, httpClientHandler, enableTokenRefresh)
        {
        }

        /// <summary>
        /// Get detailed profile view of an actor. Does not require authentication, but contains relevant metadata with authentication.
        /// </summary>
        /// <param name="actor">Handle or DID of account to fetch profile of.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> wrapping an <see cref="ActorProfile"/> containing information for the specified <paramref name="actor"/>,
        /// or any error details returned by the service.
        /// </returns>
        public async Task<HttpResult<ActorProfile>> GetActorProfile(string actor, CancellationToken cancellationToken = default)
        {
            return await GetActorProfile(actor, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get detailed profile view of an actor. Does not require authentication, but contains relevant metadata with authentication.
        /// </summary>
        /// <param name="actor">Handle or DID of account to fetch profile of.</param>
        /// <param name="service">The service to query.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> wrapping an <see cref="ActorProfile"/> containing information for the specified <paramref name="actor"/>,
        /// or any error details returned by the service.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="actor"/> is not a valid <see cref="AtIdentifier"/>.</exception>
        public async Task<HttpResult<ActorProfile>> GetActorProfile(string actor, Uri service, CancellationToken cancellationToken = default)
        {
            if (AtIdentifier.TryParse(actor, out AtIdentifier? atIdentifier))
            {
                return await GetActorProfile(atIdentifier!, service, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentException($"{actor} is not a valid AtIdentifier.", nameof(actor));
            }
        }

        /// <summary>
        /// Get detailed profile view of an actor. Does not require authentication, but contains relevant metadata with authentication.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the account whose profile to fetch.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> wrapping an <see cref="ActorProfile"/> containing information for the specified <paramref name="actor"/>,
        /// or any error details returned by the service.
        /// </returns>
        public async Task<HttpResult<ActorProfile>> GetActorProfile(AtIdentifier actor, CancellationToken cancellationToken = default)
        {
            return await GetActorProfile(actor, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get detailed profile view of an actor. Does not require authentication, but contains relevant metadata with authentication.
        /// </summary>
        /// <param name="actor">The AT Identifier (either a <see cref="Did"/> or <see cref="Handle"/> of the account whose profile to fetch.</param>
        /// <param name="service">The service to query.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> wrapping an <see cref="ActorProfile"/> containing information for the specified <paramref name="actor"/>,
        /// or any error details returned by the service.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if the actor is null.</exception>
        public async Task<HttpResult<ActorProfile>> GetActorProfile(AtIdentifier actor, Uri service, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            string? accessJwt = null;

            if (Session == null || string.IsNullOrEmpty(Session.AccessJwt))
            {
                if (service == DefaultServices.DefaultService)
                {
                    service = DefaultServices.ReadOnlyService;
                }
            }
            else
            {
                accessJwt = Session.AccessJwt;
            }

            return await BlueskyServer.GetProfile(actor, service, accessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get detailed profile view of the specified actors. Does not require authentication, but contains relevant metadata with authentication.
        /// </summary>
        /// <param name="actors">An array of AT Identifiers (either a <see cref="Did"/> or <see cref="Handle"/> of the accounts whose profile to fetch.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> wrapping an array of <see cref="ActorProfile"/> containing information for the specified <paramref name="actor"/>,
        /// or any error details returned by the service.
        /// </returns>
        public async Task<HttpResult<IReadOnlyList<ActorProfile>?>> GetActorProfiles(AtIdentifier[] actors, CancellationToken cancellationToken = default)
        {
            return await GetActorProfiles(actors, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get detailed profile view of the specified actors. Does not require authentication, but contains relevant metadata with authentication.
        /// </summary>
        /// <param name="actors">An array of AT Identifiers (either a <see cref="Did"/> or <see cref="Handle"/> of the accounts whose profile to fetch.</param>
        /// <param name="service">The service to query.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> wrapping an array of <see cref="ActorProfile"/> containing information for the specified <paramref name="actor"/>,
        /// or any error details returned by the service.
        /// </returns>
        public async Task<HttpResult<IReadOnlyList<ActorProfile>?>> GetActorProfiles(AtIdentifier[] actors, Uri service, CancellationToken cancellationToken = default)
        {
            string? accessJwt = null;

            if (Session == null || string.IsNullOrEmpty(Session.AccessJwt))
            {
                if (service == DefaultServices.DefaultService)
                {
                    if (service == DefaultServices.DefaultService)
                    {
                        service = DefaultServices.ReadOnlyService;
                    }
                }
            }
            else
            {
                accessJwt = Session.AccessJwt;
            }

            return await BlueskyServer.GetProfiles(actors, service, accessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves a list of suggested actors for the current user.
        /// </summary>
        /// <param name="limit">The maximum number of entries to retrieve.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A view of the suggested actors, and a cursor, from the <paramref name="service"/>.</returns>
        public async Task<HttpResult<ActorSuggestions>> GetSuggestions(int? limit = 50, string? cursor = null, CancellationToken cancellationToken = default)
        {
            return await GetSuggestions(DefaultService, limit, cursor, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves a list of suggested actors for the current user.
        /// </summary>
        /// <param name="service">The service to retrieve the timeline from.</param>
        /// <param name="limit">The maximum number of entries to retrieve.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A view of the suggested actors, and a cursor, from the <paramref name="service"/>.</returns>
        public async Task<HttpResult<ActorSuggestions>> GetSuggestions(Uri service, int? limit = 50, string? cursor = null, CancellationToken cancellationToken = default)
        {
            if (Session == null || string.IsNullOrEmpty(Session.AccessJwt))
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.GetSuggestions(service, limit , cursor, Session.AccessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Find actors matching the specified search criteria.
        /// </summary>
        /// <param name="service">The service to retrieve the timeline from.</param>
        /// <param name="q">Search query string. Syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
        /// <param name="limit">The maximum number of entries to retrieve.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public async Task<HttpResult<ActorSearchResults>> SearchActors(string q, int? limit = 50, string? cursor = null, CancellationToken cancellationToken = default)
        {
            Uri service = DefaultService;

            if (Session == null || string.IsNullOrEmpty(Session.AccessJwt))
            {
                service = DefaultUnauthenticatedService;
            }

            return await SearchActors(service, q, limit, cursor, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Find actors matching the specified search criteria.
        /// </summary>
        /// <param name="service">The service to retrieve the timeline from.</param>
        /// <param name="q">Search query string. Syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
        /// <param name="limit">The maximum number of entries to retrieve.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public async Task<HttpResult<ActorSearchResults>> SearchActors(Uri service, string q, int? limit = 50, string? cursor = null, CancellationToken cancellationToken = default)
        {
            string? accessToken = null;
            if (Session is not null)
            {
                accessToken = Session.AccessJwt;
            }    

            return await BlueskyServer.SearchActors(service, q, limit, cursor, accessToken, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new post on Bluesky with the specified <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The text the post should contain.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> a <see cref="RecordResponse"/> from the specified <paramref name="service"/>,
        /// or any error details returned by the service.
        /// </returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown of the current <see cref="Session"/> is not authenticated.</exception>
        public async Task<HttpResult<StrongReference>> CreatePost(string text, CancellationToken cancellationToken = default)
        {
            return await CreatePost(text, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new post on Bluesky with the specified <paramref name="text"/>.
        /// </summary>
        /// <param name="post">The <see cref="Bluesky.Post"/> to create.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> a <see cref="StrongReference"/> from the specified <paramref name="service"/>,
        /// or any error details returned by the service.
        /// </returns>
        public async Task<HttpResult<StrongReference>> CreatePost(Bluesky.Post post, CancellationToken cancellationToken = default)
        {
            return await CreatePost(post, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new post on the specified Bluesky <paramref name="service"/> with the specified <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The text the post should contain.</param>
        /// <param name="service">The service to query.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> a <see cref="StrongReference"/> from the specified <paramref name="service"/>,
        /// or any error details returned by the service.
        /// </returns>
        public async Task<HttpResult<StrongReference>> CreatePost(string text, Uri service, CancellationToken cancellationToken = default)
        {
            return await CreatePost(new Bluesky.Post(text), service, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new post on the specified Bluesky <paramref name="service"/> with the specified <paramref name="post"/>.
        /// </summary>
        /// <param name="post">The post to create.</param>
        /// <param name="service">The service to query.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> a <see cref="StrongReference"/> from the specified <paramref name="service"/>,
        /// or any error details returned by the service.
        /// </returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current <see cref="Session"/> is not authenticated.</exception>
        public async Task<HttpResult<StrongReference>> CreatePost(Bluesky.Post post, Uri service, CancellationToken cancellationToken = default)
        {
            if (Session == null || string.IsNullOrEmpty(Session.AccessJwt))
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.CreatePost(post, Session.Did, service, Session.AccessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves a view of the requesting account's home timeline.
        /// </summary>
        /// <param name="limit">The maximum number of entries to retrieve.Defaults to 50 if not specified.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <returns>A view of the requesting account's home timeline from the <paramref name="service"/>.</returns>
        public async Task<HttpResult<Timeline>> GetTimeline(int limit = 50, string? cursor = null, CancellationToken cancellationToken = default)
        {
            return await GetTimeline(DefaultService, null, limit, cursor, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves a view of the requesting account's home timeline.
        /// </summary>
        /// <param name="algorithm">Variant 'algorithm' for timeline. Implementation-specific.</param>
        /// <param name="limit">The maximum number of entries to retrieve.Defaults to 50 if not specified.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <returns>A view of the requesting account's home timeline from the <paramref name="service"/>.</returns>
        public async Task<HttpResult<Timeline>> GetTimeline(string? algorithm, int limit = 50, string? cursor = null, CancellationToken cancellationToken = default)
        {
            return await GetTimeline(DefaultService, algorithm, limit, cursor, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves a view of the requesting account's home timeline.
        /// </summary>
        /// <param name="service">The service to retrieve the timeline from.</param>
        /// <param name="algorithm">Variant 'algorithm' for timeline. Implementation-specific.</param>
        /// <param name="limit">The maximum number of entries to retrieve.Defaults to 50 if not specified.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A view of the requesting account's home timeline from the <paramref name="service"/>.</returns>
        public async Task<HttpResult<Timeline>> GetTimeline(Uri service, string? algorithm, int limit = 50, string? cursor = null, CancellationToken cancellationToken = default)
        {
            if (Session == null || string.IsNullOrEmpty(Session.AccessJwt))
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.GetTimeline(service, algorithm, limit, cursor, Session.AccessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Find posts matching the specified search criteria.
        /// </summary>
        /// <param name="service">The service to search on.</param>
        /// <param name="q">Search query string. Syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
        /// <param name="limit">The maximum number of entries to retrieve.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// A view of any posts matching the search criteria, the number of which are limited by the <paramref name="limit"/>,
        /// the total number of matching posts and a cursor for pagination.
        /// </returns>
        public async Task<HttpResult<PostSearchResults>> SearchPosts(string q, int limit = 25, string? cursor = null, CancellationToken cancellationToken = default)
        {
            return await SearchPosts(DefaultService, q, limit, cursor, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Find posts matching the specified search criteria.
        /// </summary>
        /// <param name="service">The service to search on.</param>
        /// <param name="q">Search query string. Syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
        /// <param name="limit">The maximum number of entries to retrieve.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// A view of any posts matching the search criteria, the number of which are limited by the <paramref name="limit"/>,
        /// the total number of matching posts and a cursor for pagination.
        /// </returns>
        public async Task<HttpResult<PostSearchResults>> SearchPosts(Uri service, string q, int limit = 25, string? cursor = null, CancellationToken cancellationToken = default)
        {
            if (Session == null || string.IsNullOrEmpty(Session.AccessJwt))
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.SearchPosts(service, q, limit, cursor, Session.AccessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes a record, identified by the repo, collection and rkey from the specified service.</summary>
        /// <param name="subject">The collection of the record to delete.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> DeletePost(
            StrongReference subject,
            AtCid? swapRecord = null,
            AtCid? swapCommit = null,
            CancellationToken cancellationToken = default)
        {
            return await DeletePost(subject, swapRecord, swapCommit, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes a record, identified by the repo, collection and rkey from the specified service.</summary>
        /// <param name="subject">The collection of the record to delete.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="service">The service to delete the record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> DeletePost(
            StrongReference subject,
            AtCid? swapRecord,
            AtCid? swapCommit,
            Uri service,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(subject);

            if (subject.Uri is null || subject.Uri.Repo is null || subject.Uri.RKey is null)
            {
                throw new ArgumentException($"Cannot extract repo and/or rkey from {nameof(subject)}", nameof(subject));
            }

            return await DeletePost(subject.Uri.Repo, subject.Uri.RKey, swapRecord, swapCommit, service, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes a record, identified by the repo, collection and rkey from the specified service.</summary>
        /// <param name="rkey">The record key, identifying the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> DeletePost(
            string rkey,
            AtCid? swapRecord = null,
            AtCid? swapCommit = null,
            CancellationToken cancellationToken = default)
        {
            if (Session == null || Session.Did is null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await DeletePost(Session.Did, rkey, swapRecord, swapCommit,  cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes a record, identified by the repo, collection and rkey from the specified service.</summary>
        /// <param name="repo">The handle or Did of the repo to delete from. Typically this is the Did of the account that created the record.</param>
        /// <param name="rkey">The record key, identifying the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> DeletePost(
            AtIdentifier repo,
            string rkey,
            AtCid? swapRecord = null,
            AtCid? swapCommit = null,
            CancellationToken cancellationToken = default)
        {
            return await DeletePost(repo, rkey, swapRecord, swapCommit, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes a record, identified by the repo, collection and rkey from the specified service.</summary>
        /// <param name="repo">The handle or Did of the repo to delete from. Typically this is the Did of the account that created the record.</param>
        /// <param name="rkey">The record key, identifying the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="service">The service to delete the record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> DeletePost(
            AtIdentifier repo,
            string rkey,
            AtCid? swapRecord,
            AtCid? swapCommit,
            Uri service,
            CancellationToken cancellationToken = default)
        {
            return await DeleteRecord(repo, CollectionType.Post,rkey, swapRecord, swapCommit, service, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a like record against the record specified by the <paramref name="subject"/>.
        /// </summary>
        /// <param name="subject">The subject of the record to like.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="StrongReference"/> for the new like record.</returns>
        public async Task<HttpResult<StrongReference>> LikePost(StrongReference subject, CancellationToken cancellationToken = default)
        {
            return await LikePost(subject, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a like record against the record specified by the <paramref name="subject"/>.
        /// </summary>
        /// <param name="subject">The subject of the record to like.</param>
        /// <param name="service">The service to create the like record on.</param>
        /// <param name="accessToken">An access token for the specified service.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="StrongReference"/> for the new like record.</returns>
        public async Task<HttpResult<StrongReference>> LikePost(StrongReference subject, Uri service, CancellationToken cancellationToken = default)
        {
            if (Session == null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.LikePost(subject, Session.Did, service, Session.AccessJwt, HttpClientHandler,cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the like record specified by the <paramref name="subject"/>.
        /// </summary>
        /// <param name="subject">The subject of the like record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> UndoLike(StrongReference subject, CancellationToken cancellationToken = default)
        {
            return await UndoLike(subject, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the like record specified by the <paramref name="subject"/>.
        /// </summary>
        /// <param name="subject">The subject of the like record to delete.</param>
        /// <param name="service">The service to delete the like record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> UndoLike(StrongReference subject, Uri service, CancellationToken cancellationToken = default)
        {
            if (Session == null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            ArgumentNullException.ThrowIfNull(subject);

            if (subject.Uri is null || subject.Uri.Repo is null || subject.Uri.RKey is null)
            {
                throw new ArgumentException($"Cannot extract repo and/or rkey from {nameof(subject)}", nameof(subject));
            }

            return await UndoLike(Session.Did, subject.Uri.RKey, service, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the like record specified by the <paramref name="rkey"/> from the current user's repo.
        /// </summary>
        /// <param name="rkey">The rkey of the like record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> UndoLike(string rkey, CancellationToken cancellationToken = default)
        {
            if (Session == null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await UndoLike(Session.Did, rkey, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the like record specified by the <paramref name="rkey"/> from the specified <paramref name="repo"/>.
        /// </summary>
        /// <param name="repo">The repo to delete the like record from. This is typically the current user's DID.</param>
        /// <param name="rkey">The rkey of the like record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> UndoLike(AtIdentifier repo, string rkey, CancellationToken cancellationToken = default)
        {
            return await UndoLike(repo, rkey, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the like record specified by the <paramref name="rkey"/> from the specified <paramref name="repo"/>.
        /// </summary>
        /// <param name="repo">The repo to delete the like record from. This is typically the current user's DID.</param>
        /// <param name="rkey">The rkey of the like record to delete.</param>
        /// <param name="service">The service to delete the like record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public async Task<HttpResult<bool>> UndoLike(AtIdentifier repo, string rkey, Uri service, CancellationToken cancellationToken = default)
        {
            return await DeleteRecord(repo, CollectionType.Like, rkey, null, null, service, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reposts the post record specified by the <paramref name="subject"/>.
        /// </summary>
        /// <param name="subject">The subject of the post record to repost.</param>
        /// <param name="accessToken">An access token for the specified service.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public async Task<HttpResult<StrongReference>> Repost(StrongReference subject, CancellationToken cancellationToken = default)
        {
            if (Session == null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await Repost(subject, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reposts the post record specified by the <paramref name="subject"/>.
        /// </summary>
        /// <param name="subject">The subject of the post record to repost.</param>
        /// <param name="service">The service to create the repost record on.</param>
        /// <param name="accessToken">An access token for the specified service.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public async Task<HttpResult<StrongReference>> Repost(StrongReference subject, Uri service, CancellationToken cancellationToken = default)
        {
            if (Session == null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.Repost(subject, Session.Did, service, Session.AccessJwt, HttpClientHandler,cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the repost record specified by the <paramref name="subject"/>.
        /// </summary>
        /// <param name="subject">The subject of the repost record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> UndoRepost(StrongReference subject, CancellationToken cancellationToken = default)
        {
            return await UndoRepost(subject, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the repost record specified by the <paramref name="subject"/>.
        /// </summary>
        /// <param name="subject">The subject of the repost record to delete.</param>
        /// <param name="service">The service to delete the repost record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> UndoRepost(StrongReference subject, Uri service, CancellationToken cancellationToken = default)
        {
            if (Session == null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            ArgumentNullException.ThrowIfNull(subject);

            if (subject.Uri is null || subject.Uri.Repo is null || subject.Uri.RKey is null)
            {
                throw new ArgumentException($"Cannot extract repo and/or rkey from {nameof(subject)}", nameof(subject));
            }

            return await UndoRepost(Session.Did, subject.Uri.RKey, service, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the repost record specified by the <paramref name="rkey"/> from the current user's repo.
        /// </summary>
        /// <param name="rkey">The rkey of the repost record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> UndoRepost(string rkey, CancellationToken cancellationToken = default)
        {
            if (Session == null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await UndoRepost(Session.Did, rkey, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the repost record specified by the <paramref name="rkey"/> from the specified <paramref name="repo"/>.
        /// </summary>
        /// <param name="repo">The repo to delete the repost record from. This is typically the current user's DID.</param>
        /// <param name="rkey">The rkey of the repost record to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> UndoRepost(AtIdentifier repo, string rkey, CancellationToken cancellationToken = default)
        {
            return await UndoRepost(repo, rkey, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes the repost record specified by the <paramref name="rkey"/> from the specified <paramref name="repo"/>.
        /// </summary>
        /// <param name="repo">The repo to delete the repost record from. This is typically the current user's DID.</param>
        /// <param name="rkey">The rkey of the repost record to delete.</param>
        /// <param name="service">The service to delete the repost record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public async Task<HttpResult<bool>> UndoRepost(AtIdentifier repo, string rkey, Uri service, CancellationToken cancellationToken = default)
        {
            return await DeleteRecord(repo, CollectionType.Repost, rkey, null, null, service, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a post views for a specified post <see cref="AtUri">s. This is sometimes referred to as 'hydrating' a post.
        /// </summary>
        /// <param name="atUri">The post <see cref="AtUri"/> to retrieve</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A readonly collection of any posts matching the specified AT URIs.</returns>
        public async Task<HttpResult<FeedPost>> GetPost(AtUri atUri, CancellationToken cancellationToken = default)
        {
            return await GetPost(atUri, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a post views for a specified post <see cref="AtUri">s. This is sometimes referred to as 'hydrating' a post.
        /// </summary>
        /// <param name="atUri">The post <see cref="AtUri"/> to retrieve</param>
        /// <param name="service">The service to search on.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A readonly collection of any posts matching the specified AT URIs.</returns>
        public async Task<HttpResult<FeedPost>> GetPost(AtUri atUri , Uri service, CancellationToken cancellationToken = default)
        {
            HttpResult<IReadOnlyCollection<FeedPost>> getPostsResult =
                await GetPosts(new List<AtUri>() { atUri }, service, cancellationToken).ConfigureAwait(false);

            HttpResult<FeedPost> result = new()
            {
                StatusCode = getPostsResult.StatusCode,
                Error = getPostsResult.Error
            };

            if (getPostsResult.Result is not null && getPostsResult.Result.Count != 0)
            {
                result.Result = getPostsResult.Result.First();
            }

            return result;
        }

        /// <summary>
        /// Gets post views for a specified list of <see cref="AtUri">s. This is sometimes referred to as 'hydrating' a 'feed skeleton'.
        /// </summary>
        /// <param name="q">The post <see cref="AtUri"/>s to retrieve</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A readonly collection of any posts matching the specified AT URIs.</returns>
        public async Task<HttpResult<IReadOnlyCollection<FeedPost>>> GetPosts(IEnumerable<AtUri> q, CancellationToken cancellationToken = default)
        {
            return await GetPosts(q, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets post views for a specified list of <see cref="AtUri">s. This is sometimes referred to as 'hydrating' a 'feed skeleton'.
        /// </summary>
        /// <param name="q">The post <see cref="AtUri"/>s to retrieve</param>
        /// <param name="service">The service to search on.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A readonly collection of any posts matching the specified AT URIs.</returns>
        public async Task<HttpResult<IReadOnlyCollection<FeedPost>>> GetPosts(IEnumerable<AtUri> q, Uri service, CancellationToken cancellationToken = default)
        {
            string? accessJwt = null;

            if (Session == null || string.IsNullOrEmpty(Session.AccessJwt))
            {
                if (service == DefaultServices.DefaultService)
                {
                    if (service == DefaultServices.DefaultService)
                    {
                        service = DefaultServices.ReadOnlyService;
                    }
                }
            }
            else
            {
                accessJwt = Session.AccessJwt;
            }

            return await BlueskyServer.GetPosts(service, q, accessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get posts in a thread. Does not require auth, but additional metadata and filtering will be applied for authenticated requests.
        /// </summary>
        /// <param name="atUri">The <see cref="AtUri"/> of the post those thread to get.</param>
        /// <param name="depth">How many levels of reply depth should be included in response.</param>
        /// <param name="parentHeight">How many levels of parent (and grandparent, etc) post to include.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="ThreadView"/> for the specified <paramref name="atUri"/>.</returns>
        public async Task<HttpResult<ThreadView>> GetPostThread(AtUri atUri, int? depth = null, int? parentHeight = null, CancellationToken cancellationToken = default)
        {
            return await GetPostThread(atUri, DefaultService, depth, parentHeight, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get posts in a thread. Does not require auth, but additional metadata and filtering will be applied for authenticated requests.
        /// </summary>
        /// <param name="atUri">The <see cref="AtUri"/> of the post those thread to get.</param>
        /// <param name="service">The service to search on.</param>
        /// <param name="depth">How many levels of reply depth should be included in response.</param>
        /// <param name="parentHeight">How many levels of parent (and grandparent, etc) post to include.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="ThreadView"/> for the specified <paramref name="atUri"/>.</returns>
        public async Task<HttpResult<ThreadView>> GetPostThread(AtUri atUri, Uri service, int? depth = null, int? parentHeight = null, CancellationToken cancellationToken = default)
        {
            string? accessJwt = null;

            if (Session is not null)
            {
                accessJwt = Session.AccessJwt;
            }
            else
            {
                if (service == DefaultServices.DefaultService)
                {
                    if (service == DefaultServices.DefaultService)
                    {
                        service = DefaultServices.ReadOnlyService;
                    }
                }
            }

            return await BlueskyServer.GetPostThread(service, atUri, depth, parentHeight, accessJwt, HttpClientHandler,cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a count the number of unread notifications for the requesting account.
        /// </summary>
        /// <param name="service">The service to retrieve the unread count from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping an integer indicating the unread notification count.</returns>
        public async Task<HttpResult<int>> GetUnreadCount(Uri service, CancellationToken cancellationToken = default)
        {
            return await GetUnreadCount(null, service, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a count the number of unread notifications for the requesting account.
        /// </summary>
        /// <param name="seenAt">An optional <see cref="DateTimeOffset"/> indicating when notifications were last checked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping an integer indicating the unread notification count.</returns>
        public async Task<HttpResult<int>> GetUnreadCount(DateTimeOffset? seenAt = null, CancellationToken cancellationToken = default)
        {
            return await GetUnreadCount(seenAt, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a count the number of unread notifications for the requesting account.
        /// </summary>
        /// <param name="seenAt">An optional <see cref="DateTimeOffset"/> indicating when notifications were last checked.</param>
        /// <param name="service">The service to retrieve the unread count from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping an integer indicating the unread notification count.</returns>
        public async Task<HttpResult<int>> GetUnreadCount(DateTimeOffset? seenAt, Uri service, CancellationToken cancellationToken = default)
        {
            if (Session == null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.GetUnreadCount(seenAt, service, Session.AccessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the notifications for the requesting account.
        /// </summary>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="seenAt">The date and time notifications were last checked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping a view over the notifications for the account.</returns>
        public async Task<HttpResult<NotificationsView>> ListNotifications(
            string? cursor = null,
            DateTimeOffset? seenAt = null,
            CancellationToken cancellationToken = default)
        {
            return await ListNotifications(null, cursor, seenAt, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the notifications for the requesting account.
        /// </summary>
        /// <param name="limit">The maximum number of notifications to return. If specified this should be >=1 and <= 100.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="seenAt">The date and time notifications were last checked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping a view over the notifications for the account.</returns>
        public async Task<HttpResult<NotificationsView>> ListNotifications(
            int? limit,
            string? cursor,
            DateTimeOffset? seenAt,
            CancellationToken cancellationToken = default)
        {
            return await ListNotifications(limit, cursor, seenAt, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the notifications for the requesting account.
        /// </summary>
        /// <param name="limit">The maximum number of notifications to return. If specified this should be >=1 and <= 100.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="seenAt">The date and time notifications were last checked.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping a view over the notifications for the account.</returns>
        public async Task<HttpResult<NotificationsView>> ListNotifications(
            int? limit,
            string? cursor,
            DateTimeOffset? seenAt,
            Uri service,
            CancellationToken cancellationToken = default)
        {
            if (Session == null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.ListNotifications(limit, cursor, seenAt, service, Session.AccessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates the date and time notifications were last seen for the current user.
        /// </summary>
        /// <param name="seenAt">An optional <see cref="DateTimeOffset"/> indicating when notifications were last checked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping an integer indicating the unread notification count.</returns>
        public async Task<HttpResult<EmptyResponse>> UpdateSeen(DateTimeOffset seenAt, CancellationToken cancellationToken = default)
        {

            return await UpdateSeen(seenAt, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates the date and time notifications were last seen on the specified <paramref name="service"/> for the current user.
        /// </summary>
        /// <param name="seenAt">An optional <see cref="DateTimeOffset"/> indicating when notifications were last checked.</param>
        /// <param name="service">The service to retrieve the unread count from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping an integer indicating the unread notification count.</returns>
        public async Task<HttpResult<EmptyResponse>> UpdateSeen(DateTimeOffset seenAt, Uri service, CancellationToken cancellationToken = default)
        {
            if (Session == null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.UpdateSeen(seenAt, service, Session.AccessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Generates an AT URI from the specified Uri.
        /// This Uri is very dependent on the Bluesky web client and its format is subject to change.
        /// </summary>
        /// <param name="uri">A URI from the Bluesky web client.</param>
        /// <returns>An AT URI corresponding to the resource the Bluesky web client URI.</returns>
        /// <exception cref="ArgumentException">Thrown if the provided URI is in not in the expected format.</exception>
        /// <remarks>
        /// This method makes outgoing web requests to resolve the handle in a Bluesky URI to a <see cref="DID"/>.
        /// </remarks>
        public async Task<AtUri> BuildAtUriFromBlueskyWebUri(Uri uri)
        {
            // Bluesky web client URIs should be in the format
            // https://bsky.app/profile/<handle>/post/<rkey>/

            ArgumentNullException.ThrowIfNull(uri);

            if (uri.Scheme != "https")
            {
                throw new ArgumentException("Scheme is not https.", nameof(uri));
            }

            if (uri.HostNameType != UriHostNameType.Dns || uri.Host != "bsky.app")
            {
                throw new ArgumentException($"{uri.Host} is not a known Bluesky web host.", nameof(uri));
            }

            string[] pathComponents = uri.AbsolutePath.Split('/');

            if (pathComponents.Length != 5)
            {
                throw new ArgumentException($"{uri.AbsolutePath} does not have four components.", nameof(uri));
            }

            if (pathComponents[1] != "profile")
            {
                throw new ArgumentException($"{uri.AbsolutePath} is not a profile path.", nameof(uri));
            }

            if (pathComponents[3] != "post")
            {
                throw new ArgumentException($"{uri.AbsolutePath} is not a post path.", nameof(uri));
            }

            if (!Handle.TryParse(pathComponents[2], out Handle? handle) || handle is null)
            {
                throw new ArgumentException($"{pathComponents[1]} is not a valid handle.", nameof(uri));
            }

            HttpResult<Did> resolutionResult = await ResolveHandle(handle.ToString(), DefaultServices.ReadOnlyService).ConfigureAwait(false);

            if (resolutionResult.Result is null || !resolutionResult.Succeeded)
            {
                throw new ArgumentException($"Handle resolution did not succeed {resolutionResult.StatusCode}.", nameof(uri));
            }

            Did? did = resolutionResult.Result;
            string rkey = pathComponents[4];

            string rebuiltAtUri = $"at://{did}/app.bsky.feed.post/{rkey}";

            bool parseResult = AtUri.TryParse(rebuiltAtUri, out AtUri? atUri);

            if (!parseResult || atUri is null)
            {
                throw new ArgumentException($"AtUri could not be created from {rebuiltAtUri}.", nameof(uri));
            }

            return atUri;
        }
    }
}
