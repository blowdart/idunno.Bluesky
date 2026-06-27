// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Embed.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    // https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/embed/getEmbedExternalView.json
    private const string GetEmbedExternalViewEndpoint = "/xrpc/app.bsky.embed.getEmbedExternalView";

    /// <summary>
    /// Resolve one or more <see cref="AtUri"/>s into the data needed to render an enhanced external embed.
    /// Returns `associatedRefs` (strongRefs to embed into a post's external.associatedRefs),
    /// the raw `associatedRecords`, and a hydrated `view`.
    /// The response is empty when no records were resolvable, or when validation determined the resolved records don't actually back the requested <paramref name="url"/>;
    /// clients should fall back to their own link-card rendering in that case and skip writing strongRefs to the post.
    /// </summary>
    /// <param name="url">The canonical web URL the embed represents (typically the URL the user pasted into the composer). Used as the returned view's `uri`. May be used for validation in the future.</param>
    /// <param name="uris">An array of AT-URIs to resolve into the data needed for the embed.</param>
    /// <param name="service">The service URI to use for the request.</param>
    /// <param name="accessCredentials">Optional access credentials for authentication.</param>
    /// <param name="httpClient">The HTTP client to use for the request.</param>
    /// <param name="onCredentialsUpdated">Optional callback for when credentials are updated.</param>
    /// <param name="loggerFactory">Optional logger factory for logging.</param>
    /// <param name="subscribedLabelers">Optional list of subscribed labelers.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uris"/> has &lt;1 or &gt;4 elements.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<EmbeddedExternalView>> GetEmbedExternalView(
        Uri url,
        AtUri[] uris,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(uris);
        ArgumentOutOfRangeException.ThrowIfLessThan(uris.Length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(uris.Length, 4);

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        string queryString = $"url={Uri.EscapeDataString(url.ToString())}&";
        queryString += string.Join("&", uris.Select(uri => $"uris={Uri.EscapeDataString(uri.ToString())}"));

        BlueskyHttpClient<GetEmbedExternalResponse> request = new(AppViewProxy, loggerFactory);

        AtProtoHttpResult<GetEmbedExternalResponse> response = await request.Get(
            service,
            $"{GetEmbedExternalViewEndpoint}?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<EmbeddedExternalView>(
                response.Result.View,
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<EmbeddedExternalView>(
                null,
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }

    }
}