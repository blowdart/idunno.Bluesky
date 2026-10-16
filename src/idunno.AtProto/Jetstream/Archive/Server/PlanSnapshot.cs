// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Json;

using idunno.AtProto.Jetstream.Archive;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Plans a page of the sealed Jetstream archive.
    /// </summary>
    /// <param name="request">The event selection and sequence bounds.</param>
    /// <param name="service">The HTTP or WebSocket URI of the Jetstream service.</param>
    /// <param name="apiKey">The raw archive API key.</param>
    /// <param name="httpClient">An optional HTTP client. Warning: supplied clients must enforce SSRF protection and disable automatic redirects.</param>
    /// <param name="httpClientOptions">Configuration for a default client; ignored when <paramref name="httpClient"/> is supplied.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The planned snapshot page or an HTTP error.</returns>
    /// <exception cref="ArgumentException">An API key is missing or the service URI is invalid or insecure for a non-loopback host.</exception>
    /// <remarks><para>The archive requires a v2 Jetstream host. Collection and DID filters are approximate at plan time;
    /// consumers must filter the decoded rows again. The default client uses the agent's SSRF-protected transport.
    /// Warning: a supplied client can bypass those protections and follow redirects before the SDK validates them.</para></remarks>
    public static async Task<AtProtoHttpResult<SnapshotPlan>> PlanSnapshot(
        SnapshotRequest request,
        Uri service,
        string apiKey,
        HttpClient? httpClient = null,
        HttpClientOptions? httpClientOptions = null,
        CancellationToken cancellationToken = default)
    {
        using HttpClient? configuredClient = httpClient is null && httpClientOptions is not null
            ? CreateArchiveClient(httpClientOptions) : null;
        ArgumentNullException.ThrowIfNull(request);
        using HttpRequestMessage message = CreateArchiveRequest(
            HttpMethod.Post, service, "planSnapshot", apiKey);
        message.Content = JsonContent.Create(request, SourceGenerationContext.Default.SnapshotRequest);
        return await SendArchiveJson<SnapshotPlan>(
            message, httpClient ?? configuredClient ?? s_archiveClient,
            SourceGenerationContext.Default.SnapshotPlan, cancellationToken).ConfigureAwait(false);
    }
}
