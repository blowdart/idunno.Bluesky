// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Get a paged list of lists that created by the specified <paramref name="actor"/>.
    /// </summary>
    /// <param name="actor">The actor whose lists to enumerate.</param>
    /// <param name="limit">The maximum number of lists that should be return in a page.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "RS0027",
        Justification = "The shorter overload preserves the existing optional-parameter API; the extended overload adds the lexicon option.")]
    public Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>> GetLists(
        AtIdentifier actor,
        int? limit = null,
        string? cursor = null,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        return GetLists(actor, limit, cursor, subscribedLabelers, purposes: null, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets a paged list of lists created by the specified actor.
    /// </summary>
    /// <param name="actor">The actor whose lists to enumerate.</param>
    /// <param name="limit">The maximum number of lists in a page.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="subscribedLabelers">An optional list of labelers to include.</param>
    /// <param name="purposes">An optional list-purpose filter, such as <c>modlist</c> or <c>curatelist</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 100.</exception>
    public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>> GetLists(
        AtIdentifier actor,
        int? limit,
        string? cursor,
        IEnumerable<Did>? subscribedLabelers,
        string[]? purposes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return await BlueskyServer.GetLists(
            actor,
            limit,
            cursor,
            service: AuthenticatedOrUnauthenticatedServiceUri,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            subscribedLabelers: subscribedLabelers,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken,
            purposes: purposes).ConfigureAwait(false);
    }
}
