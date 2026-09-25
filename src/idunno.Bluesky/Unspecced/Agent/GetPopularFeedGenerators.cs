// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Unspecced;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Get an unspecced, paged list of views over globally popular feed generators.
    /// </summary>
    /// <param name="query">Search query string; syntax, phrase, boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
    /// <param name="limit">The maximum number of feed generators to return.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="subscribedLabelers">An optional list of labeler <see cref="Did"/>s to accept labels from.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt;100.</exception>
    [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
    public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<GeneratorView>>> GetPopularFeedGenerators(
        string? query = null,
        int? limit = null,
        string? cursor = null,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.PopularFeedGenerators);
        }

#pragma warning disable BSKYUnspecced
        return await BlueskyServer.GetPopularFeedGenerators(
            query,
            limit,
            cursor,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            subscribedLabelers: subscribedLabelers,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore BSKYUnspecced
    }
}
