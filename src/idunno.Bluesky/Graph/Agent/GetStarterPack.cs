// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets a view of a starter pack.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the starter pack to view.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/></exception>
    public async Task<AtProtoHttpResult<StarterPackView>> GetStarterPack(
        AtUri uri,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return await BlueskyServer.GetStarterPack(
            uri,
            service: AuthenticatedOrUnauthenticatedServiceUri,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
