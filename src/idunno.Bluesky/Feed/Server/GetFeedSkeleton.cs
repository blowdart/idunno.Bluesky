// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Gets an unhydrated feed, a list of post references, from a feed generator.
    /// </summary>
    /// <param name="feed">The <see cref="AtUri"/> of the feed generator record for the feed to retrieve.</param>
    /// <param name="limit">The maximum number of posts to return. Must be between 1 and 100.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="service">
    /// The <see cref="Uri"/> of the service to send the request to. This is either the feed generator service itself,
    /// or a PDS which proxies the request to the service named by <paramref name="serviceProxy"/>.
    /// </param>
    /// <param name="serviceProxy">
    /// The optional service a PDS should proxy the request to, in the form <c>{feed generator DID}#bsky_fg</c>,
    /// or <see langword="null"/> when <paramref name="service"/> is the feed generator service itself.
    /// </param>
    /// <param name="accessCredentials">Optional <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Func{T1, T2, TResult}" /> to await if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="feed"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
    /// <remarks>
    /// <para>
    /// <c>app.bsky.feed.getFeedSkeleton</c> is implemented by feed generator services, not by the Bluesky AppView.
    /// Most applications should call <see cref="GetFeed"/>, which returns a hydrated feed.
    /// </para>
    /// <para>
    /// The lexicon defines the <c>UnknownFeed</c> error, which surfaces through <see cref="AtProtoHttpResult{TResult}.AtErrorDetail"/>.
    /// </para>
    /// <para>
    /// <see cref="SkeletonFeedPost.FeedContext"/> and <see cref="FeedSkeleton.RequestId"/> are values supplied by the feed generator.
    /// Treat them as untrusted, opaque data.
    /// </para>
    /// <para>
    /// This overload uses the caller-supplied <see cref="HttpClient"/> to contact <paramref name="service"/>. If that URI can be
    /// influenced by untrusted input, the client must enforce SSRF protection at connection time: resolve the destination and block
    /// loopback, private, link-local and other non-public IPv4 and IPv6 addresses, and disable redirects or apply the same checks to
    /// every redirect target. URI-only validation does not prevent DNS rebinding.
    /// </para>
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<FeedSkeleton>> GetFeedSkeleton(
        AtUri feed,
        int? limit,
        string? cursor,
        Uri service,
        string? serviceProxy,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.PostsToList);
        }

        StringBuilder queryStringBuilder = new();
        queryStringBuilder.Append(CultureInfo.InvariantCulture, $"feed={Uri.EscapeDataString(feed.ToString())}&");

        if (limit is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"limit={limit}&");
        }

        if (cursor is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"cursor={Uri.EscapeDataString(cursor)}&");
        }

        BlueskyHttpClient<GetFeedSkeletonResponse> client = new(serviceProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };
        AtProtoHttpResult<GetFeedSkeletonResponse> response = await client.Get(
            service,
            $"/xrpc/app.bsky.feed.getFeedSkeleton{BuildQueryString(queryStringBuilder)}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<FeedSkeleton>(
                new FeedSkeleton(
                    WithoutNullEntries(response.Result.Feed, service, nameof(response.Result.Feed), loggerFactory),
                    response.Result.Cursor,
                    response.Result.ReqId),
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<FeedSkeleton>(
                null,
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }
    }
}
