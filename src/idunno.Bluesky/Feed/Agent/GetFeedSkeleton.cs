// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets an unhydrated feed, a list of post references, from the feed generator that serves <paramref name="feed"/>.
    /// </summary>
    /// <param name="feed">The <see cref="AtUri"/> of the feed generator record for the feed to retrieve.</param>
    /// <param name="feedGenerator">
    /// The optional <see cref="Did"/> of the feed generator service. When <see langword="null"/> the service <see cref="Did"/> is
    /// looked up from the feed generator record with <see cref="GetFeedGenerator(AtUri, IEnumerable{Did}?, CancellationToken)"/>.
    /// </param>
    /// <param name="limit">The maximum number of posts to return. Must be between 1 and 100.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="feed"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
    /// <remarks>
    /// <para>
    /// The request is sent to the agent's PDS, which proxies it to the <c>#bsky_fg</c> service of <paramref name="feedGenerator"/>.
    /// Authentication is optional; when the agent is authenticated, the PDS also authenticates the user to the feed generator.
    /// Most applications should call
    /// <see cref="GetFeed(AtUri, int?, string?, ICollection{System.Net.Http.Headers.NameValueHeaderValue}?, IEnumerable{Did}?, CancellationToken)"/>,
    /// which returns a hydrated feed.
    /// </para>
    /// <para>
    /// If the feed generator record cannot be retrieved the failed lookup result is returned. The lexicon defines the
    /// <c>UnknownFeed</c> error, which surfaces through <see cref="AtProtoHttpResult{TResult}.AtErrorDetail"/>.
    /// </para>
    /// </remarks>
    public async Task<AtProtoHttpResult<FeedSkeleton>> GetFeedSkeleton(
        AtUri feed,
        Did? feedGenerator = null,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feed);

        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.PostsToList);
        }

        if (feedGenerator is null)
        {
            AtProtoHttpResult<FeedGenerator> generator = await GetFeedGenerator(feed, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!generator.Succeeded)
            {
                return new AtProtoHttpResult<FeedSkeleton>(
                    null,
                    statusCode: generator.StatusCode,
                    httpResponseHeaders: generator.HttpResponseHeaders,
                    atErrorDetail: generator.AtErrorDetail,
                    rateLimit: generator.RateLimit);
            }

            feedGenerator = generator.Result.View.Did;
        }

        return await BlueskyServer.GetFeedSkeleton(
            feed,
            limit,
            cursor,
            service: Service,
            serviceProxy: FeedGeneratorServiceProxy(feedGenerator),
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the <c>atproto-proxy</c> service reference for the feed generator service of <paramref name="feedGenerator"/>.
    /// </summary>
    /// <param name="feedGenerator">The <see cref="Did"/> of the feed generator service.</param>
    /// <returns>The service reference, <c>{feedGenerator}#bsky_fg</c>.</returns>
    private static string FeedGeneratorServiceProxy(Did feedGenerator) => $"{feedGenerator}#bsky_fg";
}
