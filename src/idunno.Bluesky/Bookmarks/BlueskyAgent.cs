// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;

using idunno.Bluesky.Bookmarks;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Creates a bookmark on the specified account from the specified <paramref name="uri"/> and <paramref name="cid"/>. Requires authentication.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the post to bookmark</param>
        /// <param name="cid">The <see cref="Cid"/> of the post to bookmark</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or <paramref name="cid"/>are <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/> does not point to a post</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown if the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> CreateBookmark(
            AtUri uri,
            Cid cid,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(cid);

            ArgumentNullException.ThrowIfNull(uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.Post);

            return await BlueskyServer.CreateBookmark(
                uri,
                cid,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a bookmark on the specified account from the specified <paramref name="strongReference"/>. Requires authentication.
        /// </summary>
        /// <param name="strongReference">The <see cref="StrongReference"/> of the post to bookmark</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is <see langword="null"/>.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> CreateBookmark(
            StrongReference strongReference,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            ArgumentNullException.ThrowIfNull(strongReference);

            return await BlueskyServer.CreateBookmark(
                strongReference.Uri,
                strongReference.Cid,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a bookmark on the specified account for the specified <paramref name="uri"/>. Requires authentication.
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the post to delete the bookmark for.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, or its Collection property is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/>'s Collection property does not point to a post.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> DeleteBookmark(
            AtUri uri,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            ArgumentNullException.ThrowIfNull(uri);

            ArgumentNullException.ThrowIfNull(uri.Collection);
            ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.Post);

            return await BlueskyServer.DeleteBookmark(
                uri,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a pageable list of the specified user's bookmarks. Requires authentication.
        /// </summary>
        /// <param name="limit">The maximum number of suggested actors to return.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the bookmarked items.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown if the agent is not authenticated.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than <see cref="Maximum.Bookmarks"/>.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<BookmarkView>>> GetBookmarks(
            int? limit,
            string? cursor = null,
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
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limitValue, Maximum.Bookmarks);

            return await BlueskyServer.GetBookmarks(
                cursor,
                limitValue,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

    }
}
