// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

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
    private const int MaximumReadResumes = 3;
    private Stream? _stream;
    private long? _length;
    private long _burst = long.MaxValue;
    private double _refill;
    private long _downloaded;
    private int _readResumes;
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

        while (true)
        {
            Memory<byte> limited = await LimitToQuotaAsync(buffer, cancellationToken).ConfigureAwait(false);
            try
            {
                int read = await _stream!.ReadAsync(limited, cancellationToken).ConfigureAwait(false);
                if (read > 0)
                {
                    Position += read;
                    _downloaded += read;
                    metrics.ArchiveBytes.Add(read, new KeyValuePair<string, object?>("server", AtProtoJetstream.ArchiveServerTag(service)));
                    return read;
                }
            }
            catch (IOException) when (!cancellationToken.IsCancellationRequested && _readResumes < MaximumReadResumes)
            {
                // An interrupted body can throw instead of returning zero. Resume from the last successful read.
            }

            if (++_readResumes > MaximumReadResumes)
            {
                throw new InvalidDataException("The archive response ended repeatedly before the requested block was complete.");
            }

            await _stream!.DisposeAsync().ConfigureAwait(false);
            _stream = null;
            await OpenAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Memory<byte>> LimitToQuotaAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (_burst == long.MaxValue)
        {
            return buffer;
        }

        while (true)
        {
            double available = _burst + _refill * _elapsed.Elapsed.TotalSeconds - _downloaded;
            if (available >= 1)
            {
                return buffer[..(int)Math.Min(buffer.Length, Math.Min(int.MaxValue, Math.Floor(available)))];
            }

            if (_refill <= 0)
            {
                throw new InvalidDataException("The archive quota has no available bytes or refill rate.");
            }

            await Task.Delay(TimeSpan.FromSeconds((1 - available) / _refill), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task OpenAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            string? resumeEtag = Position == 0 ? null : _etag;
            AtProtoHttpResult<Stream> response = blockIndex is int index
                ? await AtProtoServer.GetBlock(name, index, service, apiKey, httpClient,
                    Position, resumeEtag, cancellationToken: cancellationToken).ConfigureAwait(false)
                : await AtProtoServer.GetSegment(name, service, apiKey, httpClient,
                    Position, resumeEtag, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                metrics.ArchiveRateLimits.Add(1, new KeyValuePair<string, object?>("server", AtProtoJetstream.ArchiveServerTag(service)));
                TimeSpan? retry = GetRetryAfter(response.HttpResponseHeaders);

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

                if (response.StatusCode == HttpStatusCode.NotFound &&
                    response.AtErrorDetail?.Error is not ("SegmentNotFound" or "BlockNotFound"))
                {
                    throw new InvalidOperationException("The configured Jetstream host does not serve the v2 archive API.");
                }

                throw new HttpRequestException(
                    $"Jetstream archive download failed: {(int)response.StatusCode} {response.AtErrorDetail?.Error} {response.AtErrorDetail?.Message}",
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
                _refill = (double)refillBytes / seconds;
            }

            _stream = stream;
            return;
        }
    }

    internal static TimeSpan? GetRetryAfter(HttpResponseHeaders? headers)
    {
        RetryConditionHeaderValue? retry = headers?.RetryAfter;
        return retry?.Delta ?? (retry?.Date is DateTimeOffset date ? date - DateTimeOffset.UtcNow : null);
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
