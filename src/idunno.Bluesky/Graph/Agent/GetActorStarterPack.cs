// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Get a list of starter packs created by the <paramref name="actor"/>.
    /// </summary>
    /// <param name="actor">The <see cref="AtIdentifier"/> of the actor whose starter packs should be returned.</param>
    /// <param name="limit">The maximum number of starter packs to return from the api.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is <see langword="null" />.</exception>
    public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<StarterPackViewBasic>>> GetActorStarterPacks(
        AtIdentifier actor,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return await BlueskyServer.GetActorStarterPacks(
            actor,
            limit,
            cursor,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
