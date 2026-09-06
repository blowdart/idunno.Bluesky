// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Find actors (profiles) matching search criteria. Does not require authentication.
    /// </summary>
    /// <param name="q">The search query string. Syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
    /// <param name="limit">The number of suggested actors to return. Defaults to 50 if <see langword="null"/>.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="q" /> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt;=0 or &gt;100.</exception>
    public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> SearchActors(
        string q,
        int? limit = 50,
        string? cursor = null,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(q);

        if (limit is not null)
        {
            int limitValue = (int)limit;

            ArgumentOutOfRangeException.ThrowIfNegative(limitValue);
            ArgumentOutOfRangeException.ThrowIfZero(limitValue);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limitValue, 100);
        }

        return await BlueskyServer.SearchActors(
            q,
            limit,
            cursor,
            AuthenticatedOrUnauthenticatedServiceUri,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
