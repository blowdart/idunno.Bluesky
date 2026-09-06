// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Enumerates the lists created by the session user, and includes membership information about actor in those lists.
    /// Only supports curation and moderation lists (no reference lists, used in starter packs). Requires auth.
    /// </summary>
    /// <param name="actor">The account (actor) to check for membership.</param>
    /// <param name="limit">The maximum number of lists that should be return in a page.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="purposes">Optional filter by list purpose. If not specified, all supported types are returned. Possible values: [modlist, curatelist]</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="actor"/> is <see langword="null"/>, empty or whitespace.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
    public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ListWithMembership>>> GetListsWithMembership(
        AtIdentifier actor,
        int? limit = null,
        string? cursor = null,
        string[]? purposes = null,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetListsWithMembership(
            actor,
            limit,
            cursor,
            purposes,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
