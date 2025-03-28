// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Model;
using idunno.AtProto.Authentication;

namespace idunno.Bluesky
{
    public static partial class BlueskyServer
    {
        // https://docs.bsky.app/docs/api/app-bsky-feed-describe-feed-generator
        private const string DescribeFeedGeneratorEndpoint = "/xrpc/app.bsky.feed.describeFeedGenerator";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-actor-feeds
        private const string GetActorFeedsEndpoint = "/xrpc/app.bsky.feed.getActorFeeds";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-actor-likes
        private const string GetActorLikesEndpoint = "/xrpc/app.bsky.feed.getActorLikes";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-author-feed
        private const string GetAuthorFeedEndpoint = "/xrpc/app.bsky.feed.getAuthorFeed";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-feed-generator
        private const string GetFeedGeneratorEndpoint = "/xrpc/app.bsky.feed.getFeedGenerator";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-feed-generators
        private const string GetFeedGeneratorsEndpoint = "/xrpc/app.bsky.feed.getFeedGenerators";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-feed
        private const string GetFeedEndpoint = "/xrpc/app.bsky.feed.getFeed";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-likes
        private const string GetLikesEndpoint = "/xrpc/app.bsky.feed.getLikes";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-list-feed
        private const string GetListFeedEndpoint = "/xrpc/app.bsky.feed.getListFeed";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-post-thread
        private const string GetPostThreadEndpoint = "/xrpc/app.bsky.feed.getPostThread";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-posts
        private const string GetPostsEndpoint = "/xrpc/app.bsky.feed.getPosts";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-quotes
        private const string GetQuotesEndpoint = "/xrpc/app.bsky.feed.getQuotes";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-reposted-by
        private const string GetRepostedByEndpoint = "/xrpc/app.bsky.feed.getRepostedBy";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-suggested-feeds
        private const string GetSuggestedFeedsEndpoint = "/xrpc/app.bsky.feed.getSuggestedFeeds";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-timeline
        private const string GetTimelineEndpoint = "/xrpc/app.bsky.feed.getTimeline";

        // https://docs.bsky.app/docs/api/app-bsky-feed-search-posts
        private const string SearchPostsEndpoint = "/xrpc/app.bsky.feed.searchPosts";

        private static readonly IReadOnlyCollection<PostView> s_emptyFeedPostCollection = new List<PostView>().AsReadOnly();

        /// <summary>
        /// Gets a description for the feed generator at <paramref name="generatorUri"/>.
        /// </summary>
        /// <param name="generatorUri">The <see cref="Uri"/> of the generator whose description should be retrieved.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="generatorUri"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<FeedGeneratorDescription>> GetFeedGeneratorDescription(
            Uri generatorUri,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(generatorUri);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<FeedGeneratorDescription> client = new(AppViewProxy, loggerFactory);

            return await client.Get(
                generatorUri,
                DescribeFeedGeneratorEndpoint,
                credentials: null,
                httpClient: httpClient,
                onCredentialsUpdated: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a paginated list of feeds (feed generator records) created by the actor, in the actor's repo.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose feeds should be listed.</param>
        /// <param name="limit">The maximum number of feeds to return from the api.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<GeneratorView>>> GetActorFeeds(
            AtIdentifier actor,
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
            ArgumentNullException.ThrowIfNull(actor);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"actor={Uri.EscapeDataString(actor.ToString())}");
            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }
            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }
            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetActorFeedsResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetActorFeedsResponse> response = await client.Get(
                service,
                $"{GetActorFeedsEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<GeneratorView>>(
                    new PagedViewReadOnlyCollection<GeneratorView>(response.Result.Feeds, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<GeneratorView>>(
                    new PagedViewReadOnlyCollection<GeneratorView>(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get a list of posts liked by an actor. Requires auth, actor must be the requesting account.
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose likes should be retrieved.</param>
        /// <param name="limit">The maximum number of feeds to return from the api.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="service"/>, <paramref name="httpClient"/> or <paramref name="accessCredentials"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>> GetActorLikes(
            AtIdentifier actor,
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
            ArgumentNullException.ThrowIfNull(actor);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(accessCredentials);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"actor={Uri.EscapeDataString(actor.ToString())}");
            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }
            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }
            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetActorLikesResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetActorLikesResponse> response = await client.Get(
                service,
                $"{GetActorLikesEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>(
                    new PagedViewReadOnlyCollection<FeedViewPost>(response.Result.Feed, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>(
                    new PagedViewReadOnlyCollection<FeedViewPost>(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get a view of an actor's 'author feed' (post and reposts by the author).
        /// </summary>
        /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose feed should be retrieved.</param>
        /// <param name="limit">The maximum number of items to return from the api.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="filter">Combinations of post/repost types to include in the results.</param>
        /// <param name="includePins">Flag indicating whether to include pinned posts in the results.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the feed from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>> GetAuthorFeed(
            AtIdentifier actor,
            int? limit,
            string? cursor,
            FeedFilter? filter,
            bool? includePins,
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

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"actor={Uri.EscapeDataString(actor.ToString())}");
            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }
            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }
            if (filter is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&feedFilter={filter.GetDescription()}");
            }
            if (includePins is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&includePins={includePins}");
            }
            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetAuthorFeedResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetAuthorFeedResponse> response = await client.Get(
                service,
                $"{GetAuthorFeedEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>(
                    new PagedViewReadOnlyCollection<FeedViewPost>(response.Result.Feed, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>(
                    new PagedViewReadOnlyCollection<FeedViewPost>(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Gets information about the specified <paramref name="feed"/> generator.
        /// </summary>
        /// <param name="feed">The <see cref="AtUri"/> of the feed generator whose information should be retrieved.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the information from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="feed"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<FeedGenerator>> GetFeedGenerator(
            AtUri feed,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(feed);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<FeedGenerator> client = new(AppViewProxy, loggerFactory);

            return await client.Get(
                service,
                $"{GetFeedGeneratorEndpoint}?feed={Uri.EscapeDataString(feed.ToString())}",
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }


        /// <summary>
        /// Gets information about the specified feed generators.
        /// </summary>
        /// <param name="feeds">A collection of <see cref="AtUri"/>s of the feed generators whose information should be retrieved.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the information from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="feeds"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<IReadOnlyCollection<GeneratorView>>> GetFeedGenerators(
            IEnumerable<AtUri> feeds,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(feeds);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            List<AtUri> feedsList = [.. feeds];
            ArgumentOutOfRangeException.ThrowIfZero(feedsList.Count);

            StringBuilder queryStringBuilder = new();
            foreach (AtUri feed in feedsList)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"feeds={Uri.EscapeDataString(feed.ToString())}&");
            }
            queryStringBuilder.Length--;

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetFeedGeneratorsResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetFeedGeneratorsResponse> response = await client.Get(
                service,
                $"{GetFeedGeneratorsEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<IReadOnlyCollection<GeneratorView>>(
                    response.Result.Feeds.AsReadOnly(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<IReadOnlyCollection<GeneratorView>>(
                    new List<GeneratorView>().AsReadOnly(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get a hydrated feed from an actor's selected feed generator.
        /// </summary>
        /// <param name="feed">The <see cref="AtIdentifier"/> of the feed to be retrieved.</param>
        /// <param name="limit">The maximum number of items to return from the api.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the feed from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="feed"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>> GetFeed(
            AtUri feed,
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
            ArgumentNullException.ThrowIfNull(feed);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"feed={Uri.EscapeDataString(feed.ToString())}");
            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }
            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetFeedResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetFeedResponse> response = await client.Get(
                service,
                $"{GetFeedEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>(
                    new PagedViewReadOnlyCollection<FeedViewPost>(response.Result.Feed, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>(
                    new PagedViewReadOnlyCollection<FeedViewPost>(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get like records which reference a subject, by <paramref name="uri"/> and, optionally, <paramref name="cid"/>.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the subject whose likes should be retrieved.</param>
        /// <param name="cid">The <see cref="Cid"/> of the subject whose likes should be retrieved.</param>
        /// <param name="limit">The maximum number of items to return from the api.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the likes from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        public static async Task<AtProtoHttpResult<Likes>> GetLikes(
            AtUri uri,
            Cid? cid,
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"uri={Uri.EscapeDataString(uri.ToString())}");
            if (cid is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"cid={Uri.EscapeDataString(cid.ToString())}");
            }
            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }
            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetLikesResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetLikesResponse> response = await client.Get(
                service,
                $"{GetLikesEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<Likes>(
                    new Likes(response.Result.Uri, response.Result.Cid, response.Result.Likes, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Likes>(
                    new Likes(uri, cid),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get a feed of recent posts from a list (posts and reposts from any actors on the list).
        /// </summary>
        /// <param name="list">The <see cref="AtUri"/> of the list whose recent posts should be retrieved.</param>
        /// <param name="limit">The maximum number of items to return from the api.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the likes from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="list"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>> GetListFeed(
            AtUri list,
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
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"list={Uri.EscapeDataString(list.ToString())}");
            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }
            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetListFeedResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetListFeedResponse> response = await client.Get(
                service,
                $"{GetListFeedEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>(
                    new PagedViewReadOnlyCollection<FeedViewPost>(response.Result.Feed, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<FeedViewPost>>(
                    new PagedViewReadOnlyCollection<FeedViewPost>(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get posts in a thread. Does not require authentication, but additional metadata and filtering will be applied for authenticated requests.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the post record whose thread should be retrieved.</param>
        /// <param name="depth">How many levels of reply depth should be included in response.</param>
        /// <param name="parentHeight">How many levels of parent (and grandparent, etc) post to include.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the likes from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="parentHeight"/> or <paramref name="depth"/> is &lt; 0 or &gt; 1000.</exception>
        public static async Task<AtProtoHttpResult<PostThread>> GetPostThread(
            AtUri uri,
            int? depth,
            int? parentHeight,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (depth is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)depth, 0);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)depth, 1000);
            }

            if (parentHeight is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)parentHeight, 0);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)parentHeight, 1000);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"uri={Uri.EscapeDataString(uri.ToString())}");
            if (depth is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&depth={depth}");
            }
            if (parentHeight is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&parentHeight={parentHeight}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<PostThread> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<PostThread> response = await client.Get(
                service,
                $"{GetPostThreadEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return response;
        }

        /// <summary>
        /// Gets the post views for a specified list of posts (by <see cref="AtUri"/>).
        /// </summary>
        /// <param name="uris">List of post <see cref="AtUri" /> to return hydrated views for.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uris"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uris"/> does not contain any <see cref="AtUri"/>s or has &gt; 25 <see cref="AtUri"/>s.</exception>
        public static async Task<AtProtoHttpResult<IReadOnlyCollection<PostView>>> GetPosts(
            IEnumerable<AtUri> uris,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uris);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            var uriList = new List<AtUri>(uris);

            if (uriList.Count == 0 || uriList.Count > 25)
            {
                ArgumentOutOfRangeException.ThrowIfZero(uriList.Count);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(uriList.Count, 25);
            }

            string queryString = string.Join("&", uriList.Select(uri => $"uris={Uri.EscapeDataString(uri.ToString())}"));

            AtProtoHttpClient<GetPostsResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetPostsResponse> response = await client.Get(
                service,
                $"{GetPostsEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<IReadOnlyCollection<PostView>>(
                    response.Result.Posts,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<IReadOnlyCollection<PostView>>(
                    s_emptyFeedPostCollection,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get a list of quotes for a given post.
        /// </summary>
        /// <param name="uri"><see cref="AtUri"/> of post record whose quotes should be retrieved.</param>
        /// <param name="cid">If supplied, filters to quotes of specific version (by <see cref="AtProto.Cid">content identifier</see>) of the post record.</param>
        /// <param name="limit">The maximum number of posts to return.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri" />, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> &lt;1 or &gt;100.</exception>
        public static async Task<AtProtoHttpResult<QuotesCollection>> GetQuotes(
            AtUri uri,
            Cid? cid,
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
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"uri={Uri.EscapeDataString(uri.ToString())}");
            if (cid is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cid={Uri.EscapeDataString(cid.ToString())}");
            }
            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }
            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor.ToString())}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetQuotesResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetQuotesResponse> response = await client.Get(
                service,
                $"{GetQuotesEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<QuotesCollection>(
                    new QuotesCollection(response.Result.Uri, response.Result.Cid, response.Result.Posts, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<QuotesCollection>(
                    new QuotesCollection(uri, cid),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get a list of profiles who reposted the specified post.
        /// </summary>
        /// <param name="uri"><see cref="AtUri"/> of post record whose repost authors should be retrieved.</param>
        /// <param name="cid">If supplied, filters to quotes of specific version (by <see cref="AtProto.Cid">content identifier</see>) of the post record.</param>
        /// <param name="limit">The maximum number of posts to return.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> &lt;1 or &gt;100.</exception>
        public static async Task<AtProtoHttpResult<RepostedBy>> GetRepostedBy(
            AtUri uri,
            Cid? cid,
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
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"uri={Uri.EscapeDataString(uri.ToString())}");
            if (cid is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cid={Uri.EscapeDataString(cid.ToString())}");
            }
            if (limit is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
            }
            if (cursor is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor.ToString())}");
            }

            string queryString = queryStringBuilder.ToString();

            AtProtoHttpClient<GetRepostedByResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetRepostedByResponse> response = await client.Get(
                service,
                $"{GetRepostedByEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<RepostedBy>(
                    new RepostedBy(response.Result.Uri, response.Result.Cid, response.Result.RepostedBy, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<RepostedBy>(
                    new RepostedBy(uri, cid),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get a list of suggested feeds for the user whose access token is supplied.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<SuggestedFeeds>> GetSuggestedFeeds(
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<GetSuggestedFeedsResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetSuggestedFeedsResponse> response = await client.Get(
                service,
                GetSuggestedFeedsEndpoint,
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<SuggestedFeeds>(
                    new SuggestedFeeds(response.Result.Feeds, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<SuggestedFeeds>(
                    new SuggestedFeeds(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Get a view of the requesting account's home timeline. This is expected to be some form of reverse-chronological feed.
        /// </summary>
        /// <param name="algorithm">Variant 'algorithm' for timeline. Implementation-specific. NOTE: most feed flexibility has been moved to feed generator mechanism.</param>
        /// <param name="limit">The maximum number of post views to return.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> &lt;1 or &gt;100.</exception>
        public static async Task<AtProtoHttpResult<Timeline>> GetTimeline(
            string? algorithm,
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
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();

            if (!string.IsNullOrWhiteSpace(algorithm))
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"algorithm={Uri.EscapeDataString(algorithm)}");
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

            AtProtoHttpClient<GetTimelineResponse> client = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<GetTimelineResponse> response = await client.Get(
                service,
                $"{GetTimelineEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<Timeline>(
                    new Timeline(response.Result.Feed, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Timeline>(
                    new Timeline(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
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
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="query" />, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> &lt;1 or &gt;100.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "The api demands lowercase.")]
        public static async Task<AtProtoHttpResult<SearchResults>> SearchPosts(
            string query,
            SearchOrder? searchOrder,
            string? since,
            string? until,
            AtIdentifier? mentions,
            AtIdentifier? author,
            string? lang,
            string? domain,
            Uri? url,
            ICollection<string>? tags,
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
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryStringBuilder = new();
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"q={Uri.EscapeDataString(query)}");
            if (searchOrder is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&sort={searchOrder.ToString()!.ToLowerInvariant()}");
            }
            if (!string.IsNullOrWhiteSpace(since))
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&since={Uri.EscapeDataString(since)}");
            }
            if (!string.IsNullOrWhiteSpace(until))
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&until={Uri.EscapeDataString(until)}");
            }
            if (mentions is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&mentions={Uri.EscapeDataString(mentions.ToString())}");
            }
            if (author is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&mentions={Uri.EscapeDataString(author.ToString())}");
            }
            if (lang is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&lang={Uri.EscapeDataString(lang)}");
            }
            if (domain is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&domain={Uri.EscapeDataString(domain)}");
            }
            if (url is not null)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&url={Uri.EscapeDataString(url.ToString())}");
            }
            if (tags is not null)
            {
                foreach (string tag in tags)
                {
                    queryStringBuilder = queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&tag={Uri.EscapeDataString(tag)}");
                }
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

            AtProtoHttpClient<SearchPostsResponse> client = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<SearchPostsResponse> response = await client.Get(
                service,
                $"{SearchPostsEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<SearchResults>(
                    new SearchResults(response.Result.Posts, response.Result.HitsTotal, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<SearchResults>(
                    new SearchResults(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }
    }
}
