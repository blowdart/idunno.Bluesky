// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets mutually imported contact matches, excluding dismissed matches.
    /// </summary>
    /// <param name="limit">The page size, between 1 and 100; the service default is 50.</param>
    /// <param name="cursor">An optional pagination cursor.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task representing the authenticated contact request.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page size is less than 1 or greater than 100, or the configured maximum response size is not positive.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks>
    /// <para>Requires authentication. Named errors: <c>InvalidDid</c>, <c>InvalidLimit</c>, <c>InvalidCursor</c>, <c>InternalError</c>.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> GetContactMatches(
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetContactMatches(
            limit: limit,
            cursor: cursor,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
