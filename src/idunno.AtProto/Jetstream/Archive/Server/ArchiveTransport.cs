// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    private static readonly HttpClient s_archiveClient = CreateArchiveClient(new HttpClientOptions());

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
                if (response.StatusCode == HttpStatusCode.PartialContent &&
                    response.Content.Headers.ContentRange?.From != offset)
                {
                    throw new InvalidDataException("The archive server returned a partial response from an unexpected offset.");
                }

                if (offset > 0 && (response.StatusCode != HttpStatusCode.PartialContent ||
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
