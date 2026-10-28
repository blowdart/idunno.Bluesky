// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;

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
        ArgumentNullException.ThrowIfNull(uris);
        ArgumentOutOfRangeException.ThrowIfLessThan(uris.Length, 1);

        AtProtoHttpResult<EmbeddedExternalViewResponse> response = await GetEmbedExternalViewResponse(
            url,
            canonicalUri: null,
            uris: uris,
            service: service,
            accessCredentials: accessCredentials,
            httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            loggerFactory: loggerFactory,
            subscribedLabelers: subscribedLabelers,
            maximumResponseSize: maximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!response.Succeeded)
        {
            return new AtProtoHttpResult<EmbeddedExternalView>(
                null,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }

        if (response.Result.View is null)
        {
            return new AtProtoHttpResult<EmbeddedExternalView>(
                null,
                statusCode: HttpStatusCode.NoContent,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }

        return new AtProtoHttpResult<EmbeddedExternalView>(
            response.Result.View,
            response.StatusCode,
            response.HttpResponseHeaders,
            response.AtErrorDetail,
            response.RateLimit);
    }

    /// <summary>
    /// Resolves records for an enhanced external embed and returns the complete lexicon response.
    /// </summary>
    /// <param name="url">The canonical web URL represented by the embed.</param>
    /// <param name="canonicalUri">The canonical AT URI of the record found on the webpage.</param>
    /// <param name="uris">Legacy AT-URI inputs, ignored when <paramref name="canonicalUri"/> is supplied.</param>
    /// <param name="service">The service URI to use for the request.</param>
    /// <param name="accessCredentials">Optional credentials for authentication.</param>
    /// <param name="httpClient">The HTTP client to use for the request.</param>
    /// <param name="onCredentialsUpdated">An optional callback for credential updates.</param>
    /// <param name="loggerFactory">An optional logger factory.</param>
    /// <param name="subscribedLabelers">Optional labelers whose labels should be included.</param>
    /// <param name="maximumResponseSize">The maximum response size in bytes.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The complete response, including preferred data, persistence references and raw records.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="uris"/> contains a <see langword="null"/> entry.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The <paramref name="uris"/> array contains more than the supported number of values.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<EmbeddedExternalViewResponse>> GetEmbedExternalViewResponse(
        Uri url,
        AtUri? canonicalUri,
        AtUri[]? uris,
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
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (uris is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(uris.Length, Maximum.EmbedExternalViewUris);
            if (uris.Any(uri => uri is null))
            {
                throw new ArgumentException("The AT-URI collection cannot contain null entries.", nameof(uris));
            }
        }

        StringBuilder queryStringBuilder = new($"url={Uri.EscapeDataString(url.ToString())}");
        if (canonicalUri is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&canonicalUri={Uri.EscapeDataString(canonicalUri.ToString())}");
        }
        else if (uris is not null)
        {
            foreach (AtUri uri in uris)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&uris={Uri.EscapeDataString(uri.ToString())}");
            }
        }

        BlueskyHttpClient<GetEmbedExternalResponse> request = new(AppViewProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };
        AtProtoHttpResult<GetEmbedExternalResponse> response = await request.Get(
            service,
            $"{GetEmbedExternalViewEndpoint}?{queryStringBuilder}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<EmbeddedExternalViewResponse>(
                new EmbeddedExternalViewResponse(
                    response.Result.View,
                    response.Result.Data,
                    response.Result.AssociatedRefs is not null
                        ? WithoutNullEntries(response.Result.AssociatedRefs, service, nameof(response.Result.AssociatedRefs), loggerFactory)
                        : null,
                    response.Result.AssociatedRecords,
                    response.Result.UnverifiedAssociatedRefs is not null
                        ? WithoutNullEntries(response.Result.UnverifiedAssociatedRefs, service, nameof(response.Result.UnverifiedAssociatedRefs), loggerFactory)
                        : null,
                    response.Result.UnverifiedAssociatedRecords),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }

        return new AtProtoHttpResult<EmbeddedExternalViewResponse>(
            null,
            response.StatusCode,
            response.HttpResponseHeaders,
            response.AtErrorDetail,
            response.RateLimit);
    }
}
