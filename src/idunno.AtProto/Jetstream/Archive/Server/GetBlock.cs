// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Downloads a compressed block from a sealed segment.
    /// </summary>
    /// <param name="segment">The segment filename.</param>
    /// <param name="blockIndex">The zero-based block index.</param>
    /// <param name="service">The HTTP or WebSocket URI of the Jetstream service.</param>
    /// <param name="apiKey">The raw archive API key.</param>
    /// <param name="httpClient">An optional HTTP client. Warning: supplied clients must enforce SSRF protection and disable automatic redirects.</param>
    /// <param name="offset">The byte offset for a resumed download.</param>
    /// <param name="etag">The ETag of the generation being resumed.</param>
    /// <param name="httpClientOptions">Configuration for a default client; ignored when <paramref name="httpClient"/> is supplied.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A response-owned stream of a bare zstd frame, or an HTTP error. Dispose the stream after use.</returns>
    /// <exception cref="ArgumentException">The segment or key is missing, or a resumed download has no <paramref name="etag"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The block index or byte offset is negative.</exception>
    /// <remarks><para><c>SegmentNotFound</c> and <c>BlockNotFound</c> are returned in the HTTP result.
    /// The default client uses the agent's SSRF-protected, redirect-disabled transport. A supplied client can bypass
    /// those protections and follow redirects before the SDK validates them. Disable automatic redirects in the
    /// supplied handler before making archive requests.</para></remarks>
    public static async Task<AtProtoHttpResult<Stream>> GetBlock(
        string segment,
        int blockIndex,
        Uri service,
        string apiKey,
        HttpClient? httpClient = null,
        long offset = 0,
        string? etag = null,
        HttpClientOptions? httpClientOptions = null,
        CancellationToken cancellationToken = default)
    {
        HttpClient? configuredClient = httpClient is null && httpClientOptions is not null
            ? CreateArchiveClient(httpClientOptions) : null;
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(segment);
            ArgumentOutOfRangeException.ThrowIfNegative(blockIndex);
            using HttpRequestMessage message = CreateArchiveRequest(
                HttpMethod.Get, service, "getBlock", apiKey,
                $"segment={Uri.EscapeDataString(segment)}&blockIndex={blockIndex.ToString(CultureInfo.InvariantCulture)}");
            AtProtoHttpResult<Stream> result = await SendArchiveBinary(message,
                httpClient ?? configuredClient ?? s_archiveClient, offset, etag,
                cancellationToken).ConfigureAwait(false);
            if (configuredClient is not null)
            {
                if (result.Succeeded)
                {
                    ((ArchiveResponseStream)result.Result).AttachClient(configuredClient);
                }
                else
                {
                    configuredClient.Dispose();
                }
            }
            configuredClient = null;
            return result;
        }
        finally
        {
            configuredClient?.Dispose();
        }
    }
}
