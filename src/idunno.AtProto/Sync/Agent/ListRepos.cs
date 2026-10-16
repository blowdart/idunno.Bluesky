// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Sync;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Enumerates the DID, revision and commit CID of every repository hosted by a personal data server or relay.
    /// </summary>
    /// <param name="limit">The maximum number of repositories to return. Must be between 1 and 1000 if specified. The server default is 500.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="service">The personal data server or relay to query. Defaults to the agent's <see cref="Service"/> if <see langword="null"/>.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 1000.</exception>
    public async Task<AtProtoHttpResult<PagedReadOnlyCollection<HostedRepository>>> ListRepos(
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

        return await AtProtoServer.ListRepos(
            limit,
            cursor,
            service,
            HttpClient,
            LoggerFactory,
            MaximumResponseSize,
            cancellationToken).ConfigureAwait(false);
    }
}
