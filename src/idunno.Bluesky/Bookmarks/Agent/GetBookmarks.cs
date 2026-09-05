// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

using idunno.Bluesky.Bookmarks;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{

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