// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Gets a description for the feed generator at <paramref name="generatorUri"/>.
        /// </summary>
        /// <param name="generatorUri">The <see cref="Uri"/> of the generator whose description should be retrieved.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="generatorUri"/> is null.</exception>
        public async Task<AtProtoHttpResult<FeedGeneratorDescription>> GetFeedGeneratorDescription(
            Uri generatorUri,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(generatorUri);

            return await BlueskyServer.GetFeedGeneratorDescription(
                generatorUri,
                httpClient: HttpClient,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a paginated list of feeds (feed generator records) created by the actor, in the actor's repo.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose feeds should be listed.</param>
        /// <param name="limit">The maximum number of feeds to return from the api.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is null.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<GeneratorView>>> GetActorFeeds(
            AtIdentifier actor,
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            return await BlueskyServer.GetActorFeeds(
                actor,
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
        /// Get a list of posts liked the current actor. Requires authentication.
        /// </summary>
        /// <param name="limit">The maximum number of feeds to return from the api.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>> GetActorLikes(
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.GetActorLikes(
                Did,
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
        /// Get a view of an actor's 'author feed' (post and reposts by the author).
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose feed should be retrieved.</param>
        /// <param name="limit">The maximum number of feeds to return from the api.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="filter">Combinations of post/repost types to include in the results.</param>
        /// <param name="includePins">Flag indicating whether to include pinned posts in the results.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is null.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>> GetAuthorFeed(
            AtIdentifier actor,
            int? limit = null,
            string? cursor = null,
            FeedFilter? filter = null,
            bool? includePins = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(actor);

            return await BlueskyServer.GetAuthorFeed(
                actor,
                limit,
                cursor,
                filter,
                includePins,
                AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets information about the specified <paramref name="feed"/> generator.
        /// </summary>
        /// <param name="feed">The <see cref="AtUri"/> of the feed generator whose information should be retrieved.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="feed"/> is null.</exception>
        public async Task<AtProtoHttpResult<FeedGenerator>> GetFeedGenerator(
            AtUri feed,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(feed);

            return await BlueskyServer.GetFeedGenerator(
                feed,
                AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets information about the specified feed generators.
        /// </summary>
        /// <param name="feeds">The <see cref="AtUri"/> of the feed generators whose information should be retrieved.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="feeds"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="feeds"/> is empty.</exception>
        public async Task<AtProtoHttpResult<IReadOnlyCollection<GeneratorView>>> GetFeedGenerators(
            IEnumerable<AtUri> feeds,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(feeds);
            ArgumentOutOfRangeException.ThrowIfZero(feeds.Count());

            return await BlueskyServer.GetFeedGenerators(
                feeds,
                AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a hydrated feed from an actor's selected feed generator.
        /// </summary>
        /// <param name="feed">The <see cref="AtIdentifier"/> of the feed to be retrieved.</param>
        /// <param name="limit">The maximum number of items to return from the api.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="feed"/> is null.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>> GetFeed(
            AtUri feed,
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(feed);

            return await BlueskyServer.GetFeed(
                feed,
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
        /// Get like records which reference a subject, by <paramref name="uri"/> and, optionally, <paramref name="cid"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the subject whose likes should be retrieved.</param>
        /// <param name="cid">The <see cref="Cid"/> of the subject whose likes should be retrieved.</param>
        /// <param name="limit">The maximum number of items to return from the api.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>  is null.</exception>
        public async Task<AtProtoHttpResult<Likes>> GetLikes(
            AtUri uri,
            Cid? cid = null,
            int? limit = null,
            string? cursor = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            return await BlueskyServer.GetLikes(
                uri,
                cid,
                limit,
                cursor,
                AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a feed of recent posts from a list (posts and reposts from any actors on the list)
        /// </summary>
        /// <param name="list">The <see cref="AtIdentifier"/> of the list whose posts should be retrieved.</param>
        /// <param name="limit">The maximum number of items to return from the api.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="list"/> is null.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>> GetListFeed(
            AtUri list,
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(list);

            return await BlueskyServer.GetListFeed(
                list,
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
        /// Get posts in a thread. Does not require authentication, but additional metadata and filtering will be applied for authenticated requests.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the post record whose thread should be retrieved.</param>
        /// <param name="depth">How many levels of reply depth should be included in response.</param>
        /// <param name="parentHeight">How many levels of parent (and grandparent, etc) post to include.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        public async Task<AtProtoHttpResult<PostThread>> GetPostThread(
            AtUri uri,
            int? depth,
            int? parentHeight,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            return await BlueskyServer.GetPostThread(
                uri,
                depth,
                parentHeight,
                AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken : cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the <see cref="FeedViewPost"/>s for a specified list of posts (by <see cref="AtUri"/>).
        /// </summary>
        /// <param name="uris">List of post <see cref="AtUri" /> to return views for.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uris"/> is null.</exception>
        public async Task<AtProtoHttpResult<IReadOnlyCollection<PostView>>> GetPosts(
            IEnumerable<AtUri> uris,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uris);

            return await BlueskyServer.GetPosts(
                uris,
                AuthenticatedOrUnauthenticatedServiceUri,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the <see cref="PostView"/> for the specified <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="strongReference">The <see cref="StrongReference" /> of the post to return the <see cref="PostView"/> for.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        public async Task<AtProtoHttpResult<PostView>> GetPostView(
            StrongReference strongReference,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            return await GetPostView(strongReference.Uri, subscribedLabelers: subscribedLabelers, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the <see cref="PostView"/> for the specified <see cref="StrongReference"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri" /> of the post to return the <see cref="PostView"/> for.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        public async Task<AtProtoHttpResult<PostView>> GetPostView(
            AtUri uri,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            AtProtoHttpResult<IReadOnlyCollection<PostView>> postViewsResult =
                await GetPosts(
                    [uri],
                    subscribedLabelers: subscribedLabelers,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

            if (postViewsResult.Succeeded)
            {
                if (postViewsResult.Result.Count != 0)
                {
                    return new AtProtoHttpResult<PostView>(
                        postViewsResult.Result.ElementAt(0),
                        postViewsResult.StatusCode,
                        postViewsResult.HttpResponseHeaders,
                        postViewsResult.AtErrorDetail,
                        postViewsResult.RateLimit);
                }
                else
                {
                    return new AtProtoHttpResult<PostView>(
                        null,
                        postViewsResult.StatusCode,
                        postViewsResult.HttpResponseHeaders,
                        postViewsResult.AtErrorDetail,
                        postViewsResult.RateLimit);
                }
            }
            else
            {
                return new AtProtoHttpResult<PostView>(
                    null,
                    postViewsResult.StatusCode,
                    postViewsResult.HttpResponseHeaders,
                    postViewsResult.AtErrorDetail,
                    postViewsResult.RateLimit);
            }
        }

        /// <summary>
        /// Gets a strong reference to parent post for the specified Bluesky post.
        /// </summary>
        /// <param name="strongReference">The <see cref="StrongReference" /> of the post to return the parent <paramref name="strongReference"/> for.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        public async Task<AtProtoHttpResult<StrongReference>> GetPostParent(
            StrongReference strongReference,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            AtProtoHttpResult<Record.PostRecord> getPostResult = await GetPostRecord(strongReference, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (getPostResult.Succeeded)
            {
                if (getPostResult.Result.Value.Reply == null)
                {
                    // The post has no reply reference, so it is not part of a thread, and its parent is itself.
                    return new AtProtoHttpResult<StrongReference>(
                        getPostResult.Result.StrongReference,
                        getPostResult.StatusCode,
                        getPostResult.HttpResponseHeaders,
                        getPostResult.AtErrorDetail,
                        getPostResult.RateLimit);

                }
                else
                {
                    return new AtProtoHttpResult<StrongReference>(
                        getPostResult.Result.Value.Reply.Parent,
                        getPostResult.StatusCode,
                        getPostResult.HttpResponseHeaders,
                        getPostResult.AtErrorDetail,
                        getPostResult.RateLimit);
                }
            }
            else
            {
                return new AtProtoHttpResult<StrongReference>(
                    null,
                    getPostResult.StatusCode,
                    getPostResult.HttpResponseHeaders,
                    getPostResult.AtErrorDetail,
                    getPostResult.RateLimit);
            }
        }

        /// <summary>
        /// Gets a strong reference to root post for the specified Bluesky post.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the post whose root <see cref="StrongReference"/> should be retrieved.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        public async Task<AtProtoHttpResult<StrongReference>> GetPostRoot(
            StrongReference strongReference,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            AtProtoHttpResult<Record.PostRecord> getPostResult = await GetPostRecord(strongReference, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (getPostResult.Succeeded)
            {
                if (getPostResult.Result.Value.Reply == null)
                {
                    // The post has no reply reference, so it is not part of a thread, and its root is itself.
                    return new AtProtoHttpResult<StrongReference>(
                        getPostResult.Result.StrongReference,
                        getPostResult.StatusCode,
                        getPostResult.HttpResponseHeaders,
                        getPostResult.AtErrorDetail,
                        getPostResult.RateLimit);

                }
                else
                {
                    return new AtProtoHttpResult<StrongReference>(
                        getPostResult.Result.Value.Reply.Root,
                        getPostResult.StatusCode,
                        getPostResult.HttpResponseHeaders,
                        getPostResult.AtErrorDetail,
                        getPostResult.RateLimit);
                }
            }
            else
            {
                return new AtProtoHttpResult<StrongReference>(
                    null,
                    getPostResult.StatusCode,
                    getPostResult.HttpResponseHeaders,
                    getPostResult.AtErrorDetail,
                    getPostResult.RateLimit);
            }
        }

        /// <summary>
        /// Gets the <see cref="ReplyReferences"/> for the specified Bluesky post, suitable for using when making a reply post.
        /// </summary>
        /// <param name="strongReference">A <see cref="StrongReference"/> to the post whose <see cref="ReplyReferences"/> should be retrieved.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null.</exception>
        public async Task<AtProtoHttpResult<ReplyReferences>> GetReplyReferences(
            StrongReference strongReference,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            AtProtoHttpResult<Record.PostRecord> getPostRecordResult = await GetPostRecord(strongReference, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (getPostRecordResult.Succeeded)
            {
                if (getPostRecordResult.Result.Value.Reply is null)
                {
                    return new AtProtoHttpResult<ReplyReferences>(
                        new ReplyReferences(getPostRecordResult.Result.StrongReference, getPostRecordResult.Result.StrongReference),
                        getPostRecordResult.StatusCode,
                        getPostRecordResult.HttpResponseHeaders,
                        getPostRecordResult.AtErrorDetail,
                        getPostRecordResult.RateLimit);
                }
                else
                {
                    return new AtProtoHttpResult<ReplyReferences>(
                        new ReplyReferences(getPostRecordResult.Result.StrongReference, getPostRecordResult.Result.Value.Reply.Root),
                        getPostRecordResult.StatusCode,
                        getPostRecordResult.HttpResponseHeaders,
                        getPostRecordResult.AtErrorDetail,
                        getPostRecordResult.RateLimit);
                }
            }
            else
            {
                return new AtProtoHttpResult<ReplyReferences>(
                    null,
                    getPostRecordResult.StatusCode,
                    getPostRecordResult.HttpResponseHeaders,
                    getPostRecordResult.AtErrorDetail,
                    getPostRecordResult.RateLimit);
            }
        }

        /// <summary>
        /// Get a list of quotes for a given post.
        /// </summary>
        /// <param name="uri"><see cref="AtUri"/> of post record whose quotes should be retrieved.</param>
        /// <param name="cid">If supplied, filters to quotes of specific version (by <see cref="AtProto.Cid">content identifier</see>) of the post record.</param>
        /// <param name="limit">The maximum number of posts to return.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri" /> is null.</exception>
        public async Task<AtProtoHttpResult<QuotesCollection>> GetQuotes(
            AtUri uri,
            Cid? cid = null,
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            return await BlueskyServer.GetQuotes(
                uri,
                cid,
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
        /// Get a list of profiles who reposted the specified post.
        /// </summary>
        /// <param name="uri"><see cref="AtUri"/> of post record whose repost authors should be retrieved.</param>
        /// <param name="cid">If supplied, filters to quotes of specific version (by <see cref="Cid">content identifier</see>) of the post record.</param>
        /// <param name="limit">The maximum number of posts to return.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        public async Task<AtProtoHttpResult<RepostedBy>> GetRepostedBy(
            AtUri uri,
            Cid? cid = null,
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            return await BlueskyServer.GetRepostedBy(
                uri,
                cid,
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
        /// Get a list of suggested feeds for the current user
        /// </summary>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<SuggestedFeeds>> GetSuggestedFeeds(
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.GetSuggestedFeeds(
                Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a view of the requesting account's home timeline. This is expected to be some form of reverse-chronological feed.
        /// </summary>
        /// <param name="algorithm">Variant 'algorithm' for timeline. Implementation-specific. NOTE: most feed flexibility has been moved to feed generator mechanism.</param>
        /// <param name="limit">The maximum number of posts to return.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Timeline>> GetTimeline(
            string? algorithm = null,
            int? limit = 50,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.GetTimeline(
                algorithm,
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
        /// Find posts matching search criteria, returning views of those posts
        /// </summary>
        /// <param name="query">Search query string; syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
        /// <param name="searchOrder">Specifies the ranking order of results.</param>
        /// <param name="since">Filter results for posts after the indicated datetime (inclusive). Expected to use 'sortAt' timestamp, which may not match 'createdAt'. Can be a datetime, or just an ISO date (YYYY-MM-DD).</param>
        /// <param name="until">Filter results for posts before the indicated datetime (not inclusive). Expected to use 'sortAt' timestamp, which may not match 'createdAt'. Can be a datetime, or just an ISO date (YYY-MM-DD).</param>
        /// <param name="mentions">Filter to posts which mention the given account. Handles are resolved to DID before query-time. Only matches rich-text facet mentions.</param>
        /// <param name="author">Filter to posts by the given account. Handles are resolved to DID before query-time.</param>
        /// <param name="lang">Filter to posts in the given language. Expected to be based on post language field, though server may override language detection.</param>
        /// <param name="domain">Filter to posts with URLs (facet links or embeds) linking to the given domain (hostname). Server may apply hostname normalization.</param>
        /// <param name="url">Filter to posts with links (facet links or embeds) pointing to this URL. Server may apply URL normalization or fuzzy matching.</param>
        /// <param name="tags">Filter to posts with the given tag (hashtag), based on rich-text facet or tag field. Do not include the hash (#) prefix.</param>
        /// <param name="limit">The maximum number of post views to return</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="query" /> is null or whitespace</exception>
        public async Task<AtProtoHttpResult<SearchResults>> SearchPosts(
            string query,
            SearchOrder? searchOrder = null,
            string? since = null,
            string? until = null,
            AtIdentifier? mentions = null,
            AtIdentifier? author = null,
            string? lang = null,
            string? domain = null,
            Uri? url = null,
            ICollection<string>? tags = null,
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(query);

            return await BlueskyServer.SearchPosts(
                query,
                searchOrder,
                since,
                until,
                mentions,
                author,
                lang,
                domain,
                url,
                tags,
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
        /// Find posts matching search criteria, returning views of those posts
        /// </summary>
        /// <param name="query">Search query string; syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
        /// <param name="searchOrder">Specifies the ranking order of results.</param>
        /// <param name="since">Filter results for posts after the indicated datetime (inclusive). Expected to use 'sortAt' timestamp, which may not match 'createdAt'.</param>
        /// <param name="until">Filter results for posts before the indicated datetime (not inclusive). Expected to use 'sortAt' timestamp, which may not match 'createdAt'.</param>
        /// <param name="mentions">Filter to posts which mention the given account. Handles are resolved to DID before query-time. Only matches rich-text facet mentions.</param>
        /// <param name="author">Filter to posts by the given account. Handles are resolved to DID before query-time.</param>
        /// <param name="lang">Filter to posts in the given language. Expected to be based on post language field, though server may override language detection.</param>
        /// <param name="domain">Filter to posts with URLs (facet links or embeds) linking to the given domain (hostname). Server may apply hostname normalization.</param>
        /// <param name="url">Filter to posts with links (facet links or embeds) pointing to this URL. Server may apply URL normalization or fuzzy matching.</param>
        /// <param name="tags">Filter to posts with the given tag (hashtag), based on rich-text facet or tag field. Do not include the hash (#) prefix.</param>
        /// <param name="limit">The maximum number of post views to return</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="query" /> is null or whitespace</exception>
        public async Task<AtProtoHttpResult<SearchResults>> SearchPosts(
            string query,
            DateTimeOffset since ,
            DateTimeOffset until,
            SearchOrder? searchOrder = null,
            AtIdentifier? mentions = null,
            AtIdentifier? author = null,
            string? lang = null,
            string? domain = null,
            Uri? url = null,
            ICollection<string>? tags = null,
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(query);

            string? sinceSerialized = JsonSerializer.Serialize(since).Replace("\"", string.Empty, StringComparison.InvariantCultureIgnoreCase);
            string? untilSerialized = JsonSerializer.Serialize(until).Replace("\"", string.Empty, StringComparison.InvariantCultureIgnoreCase);

            return await BlueskyServer.SearchPosts(
                query,
                searchOrder,
                sinceSerialized,
                untilSerialized,
                mentions,
                author,
                lang,
                domain,
                url,
                tags,
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
        /// Find posts matching search criteria, returning views of those posts
        /// </summary>
        /// <param name="query">Search query string; syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
        /// <param name="searchOrder">Specifies the ranking order of results.</param>
        /// <param name="since">Filter results for posts after the indicated datetime (inclusive). Expected to use 'sortAt' timestamp, which may not match 'createdAt'.</param>
        /// <param name="until">Filter results for posts before the indicated datetime (not inclusive). Expected to use 'sortAt' timestamp, which may not match 'createdAt'.</param>
        /// <param name="mentions">Filter to posts which mention the given account. Handles are resolved to DID before query-time. Only matches rich-text facet mentions.</param>
        /// <param name="author">Filter to posts by the given account. Handles are resolved to DID before query-time.</param>
        /// <param name="lang">Filter to posts in the given language. Expected to be based on post language field, though server may override language detection.</param>
        /// <param name="domain">Filter to posts with URLs (facet links or embeds) linking to the given domain (hostname). Server may apply hostname normalization.</param>
        /// <param name="url">Filter to posts with links (facet links or embeds) pointing to this URL. Server may apply URL normalization or fuzzy matching.</param>
        /// <param name="tags">Filter to posts with the given tag (hashtag), based on rich-text facet or tag field. Do not include the hash (#) prefix.</param>
        /// <param name="limit">The maximum number of post views to return</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="query" /> is null or whitespace</exception>
        public async Task<AtProtoHttpResult<SearchResults>> SearchPosts(
            string query,
            DateOnly since,
            DateOnly until,
            SearchOrder? searchOrder = null,
            AtIdentifier? mentions = null,
            AtIdentifier? author = null,
            string? lang = null,
            string? domain = null,
            Uri? url = null,
            ICollection<string>? tags = null,
            int? limit = null,
            string? cursor = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(query);

            string? sinceSerialized = JsonSerializer.Serialize(since).Replace("\"", string.Empty, StringComparison.InvariantCultureIgnoreCase);
            string? untilSerialized = JsonSerializer.Serialize(until).Replace("\"", string.Empty, StringComparison.InvariantCultureIgnoreCase);

            return await BlueskyServer.SearchPosts(
                query,
                searchOrder,
                sinceSerialized,
                untilSerialized,
                mentions,
                author,
                lang,
                domain,
                url,
                tags,
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
    }
}
