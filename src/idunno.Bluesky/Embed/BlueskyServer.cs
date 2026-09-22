// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

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
    /// </summary>
    /// <param name="url">The canonical web URL the embed represents (typically the URL the user pasted into the composer). Used as the returned view's uri. May be used for validation in the future.</param>
    /// <param name="uris">An array of AT-URIs to resolve into the data needed for the embed.</param>
    /// <param name="service">The service URI to use for the request.</param>
    /// <param name="accessCredentials">Optional access credentials for authentication.</param>
    /// <param name="httpClient">The HTTP client to use for the request.</param>
    /// <param name="onCredentialsUpdated">Optional callback for when credentials are updated.</param>
    /// <param name="loggerFactory">Optional logger factory for logging.</param>
    /// <param name="subscribedLabelers">Optional list of subscribed labelers.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="uris"/> is empty or has more than <see cref="Maximum.EmbedExternalViewUris"/> elements.
    /// </exception>
    /// <remarks>
    /// <para>Only the hydrated view is returned. The associated references the service echoes back are the ones supplied in <paramref name="uris"/>,
    /// and the raw associated records are not surfaced.</para>
    /// <para>The service returns an empty response when no records were resolvable, or when validation determined the resolved records do not actually
    /// back the requested <paramref name="url"/>. That case is reported as an unsuccessful result whose
    /// <see cref="AtProtoHttpResult{T}.StatusCode"/> is <see cref="HttpStatusCode.NoContent"/> and whose
    /// <see cref="AtProtoHttpResult{T}.AtErrorDetail"/> is <see langword="null" />. Callers should fall back to their own link card rendering in that
    /// case and skip writing strong references to the post.</para>
    /// </remarks>
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
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        IEnumerable<Did>? subscribedLabelers = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(uris);
        ArgumentOutOfRangeException.ThrowIfLessThan(uris.Length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(uris.Length, Maximum.EmbedExternalViewUris);

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        string queryString = $"url={Uri.EscapeDataString(url.ToString())}&";
        queryString += string.Join("&", uris.Select(uri => $"uris={Uri.EscapeDataString(uri.ToString())}"));

        BlueskyHttpClient<GetEmbedExternalResponse> request = new(AppViewProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };

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
            if (response.Result.View is null)
            {
                // The service returned an empty response, which the lexicon documents as meaning nothing resolved, or the resolved records did not
                // back the requested url. Report that as NoContent so it can be told apart from a failure.
                return new AtProtoHttpResult<EmbeddedExternalView>(
                    null,
                    statusCode: HttpStatusCode.NoContent,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }

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