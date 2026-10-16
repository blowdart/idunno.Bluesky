// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

using idunno.AtProto.Jetstream.Archive;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Lists a page of sealed segments.
    /// </summary>
    /// <param name="service">The HTTP or WebSocket URI of the Jetstream service.</param>
    /// <param name="apiKey">The raw archive API key.</param>
    /// <param name="httpClient">An optional HTTP client. Warning: supplied clients must enforce SSRF protection and disable automatic redirects.</param>
    /// <param name="limit">The maximum number of segments, from 1 through 1000.</param>
    /// <param name="cursor">An optional pagination cursor.</param>
    /// <param name="httpClientOptions">Configuration for a default client; ignored when <paramref name="httpClient"/> is supplied.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A page of sealed segments or an HTTP error.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page limit is outside the supported range.</exception>
    /// <remarks><para>The default client uses the agent's SSRF-protected transport. A supplied client can bypass
    /// those protections and follow redirects before the SDK validates them. Disable automatic redirects in the
    /// supplied handler before making archive requests.</para></remarks>
    public static async Task<AtProtoHttpResult<SegmentList>> ListSegments(
        Uri service,
        string apiKey,
        HttpClient? httpClient = null,
        int limit = 100,
        string? cursor = null,
        HttpClientOptions? httpClientOptions = null,
        CancellationToken cancellationToken = default)
    {
        using HttpClient? configuredClient = httpClient is null && httpClientOptions is not null
            ? CreateArchiveClient(httpClientOptions) : null;
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 1000);
        using HttpRequestMessage message = CreateArchiveRequest(
            HttpMethod.Get, service, "listSegments", apiKey,
            $"limit={limit.ToString(CultureInfo.InvariantCulture)}" +
            (cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}"));
        return await SendArchiveJson<SegmentList>(
            message, httpClient ?? configuredClient ?? s_archiveClient,
            SourceGenerationContext.Default.SegmentList, cancellationToken).ConfigureAwait(false);
    }
}
