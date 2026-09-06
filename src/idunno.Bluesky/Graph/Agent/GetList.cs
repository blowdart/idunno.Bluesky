// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Get a <see cref="ListView"/> of a Bluesky list and a paginated collection of <see cref="ListItemView"/>s of its items.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the list to get.</param>
    /// <param name="limit">The maximum number of lists that should be return in a page.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
    public async Task<AtProtoHttpResult<ListViewWithItems>> GetList(
        AtUri uri,
        int? limit = 50,
        string? cursor = null,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return await BlueskyServer.GetList(
            uri,
            limit,
            cursor,
            service: AuthenticatedOrUnauthenticatedServiceUri,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}