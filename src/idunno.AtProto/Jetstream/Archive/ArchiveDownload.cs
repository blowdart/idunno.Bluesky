// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace idunno.AtProto.Jetstream.Archive;

internal sealed class ArchiveDownload(
    string name,
    int? blockIndex,
    string checksum,
    string apiKey,
    Uri service,
    HttpClient httpClient,
    long offset,
    JetstreamMetrics metrics) : IAsyncDisposable
{
    private Stream? _stream;
    private long? _length;
    private long _burst = long.MaxValue;
    private long _refill;
    private long _downloaded;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly string _etag = blockIndex is int index ? $"\"{checksum}:{index}\"" : $"\"{checksum}\"";

    internal long Position { get; private set; } = offset;

    internal async Task ReadExactlyAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (!buffer.IsEmpty)
        {
            int read = await ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new InvalidDataException("The archive response ended before the requested block was complete.");
            }

            buffer = buffer[read..];
        }
    }

    internal async Task<byte[]> ReadFrameAsync(CancellationToken cancellationToken)
    {
        await OpenAsync(cancellationToken).ConfigureAwait(false);
        if (_length is not long length || length <= 0 || length > JssBlockReader.MaximumFrameSize)
        {
            throw new InvalidDataException("The archive block has no valid Content-Length.");
        }

        byte[] frame = new byte[checked((int)length)];
        await ReadExactlyAsync(frame, cancellationToken).ConfigureAwait(false);
        return frame;
    }

    private async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_stream is null)
        {
            await OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_length is long length && Position >= length)
        {
            return 0;
        }

        if (_refill > 0 && _downloaded > _burst)
        {
            double requiredSeconds = (double)(_downloaded - _burst) / _refill;
            TimeSpan wait = TimeSpan.FromSeconds(requiredSeconds) - _elapsed.Elapsed;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
        }

        int read = await _stream!.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read == 0 && (_length is null || Position < _length))
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _stream = null;
            await OpenAsync(cancellationToken).ConfigureAwait(false);
            read = await _stream!.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        Position += read;
        _downloaded += read;
        metrics.ArchiveBytes.Add(read, new KeyValuePair<string, object?>("server", AtProtoJetstream.ArchiveServerTag(service)));
        return read;
    }

    private async Task OpenAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            string? resumeEtag = Position == 0 ? null : _etag;
            AtProtoHttpResult<Stream> response = blockIndex is int index
                ? await AtProtoServer.GetBlock(name, index, service, apiKey, httpClient,
                    Position, resumeEtag, cancellationToken).ConfigureAwait(false)
                : await AtProtoServer.GetSegment(name, service, apiKey, httpClient,
                    Position, resumeEtag, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                metrics.ArchiveRateLimits.Add(1, new KeyValuePair<string, object?>("server", AtProtoJetstream.ArchiveServerTag(service)));
                TimeSpan? retry = response.HttpResponseHeaders?.RetryAfter?.Delta;
                if (retry is null && response.HttpResponseHeaders?.RetryAfter?.Date is DateTimeOffset date)
                {
                    retry = date - DateTimeOffset.UtcNow;
                }

                if (retry is null)
                {
                    throw new HttpRequestException("Jetstream archive is rate limited without a Retry-After header.",
                        null, response.StatusCode);
                }

                await Task.Delay(retry.Value > TimeSpan.Zero ? retry.Value : TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!response.Succeeded)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new InvalidOperationException("The configured Jetstream archive API key was rejected.");
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException("The configured Jetstream host does not serve the v2 archive API.");
                }

                throw new HttpRequestException(
                    $"Jetstream archive download failed: {(int)response.StatusCode} {response.AtErrorDetail?.Error}",
                    null, response.StatusCode);
            }

            AtProtoServer.ArchiveResponseStream stream = (AtProtoServer.ArchiveResponseStream)response.Result;
            if (stream.ETag != _etag)
            {
                string receivedEtag = stream.ETag ?? "(missing)";
                await stream.DisposeAsync().ConfigureAwait(false);
                throw new InvalidDataException(
                    $"Archive download ETag mismatch for segment '{name}', " +
                    $"block {(blockIndex is int block ? block.ToString(CultureInfo.InvariantCulture) : "(whole segment)")}, " +
                    $"offset {Position}: planned checksum '{checksum}', expected ETag {_etag}, " +
                    $"response ETag {receivedEtag}, HTTP {(int)response.StatusCode}. " +
                    "The segment may have been compacted since planning, or the download response may be missing its ETag.");
            }

            _length = stream.ContentLength;
            if (stream.Headers.TryGetValues("headwind-quota-burst-bytes", out IEnumerable<string>? burst) &&
                long.TryParse(burst.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out long burstBytes) &&
                burstBytes > 0)
            {
                _burst = burstBytes;
            }

            if (stream.Headers.TryGetValues("headwind-quota-refill-bytes", out IEnumerable<string>? refill) &&
                stream.Headers.TryGetValues("headwind-quota-refill-period-seconds", out IEnumerable<string>? period) &&
                long.TryParse(refill.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out long refillBytes) &&
                long.TryParse(period.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out long seconds) &&
                seconds > 0)
            {
                _refill = refillBytes / seconds;
            }

            _stream = stream;
            return;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
