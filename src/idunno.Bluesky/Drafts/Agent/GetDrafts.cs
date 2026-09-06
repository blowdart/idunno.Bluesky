// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Drafts;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Get a paged list of suggested drafts for the authenticated user.
    /// </summary>
    /// <param name="limit">The maximum number of drafts to return.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is negative, zero, or greater than <see cref="Maximum.ListedDrafts"/>.</exception>
    public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<DraftView>>> GetDrafts(
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(limit.Value);
            ArgumentOutOfRangeException.ThrowIfZero(limit.Value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.ListedDrafts);
        }

        return await BlueskyServer.GetDrafts(
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
