// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto.Jetstream.Archive;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    private static readonly HttpClient s_archiveClient = CreateArchiveClient(new HttpClientOptions());

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

    /// <summary>
    /// Downloads a sealed Jetstream segment as a stream.
    /// </summary>
    /// <param name="name">The segment filename.</param>
    /// <param name="service">The HTTP or WebSocket URI of the Jetstream service.</param>
    /// <param name="apiKey">The raw archive API key.</param>
    /// <param name="httpClient">An optional HTTP client. Warning: supplied clients must enforce SSRF protection and disable automatic redirects.</param>
    /// <param name="offset">The byte offset for a resumed download.</param>
    /// <param name="etag">The ETag of the generation being resumed.</param>
    /// <param name="httpClientOptions">Configuration for a default client; ignored when <paramref name="httpClient"/> is supplied.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A response-owned segment stream, or an HTTP error. Dispose the stream after use.</returns>
    /// <exception cref="ArgumentException">The name or key is missing, or a resumed download has no <paramref name="etag"/>.</exception>
    /// <remarks><para><c>SegmentNotFound</c> is returned in the HTTP result.
    /// The default client uses the agent's SSRF-protected, redirect-disabled transport. A supplied client can bypass
    /// those protections and follow redirects before the SDK validates them. Disable automatic redirects in the
    /// supplied handler before making archive requests.</para></remarks>
    public static async Task<AtProtoHttpResult<Stream>> GetSegment(
        string name,
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
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            using HttpRequestMessage message = CreateArchiveRequest(
                HttpMethod.Get, service, "getSegment", apiKey, $"name={Uri.EscapeDataString(name)}");
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

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "The HttpClient owns the handler and the caller disposes the client or transfers it to the response stream.")]
    private static HttpClient CreateArchiveClient(HttpClientOptions options)
    {
        HttpClient client = new(Agent.CreateHttpMessageHandler(options, null));
        try
        {
            Agent.InternalConfigureHttpClient(client, options.HttpUserAgent, options.Timeout);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static HttpRequestMessage CreateArchiveRequest(
        HttpMethod method, Uri service, string endpoint, string apiKey, string? query = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        if (!service.IsAbsoluteUri || service.Scheme is not ("wss" or "ws" or "https" or "http") ||
            (service.Scheme is "ws" or "http" && !service.IsLoopback))
        {
            throw new ArgumentException("The archive service must use HTTPS or WSS except on loopback.", nameof(service));
        }

        UriBuilder builder = new(service)
        {
            Scheme = service.Scheme switch { "wss" => "https", "ws" => "http", _ => service.Scheme },
            Path = $"/xrpc/network.bsky.jetstream.{endpoint}",
            Query = query ?? "",
            Fragment = ""
        };
        HttpRequestMessage message = new(method, builder.Uri);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return message;
    }

    private static async Task<AtProtoHttpResult<T>> SendArchiveJson<T>(
        HttpRequestMessage message, HttpClient httpClient, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        using HttpResponseMessage response = await httpClient.SendAsync(
            message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            AtErrorDetail error = await ReadArchiveError(response, message, cancellationToken).ConfigureAwait(false);
            return new(null, response.StatusCode, response.Headers, error);
        }

        string json = await HttpContentReader.ReadAsString(
            response.Content, 8 * 1024 * 1024, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The Jetstream archive JSON response exceeded 8 MB.");
        T? result = JsonSerializer.Deserialize(json, jsonTypeInfo);
        return new(result ?? throw new JsonException("The archive response was empty."), response.StatusCode, response.Headers);
    }

    private static async Task<AtProtoHttpResult<Stream>> SendArchiveBinary(
        HttpRequestMessage message, HttpClient httpClient, long offset, string? etag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (offset > 0)
        {
            if (string.IsNullOrWhiteSpace(etag))
            {
                throw new ArgumentException("A resumed download requires the original ETag.", nameof(etag));
            }

            message.Headers.Range = new RangeHeaderValue(offset, null);
            message.Headers.IfRange = new RangeConditionHeaderValue(etag);
        }

        HttpResponseMessage response = await httpClient.SendAsync(
            message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.RequestMessage?.RequestUri is Uri responseUri && responseUri != message.RequestUri)
        {
            response.Dispose();
            throw new InvalidDataException("The archive HTTP client followed a redirect automatically; disable automatic redirects.");
        }
        if (response.StatusCode is HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
        {
            Uri? destination = response.Headers.Location;
            response.Dispose();
            if (destination is null || !destination.IsAbsoluteUri || destination.UserInfo.Length > 0 ||
                destination.Scheme is not ("https" or "http") ||
                (destination.Scheme == "http" && !destination.IsLoopback) ||
                (message.RequestUri?.Scheme == "https" && destination.Scheme != "https"))
            {
                throw new InvalidDataException("The archive returned an invalid download redirect.");
            }

            using HttpRequestMessage redirected = new(HttpMethod.Get, destination);
            redirected.Headers.Range = message.Headers.Range;
            redirected.Headers.IfRange = message.Headers.IfRange;
            response = await httpClient.SendAsync(
                redirected, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.RequestMessage?.RequestUri is Uri redirectedUri && redirectedUri != destination)
            {
                response.Dispose();
                throw new InvalidDataException("The archive HTTP client followed a redirect automatically; disable automatic redirects.");
            }
        }

        if (response.IsSuccessStatusCode)
        {
            try
            {
                if (offset > 0 && (response.StatusCode != HttpStatusCode.PartialContent ||
                    response.Content.Headers.ContentRange?.From != offset ||
                    response.Headers.ETag?.ToString() != etag))
                {
                    throw new InvalidDataException("The archive server did not resume the expected segment generation.");
                }

                Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                return new(new ArchiveResponseStream(stream, response), response.StatusCode, response.Headers);
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }

        using (response)
        {
            AtErrorDetail error = await ReadArchiveError(response, message, cancellationToken).ConfigureAwait(false);
            return new(null, response.StatusCode, response.Headers, error);
        }
    }

    private static async Task<AtErrorDetail> ReadArchiveError(
        HttpResponseMessage response, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string content = await HttpContentReader.ReadAsStringTruncating(
            response.Content, 64 * 1024, cancellationToken).ConfigureAwait(false);
        AtErrorDetail error = new()
        {
            Instance = request.RequestUri,
            HttpMethod = request.Method,
            RawContent = content
        };

        if (response.Content.Headers.ContentType?.MediaType == "application/json" && content.Length > 0)
        {
            try
            {
                AtErrorDetail? parsed = JsonSerializer.Deserialize(
                    content, SourceGenerationContext.Default.AtErrorDetail);
                if (parsed is not null)
                {
                    error.Error = parsed.Error;
                    error.Message = parsed.Message;
                }
            }
            catch (JsonException)
            {
                // Keep the raw error body if it was not valid JSON.
            }
        }

        return error;
    }

    internal sealed class ArchiveResponseStream(Stream stream, HttpResponseMessage response) : Stream
    {
        private readonly Stream _stream = stream;
        private readonly HttpResponseMessage _response = response;
        private HttpClient? _ownedClient;

        internal void AttachClient(HttpClient client) => _ownedClient = client;

        internal long? ContentLength => _response.Content.Headers.ContentRange?.Length ??
            (_response.Content.Headers.ContentRange?.From is long from && _response.Content.Headers.ContentLength is long remaining
                ? checked(from + remaining)
                : _response.Content.Headers.ContentLength);
        internal string? ETag => _response.Headers.ETag?.ToString();
        internal HttpResponseHeaders Headers => _response.Headers;

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _stream.Length;
        public override long Position { get => _stream.Position; set => throw new NotSupportedException(); }
        public override void Flush() => _stream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _stream.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _stream.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _stream.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream.Dispose();
                _response.Dispose();
                _ownedClient?.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _response.Dispose();
            _ownedClient?.Dispose();
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
