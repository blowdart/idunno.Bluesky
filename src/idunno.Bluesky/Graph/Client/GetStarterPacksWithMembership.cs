// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Enumerates the starter packs created by the authenticated user, and includes membership information about <paramref name="actor"/> in those starter packs. Requires authentication.
    /// </summary>
    /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose follows should be enumerated.</param>
    /// <param name="limit">The maximum number of lists that should be return in a page.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 100.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the user is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<StarterPackWithMembership>>> GetStarterPacksWithMembership(
        AtIdentifier actor,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (limit.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfZero(limit.Value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, 100);
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetStarterPacksWithMembership(
            actor,
            limit,
            cursor,
            service: AuthenticatedOrUnauthenticatedServiceUri,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
