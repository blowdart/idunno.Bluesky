// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Sync;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Enumerates the DIDs of all repositories which have records in the specified collection.
    /// </summary>
    /// <param name="collection">The NSID of the collection to enumerate repositories for.</param>
    /// <param name="limit">
    /// The maximum number of DIDs to return. Must be between 1 and 2000 if specified. The server default is 500.
    /// A large limit (1000 or more) is recommended when enumerating large lists.
    /// </param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="service">The relay or collection directory to query. Defaults to the agent's <see cref="Service"/> if <see langword="null"/>.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="collection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 2000.</exception>
    /// <remarks>
    /// <para>
    /// This endpoint is only implemented by relays or collection directory services. Personal data servers do not implement it
    /// and will return an error, so <paramref name="service"/> should be a relay or collection directory service.
    /// </para>
    /// <para>If the agent is authenticated and <paramref name="service"/> is the service the agent is authenticated to, the agent's access credentials are sent with the request.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<PagedDidCollection>> ListReposByCollection(
        Nsid collection,
        int? limit = null,
        string? cursor = null,
        Uri? service = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (limit is < 1 or > 2000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be between 1 and 2000.");
        }

        service ??= Service;

        return await AtProtoServer.ListReposByCollection(
            collection,
            limit,
            cursor,
            service,
            HttpClient,
            GetSyncCredentials(service),
            InternalOnCredentialsUpdatedCallBack,
            LoggerFactory,
            MaximumResponseSize,
            cancellationToken).ConfigureAwait(false);
    }
}
