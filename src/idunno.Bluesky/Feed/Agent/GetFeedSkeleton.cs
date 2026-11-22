// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.AtProto.Authentication;
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
    /// Authenticated requests are sent to the agent's PDS, which proxies them to the <c>#bsky_fg</c> service of
    /// <paramref name="feedGenerator"/> and authenticates the user to the feed generator. Unauthenticated requests resolve the
    /// service endpoint from the feed generator's DID document and call it directly without a proxy or credentials. Because that
    /// endpoint is externally controlled, so the HTTP client supplied to the agent should enforce SSRF protection and disable
    /// redirects.
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

        Uri service = Service;
        string? serviceProxy = FeedGeneratorServiceProxy(feedGenerator);
        AccessCredentials? accessCredentials = Credentials;
        if (accessCredentials is null)
        {
            DidDocument? document = await IdentityResolution.ResolveDidDocumentAsync(
                did: feedGenerator,
                loggerFactory: LoggerFactory,
                httpClient: HttpClient,
                maximumResponseSize: MaximumResponseSize,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            Uri? directService = document?.Id == feedGenerator
                ? document.Services?.FirstOrDefault(service => service.Id == "#bsky_fg")?.ServiceEndpoint
                : null;

            if (directService is null || !IsSupportedFeedGeneratorEndpoint(directService))
            {
                return new AtProtoHttpResult<FeedSkeleton>(
                    null,
                    statusCode: HttpStatusCode.BadGateway,
                    httpResponseHeaders: null,
                    atErrorDetail: new("FeedGeneratorServiceNotFound", "The feed generator DID does not publish a supported #bsky_fg service endpoint."));
            }

            service = directService;
            serviceProxy = null;
        }

        return await BlueskyServer.GetFeedSkeleton(
            feed,
            limit,
            cursor,
            service: service,
            serviceProxy: serviceProxy,
            accessCredentials: accessCredentials,
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

    /// <summary>Accepts secure service endpoints and loopback HTTP endpoints used for local development.</summary>
    private static bool IsSupportedFeedGeneratorEndpoint(Uri serviceEndpoint) =>
        serviceEndpoint.IsAbsoluteUri &&
        serviceEndpoint.UserInfo.Length == 0 &&
        serviceEndpoint.Query.Length == 0 &&
        serviceEndpoint.Fragment.Length == 0 &&
        (serviceEndpoint.Scheme == Uri.UriSchemeHttps ||
            serviceEndpoint.Scheme == Uri.UriSchemeHttp && serviceEndpoint.IsLoopback);
}
