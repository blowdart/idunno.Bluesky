// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.Net;
using System.Runtime.CompilerServices;

using idunno.AtProto.Jetstream.Archive;

namespace idunno.AtProto.Jetstream;

public partial class AtProtoJetstream
{
    internal static string ArchiveServerTag(Uri service) =>
        new UriBuilder(service.Scheme, service.IdnHost, service.Port).Uri.GetLeftPart(UriPartial.Authority);

    /// <summary>
    /// Reads a bounded, decoded snapshot of the sealed Jetstream archive.
    /// </summary>
    /// <param name="request">The event and sequence filters.</param>
    /// <param name="checkpoint">An optional previously persisted position, including its original pinned sealed tip.</param>
    /// <param name="onCheckpoint">A synchronous callback invoked after the preceding events have been consumed by the enumerator.</param>
    /// <param name="cancellationToken">A cancellation token for planning, downloads, retries and decoding.</param>
    /// <returns>An at-least-once stream of typed Jetstream events.</returns>
    /// <exception cref="ArgumentNullException">The request is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The request or checkpoint has invalid bounds or filters.</exception>
    /// <exception cref="InvalidOperationException">No API key is configured or a v1 service is selected.</exception>
    /// <exception cref="InvalidDataException">An archive response or checkpoint is inconsistent.</exception>
    /// <exception cref="HttpRequestException">The archive request fails or is rate limited without retry instructions.</exception>
    /// <remarks><para>When resuming, persist checkpoints only after applying the preceding events. A changed segment
    /// checksum restarts that segment; its events can be delivered again. Collection filters do not suppress account,
    /// identity or sync markers. Filter lists are copied when this method is called; later changes to the caller's
    /// lists do not affect the snapshot or its checkpoints.</para></remarks>
    public IAsyncEnumerable<JetstreamEvent> SnapshotAsync(
        SnapshotRequest request,
        SnapshotCheckpoint? checkpoint = null,
        Action<SnapshotCheckpoint>? onCheckpoint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Options.ProtocolVersion != JetstreamProtocolVersion.V2)
        {
            throw new InvalidOperationException("The archive API requires a Jetstream v2 service.");
        }

        string key = Options.ApiKey ?? throw new InvalidOperationException(
            "Jetstream archive access requires an API key. Configure JetstreamOptions.ApiKey or AtProtoJetstreamBuilder.WithApiKey().");
        SnapshotRequest capturedRequest = request.SnapshotFilters();
        ValidateSnapshotRequest(capturedRequest, checkpoint, _uri);
        return SnapshotCoreAsync(capturedRequest, checkpoint, onCheckpoint, key, capturedRequest.Fingerprint(_uri), cancellationToken);
    }

    private async IAsyncEnumerable<JetstreamEvent> SnapshotCoreAsync(
        SnapshotRequest request,
        SnapshotCheckpoint? checkpoint,
        Action<SnapshotCheckpoint>? onCheckpoint,
        string key,
        string fingerprint,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long after = checkpoint?.PlanAfterSeq ?? request.AfterSeq ?? 0;
        long? pinned = checkpoint?.SealedTipSeq;
        SnapshotCheckpoint? position = checkpoint;
        bool seeking = checkpoint?.SegmentName is not null;
        HashSet<string>? dids = request.Dids is { Count: > 0 }
            ? request.Dids.Select(did => did.Value).ToHashSet(StringComparer.Ordinal) : null;
        HashSet<string>? collections = request.Collections is { Count: > 0 }
            ? request.Collections.Where(collection => !collection.IsWildcard)
                .Select(collection => collection.ToString()).ToHashSet(StringComparer.Ordinal) : null;
        string[] wildcardPrefixes = request.Collections is { Count: > 0 }
            ? request.Collections.Where(collection => collection.IsWildcard)
                .Select(collection => collection.ToString()[..^1]).ToArray() : [];

        if (pinned is long sealedTip && after >= sealedTip && !seeking)
        {
            onCheckpoint?.Invoke(position!);
            yield break;
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AtProtoHttpResult<SnapshotPlan> result;
            while (true)
            {
                result = await AtProtoServer.PlanSnapshot(
                    request with { AfterSeq = after, BeforeSeq = pinned ?? request.BeforeSeq },
                    _uri, key, _httpClient, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (result.StatusCode != HttpStatusCode.TooManyRequests)
                {
                    break;
                }

                _metrics.ArchiveRateLimits.Add(1, new KeyValuePair<string, object?>("server", ArchiveServerTag(_uri)));
                TimeSpan? retry = ArchiveDownload.GetRetryAfter(result.HttpResponseHeaders);
                TimeSpan delay = retry ?? throw new HttpRequestException(
                    "The Jetstream archive planner was rate limited without a Retry-After header.",
                    null, result.StatusCode);

                await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero,
                    cancellationToken).ConfigureAwait(false);
            }
            ThrowOnArchiveError(result);
            SnapshotPlan plan = result.Result ?? throw new InvalidDataException("The archive planner returned no plan.");
            pinned ??= plan.SealedTipSeq;
            if (plan.SealedTipSeq < 0 || plan.PlannedThroughSeq < 0 ||
                plan.SealedTipSeq != pinned ||
                (request.BeforeSeq is long before && plan.SealedTipSeq > before) ||
                (after >= pinned
                    ? plan.Segments.Count != 0 || plan.PlannedThroughSeq < pinned || plan.PlannedThroughSeq > after
                    : plan.PlannedThroughSeq <= after))
            {
                throw new InvalidDataException("The archive planner returned a non-progressing or unpinned page.");
            }

            _metrics.ArchivePlans.Add(1, new KeyValuePair<string, object?>("server", ArchiveServerTag(_uri)));
            if (position is null)
            {
                position = new SnapshotCheckpoint { SealedTipSeq = pinned.Value, PlanAfterSeq = after, RequestFingerprint = fingerprint };
                onCheckpoint?.Invoke(position);
            }

            bool foundResumeSegment = !seeking;
            foreach (PlannedSegment segment in plan.Segments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (seeking && segment.Name != checkpoint!.SegmentName)
                {
                    continue;
                }

                foundResumeSegment = true;
                seeking = false;
                bool sameGeneration = segment.Name == checkpoint?.SegmentName &&
                    string.Equals(segment.Checksum, checkpoint.SegmentChecksum, StringComparison.Ordinal);
                int startBlock = sameGeneration ? checkpoint!.NextBlockIndex : 0;
                long startOffset = sameGeneration ? checkpoint!.NextByteOffset : 0;
                if (segment.Mode == "blocks")
                {
                    if (segment.Blocks is not { Count: > 0 })
                    {
                        throw new InvalidDataException("A blocks plan has no block ranges.");
                    }

                    if (sameGeneration && startBlock > segment.Blocks.Max(range => range.Last) + (long)1)
                    {
                        throw new InvalidDataException("The checkpoint block index exceeds the planned segment.");
                    }

                    foreach (BlockRange range in segment.Blocks)
                    {
                        if (range.First < 0 || range.Last < range.First)
                        {
                            throw new InvalidDataException("An archive block range is invalid.");
                        }

                        for (int block = Math.Max(range.First, startBlock); block <= range.Last; block++)
                        {
                            byte[] frame = await DownloadFrame(segment.Name, block, segment.Checksum, key, cancellationToken).ConfigureAwait(false);
                            foreach (JssRow row in JssBlockReader.Decode(frame))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (MatchesSnapshot(row, request, after, plan.PlannedThroughSeq, pinned.Value, dids, collections, wildcardPrefixes))
                                {
                                    JetstreamEvent decodedEvent = row.ToEvent();
                                    _metrics.ArchiveEvents.Add(1, new KeyValuePair<string, object?>("server", ArchiveServerTag(_uri)));
                                    yield return decodedEvent;
                                }
                                else
                                {
                                    _metrics.ArchiveFilteredEvents.Add(1, new KeyValuePair<string, object?>("server", ArchiveServerTag(_uri)));
                                }
                            }

                            _metrics.ArchiveBlocks.Add(1, new KeyValuePair<string, object?>("server", ArchiveServerTag(_uri)));
                            position = new SnapshotCheckpoint
                            {
                                SealedTipSeq = pinned.Value, PlanAfterSeq = after,
                                RequestFingerprint = fingerprint,
                                SegmentName = segment.Name, SegmentChecksum = segment.Checksum,
                                NextBlockIndex = checked(block + 1)
                            };
                            onCheckpoint?.Invoke(position);
                        }
                    }
                }
                else if (segment.Mode == "segment")
                {
                    long offset = startOffset > 0 ? startOffset : 0;
                    ArchiveDownload download = new(
                        segment.Name, null, segment.Checksum, key, _uri, _httpClient, offset, _metrics,
                        Options.ArchiveReadTimeout, allowCrossOriginRedirect: _serviceProvider is not null);
                    await using ConfiguredAsyncDisposable disposal = download.ConfigureAwait(false);
                    if (offset == 0)
                    {
                        byte[] header = new byte[256];
                        await download.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
                        int blockCount = ReadSegmentBlockCount(header);
                        if (sameGeneration && startBlock > blockCount)
                        {
                            throw new InvalidDataException("The checkpoint block index exceeds the segment header.");
                        }

                        for (int block = 0; block < blockCount; block++)
                        {
                            await foreach (JssRow row in ReadSegmentBlock(download, cancellationToken).ConfigureAwait(false))
                            {
                                if (MatchesSnapshot(row, request, after, plan.PlannedThroughSeq, pinned.Value, dids, collections, wildcardPrefixes))
                                {
                                    JetstreamEvent decodedEvent = row.ToEvent();
                                    _metrics.ArchiveEvents.Add(1, new KeyValuePair<string, object?>("server", ArchiveServerTag(_uri)));
                                    yield return decodedEvent;
                                }
                                else
                                {
                                    _metrics.ArchiveFilteredEvents.Add(1, new KeyValuePair<string, object?>("server", ArchiveServerTag(_uri)));
                                }
                            }

                            _metrics.ArchiveBlocks.Add(1, new KeyValuePair<string, object?>("server", ArchiveServerTag(_uri)));
                            position = new SnapshotCheckpoint
                            {
                                SealedTipSeq = pinned.Value, PlanAfterSeq = after,
                                RequestFingerprint = fingerprint,
                                SegmentName = segment.Name, SegmentChecksum = segment.Checksum,
                                NextBlockIndex = block + 1, NextByteOffset = download.Position
                            };
                            onCheckpoint?.Invoke(position);
                        }
                    }
                    else
                    {
                        // Resume by reading the block count from the header in a separate ranged request.
                        int blockCount = await GetSegmentBlockCount(
                            segment.Name, segment.Checksum, key, cancellationToken).ConfigureAwait(false);
                        if (startBlock > blockCount)
                        {
                            throw new InvalidDataException("The checkpoint block index exceeds the segment header.");
                        }

                        for (int block = startBlock; block < blockCount; block++)
                        {
                            await foreach (JssRow row in ReadSegmentBlock(download, cancellationToken).ConfigureAwait(false))
                            {
                                if (MatchesSnapshot(row, request, after, plan.PlannedThroughSeq, pinned.Value, dids, collections, wildcardPrefixes))
                                {
                                    JetstreamEvent decodedEvent = row.ToEvent();
                                    _metrics.ArchiveEvents.Add(1, new KeyValuePair<string, object?>("server", ArchiveServerTag(_uri)));
                                    yield return decodedEvent;
                                }
                                else
                                {
                                    _metrics.ArchiveFilteredEvents.Add(1, new KeyValuePair<string, object?>("server", ArchiveServerTag(_uri)));
                                }
                            }

                            _metrics.ArchiveBlocks.Add(1, new KeyValuePair<string, object?>("server", ArchiveServerTag(_uri)));
                            position = new SnapshotCheckpoint
                            {
                                SealedTipSeq = pinned.Value, PlanAfterSeq = after,
                                RequestFingerprint = fingerprint,
                                SegmentName = segment.Name, SegmentChecksum = segment.Checksum,
                                NextBlockIndex = block + 1, NextByteOffset = download.Position
                            };
                            onCheckpoint?.Invoke(position);
                        }
                    }
                }
                else
                {
                    throw new InvalidDataException($"Unknown archive download mode '{segment.Mode}'.");
                }

                _metrics.ArchiveSegments.Add(1, new KeyValuePair<string, object?>("server", ArchiveServerTag(_uri)));
                checkpoint = null;
            }

            if (!foundResumeSegment)
            {
                throw new InvalidDataException("The checkpoint segment is missing from its plan page.");
            }

            if (plan.PlannedThroughSeq >= pinned)
            {
                break;
            }

            after = plan.PlannedThroughSeq;
            position = new SnapshotCheckpoint { SealedTipSeq = pinned.Value, PlanAfterSeq = after, RequestFingerprint = fingerprint };
            onCheckpoint?.Invoke(position);
        }
    }

    private static async IAsyncEnumerable<JssRow> ReadSegmentBlock(
        ArchiveDownload download, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        byte[] lengthBytes = new byte[8];
        await download.ReadExactlyAsync(lengthBytes, cancellationToken).ConfigureAwait(false);
        ulong length = BinaryPrimitives.ReadUInt64LittleEndian(lengthBytes);
        if (length is 0 or > JssBlockReader.MaximumFrameSize)
        {
            throw new InvalidDataException("The archive block length is invalid.");
        }

        byte[] frame = new byte[checked((int)length)];
        await download.ReadExactlyAsync(frame, cancellationToken).ConfigureAwait(false);
        foreach (JssRow row in JssBlockReader.Decode(frame))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    private async Task<byte[]> DownloadFrame(
        string name, int index, string checksum, string key, CancellationToken cancellationToken)
    {
        ArchiveDownload download = new(name, index, checksum, key, _uri, _httpClient, 0, _metrics,
            Options.ArchiveReadTimeout, allowCrossOriginRedirect: _serviceProvider is not null);
        await using ConfiguredAsyncDisposable disposal = download.ConfigureAwait(false);
        return await download.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> GetSegmentBlockCount(
        string name, string checksum, string key, CancellationToken cancellationToken)
    {
        ArchiveDownload download = new(name, null, checksum, key, _uri, _httpClient, 0, _metrics,
            Options.ArchiveReadTimeout, allowCrossOriginRedirect: _serviceProvider is not null);
        await using ConfiguredAsyncDisposable disposal = download.ConfigureAwait(false);
        byte[] header = new byte[256];
        await download.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        return ReadSegmentBlockCount(header);
    }

    private static int ReadSegmentBlockCount(ReadOnlySpan<byte> header)
    {
        if (!header[..4].SequenceEqual("jss0"u8) ||
            BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(12, 2)) != 1)
        {
            throw new InvalidDataException("The archive segment header is invalid.");
        }

        uint blockCount = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(14, 4));
        if (blockCount is 0 or > int.MaxValue)
        {
            throw new InvalidDataException("The archive segment block count is invalid.");
        }

        return (int)blockCount;
    }

    private static bool MatchesSnapshot(JssRow row, SnapshotRequest request, long after, long through, long tip,
        HashSet<string>? dids, HashSet<string>? collections, string[] wildcardPrefixes)
    {
        if (row.Seq <= after || row.Seq > through || row.Seq > tip ||
            (request.BeforeSeq is long before && row.Seq > before) ||
            (dids is not null && !dids.Contains(row.Did)) ||
            (request.Kinds is { Count: > 0 } && !request.Kinds.Contains(row.EventKind)))
        {
            return false;
        }

        return row.EventKind != JetStreamEventKind.Commit ||
            request.Collections is not { Count: > 0 } ||
            (collections is not null && collections.Contains(row.Collection) ||
             wildcardPrefixes.Any(prefix => row.Collection.StartsWith(prefix, StringComparison.Ordinal)));
    }

    private static void ValidateSnapshotRequest(SnapshotRequest request, SnapshotCheckpoint? checkpoint, Uri service)
    {
        if (request.AfterSeq < 0 || request.BeforeSeq < 0 ||
            (request.AfterSeq is long after && request.BeforeSeq is long before && after > before) ||
            request.Dids?.Count > MaximumV2Dids ||
            request.Collections?.Count > MaximumV2Collections || request.Kinds?.Count > 4 ||
            (request.Collections is { Count: > 0 } && request.Kinds is { Count: > 0 } &&
             !request.Kinds.Contains(JetStreamEventKind.Commit)) ||
            (request.Kinds?.Any(kind => kind is JetStreamEventKind.Unknown || !Enum.IsDefined(kind)) ?? false))
        {
            throw new ArgumentException("The snapshot request has invalid bounds or filters.", nameof(request));
        }

        if (checkpoint is not null && (checkpoint.RequestFingerprint != request.Fingerprint(service) ||
            checkpoint.ReplayAfterSeq is not null || checkpoint.PlanAfterSeq < (request.AfterSeq ?? 0) ||
            checkpoint.SealedTipSeq < 0 || checkpoint.PlanAfterSeq < 0 ||
            (checkpoint.PlanAfterSeq > checkpoint.SealedTipSeq &&
             (checkpoint.SegmentName is not null || checkpoint.NextBlockIndex != 0 || checkpoint.NextByteOffset != 0)) ||
            checkpoint.NextByteOffset < 0 ||
            checkpoint.NextBlockIndex < 0 || checkpoint.LiveAfterSeq is not null || (request.BeforeSeq is not null &&
            request.BeforeSeq < checkpoint.SealedTipSeq)))
        {
            throw new ArgumentException("The snapshot checkpoint has invalid bounds.", nameof(checkpoint));
        }
    }

    private static void ThrowOnArchiveError<T>(AtProtoHttpResult<T> result)
    {
        if (result.Succeeded)
        {
            return;
        }

        if (result.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException("The configured Jetstream archive API key was rejected.");
        }

        if (result.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("The configured Jetstream host does not serve the v2 archive API.");
        }

        throw new HttpRequestException(
            $"Jetstream archive request failed: {(int)result.StatusCode} {result.AtErrorDetail?.Error} {result.AtErrorDetail?.Message}",
            null, result.StatusCode);
    }
}
