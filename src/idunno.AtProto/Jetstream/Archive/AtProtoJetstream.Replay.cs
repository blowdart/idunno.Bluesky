// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using idunno.AtProto.Jetstream.Archive;

namespace idunno.AtProto.Jetstream;

public partial class AtProtoJetstream
{
    private const string OutdatedLiveCursorMessage = "The live Jetstream cursor is outside the server's retained events.";

    /// <summary>
    /// Replays the sealed archive and then continues from its pinned tip on the live v2 stream.
    /// </summary>
    /// <param name="request">The filters and initial exclusive starting sequence.</param>
    /// <param name="checkpoint">A saved archive or live checkpoint, if resuming an interrupted replay.</param>
    /// <param name="onCheckpoint">A callback for durably persistable progress after preceding events are consumed.</param>
    /// <param name="cancellationToken">A cancellation token for the archive and live stream.</param>
    /// <param name="onArchiveError">An optional callback that chooses whether an invalid archive record or block stops or is skipped.</param>
    /// <returns>An at-least-once sequence of decoded archive and live events.</returns>
    /// <exception cref="ArgumentNullException">The request is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The request or replay checkpoint has invalid bounds or filters.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The archive error callback returns an action that is not valid for the failure.</exception>
    /// <exception cref="InvalidOperationException">A v2 service or archive API key is not configured.</exception>
    /// <exception cref="ObjectDisposedException">The Jetstream client has been disposed.</exception>
    /// <exception cref="InvalidDataException">An archive plan or response is inconsistent, including a download ETag mismatch.</exception>
    /// <remarks><para>Without <paramref name="onArchiveError"/>, or when it returns <see cref="JetstreamArchiveErrorAction.Stop"/>,
    /// an invalid matching archive record or block stops replay. The callback receives the record sequence for record failures,
    /// or <see langword="null"/> for a block failure, and the original decoding or row-validation exception, which can be an <see cref="InvalidDataException"/>,
    /// <see cref="System.Text.Json.JsonException"/>, <see cref="ArgumentException"/>,
    /// <see cref="NsidFormatException"/>, <see cref="RecordKeyFormatException"/>, <see cref="OverflowException"/>,
    /// <see cref="System.Text.DecoderFallbackException"/>, or <see cref="ZstdSharp.ZstdException"/>.
    /// Returning <see cref="JetstreamArchiveErrorAction.SkipRecord"/> omits only the failing record and continues the block.
    /// Returning <see cref="JetstreamArchiveErrorAction.SkipBlock"/> omits the failing record and the rest of its block,
    /// or the entire block when the error occurred during block decoding. A live cursor
    /// is inclusive. When its lookback is exhausted after a long backfill or outage,
    /// replay returns to the archive from the last delivered sequence before reconnecting. Filter lists are copied
    /// when this method is called, so later changes to the caller's lists do not affect replay or its checkpoints.</para>
    /// <para>Archive segment generations can change after planning or during a download. An ETag mismatch stops replay
    /// with <see cref="InvalidDataException"/>; it is not automatically retried or passed to <paramref name="onArchiveError"/>.
    /// Recovery does not require restarting the application or replacing the client: start a new replay enumeration with
    /// the original request, with its sequence bounds and filters unchanged, and latest durably persisted checkpoint.
    /// Pass the entire checkpoint unchanged, including its request fingerprint, pinned tip, plan and replay cursors,
    /// segment name and checksum, next block index, byte offset and live cursor.
    /// When the fresh archive plan reports a changed checksum, the affected segment starts again and events may repeat.
    /// Make event processing idempotent, delay between bounded retries and surface persistent mismatches.
    /// Do not retry every <see cref="InvalidDataException"/>, because corrupt data and other inconsistencies use the same type.</para></remarks>
    [SuppressMessage("Design", "CA1068:Method should take CancellationToken as the last parameter",
        Justification = "Appending the optional callback preserves the existing positional cancellationToken argument.")]
    public IAsyncEnumerable<JetstreamEvent> ReplayAsync(
        SnapshotRequest request,
        SnapshotCheckpoint? checkpoint = null,
        Action<SnapshotCheckpoint>? onCheckpoint = null,
        CancellationToken cancellationToken = default,
        Func<long?, Exception, JetstreamArchiveErrorAction>? onArchiveError = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Options.ProtocolVersion != JetstreamProtocolVersion.V2 || Options.ApiKey is null)
        {
            throw new InvalidOperationException("Jetstream replay requires a v2 service and an archive API key.");
        }

        SnapshotRequest capturedRequest = request.SnapshotFilters();
        ValidateSnapshotRequest(capturedRequest, null, _uri);
        if (checkpoint is not null && (checkpoint.RequestFingerprint != capturedRequest.Fingerprint(_uri) ||
            checkpoint.ReplayAfterSeq < (capturedRequest.AfterSeq ?? 0) ||
            (checkpoint.LiveAfterSeq is null && checkpoint.ReplayAfterSeq is null &&
             checkpoint.PlanAfterSeq < (capturedRequest.AfterSeq ?? 0))))
        {
            throw new ArgumentException("The replay checkpoint belongs to a different request or service.", nameof(checkpoint));
        }

        if (checkpoint?.LiveAfterSeq is long liveAfterSeq &&
            (liveAfterSeq < checkpoint.SealedTipSeq || checkpoint.PlanAfterSeq < 0 ||
             (checkpoint.PlanAfterSeq > checkpoint.SealedTipSeq &&
              (checkpoint.SegmentName is not null || checkpoint.NextBlockIndex != 0 || checkpoint.NextByteOffset != 0)) ||
             (checkpoint.ReplayAfterSeq is null &&
             capturedRequest.BeforeSeq is not null &&
             capturedRequest.BeforeSeq < checkpoint.SealedTipSeq)))
        {
            throw new ArgumentException("The live replay checkpoint has invalid bounds.", nameof(checkpoint));
        }

        return ReplayCoreAsync(capturedRequest, checkpoint, onCheckpoint, onArchiveError, cancellationToken);
    }

    private async IAsyncEnumerable<JetstreamEvent> ReplayCoreAsync(
        SnapshotRequest request,
        SnapshotCheckpoint? checkpoint,
        Action<SnapshotCheckpoint>? onCheckpoint,
        Func<long?, Exception, JetstreamArchiveErrorAction>? onArchiveError,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long lastDelivered = request.AfterSeq ?? 0;
        SnapshotCheckpoint? resume = checkpoint;
        SnapshotRequest currentRequest = checkpoint?.ReplayAfterSeq is long replayAfter
            ? request with { AfterSeq = replayAfter, BeforeSeq = null }
            : request;
        string fingerprint = request.Fingerprint(_uri);

        while (true)
        {
            long? tip = resume?.SealedTipSeq;
            if (resume?.LiveAfterSeq is long liveAfterSeq)
            {
                lastDelivered = Math.Max(lastDelivered, liveAfterSeq);
                resume = null;
            }
            else
            {
                SnapshotCheckpoint? archiveResume = resume is null ? null : resume with
                {
                    RequestFingerprint = currentRequest.Fingerprint(_uri),
                    ReplayAfterSeq = null
                };
                await foreach (JetstreamEvent item in SnapshotAsync(
                    currentRequest, archiveResume, progress =>
                    {
                        tip = progress.SealedTipSeq;
                        onCheckpoint?.Invoke(progress with
                        {
                            RequestFingerprint = fingerprint,
                            ReplayAfterSeq = currentRequest == request ? null : currentRequest.AfterSeq
                        });
                    }, cancellationToken, onArchiveError).ConfigureAwait(false))
                {
                    if (item.Sequence is long sequence)
                    {
                        lastDelivered = Math.Max(lastDelivered, sequence);
                    }

                    yield return item;
                }
            }

            if (tip is null)
            {
                throw new InvalidDataException("The archive snapshot completed without a sealed tip.");
            }

            bool restartFromArchive = false;
            while (true)
            {
                _metrics.ReplayHandoffs.Add(1, new KeyValuePair<string, object?>("server", ArchiveTag));
                IAsyncEnumerator<JetstreamEvent> live = LiveEventsAsync(
                    currentRequest, Math.Max(tip.Value, lastDelivered), cancellationToken).GetAsyncEnumerator(cancellationToken);
                await using ConfiguredAsyncDisposable liveDisposal = live.ConfigureAwait(false);
                while (true)
                {
                    bool hasEvent;
                    try
                    {
                        hasEvent = await live.MoveNextAsync().ConfigureAwait(false);
                    }
                    catch (JetstreamConnectionException ex) when (ex.ErrorDetail?.Error == "CursorTooOld")
                    {
                        JetStreamLogger.ReplayRecovering(_logger, "Expired cursor; archive fallback", lastDelivered, ex);
                        restartFromArchive = true;
                        break;
                    }
                    catch (InvalidDataException ex) when (ex.Message == OutdatedLiveCursorMessage)
                    {
                        JetStreamLogger.ReplayRecovering(_logger, "Expired cursor; archive fallback", lastDelivered, ex);
                        restartFromArchive = true;
                        break;
                    }
                    catch (JetstreamConnectionException ex) when (!cancellationToken.IsCancellationRequested &&
                        ex.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
                            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
                    {
                        JetStreamLogger.ReplayRecovering(_logger, "Transient upgrade failure", lastDelivered, ex);
                        break;
                    }
                    catch (IOException ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        JetStreamLogger.ReplayRecovering(_logger, "Live stream failure", lastDelivered, ex);
                        break;
                    }
                    catch (WebSocketException ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        JetStreamLogger.ReplayRecovering(_logger, "Transport failure", lastDelivered, ex);
                        break;
                    }
                    catch (HttpRequestException ex) when (!cancellationToken.IsCancellationRequested &&
                        (ex.StatusCode is null or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
                            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout))
                    {
                        JetStreamLogger.ReplayRecovering(_logger, "HTTP transport failure", lastDelivered, ex);
                        break;
                    }

                    if (!hasEvent)
                    {
                        yield break;
                    }

                    JetstreamEvent item = live.Current;
                    if (item.Sequence is not long sequence || sequence <= lastDelivered || sequence <= tip)
                    {
                        continue;
                    }

                    lastDelivered = sequence;
                    yield return item;
                    onCheckpoint?.Invoke(new SnapshotCheckpoint
                    {
                        SealedTipSeq = tip.Value,
                        PlanAfterSeq = currentRequest.AfterSeq ?? 0,
                        RequestFingerprint = fingerprint,
                        ReplayAfterSeq = currentRequest == request ? null : currentRequest.AfterSeq,
                        LiveAfterSeq = lastDelivered
                    });
                }

                if (restartFromArchive)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), Options.TimeProvider, cancellationToken).ConfigureAwait(false);
            }

            if (restartFromArchive)
            {
                resume = null;
                currentRequest = request with { AfterSeq = lastDelivered, BeforeSeq = null };
            }
        }
    }

    private async IAsyncEnumerable<JetstreamEvent> LiveEventsAsync(
        SnapshotRequest request,
        long cursor,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Channel<JetstreamEvent> channel = Channel.CreateBounded<JetstreamEvent>(
            new BoundedChannelOptions(1024) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
        int outdatedCursor = 0;
        int cleaningUp = 0;
        long lastReceived = cursor;
        AtProtoJetstream live = new(
            uri: _uri,
            options: Options with { MaximumConcurrentMessageParsers = 1 },
            webSocketOptions: WebSocketOptions,
            collections: request.Collections?.ToArray(),
            dids: request.Dids?.ToArray());
        await using ConfiguredAsyncDisposable liveDisposal = live.ConfigureAwait(false);
        void Complete(string reason, Exception exception)
        {
            if (channel.Writer.TryComplete(exception))
            {
                JetStreamLogger.ReplayLiveEnded(_logger, reason, cursor, Interlocked.Read(ref lastReceived), channel.Reader.Count, exception);
            }
        }

        void CompleteAfterParsers(string reason, Exception exception)
        {
#pragma warning disable CS4014 // Completion waits for records received before the terminal signal.
            live.DrainMessageParsersAsync(CancellationToken.None).ContinueWith(
                completed => Complete(completed.IsFaulted ? "Parser drain failure" : reason,
                    completed.Exception?.GetBaseException() ?? exception),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
#pragma warning restore CS4014
        }

        if (request.Kinds is { Count: > 0 })
        {
            live.KindFilter = [.. request.Kinds];
        }

        live.RecordReceived += (_, args) =>
        {
            if (Volatile.Read(ref outdatedCursor) != 0)
            {
                return;
            }

            if (args.ParsedEvent is not JetstreamEvent { Sequence: long sequence } parsed)
            {
                Complete("Invalid event", new InvalidDataException("The live Jetstream event has no sequence cursor."));
            }
            else
            {
                Interlocked.Exchange(ref lastReceived, sequence);
                if (!channel.Writer.TryWrite(parsed))
                {
                    Complete("Buffer overflow", new IOException("The replay consumer fell behind the live stream."));
                }
            }
        };
        live.ConnectionStateChanged += (_, args) =>
        {
            if (Volatile.Read(ref cleaningUp) == 0 && args.State is WebSocketState.Closed or WebSocketState.Aborted)
            {
                CompleteAfterParsers(live.DisconnectedGracefully ? "Remote close" : "Transport disconnect",
                    new IOException("The live Jetstream disconnected."));
            }
        };
        live.FaultRaised += (_, args) =>
        {
            string reason = (args.Error, live.State) switch
            {
                (not null, _) => "Server error",
                (_, WebSocketState.Aborted) => "Transport failure",
                _ => "Receive failure"
            };
            CompleteAfterParsers(reason,
                new IOException($"The live Jetstream reported an error: {ForLogging(args.Fault)}"));
        };
        live.InfoReceived += (_, args) =>
        {
            if (args.Name == "OutdatedCursor")
            {
                Interlocked.Exchange(ref outdatedCursor, 1);
                Complete("Expired cursor", new InvalidDataException(OutdatedLiveCursorMessage));
            }
        };

        try
        {
            await live.ConnectAsync(uri: null, cursor: cursor, httpClient: _httpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await foreach (JetstreamEvent item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (Volatile.Read(ref outdatedCursor) != 0)
                {
                    throw new InvalidDataException(OutdatedLiveCursorMessage);
                }

                yield return item;
            }
        }
        finally
        {
            Interlocked.Exchange(ref cleaningUp, 1);
            JetStreamLogger.ReplayLiveCleanup(_logger, cancellationToken.IsCancellationRequested ? "Cancellation" : "Enumeration disposal", cursor);
        }
    }
}
