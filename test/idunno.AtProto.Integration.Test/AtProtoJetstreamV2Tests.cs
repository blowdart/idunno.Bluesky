// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable CS0618 // The shared callback retains its legacy base event type.

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;

using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;

using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Archive;
using idunno.AtProto.Jetstream.Events;

using ZstdSharp;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class AtProtoJetstreamV2Tests
{
    private const string TestDid = "did:plc:g6ylltenitt4tp27bpwalh7b";
    private const string SubProtocol = "xrpc.v1.json";

    private static string IdentityEvent(long sequence) =>
        $$$"""
        {"$type":"message","payload":{"$type":"network.bsky.jetstream.subscribeEvents#identity","did":"{{{TestDid}}}","seq":{{{sequence}}},"time":"2026-09-26T00:19:35Z","identity":{"did":"{{{TestDid}}}","seq":1,"time":"2026-09-26T00:19:35Z"} } }
        """;

    [Fact]
    public async Task LiveStreamReconnectsFromLastYieldedSequenceWithoutDuplicates()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestJetstreamServer();
        await server.Start(async (socket, connection, token) =>
        {
            if (connection == 1)
            {
                await SendText(socket, IdentityEvent(41), token);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, token);
            }
            else
            {
                await SendText(socket, IdentityEvent(41), token);
                await SendText(socket, IdentityEvent(42), token);
            }
        });

        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri, options: new JetstreamOptions { UseCompression = false });
        await using IAsyncEnumerator<JetstreamEvent> stream = jetstream.StreamAsync(cursor: 40, cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await stream.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(41, stream.Current.Sequence);
        Assert.True(await stream.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(42, stream.Current.Sequence);
        Assert.Equal(2, server.Connections.Count);
        Assert.Contains("cursor=40", server.Connections.First().Query, StringComparison.Ordinal);
        Assert.Contains("cursor=41", server.Connections.Last().Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveStreamExcludesEventHandlersConnectionsAndOtherEnumerators()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        TaskCompletionSource secondConnection = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new TestJetstreamServer();
        await server.Start(async (socket, connection, token) =>
        {
            if (connection == 2)
            {
                secondConnection.TrySetResult();
            }

            await SendText(socket, IdentityEvent(41), token);
        });
        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri, options: new JetstreamOptions { UseCompression = false });
        EventHandler<RecordReceivedEventArgs> handler = (_, _) => { };
        jetstream.RecordReceived += handler;
        await using (IAsyncEnumerator<JetstreamEvent> refused = jetstream.StreamAsync(cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => refused.MoveNextAsync().AsTask());
        }

        jetstream.RecordReceived -= handler;
        await using (IAsyncEnumerator<JetstreamEvent> stream = jetstream.StreamAsync(cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken))
        {
            Assert.True(await stream.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
            Assert.Throws<InvalidOperationException>(() => jetstream.RecordReceived += handler);
            await Assert.ThrowsAsync<InvalidOperationException>(() => jetstream.ConnectAsync(cancellationToken));
            await using IAsyncEnumerator<JetstreamEvent> second = jetstream.StreamAsync(cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(() => second.MoveNextAsync().AsTask());
        }

        jetstream.RecordReceived += handler;
        await jetstream.ConnectAsync(cancellationToken);
        await secondConnection.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        Assert.Equal(2, server.Connections.Count);
        await jetstream.CloseAsync(cancellationToken: cancellationToken);
    }

    [Fact]
    public async Task LiveStreamWaitsForParsersFromClosedEventConnection()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PausedTaskScheduler scheduler = new();
        using var server = new TestJetstreamServer();
        await server.Start(async (socket, connection, token) =>
        {
            await SendText(socket, IdentityEvent(connection == 1 ? 41 : 42), token);
        });

        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri,
            options: new JetstreamOptions
            {
                UseCompression = false, MaximumConcurrentMessageParsers = 1,
                TaskFactory = new TaskFactory(scheduler)
            });
        EventHandler<RecordReceivedEventArgs> handler = (_, _) => { };
        jetstream.RecordReceived += handler;
        await jetstream.ConnectAsync(cancellationToken);
        await scheduler.Scheduled.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        await jetstream.CloseAsync(cancellationToken: cancellationToken);
        jetstream.RecordReceived -= handler;

        await using IAsyncEnumerator<JetstreamEvent> stream = jetstream.StreamAsync(
            cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        Task<bool> next = stream.MoveNextAsync().AsTask();
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            Assert.Single(server.Connections);
        }
        finally
        {
            scheduler.RunPending();
        }

        Assert.True(await next.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(42, stream.Current.Sequence);
        Assert.Equal(2, server.Connections.Count);
    }

    [Fact]
    public async Task LiveStreamDoesNotDiscardAnExpiredCursor()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestJetstreamServer
        {
            RefuseWith = (_, _) => (HttpStatusCode.BadRequest, """{"error":"CursorTooOld"}""")
        };
        await server.Start((_, _, _) => Task.CompletedTask);
        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri, options: new JetstreamOptions { UseCompression = false });
        await using IAsyncEnumerator<JetstreamEvent> stream = jetstream.StreamAsync(cursor: 1, cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        JetstreamConnectionException error = await Assert.ThrowsAsync<JetstreamConnectionException>(
            () => stream.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal("CursorTooOld", error.ErrorDetail?.Error);
        Assert.Empty(server.Connections);
    }

    [Fact]
    public async Task LiveStreamPropagatesOutdatedCursorNotice()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestJetstreamServer();
        await server.Start((socket, _, token) => SendText(socket,
            """{"$type":"message","payload":{"$type":"network.bsky.jetstream.subscribeEvents#info","name":"OutdatedCursor"}}""",
            token));
        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri, options: new JetstreamOptions { UseCompression = false });
        await using IAsyncEnumerator<JetstreamEvent> stream = jetstream.StreamAsync(
            cursor: 1, maximumReconnectAttempts: 1, cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(
            () => stream.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Contains("outside the server's retained events", error.Message, StringComparison.Ordinal);
        Assert.Single(server.Connections);
    }

    [Fact]
    public async Task LiveStreamRetriesTransientUpgradeFailure()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestJetstreamServer
        {
            RefuseWith = (_, attempt) => attempt == 1
                ? (HttpStatusCode.ServiceUnavailable, """{"error":"Unavailable"}""") : null
        };
        await server.Start((socket, _, token) => SendText(socket, IdentityEvent(42), token));
        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri, options: new JetstreamOptions { UseCompression = false });
        await using IAsyncEnumerator<JetstreamEvent> stream = jetstream.StreamAsync(cursor: 41, cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await stream.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(42, stream.Current.Sequence);
        Assert.Contains("cursor=41", Assert.Single(server.Connections).Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveStreamStopsAfterFiveTransientReconnectAttempts()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        int attempts = 0;
        using var server = new TestJetstreamServer
        {
            RefuseWith = (_, _) =>
            {
                Interlocked.Increment(ref attempts);
                return (HttpStatusCode.ServiceUnavailable, """{"error":"Unavailable"}""");
            }
        };
        await server.Start((_, _, _) => Task.CompletedTask);
        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri, options: new JetstreamOptions { UseCompression = false });
        await using IAsyncEnumerator<JetstreamEvent> stream = jetstream.StreamAsync(
            cursor: 41, cancellationToken: cancellationToken, maximumReconnectAttempts: 5).GetAsyncEnumerator(cancellationToken);

        IOException error = await Assert.ThrowsAsync<IOException>(
            () => stream.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Contains("5 reconnection attempts", error.Message, StringComparison.Ordinal);
        Assert.Equal(12, attempts); // The rejected WebSocket upgrade and its error-detail request each count once.
        Assert.Throws<ArgumentOutOfRangeException>(() => jetstream.StreamAsync(
            cursor: null, maximumReconnectAttempts: -1, cancellationToken: cancellationToken));
    }

    [Fact]
    public async Task LiveStreamRejectsExistingEventDrivenConnection()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestJetstreamServer();
        await server.Start((_, _, _) => Task.CompletedTask);
        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri, options: new JetstreamOptions { UseCompression = false });
        await jetstream.ConnectAsync(cancellationToken);
        await using IAsyncEnumerator<JetstreamEvent> stream = jetstream.StreamAsync(cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => stream.MoveNextAsync().AsTask());
        await jetstream.CloseAsync(cancellationToken: cancellationToken);
    }

    [Fact]
    public async Task ReplayHandsOffFromPinnedArchiveTipWithoutRepeatingItsCursor()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestJetstreamServer
        {
            ArchiveRequest = context => TestJetstreamServer.Respond(context, HttpStatusCode.OK, "application/json",
                Encoding.UTF8.GetBytes(
                    """{"plannedThroughSeq":40,"sealedTipSeq":40,"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}"""))
        };
        await server.Start(async (socket, _, serverCancellationToken) =>
        {
            await SendText(socket, IdentityEvent(40), serverCancellationToken);
            await SendText(socket, IdentityEvent(41), serverCancellationToken);
        });

        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(),
            uri: server.Uri,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        await using IAsyncEnumerator<JetstreamEvent> replay = jetstream.ReplayAsync(
            new SnapshotRequest { AfterSeq = 0 }, cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await replay.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(41, replay.Current.Sequence);
        Assert.Contains("cursor=40", Assert.Single(server.Connections).Query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplayReplansWhenLiveCursorExpires(bool duringUpgrade)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        int plans = 0;
        using var logs = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Debug));
        using var server = new TestJetstreamServer
        {
            RefuseWith = (_, attempt) => duringUpgrade && attempt == 1
                ? (HttpStatusCode.BadRequest, """{"error":"CursorTooOld"}""") : null,
            ArchiveRequest = context => TestJetstreamServer.Respond(context, HttpStatusCode.OK, "application/json",
                Encoding.UTF8.GetBytes(Interlocked.Increment(ref plans) == 1
                    ? """{"plannedThroughSeq":40,"sealedTipSeq":40,"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}"""
                    : """{"plannedThroughSeq":50,"sealedTipSeq":50,"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}"""))
        };
        await server.Start(async (socket, connection, token) =>
        {
            if (!duringUpgrade && connection == 1)
            {
                await SendText(socket,
                    """{"$type":"message","payload":{"$type":"network.bsky.jetstream.subscribeEvents#info","name":"OutdatedCursor"}}""",
                    token);
            }
            else
            {
                await SendText(socket, IdentityEvent(51), token);
            }
        });
        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false, LoggerFactory = loggerFactory });
        await using IAsyncEnumerator<JetstreamEvent> replay = jetstream.ReplayAsync(
            new SnapshotRequest { AfterSeq = 0 }, cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);

        Assert.True(await replay.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(51, replay.Current.Sequence);
        Assert.Equal(2, plans);
        Assert.Equal(duringUpgrade ? 1 : 2, server.Connections.Count);
        if (!duringUpgrade)
        {
            Assert.Contains("cursor=40", server.Connections.First().Query, StringComparison.Ordinal);
        }

        Assert.Contains("cursor=50", server.Connections.Last().Query, StringComparison.Ordinal);
        Assert.Contains(logs.Entries, entry => entry.EventId == 44 &&
            entry.Message.Contains("Expired cursor; archive fallback", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReplayKeepsOriginalFiltersAfterCallerMutatesLists()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        List<Did> dids = [new(TestDid)];
        List<CollectionSelector> collections = [new("app.bsky.feed.post")];
        List<JetStreamEventKind> kinds = [JetStreamEventKind.Commit, JetStreamEventKind.Identity];
        SnapshotRequest request = new() { Dids = dids, Collections = collections, Kinds = kinds };
        using var server = new TestJetstreamServer
        {
            ArchiveRequest = context =>
            {
                Assert.Equal("POST", context.Request.HttpMethod);
                using StreamReader reader = new(context.Request.InputStream);
                string body = reader.ReadToEnd();
                Assert.Contains(TestDid, body, StringComparison.Ordinal);
                Assert.Contains("app.bsky.feed.post", body, StringComparison.Ordinal);
                Assert.DoesNotContain("app.bsky.feed.like", body, StringComparison.Ordinal);
                return TestJetstreamServer.Respond(context, HttpStatusCode.OK, "application/json",
                    Encoding.UTF8.GetBytes(
                        """{"plannedThroughSeq":40,"sealedTipSeq":40,"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}"""));
            }
        };
        await server.Start(async (socket, _, token) =>
        {
            await SendText(socket, IdentityEvent(41), token);
            await SendText(socket, IdentityEvent(42), token);
        });
        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        string fingerprint = request.Fingerprint(server.Uri);
        SnapshotCheckpoint? saved = null;
        IAsyncEnumerable<JetstreamEvent> events = jetstream.ReplayAsync(
            request, onCheckpoint: progress => saved = progress, cancellationToken: cancellationToken);
        dids[0] = new Did("did:plc:aaaaaaaaaaaaaaaaaaaaaaaa");
        collections[0] = new CollectionSelector("app.bsky.feed.like");
        kinds.Clear();

        await using IAsyncEnumerator<JetstreamEvent> replay = events.GetAsyncEnumerator(cancellationToken);
        Assert.True(await replay.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(41, replay.Current.Sequence);
        Assert.True(await replay.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(42, replay.Current.Sequence);
        Assert.Equal(fingerprint, saved?.RequestFingerprint);
        Assert.Contains(TestDid, Uri.UnescapeDataString(Assert.Single(server.Connections).Query), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReplayReconnectsFromLastDeliveredSequenceAfterLiveDisconnect()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestJetstreamServer
        {
            ArchiveRequest = context => TestJetstreamServer.Respond(context, HttpStatusCode.OK, "application/json",
                Encoding.UTF8.GetBytes(
                    """{"plannedThroughSeq":40,"sealedTipSeq":40,"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}"""))
        };
        await server.Start(async (socket, connectionNumber, serverCancellationToken) =>
        {
            await SendText(socket, IdentityEvent(connectionNumber == 1 ? 41 : 42), serverCancellationToken);
            if (connectionNumber == 1)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, serverCancellationToken);
            }
        });
        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(),
            uri: server.Uri,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        SnapshotCheckpoint? checkpoint = null;
        await using IAsyncEnumerator<JetstreamEvent> replay = jetstream.ReplayAsync(
            new SnapshotRequest { AfterSeq = 0 }, onCheckpoint: progress => checkpoint = progress,
            cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await replay.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(41, replay.Current.Sequence);
        Assert.True(await replay.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(42, replay.Current.Sequence);
        Assert.Equal(41, checkpoint?.LiveAfterSeq);
        Assert.Equal(2, server.Connections.Count);
        Assert.Contains("cursor=41", server.Connections.Last().Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReplayResumesFromLiveCheckpointWithoutReplanningTheArchive()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestJetstreamServer
        {
            ArchiveRequest = _ => throw new InvalidOperationException("Live checkpoints must not replan.")
        };
        await server.Start(async (socket, _, serverCancellationToken) =>
        {
            await SendText(socket, IdentityEvent(41), serverCancellationToken);
            await SendText(socket, IdentityEvent(42), serverCancellationToken);
        });
        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(),
            uri: server.Uri,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        await using IAsyncEnumerator<JetstreamEvent> replay = jetstream.ReplayAsync(
            new SnapshotRequest { AfterSeq = 0 },
            new SnapshotCheckpoint
            {
                PlanAfterSeq = 0, SealedTipSeq = 40, LiveAfterSeq = 41,
                RequestFingerprint = new SnapshotRequest { AfterSeq = 0 }.Fingerprint(server.Uri)
            },
            cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await replay.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(42, replay.Current.Sequence);
        Assert.Contains("cursor=41", Assert.Single(server.Connections).Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReplayOverflowDrainsBufferedEventsAndReconnectsWithoutSkippingSequences()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        TaskCompletionSource burst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var logs = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Debug));
        using var server = new TestJetstreamServer
        {
            ArchiveRequest = context => TestJetstreamServer.Respond(context, HttpStatusCode.OK, "application/json",
                Encoding.UTF8.GetBytes(
                    """{"plannedThroughSeq":40,"sealedTipSeq":40,"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}"""))
        };
        await server.Start(async (socket, connection, token) =>
        {
            if (connection == 1)
            {
                await SendText(socket, IdentityEvent(41), token);
                await burst.Task.WaitAsync(token);
            }

            for (long sequence = connection == 1 ? 42 : 1065; sequence <= 1540; sequence++)
            {
                await SendText(socket, IdentityEvent(sequence), token);
            }
        });
        await using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false, LoggerFactory = loggerFactory });
        SnapshotRequest request = new() { AfterSeq = 0 };
        SnapshotCheckpoint? checkpoint = null;
        await using IAsyncEnumerator<JetstreamEvent> replay = jetstream.ReplayAsync(
            request, onCheckpoint: progress => checkpoint = progress,
            cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await replay.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(41, replay.Current.Sequence);
        Assert.Null(checkpoint?.LiveAfterSeq);

        // Stop consuming until the bounded channel has actually overflowed, rather than relying on a timing delay.
        burst.SetResult();
        await WaitUntil(() => logs.Entries.Any(entry => entry.EventId == 42 &&
            entry.Message.Contains("Buffer overflow", StringComparison.Ordinal)), cancellationToken);

        for (long sequence = 42; sequence <= 1540; sequence++)
        {
            Assert.True(await replay.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
            Assert.Equal(sequence, replay.Current.Sequence);
            Assert.Equal(sequence - 1, checkpoint?.LiveAfterSeq);
            Assert.Equal(40, checkpoint?.SealedTipSeq);
            Assert.Equal(request.Fingerprint(server.Uri), checkpoint?.RequestFingerprint);
        }

        Assert.Equal(2, server.Connections.Count);
        Assert.Contains("cursor=1065", server.Connections.Last().Query, StringComparison.Ordinal);
        Assert.Contains(logs.Entries, entry => entry.EventId == 42 && entry.Message.Contains("buffered events 1024", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoteCloseDrainsTheLastScheduledParserBeforeReplayReconnects(bool abort)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        PausedTaskScheduler scheduler = new();
        TaskCompletionSource disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var logs = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Debug));
        using var server = new TestJetstreamServer
        {
            ArchiveRequest = context => TestJetstreamServer.Respond(context, HttpStatusCode.OK, "application/json",
                Encoding.UTF8.GetBytes(
                    """{"plannedThroughSeq":40,"sealedTipSeq":40,"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}"""))
        };
        await server.Start(async (socket, connection, token) =>
        {
            await SendText(socket, IdentityEvent(41), token);
            if (connection == 1)
            {
                await scheduler.Scheduled.WaitAsync(token);
                if (abort)
                {
                    socket.Abort();
                }
                else
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Remote close", token);
                }

                disconnected.TrySetResult();
            }
            else
            {
                await SendText(socket, IdentityEvent(42), token);
            }
        });
        await using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri,
            options: new JetstreamOptions
            {
                ApiKey = "test-key", UseCompression = false, LoggerFactory = loggerFactory,
                TaskFactory = new TaskFactory(scheduler)
            });
        await using IAsyncEnumerator<JetstreamEvent> replay = jetstream.ReplayAsync(
            new SnapshotRequest(), cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        Task<bool> first = replay.MoveNextAsync().AsTask();
        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        if (abort)
        {
            await WaitUntil(() => logs.Entries.Any(entry => entry.EventId == 3), cancellationToken);
        }

        scheduler.RunPending();
        await WaitUntil(() => first.IsCompleted || server.Connections.Count == 2, cancellationToken);
        if (!first.IsCompleted)
        {
            await WaitUntil(() => scheduler.GetPendingCount() != 0, cancellationToken);
            scheduler.RunPending();
        }

        Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(41, replay.Current.Sequence);
        int bufferedEventConnectionCount = server.Connections.Count;
        Task<bool> second = replay.MoveNextAsync().AsTask();
        await WaitUntil(() => server.Connections.Count == 2, cancellationToken);
        while (!second.IsCompleted)
        {
            await WaitUntil(() => second.IsCompleted || scheduler.GetPendingCount() != 0, cancellationToken);
            scheduler.RunPending();
        }

        Assert.True(await second.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(42, replay.Current.Sequence);
        Assert.Equal(1, bufferedEventConnectionCount);
        Assert.Contains("cursor=41", server.Connections.Last().Query, StringComparison.Ordinal);
        Assert.Contains(logs.Entries, entry => entry.EventId == 42 &&
            entry.Message.Contains(abort ? "Transport" : "Remote close", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Closed")]
    [InlineData("Disposed")]
    [InlineData("Cancelled")]
    public async Task CloseReplyRechecksTheSocketAfterWaitingForTheSendSlot(string cleanup)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using CancellationTokenSource connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        TaskCompletionSource close = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var logs = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Debug));
        using var server = new TestJetstreamServer();
        await server.Start(async (socket, _, token) =>
        {
            await close.Task.WaitAsync(token);
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Remote close", token);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
        using var jetstream = new AtProtoJetstream(uri: server.Uri,
            options: new JetstreamOptions { UseCompression = false, LoggerFactory = loggerFactory });
        using var httpClient = new HttpClient();
        await jetstream.ConnectAsync(httpClient: httpClient, cancellationToken: connectionCancellation.Token);
        ClientWebSocket client = GetField<ClientWebSocket>(jetstream, "_client");
        SemaphoreSlim send = GetField<SemaphoreSlim>(jetstream, "_sendSemaphore");
        Task receiver = GetField<Task>(jetstream, "_receiveLoopTask");
        await send.WaitAsync(cancellationToken);
        try
        {
            close.SetResult();
            await WaitUntil(() => client.State == WebSocketState.CloseReceived, cancellationToken);
            // Force the competing cleanup to win while ReceiveLoop is waiting to reply.
            if (cleanup == "Disposed")
            {
                jetstream.Dispose();
            }
            else if (cleanup == "Closed")
            {
                await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Concurrent close", cancellationToken);
                Assert.Equal(WebSocketState.Closed, client.State);
            }
            else
            {
                await connectionCancellation.CancelAsync();
            }
        }
        finally
        {
            send.Release();
        }

        await receiver.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        await jetstream.DisposeAsync();
        Assert.DoesNotContain(logs.Entries, entry => entry.Level == LogLevel.Error);
        Assert.DoesNotContain(logs.Entries, entry => entry.Exception is ObjectDisposedException or WebSocketException);
    }

    [Fact]
    public async Task ReplayCancellationDuringCleanupStillFinishesTheBoundedClose()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using CancellationTokenSource replayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var logs = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Debug));
        using var server = new TestJetstreamServer
        {
            HoldCloseResponse = true,
            ArchiveRequest = context => TestJetstreamServer.Respond(context, HttpStatusCode.OK, "application/json",
                Encoding.UTF8.GetBytes(
                    """{"plannedThroughSeq":40,"sealedTipSeq":40,"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}"""))
        };
        await server.Start((socket, _, token) => SendText(socket, IdentityEvent(41), token));
        await using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri,
            options: new JetstreamOptions
            {
                ApiKey = "test-key", UseCompression = false, LoggerFactory = loggerFactory,
                CloseTimeout = TimeSpan.FromSeconds(1)
            });
        SnapshotCheckpoint? checkpoint = null;
        IAsyncEnumerator<JetstreamEvent> replay = jetstream.ReplayAsync(
            new SnapshotRequest(), onCheckpoint: progress => checkpoint = progress,
            cancellationToken: replayCancellation.Token).GetAsyncEnumerator(replayCancellation.Token);
        Assert.True(await replay.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(41, replay.Current.Sequence);
        Task cleanup = replay.DisposeAsync().AsTask();
        await server.CloseReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        await replayCancellation.CancelAsync();
        await cleanup.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.Null(checkpoint?.LiveAfterSeq);
        Assert.Single(server.Connections);
        Assert.Contains(logs.Entries, entry => entry.EventId == 25);
        Assert.DoesNotContain(logs.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task AsyncDisposalDoesNotAllowANewConnectionToKeepTheReceiveLoopAlive()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var logs = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Debug));
        using var server = new TestJetstreamServer { HoldCloseResponse = true };
        await server.Start((_, _, _) => Task.CompletedTask);
        await using var jetstream = new AtProtoJetstream(uri: server.Uri,
            options: new JetstreamOptions
            {
                UseCompression = false, CloseTimeout = TimeSpan.FromSeconds(1), LoggerFactory = loggerFactory
            });
        using var httpClient = new HttpClient();
        await jetstream.ConnectAsync(httpClient: httpClient, cancellationToken: cancellationToken);
        Task disposal = jetstream.DisposeAsync().AsTask();
        await server.CloseReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            jetstream.ConnectAsync(httpClient: httpClient, cancellationToken: cancellationToken));
        await disposal.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        Assert.Single(server.Connections);
        Assert.True(GetField<Task>(jetstream, "_receiveLoopTask").IsCompleted);
        Assert.Contains(logs.Entries, entry => entry.EventId == 25);
        Assert.DoesNotContain(logs.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ReplayResumesFallbackArchiveWithOriginalRequestIdentity()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        int plans = 0;
        using var server = new TestJetstreamServer
        {
            ArchiveRequest = context =>
            {
                Interlocked.Increment(ref plans);
                return TestJetstreamServer.Respond(context, HttpStatusCode.OK, "application/json",
                    Encoding.UTF8.GetBytes(
                        """{"plannedThroughSeq":50,"sealedTipSeq":50,"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}"""));
            }
        };
        await server.Start(async (socket, _, token) =>
        {
            await SendText(socket, IdentityEvent(51), token);
            await SendText(socket, IdentityEvent(52), token);
        });
        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        SnapshotRequest original = new() { AfterSeq = 0, BeforeSeq = 40 };
        SnapshotCheckpoint checkpoint = new()
        {
            PlanAfterSeq = 25, SealedTipSeq = 50, ReplayAfterSeq = 25,
            RequestFingerprint = original.Fingerprint(server.Uri)
        };
        SnapshotCheckpoint? saved = null;
        await using IAsyncEnumerator<JetstreamEvent> replay = jetstream.ReplayAsync(
            original, checkpoint, progress => saved = progress,
            cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await replay.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(51, replay.Current.Sequence);
        Assert.True(await replay.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(1, plans);
        Assert.Equal(original.Fingerprint(server.Uri), saved?.RequestFingerprint);
        Assert.Equal(25, saved?.ReplayAfterSeq);
        Assert.Equal(51, saved?.LiveAfterSeq);
        Assert.Contains("cursor=50", Assert.Single(server.Connections).Query, StringComparison.Ordinal);
        SnapshotCheckpoint liveCheckpoint = Assert.IsType<SnapshotCheckpoint>(saved);
        Assert.Throws<ArgumentException>(() => jetstream.ReplayAsync(
            original, liveCheckpoint with { ReplayAfterSeq = null }, cancellationToken: cancellationToken));
        await using IAsyncEnumerator<JetstreamEvent> resumed = jetstream.ReplayAsync(
            original, liveCheckpoint, cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await resumed.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(52, resumed.Current.Sequence);
        Assert.Equal(1, plans);
        Assert.Contains("cursor=51", server.Connections.Last().Query, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => jetstream.ReplayAsync(
            original with { BeforeSeq = 41 }, checkpoint, cancellationToken: cancellationToken));
    }

    [Fact]
    public async Task ReplayRetriesTransientUpgradeFailure()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using var server = new TestJetstreamServer
        {
            ArchiveRequest = context => TestJetstreamServer.Respond(context, HttpStatusCode.OK, "application/json",
                Encoding.UTF8.GetBytes(
                    """{"plannedThroughSeq":40,"sealedTipSeq":40,"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}""")),
            RefuseWith = (_, attempt) => attempt == 1
                ? (HttpStatusCode.ServiceUnavailable, """{"error":"Unavailable"}""") : null
        };
        await server.Start((socket, _, token) => SendText(socket, IdentityEvent(41), token));
        using var jetstream = new AtProtoJetstream(
            httpClientFactory: new LocalHttpClientFactory(), uri: server.Uri,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        await using IAsyncEnumerator<JetstreamEvent> replay = jetstream.ReplayAsync(
            new SnapshotRequest { AfterSeq = 0 }, cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        Assert.True(await replay.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
        Assert.Equal(41, replay.Current.Sequence);
        Assert.Single(server.Connections);
    }

    [Fact]
    public async Task AnUncompressedV2EventIsDelivered()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, connectionNumber, serverCancellationToken) =>
        {
            await SendText(webSocket, IdentityEvent(12), serverCancellationToken);
        });

        TaskCompletionSource<AtJetstreamEvent> recordReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { UseCompression = false });

        jetstream.KindFilter = [JetStreamEventKind.Identity];
        jetstream.RecordReceived += (sender, e) => recordReceived.TrySetResult(e.ParsedEvent);

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(uri: server.Uri, cursor: 3, httpClient: httpClient, cancellationToken: cancellationToken);

        AtJetstreamEvent received = await recordReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        JetstreamIdentityEvent identityEvent = Assert.IsType<JetstreamIdentityEvent>(received);
        Assert.Equal(12, identityEvent.Sequence);
        Assert.Equal(12, jetstream.LastSequence);

        TestJetstreamServer.Connection connection = Assert.Single(server.Connections);
        Assert.Equal("/xrpc/network.bsky.jetstream.subscribeEvents", connection.Path);
        Assert.Equal(SubProtocol, connection.SubProtocol);
        Assert.Contains("kinds=identity", connection.Query, StringComparison.Ordinal);
        Assert.Contains("cursor=3", connection.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("compress", connection.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("zstdDictionary", connection.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACompressedV2EventIsDecompressedWithTheDictionaryTheServerServes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        byte[] dictionary = new JetstreamOptions().Dictionary!;
        uint dictionaryId = BitConverter.ToUInt32(dictionary, 4);

        using var server = new TestJetstreamServer { Dictionary = dictionary };

        await server.Start(async (webSocket, connectionNumber, serverCancellationToken) =>
        {
            using var compressor = new Compressor();
            compressor.LoadDictionary(dictionary);

            await webSocket.SendAsync(
                compressor.Wrap(Encoding.UTF8.GetBytes(IdentityEvent(7))).ToArray(),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                serverCancellationToken);
        });

        TaskCompletionSource<AtJetstreamEvent> recordReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using var jetstream = new AtProtoJetstream(uri: server.Uri);

        jetstream.RecordReceived += (sender, e) => recordReceived.TrySetResult(e.ParsedEvent);

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(uri: server.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        AtJetstreamEvent received = await recordReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.Equal(7, received.Sequence);

        TestJetstreamServer.Connection connection = Assert.Single(server.Connections);
        Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"zstdDictionary={dictionaryId}"), connection.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusedConnectionThrowsWithTheServersError()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer
        {
            RefuseWith = (_, _) => (HttpStatusCode.BadRequest, """{"error":"CursorTooOld","message":"cursor 1 below lookback floor"}""")
        };

        await server.Start((webSocket, connectionNumber, serverCancellationToken) => Task.CompletedTask);

        using var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { UseCompression = false });

        using var httpClient = new HttpClient();

        JetstreamConnectionException exception = await Assert.ThrowsAsync<JetstreamConnectionException>(
            () => jetstream.ConnectAsync(uri: server.Uri, cursor: 1, httpClient: httpClient, cancellationToken: cancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.NotNull(exception.ErrorDetail);
        Assert.Equal("CursorTooOld", exception.ErrorDetail.Error);
        Assert.Equal("cursor 1 below lookback floor", exception.ErrorDetail.Message);
        Assert.False(jetstream.IsConnected);
    }

    [Fact]
    public async Task AnOutdatedDictionaryIsDownloadedAgainAndTheConnectionRetried()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        byte[] dictionary = new JetstreamOptions().Dictionary!;

        using var server = new TestJetstreamServer
        {
            Dictionary = dictionary,
            RefuseWith = (_, upgradeAttempt) => upgradeAttempt == 1
                ? (HttpStatusCode.BadRequest, """{"error":"UnknownZstdDictionary","message":"current dictionary id is 1"}""")
                : null
        };

        await server.Start((webSocket, connectionNumber, serverCancellationToken) => Task.CompletedTask);

        using var jetstream = new AtProtoJetstream(uri: server.Uri);

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(uri: server.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        Assert.True(jetstream.IsConnected);
        Assert.Equal(2, server.DictionaryRequests);
    }

    [Fact]
    public async Task AReplacedReceiveLoopRemovesLateMessageMetricState()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        TaskCompletionSource sendMessage = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource measurementStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource messageDelivered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource replacementOpened = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ConcurrentQueue<WebSocketState> stateChanges = new();
        using ManualResetEventSlim releaseMeasurement = new(false);
        using var server = new TestJetstreamServer();
        await server.Start(async (socket, connectionNumber, token) =>
        {
            if (connectionNumber == 1)
            {
                await sendMessage.Task.WaitAsync(token);
                await SendText(socket, IdentityEvent(100), token);
            }
        });
        using var meterFactory = new TestMeterFactory();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Scope == meterFactory && instrument.Name == "idunno.atproto.jetstream.total.messages")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) =>
        {
            measurementStarted.TrySetResult();
            Assert.True(releaseMeasurement.Wait(TimeSpan.FromSeconds(30), cancellationToken));
        });
        listener.Start();
        using var jetstream = new AtProtoJetstream(uri: server.Uri, options: new JetstreamOptions
        {
            UseCompression = false,
            MeterFactory = meterFactory
        });
        using var closedCollector = new MetricCollector<long>(
            meterFactory, JetstreamMetrics.MeterName, "idunno.atproto.jetstream.total.connections_closed");
        using var httpClient = new HttpClient();
        jetstream.MessageReceived += (_, _) => messageDelivered.TrySetResult();
        await jetstream.ConnectAsync(uri: server.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);
        jetstream.ConnectionStateChanged += (_, e) =>
        {
            stateChanges.Enqueue(e.State);
            if (e.State == WebSocketState.Open)
            {
                replacementOpened.TrySetResult();
            }
        };

        try
        {
            sendMessage.SetResult();
            await measurementStarted.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            // Hold the old loop before it stores the message timestamp until replacement has removed its metric state.
            jetstream.DidFilter = [new Did(TestDid)];
            await replacementOpened.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        finally
        {
            releaseMeasurement.Set();
        }

        await messageDelivered.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        JetstreamMetrics metrics = (JetstreamMetrics)typeof(AtProtoJetstream)
            .GetField("_metrics", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(jetstream)!;
        var timestamps = (ConcurrentDictionary<object, long>)typeof(JetstreamMetrics)
            .GetField("_lastMessageTimestamps", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(metrics)!;

        for (int attempt = 0; attempt < 100 && !timestamps.IsEmpty; attempt++)
        {
            await Task.Delay(20, cancellationToken);
        }

        Assert.Empty(timestamps);
        Assert.Equal(1, closedCollector.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
        Assert.DoesNotContain(WebSocketState.Aborted, stateChanges);
        Assert.Equal(1, stateChanges.Count(state => state == WebSocketState.Closed));
    }

    [Fact]
    public async Task AbortingAReplacedConnectionDecrementsActiveConnectionMetrics()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer { HoldCloseResponse = true };
        await server.Start((_, _, _) => Task.CompletedTask);

        using var meterFactory = new TestMeterFactory();
        using var activeCollector = new MetricCollector<long>(
            meterFactory,
            JetstreamMetrics.MeterName,
            "idunno.atproto.jetstream.connections.active");
        using var closedCollector = new MetricCollector<long>(
            meterFactory,
            JetstreamMetrics.MeterName,
            "idunno.atproto.jetstream.total.connections_closed");
        using CancellationTokenSource connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions
            {
                MeterFactory = meterFactory,
                UseCompression = false,
                CloseTimeout = TimeSpan.FromMinutes(1)
            });
        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(
            uri: server.Uri,
            cursor: null,
            httpClient: httpClient,
            cancellationToken: connectionCancellation.Token);

        jetstream.DidFilter = [new Did(TestDid)];

        await server.CloseReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        await connectionCancellation.CancelAsync();

        for (int attempt = 0; attempt < 100 && closedCollector.GetMeasurementSnapshot().Sum(measurement => measurement.Value) == 0; attempt++)
        {
            await Task.Delay(20, cancellationToken);
        }

        Assert.Equal(1, closedCollector.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
        Assert.Equal(0, activeCollector.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
    }

    [Fact]
    public async Task ChangingAFilterReconnectsFromTheLastSequenceWithoutRedeliveringIt()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, connectionNumber, serverCancellationToken) =>
        {
            if (connectionNumber == 1)
            {
                await SendText(webSocket, IdentityEvent(100), serverCancellationToken);
            }
            else
            {
                // The cursor is inclusive, so the server sends the event the cursor names again.
                await SendText(webSocket, IdentityEvent(100), serverCancellationToken);
                await SendText(webSocket, IdentityEvent(101), serverCancellationToken);
            }
        });

        ConcurrentQueue<long?> sequences = new();
        ConcurrentQueue<WebSocketState> stateChanges = new();
        TaskCompletionSource firstReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { UseCompression = false, MaximumConcurrentMessageParsers = 1 });

        jetstream.RecordReceived += (sender, e) =>
        {
            sequences.Enqueue(e.ParsedEvent.Sequence);

            if (e.ParsedEvent.Sequence == 100)
            {
                firstReceived.TrySetResult();
            }
            else if (e.ParsedEvent.Sequence == 101)
            {
                secondReceived.TrySetResult();
            }
        };

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(uri: server.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        await firstReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        jetstream.ConnectionStateChanged += (sender, e) => stateChanges.Enqueue(e.State);

        jetstream.DidFilter = [new Did(TestDid)];

        await secondReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.Equal([100, 101], sequences);
        Assert.True(jetstream.IsConnected);
        Assert.Equal(101, jetstream.LastSequence);

        Assert.Equal(2, server.Connections.Count);
        TestJetstreamServer.Connection reconnection = server.Connections.Last();
        Assert.Contains("cursor=100", reconnection.Query, StringComparison.Ordinal);
        Assert.Contains("dids=did%3aplc%3ag6ylltenitt4tp27bpwalh7b", reconnection.Query, StringComparison.OrdinalIgnoreCase);

        // The replaced connection is not reported as a disconnection, only the connection which replaced it.
        Assert.DoesNotContain(WebSocketState.Aborted, stateChanges);
        Assert.Contains(WebSocketState.Open, stateChanges);
    }

    [Fact]
    public async Task ClosingWhilstAFilterChangeIsReconnectingLeavesTheJetstreamDisconnected()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        byte[] dictionary = new JetstreamOptions().Dictionary!;

        TaskCompletionSource reconnecting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseReconnection = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = new TestJetstreamServer
        {
            Dictionary = dictionary,
            BeforeUpgrade = async upgradeNumber =>
            {
                // The first upgrade belongs to the initial connection, which must be allowed to complete.
                if (upgradeNumber == 2)
                {
                    reconnecting.TrySetResult();
                    await releaseReconnection.Task;
                }
            }
        };

        await server.Start((webSocket, connectionNumber, serverCancellationToken) => Task.CompletedTask);

        using var jetstream = new AtProtoJetstream(uri: server.Uri);

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(uri: server.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        Assert.True(jetstream.IsConnected);

        TaskCompletionSource replacementOpened = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool closeRequested = false;

        jetstream.ConnectionStateChanged += (sender, e) =>
        {
            if (closeRequested && e.State == WebSocketState.Open)
            {
                replacementOpened.TrySetResult();
            }
        };

        // Changing a filter reconnects away from the caller, so this leaves a reconnection part way through replacing
        // the socket, held at the upgrade of the replacement it has yet to install.
        jetstream.DidFilter = [new Did(TestDid)];

        await reconnecting.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        closeRequested = true;

        Task close = jetstream.CloseAsync(cancellationToken: cancellationToken);

        // The close cannot complete until the reconnection has finished installing its replacement socket, so releasing
        // it here is what lets the close find that replacement rather than the socket the reconnection replaced.
        releaseReconnection.TrySetResult();

        await close.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        // Waited for so the assertion is made once the reconnection has installed its replacement, rather than before
        // it gets that far, which would pass whether or not the close covered the replacement.
        await replacementOpened.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.False(jetstream.IsConnected);
    }

    [Fact]
    public async Task ConnectingToADifferentServerDiscardsTheSequenceThePreviousServerIssued()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var firstServer = new TestJetstreamServer();
        using var secondServer = new TestJetstreamServer();

        await firstServer.Start(async (webSocket, connectionNumber, serverCancellationToken) =>
        {
            await SendText(webSocket, IdentityEvent(100), serverCancellationToken);
        });

        await secondServer.Start((webSocket, connectionNumber, serverCancellationToken) => Task.CompletedTask);

        TaskCompletionSource firstReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource reconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using var jetstream = new AtProtoJetstream(
            uri: firstServer.Uri,
            options: new JetstreamOptions { UseCompression = false, MaximumConcurrentMessageParsers = 1 });

        jetstream.RecordReceived += (sender, e) => firstReceived.TrySetResult();

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(uri: firstServer.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        await firstReceived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.Equal(100, jetstream.LastSequence);

        await jetstream.CloseAsync(cancellationToken: cancellationToken);

        await jetstream.ConnectAsync(uri: secondServer.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        Assert.Null(jetstream.LastSequence);

        jetstream.ConnectionStateChanged += (sender, e) =>
        {
            if (e.State == WebSocketState.Open)
            {
                reconnected.TrySetResult();
            }
        };

        jetstream.DidFilter = [new Did(TestDid)];

        await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        // The reconnection must not resume from a cursor the first server issued, which the second knows nothing about.
        TestJetstreamServer.Connection reconnection = secondServer.Connections.Last();
        Assert.DoesNotContain("cursor=", reconnection.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectingToADifferentServerDownloadsThatServersDictionary()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        byte[] dictionary = new JetstreamOptions().Dictionary!;

        using var firstServer = new TestJetstreamServer { Dictionary = dictionary };
        using var secondServer = new TestJetstreamServer { Dictionary = dictionary };

        static Task Silent(WebSocket webSocket, int connectionNumber, CancellationToken cancellationToken) => Task.CompletedTask;

        await firstServer.Start(Silent);
        await secondServer.Start(Silent);

        using var jetstream = new AtProtoJetstream(uri: firstServer.Uri);

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(uri: firstServer.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        Assert.Equal(1, firstServer.DictionaryRequests);

        await jetstream.CloseAsync(cancellationToken: cancellationToken);

        await jetstream.ConnectAsync(uri: secondServer.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        // A dictionary ID only means anything to the server which issued it, so the second server must be asked for its own.
        Assert.Equal(1, secondServer.DictionaryRequests);
        Assert.Equal(1, firstServer.DictionaryRequests);
    }

    [Fact]
    public async Task AFilterChangedWhilstConnectingIsAppliedOnceTheConnectionIsOpen()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        byte[] dictionary = new JetstreamOptions().Dictionary!;

        TaskCompletionSource dictionaryRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseDictionary = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource filteredConnection = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = new TestJetstreamServer
        {
            Dictionary = dictionary,
            BeforeDictionary = async () =>
            {
                dictionaryRequested.TrySetResult();
                await releaseDictionary.Task;
            }
        };

        await server.Start((webSocket, connectionNumber, serverCancellationToken) =>
        {
            if (connectionNumber == 2)
            {
                filteredConnection.TrySetResult();
            }

            return Task.CompletedTask;
        });

        using var jetstream = new AtProtoJetstream(uri: server.Uri);

        using var httpClient = new HttpClient();

        Task connect = jetstream.ConnectAsync(uri: server.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        // The dictionary is fetched after the filters have been copied for the query string but before the web socket
        // is opened, so holding it there changes a filter in the window where the change would otherwise be lost.
        await dictionaryRequested.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        jetstream.DidFilter = [new Did(TestDid)];

        releaseDictionary.TrySetResult();

        await connect.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        await filteredConnection.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        TestJetstreamServer.Connection reconnection = server.Connections.Last();
        Assert.Contains("dids=did%3aplc%3ag6ylltenitt4tp27bpwalh7b", reconnection.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnErrorFrameIsRaisedAsAFault()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using var server = new TestJetstreamServer();

        await server.Start(async (webSocket, connectionNumber, serverCancellationToken) =>
        {
            await SendText(webSocket, """{"$type":"error","error":"ConsumerTooSlow","message":"too slow"}""", serverCancellationToken);
        });

        TaskCompletionSource<FaultRaisedEventArgs> faultRaised = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using var jetstream = new AtProtoJetstream(
            uri: server.Uri,
            options: new JetstreamOptions { UseCompression = false });

        jetstream.FaultRaised += (sender, e) => faultRaised.TrySetResult(e);

        using var httpClient = new HttpClient();

        await jetstream.ConnectAsync(uri: server.Uri, cursor: null, httpClient: httpClient, cancellationToken: cancellationToken);

        FaultRaisedEventArgs fault = await faultRaised.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.Equal("ConsumerTooSlow", fault.Error);
    }

    private static async Task SendText(WebSocket webSocket, string message, CancellationToken cancellationToken)
    {
        await webSocket.SendAsync(
            Encoding.UTF8.GetBytes(message),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
    }

    private static T GetField<T>(AtProtoJetstream jetstream, string name) where T : class =>
        Assert.IsAssignableFrom<T>(typeof(AtProtoJetstream).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(jetstream));

    private static async Task WaitUntil(Func<bool> condition, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class PausedTaskScheduler : TaskScheduler
    {
        private readonly ConcurrentQueue<Task> _pending = new();
        private readonly TaskCompletionSource _scheduled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Scheduled => _scheduled.Task;

        internal int GetPendingCount() => _pending.Count;

        protected override IEnumerable<Task> GetScheduledTasks() => _pending.ToArray();

        protected override void QueueTask(Task task)
        {
            _pending.Enqueue(task);
            _scheduled.TrySetResult();
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        internal void RunPending()
        {
            while (_pending.TryDequeue(out Task? task))
            {
                TryExecuteTask(task);
            }
        }
    }

    private sealed class LocalHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    /// <summary>
    /// A minimal version 2 jetstream server, which serves a dictionary, can refuse upgrades, and accepts the
    /// subprotocol a client asks for.
    /// </summary>
    private sealed class TestJetstreamServer : IDisposable
    {
        private HttpListener _listener = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly ConcurrentQueue<Connection> _connections = new();
        private int _connectionCount;
        private int _dictionaryRequests;
        private int _upgradeAttempts;
        private int _upgrades;

        public sealed record Connection(string Path, string Query, string? SubProtocol);

        public Uri Uri { get; private set; } = new("ws://localhost");

        public byte[]? Dictionary { get; init; }

        /// <summary>
        /// Awaited before the dictionary is served, to hold a connection open in the window between the filters being
        /// copied and the web socket being opened.
        /// </summary>
        public Func<Task>? BeforeDictionary { get; init; }

        /// <summary>
        /// Awaited, with the number of the upgrade it is about to accept, before a web socket upgrade is completed, to
        /// hold a connection open in the window where the client has yet to install the socket it is opening.
        /// </summary>
        public Func<int, Task>? BeforeUpgrade { get; init; }

        /// <summary>
        /// Decides, from the query and the number of upgrade attempts made so far, whether to refuse a subscription and
        /// with what status and body. Consulted for both the upgrade and the plain request made afterwards to read why
        /// it was refused.
        /// </summary>
        public Func<string, int, (HttpStatusCode StatusCode, string Body)?>? RefuseWith { get; init; }

        public Func<HttpListenerContext, Task>? ArchiveRequest { get; init; }

        public bool HoldCloseResponse { get; init; }

        public TaskCompletionSource CloseReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyCollection<Connection> Connections => [.. _connections];

        public int DictionaryRequests => _dictionaryRequests;

        public Task Start(Func<WebSocket, int, CancellationToken, Task> onConnected)
        {
            StartListener();

            _ = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    HttpListenerContext context = await _listener.GetContextAsync();

                    string path = context.Request.Url?.AbsolutePath ?? string.Empty;
                    string query = context.Request.Url?.Query ?? string.Empty;

                    if (string.Equals(path, "/xrpc/network.bsky.jetstream.getZstdDictionary", StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref _dictionaryRequests);

                        if (BeforeDictionary is not null)
                        {
                            await BeforeDictionary().ConfigureAwait(false);
                        }

                        await Respond(context, HttpStatusCode.OK, "application/octet-stream", Dictionary ?? []);
                        continue;
                    }

                    if (!context.Request.IsWebSocketRequest)
                    {
                        if (ArchiveRequest is not null && path.EndsWith(".planSnapshot", StringComparison.Ordinal))
                        {
                            await ArchiveRequest(context);
                            continue;
                        }

                        (HttpStatusCode StatusCode, string Body)? refusal = RefuseWith?.Invoke(query, Volatile.Read(ref _upgradeAttempts));

                        if (refusal is not null)
                        {
                            await Respond(context, refusal.Value.StatusCode, "application/json", Encoding.UTF8.GetBytes(refusal.Value.Body));
                        }
                        else
                        {
                            await Respond(context, HttpStatusCode.UpgradeRequired, "text/plain", []);
                        }

                        continue;
                    }

                    (HttpStatusCode StatusCode, string Body)? upgradeRefusal = RefuseWith?.Invoke(query, Interlocked.Increment(ref _upgradeAttempts));

                    if (upgradeRefusal is not null)
                    {
                        await Respond(context, upgradeRefusal.Value.StatusCode, "application/json", Encoding.UTF8.GetBytes(upgradeRefusal.Value.Body));
                        continue;
                    }

                    string? subProtocol = context.Request.Headers["Sec-WebSocket-Protocol"];

                    if (BeforeUpgrade is not null)
                    {
                        await BeforeUpgrade(Interlocked.Increment(ref _upgrades)).ConfigureAwait(false);
                    }

                    HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(subProtocol);

                    _connections.Enqueue(new Connection(path, query, webSocketContext.WebSocket.SubProtocol));

                    int connectionNumber = Interlocked.Increment(ref _connectionCount);

                    _ = Task.Run(async () =>
                    {
                        using (WebSocket webSocket = webSocketContext.WebSocket)
                        {
                            try
                            {
                                await onConnected(webSocket, connectionNumber, _cancellationTokenSource.Token);

                                // Read until the client closes and the close answered, as a real server does.
                                byte[] buffer = new byte[16 * 1024];

                                while (webSocket.State == WebSocketState.Open && !_cancellationTokenSource.IsCancellationRequested)
                                {
                                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, _cancellationTokenSource.Token);

                                    if (result.MessageType == WebSocketMessageType.Close)
                                    {
                                        if (HoldCloseResponse)
                                        {
                                            CloseReceived.TrySetResult();
                                            await Task.Delay(Timeout.InfiniteTimeSpan, _cancellationTokenSource.Token);
                                        }

                                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, _cancellationTokenSource.Token);
                                        break;
                                    }
                                }
                            }
                            catch (OperationCanceledException)
                            {
                            }
                            catch (WebSocketException)
                            {
                            }
                        }
                    }, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);

            return Task.CompletedTask;
        }

        private void StartListener()
        {
            const int maximumAttempts = 20;

            for (int attempt = 1; ; attempt++)
            {
                int port = FreePort();

                // A listener that fails to start disposes itself, so each attempt needs a new one.
                if (attempt > 1)
                {
                    _listener.Close();
                    _listener = new HttpListener();
                }

                _listener.Prefixes.Add(string.Create(CultureInfo.InvariantCulture, $"http://localhost:{port}/"));

                try
                {
                    _listener.Start();
                }
                catch (HttpListenerException) when (attempt < maximumAttempts)
                {
                    continue;
                }

                Uri = new Uri(string.Create(CultureInfo.InvariantCulture, $"ws://localhost:{port}"));

                return;
            }
        }

        internal static async Task Respond(HttpListenerContext context, HttpStatusCode statusCode, string contentType, byte[] body)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = body.Length;

            await context.Response.OutputStream.WriteAsync(body);

            context.Response.Close();
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();

            try
            {
                _listener.Abort();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (HttpListenerException)
            {
            }
            catch (SocketException)
            {
            }
            finally
            {
                _cancellationTokenSource.Dispose();
            }
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);

            listener.Start();

            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
