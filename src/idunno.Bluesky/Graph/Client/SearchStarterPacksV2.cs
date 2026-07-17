// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Find starter packs matching search criteria.
    /// </summary>
    /// <param name="q">Search query string. Syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended</param>
    /// <param name="limit">The maximum number of lists that should be return in a page.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="q"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 100.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the user is not authenticated.</exception>
    /// <remarks><para>The XRPC endpoint for searching starter packs currently returns a 404.</para></remarks>
    public async Task<AtProtoHttpResult<SearchStarterPacksV2Result>> SearchStarterPacksV2(
        string q,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(q);
        if (limit.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, 100);
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.SearchStarterPacksV2(
            q,
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