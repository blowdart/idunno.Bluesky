// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Sync;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Enumerates the upstream hosts, such as personal data servers or relays, that a relay consumes from.
    /// </summary>
    /// <param name="limit">The maximum number of hosts to return. Must be between 1 and 1000 if specified. The server default is 200.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="service">The relay to query. Defaults to the agent's <see cref="Service"/> if <see langword="null"/>.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 1000.</exception>
    /// <remarks>
    /// <para>
    /// This endpoint is only implemented by relays. Personal data servers do not implement it and will return an error,
    /// so <paramref name="service"/> should be a relay.
    /// </para>
    /// <para>If the agent is authenticated and <paramref name="service"/> is the service the agent is authenticated to, the agent's access credentials are sent with the request.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<PagedReadOnlyCollection<HostDescription>>> ListHosts(
        int? limit = null,
        string? cursor = null,
        Uri? service = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be between 1 and 1000.");
        }

        service ??= Service;

        return await AtProtoServer.ListHosts(
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
