// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text.Json;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    [SuppressMessage(
        "Major Code Smell",
        "S108:Nested blocks of code should not be left empty",
        Justification = "Malformed JSON error bodies are still reported through the retained raw response content.")]
    private static async Task<AtProtoHttpResult<Stream>> GetStreamingResponse(
        Uri requestUri,
        string acceptMediaType,
        HttpClient httpClient,
        int maximumResponseSize,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptMediaType));

        HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                string responseContent = await HttpContentReader.ReadAsStringTruncating(
                    response.Content,
                    maximumResponseSize,
                    cancellationToken).ConfigureAwait(false);
                AtErrorDetail errorDetail = new()
                {
                    Instance = requestUri,
                    HttpMethod = HttpMethod.Get,
                    RawContent = responseContent
                };

                if (response.Content.Headers.ContentType?.MediaType?.Equals("application/json", StringComparison.OrdinalIgnoreCase) == true &&
                    responseContent.Length > 0)
                {
                    try
                    {
                        AtErrorDetail? parsedError = JsonSerializer.Deserialize(
                            responseContent,
                            SourceGenerationContext.Default.AtErrorDetail);

                        if (parsedError is not null)
                        {
                            errorDetail.Error = parsedError.Error;
                            errorDetail.Message = parsedError.Message;
                            errorDetail.ExtensionData = parsedError.ExtensionData;
                        }
                    }
                    catch (JsonException)
                    {
                    }
                }

                return new AtProtoHttpResult<Stream>(
                    result: null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.Headers,
                    atErrorDetail: errorDetail);
            }
        }

        try
        {
            Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return new AtProtoHttpResult<Stream>(
                result: new ResponseOwnedStream(responseStream, response),
                statusCode: response.StatusCode,
                httpResponseHeaders: response.Headers);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private sealed class ResponseOwnedStream(Stream stream, HttpResponseMessage response) : Stream
    {
        private readonly Stream _stream = stream;
        private readonly HttpResponseMessage _response = response;
        private bool _disposed;

        public HttpContentHeaders ContentHeaders => _response.Content.Headers;

        public override bool CanRead => !_disposed && _stream.CanRead;
        public override bool CanSeek => !_disposed && _stream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _stream.Length;
        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _stream.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            return _stream.Read(buffer);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _stream.ReadAsync(buffer, cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                try
                {
                    _stream.Dispose();
                }
                finally
                {
                    _response.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            try
            {
                if (!_disposed)
                {
                    _disposed = true;
                    await _stream.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                _response.Dispose();
                await base.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
