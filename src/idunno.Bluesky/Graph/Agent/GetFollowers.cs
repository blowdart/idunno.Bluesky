// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets a paged list of followers for the specified <paramref name="actor"/>.
    /// </summary>
    /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose followers should be retrieved.</param>
    /// <param name="limit">The maximum number of followers that will be included in the paged results.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="sort">An optional sort order. Known values are "latest" and "top".</param>
    /// <param name="subscribedLabelers">An optional list of labeler <see cref="Did"/>s to accept labels from.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<Followers>> GetFollowers(
        AtIdentifier actor,
        int? limit = null,
        string? cursor = null,
        string? sort = null,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return await BlueskyServer.GetFollowers(
            actor,
            limit,
            cursor,
            sort,
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
    /// <param name="sort">An optional sort order for the results. Known values are "latest" and "top".</param>
    /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
    public async Task<AtProtoHttpResult<Followers>> GetFollowers(
        int? limit = null,
        string? cursor = null,
        string? sort = null,
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
            sort,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}