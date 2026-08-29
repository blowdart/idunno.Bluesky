// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Enumerates which accounts the requesting account is currently blocking. Requires authentication.
    /// </summary>
    /// <param name="limit">The maximum number of followers that should be return in a page.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> GetBlocks(
        int? limit = null,
        string? cursor = null,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetBlocks(
            limit,
            cursor,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
