// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Lists blobs referenced by the current account's repository that are missing from its personal data server.
    /// </summary>
    /// <param name="limit">The maximum number of blobs to return. Must be between 1 and 1000 if specified; the server default is 500.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the request.</param>
    /// <returns>A page of missing blobs and an optional pagination cursor.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 1000.</exception>
    public async Task<AtProtoHttpResult<PagedReadOnlyCollection<MissingBlob>>> ListMissingBlobs(
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be between 1 and 1000.");
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await AtProtoServer.ListMissingBlobs(
            limit,
            cursor,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
