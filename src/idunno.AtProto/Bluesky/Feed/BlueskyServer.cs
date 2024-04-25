// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Text;

using idunno.AtProto.Bluesky.Feed;

namespace idunno.AtProto.Bluesky
{
    /// <summary>
    /// Provides a class for sending requests to and receiving responses from an Bluesky service, identified by its service URI.
    /// </summary>
    internal partial class BlueskyServer
    {
        // https://docs.bsky.app/docs/api/app-bsky-feed-get-timeline
        private const string GetTimelineEndpoint = "/xrpc/app.bsky.feed.getTimeline";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-posts
        private const string GetPostsEndpoint = "xrpc/app.bsky.feed.getPosts";

        // https://docs.bsky.app/docs/api/app-bsky-feed-search-posts
        private const string SearchPostsEndpoint = "/xrpc/app.bsky.feed.searchPosts";

        // https://docs.bsky.app/docs/api/app-bsky-feed-get-post-thread
        private const string GetPostThreadEndpoint = "/xrpc/app.bsky.feed.getPostThread";

        /// <summary>
        /// Retrieves a view of the requesting account's home timeline.
        /// </summary>
        /// <param name="service">The service to retrieve the timeline from.</param>
        /// <param name="algorithm">Variant 'algorithm' for timeline. Implementation-specific.</param>
        /// <param name="limit">The maximum number of entries to retrieve.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="accessToken">The access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A view of the requesting account's home timeline, and a cursor, from the <paramref name="service"/>.</returns>
        public static async Task<HttpResult<Timeline>> GetTimeline(
            Uri service,
            string? algorithm,
            int? limit,
            string? cursor,
            string? accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            StringBuilder queryString = new ();

            if (algorithm is not null)
            {
                queryString.Append(CultureInfo.InvariantCulture, $"algorithm={Uri.EscapeDataString(algorithm)}&");
            }

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

            AtProtoRequest<Timeline> request = new();

            return await request.Get(
                service,
                $"{GetTimelineEndpoint}?{queryString}",
                accessToken,
                httpClientHandler,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Find posts matching the specified search criteria.
        /// </summary>
        /// <param name="service">The service to search on.</param>
        /// <param name="q">Search query string. Syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
        /// <param name="limit">The maximum number of entries to retrieve.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="accessToken">The access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// A view of any posts matching the search criteria, the number of which are limited by the <paramref name="limit"/>,
        /// the total number of matching posts and a cursor for pagination.
        /// </returns>
        public static async Task<HttpResult<PostSearchResults>> SearchPosts(
            Uri service,
            string q,
            int? limit,
            string? cursor,
            string? accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            StringBuilder queryString = new();

            queryString.Append(CultureInfo.InvariantCulture, $"q={Uri.EscapeDataString(q)}&");

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

            AtProtoRequest<PostSearchResults> request = new();

            return await request.Get(
                service,
                $"{SearchPostsEndpoint}?{queryString}",
                accessToken,
                httpClientHandler,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets post views for a specified list of <see cref="AtUri">s. This is sometimes referred to as 'hydrating' a 'feed skeleton'.
        /// </summary>
        /// <param name="service">The service to search on.</param>
        /// <param name="q">The post <see cref="AtUri"/>s to retrieve</param>
        /// <param name="accessToken">The access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A readonly collection of any posts matching the specified AT URIs.</returns>
        public static async Task<HttpResult<IReadOnlyCollection<FeedPost>>> GetPosts(
            Uri service,
            IEnumerable<AtUri> q,
            string? accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            const int maxUriCount = 25;

            IReadOnlyCollection<FeedPost> emptyList = new List<FeedPost>().AsReadOnly();
            var emptyResult = new HttpResult<IReadOnlyCollection<FeedPost>>
            {
                Result = emptyList,
                StatusCode = HttpStatusCode.OK
            };

            var queryUris = new List<AtUri>(q);
            if (queryUris.Count == 0)
            {
                return emptyResult;
            }

            if (queryUris.Count > maxUriCount)
            {
                throw new ArgumentOutOfRangeException(nameof(q), $"Maximum size is {maxUriCount}.");
            }

            string queryString = string.Join("&", queryUris.Select(uri => $"uris={uri}"));

            AtProtoRequest<FeedPosts> request = new();

            HttpResult<FeedPosts> result = await request.Get(
                service,
                $"{GetPostsEndpoint}?{queryString}",
                accessToken,
                httpClientHandler,
                cancellationToken).ConfigureAwait(false);

            // Flatten
            var flattenedResult = new HttpResult<IReadOnlyCollection<FeedPost>>
            {
                StatusCode = result.StatusCode,
                Error = result.Error
            };

            if (result.Result is null ||
                result.Result.Posts is null ||
                !result.Result.Posts.Any())
            {
                flattenedResult.Result = emptyList;
            }
            else
            {
               ReadOnlyCollection<FeedPost> extractedPosts = new List<FeedPost>(result.Result.Posts).AsReadOnly();
               flattenedResult.Result = extractedPosts;
            }

            return flattenedResult;
        }

        /// <summary>
        /// Get posts in a thread. Does not require auth, but additional metadata and filtering will be applied for authenticated requests.
        /// </summary>
        /// <param name="service">The service to search on.</param>
        /// <param name="atUri">The <see cref="AtUri"/> of the post those thread to get.</param>
        /// <param name="depth">How many levels of reply depth should be included in response.</param>
        /// <param name="parentHeight">How many levels of parent (and grandparent, etc) post to include.</param>
        /// <param name="accessToken">The access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="ThreadView"/> for the specified <paramref name="atUri"/>.</returns>
        public static async Task<HttpResult<ThreadView>> GetPostThread(
            Uri service,
            AtUri atUri,
            int? depth,
            int? parentHeight,
            string? accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            StringBuilder queryString = new();

            queryString.Append(CultureInfo.InvariantCulture, $"uri={atUri}&");

            if (depth is not null)
            {
                queryString.Append(CultureInfo.InvariantCulture, $"depth={depth}&");
            }

            if (parentHeight is not null)
            {
                queryString.Append(CultureInfo.InvariantCulture, $"parentHeight={parentHeight}&");
            }

            if (queryString.Length > 0 && queryString[queryString.Length - 1] == '&')
            {
                queryString.Remove(queryString.Length - 1, 1);
            }

            AtProtoRequest<ThreadView> request = new();

            return await request.Get(
                service,
                $"{GetPostThreadEndpoint}?{queryString}",
                accessToken,
                httpClientHandler,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
