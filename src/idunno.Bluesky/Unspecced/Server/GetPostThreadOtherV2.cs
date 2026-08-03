// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Unspecced;
using idunno.Bluesky.Unspecced.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    ///  Get additional posts under a thread e.g. replies hidden by threadgate.
    ///  Based on an anchor post at any depth of the tree, returns top-level replies below that anchor.
    ///  It does not include ancestors nor the anchor itself.
    ///  This should be called after exhausting <see cref="GetPostThreadV2(AtUri, bool?, int?, int?, string?, Uri, AccessCredentials, HttpClient, Action{AtProtoCredential}?, ILoggerFactory?, IEnumerable{Did}?, CancellationToken)"/>.
    ///  Does not require authentication, but additional metadata and filtering will be applied for authed requests.
    /// </summary>
    /// <param name="anchor">Reference <see cref="AtUri"/> to post record. This is the anchor post, and the thread will be built around it. It can be any post in the tree, not necessarily a root post.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="anchor"/>, <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient" /> are <see langword="null"/>.</exception>
    [UnconditionalSuppressMessage("Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "javascript require lowercase")]
    public static async Task<AtProtoHttpResult<IReadOnlyCollection<ThreadItem>>> GetPostThreadOtherV2(
        AtUri anchor,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        StringBuilder queryStringBuilder = new($"anchor={Uri.EscapeDataString(anchor.ToString())}");

        string queryString = queryStringBuilder.ToString();

        BlueskyHttpClient<GetPostThreadOtherV2Response> request = new(AppViewProxy, loggerFactory);

        AtProtoHttpResult<GetPostThreadOtherV2Response> result = await request.Get(
            service,
            $"xrpc/app.bsky.unspecced.getPostThreadOtherV2?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.Succeeded)
        {
            return new AtProtoHttpResult<IReadOnlyCollection<ThreadItem>>(
                result.Result.Thread,
                statusCode: result.StatusCode,
                httpResponseHeaders: result.HttpResponseHeaders,
                atErrorDetail: result.AtErrorDetail,
                rateLimit: result.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<IReadOnlyCollection<ThreadItem>>(
                null,
                statusCode: result.StatusCode,
                httpResponseHeaders: result.HttpResponseHeaders,
                atErrorDetail: result.AtErrorDetail,
                rateLimit: result.RateLimit);
        }
    }
}
