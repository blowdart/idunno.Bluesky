// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

using idunno.AtProto.Sync;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Gets a blob, by its <see cref="Cid"/>, from a personal data server.
    /// </summary>
    /// <param name="did">The DID of the repository that references the blob.</param>
    /// <param name="cid">The CID of the blob to retrieve.</param>
    /// <param name="service">The personal data server hosting the repository.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for the request.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from an error response.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// A result containing a <see cref="BlobContent"/> for the blob. The <see cref="BlobContent"/> owns the response and must be disposed.
    /// The <see cref="BlobContent.ContentType"/> is supplied by the server and is untrusted.
    /// </returns>
    /// <exception cref="HttpRequestException">Thrown when the request failed due to an underlying issue such as network connectivity, DNS failure, server certificate validation, or timeout.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request URI is invalid.</exception>
    /// <exception cref="TaskCanceledException">Thrown when the request was canceled.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/>, <paramref name="cid"/>, <paramref name="service"/>, or <paramref name="httpClient"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumResponseSize"/> is less than 1.</exception>
    /// <remarks>
    /// <para>
    /// The blob content is streamed rather than buffered, so the blob size is not limited by <paramref name="maximumResponseSize"/>.
    /// </para>
    /// </remarks>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Ownership of the BlobContent, and the response it wraps, is transferred to the caller.")]
    public static async Task<AtProtoHttpResult<BlobContent>> GetBlob(
        Did did,
        Cid cid,
        Uri service,
        HttpClient httpClient,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);
        ArgumentNullException.ThrowIfNull(cid);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumResponseSize, 1);

        Uri requestUri = new(
            service,
            $"/xrpc/com.atproto.sync.getBlob?did={Uri.EscapeDataString(did.Value)}&cid={Uri.EscapeDataString(cid.ToString())}");

        AtProtoHttpResult<Stream> streamResult = await GetStreamingResponse(
            requestUri,
            "*/*",
            httpClient,
            maximumResponseSize,
            cancellationToken).ConfigureAwait(false);

        if (!streamResult.Succeeded)
        {
            return new AtProtoHttpResult<BlobContent>(
                result: null,
                statusCode: streamResult.StatusCode,
                httpResponseHeaders: streamResult.HttpResponseHeaders,
                atErrorDetail: streamResult.AtErrorDetail,
                rateLimit: streamResult.RateLimit);
        }

        HttpContentHeaders? contentHeaders = (streamResult.Result as ResponseOwnedStream)?.ContentHeaders;

        return new AtProtoHttpResult<BlobContent>(
            result: new BlobContent(
                streamResult.Result,
                cid,
                contentHeaders?.ContentType,
                contentHeaders?.ContentLength),
            statusCode: streamResult.StatusCode,
            httpResponseHeaders: streamResult.HttpResponseHeaders,
            rateLimit: streamResult.RateLimit);
    }
}
