// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Archive;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Time.Testing;

using ZstdSharp;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class JetstreamArchiveTests
{
    private const string Checksum = "0123456789abcdef";
    private const string Segment = "seg_0000000000.jss";
    private const string TestDid = "did:plc:g6ylltenitt4tp27bpwalh7b";
    private static readonly Uri s_server = new("wss://test.internal:443/some/path?ignored=1");

    private sealed class SignalingTimeProvider : FakeTimeProvider
    {
        private readonly TaskCompletionSource _quotaDelayScheduled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task QuotaDelayScheduled => _quotaDelayScheduled.Task;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ITimer timer = base.CreateTimer(callback, state, dueTime, period);
            if (dueTime == TimeSpan.FromSeconds(1))
            {
                _quotaDelayScheduled.TrySetResult();
            }

            return timer;
        }
    }

    [Theory]
    [InlineData("http://archive.example")]
    [InlineData("ws://archive.example")]
    public async Task ArchiveRefusesToSendApiKeyOverInsecureRemoteTransport(string service)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, _ =>
            throw new InvalidOperationException("The archive request must not be sent."));
        using HttpClient client = server.CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() => AtProtoServer.PlanSnapshot(
            new SnapshotRequest(), new Uri(service), "test-key", client,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ArchiveMethodsApplyOptionsOnlyWhenNoClientIsSupplied()
    {
        HttpClientOptions invalidTimeout = new(timeout: TimeSpan.Zero);
        Uri service = TestServerBuilder.DefaultUri;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => AtProtoServer.PlanSnapshot(
            new SnapshotRequest(), service, "test-key", httpClientOptions: invalidTimeout,
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => AtProtoServer.ListSegments(
            service, "test-key", httpClientOptions: invalidTimeout,
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => AtProtoServer.GetBlock(
            Segment, 0, service, "test-key", httpClientOptions: invalidTimeout,
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => AtProtoServer.GetSegment(
            Segment, service, "test-key", httpClientOptions: invalidTimeout,
            cancellationToken: TestContext.Current.CancellationToken));

        using TestServer server = TestServerBuilder.CreateServer(service, async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"plannedThroughSeq":0,"sealedTipSeq":0,"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}""");
        });
        using HttpClient supplied = server.CreateClient();
        AtProtoHttpResult<SnapshotPlan> plan = await AtProtoServer.PlanSnapshot(
            new SnapshotRequest(), service, "test-key", supplied, httpClientOptions: invalidTimeout,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(plan.Succeeded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArchiveRejectsNullSegmentLists(bool plan)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(plan
                ? """{"plannedThroughSeq":0,"sealedTipSeq":0,"segments":null,"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}"""
                : """{"cursor":null,"segments":null}""");
        });
        using HttpClient client = server.CreateClient();
        if (plan)
        {
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => AtProtoServer.PlanSnapshot(
                new SnapshotRequest(), TestServerBuilder.DefaultUri, "test-key", client,
                cancellationToken: TestContext.Current.CancellationToken));
        }
        else
        {
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => AtProtoServer.ListSegments(
                TestServerBuilder.DefaultUri, "test-key", client,
                cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task ArchiveRejectsNullPlanStatistics()
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"plannedThroughSeq":0,"sealedTipSeq":0,"segments":[],"stats":null}""");
        });
        using HttpClient client = server.CreateClient();
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => AtProtoServer.PlanSnapshot(
            new SnapshotRequest(), TestServerBuilder.DefaultUri, "test-key", client,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true, "segments", "null")]
    [InlineData(true, "segments", "[null]")]
    [InlineData(true, "name", "null")]
    [InlineData(true, "checksum", "null")]
    [InlineData(true, "blocks", "[null]")]
    [InlineData(false, "segments", "[null]")]
    [InlineData(false, "name", "null")]
    [InlineData(false, "checksum", "null")]
    public async Task ArchiveRejectsNullPlannerFieldsAndEntries(bool plan, string field, string replacement)
    {
        string body = plan
            ? $$$"""{"plannedThroughSeq":1,"sealedTipSeq":1,"segments":[{"name":"{{{Segment}}}","index":0,"checksum":"{{{Checksum}}}","minSeq":0,"maxSeq":1,"mode":"blocks","blocks":[{"first":0,"last":0}]}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":1,"entries":1}}"""
            : $$$"""{"cursor":null,"segments":[{"name":"{{{Segment}}}","index":0,"sizeBytes":1,"checksum":"{{{Checksum}}}","eventCount":1,"minSeq":0,"maxSeq":1,"minWitnessedAt":0,"maxWitnessedAt":0}]}""";
        string original = field switch
        {
            "segments" => plan
                ? $$$"""[{"name":"{{{Segment}}}","index":0,"checksum":"{{{Checksum}}}","minSeq":0,"maxSeq":1,"mode":"blocks","blocks":[{"first":0,"last":0}]}]"""
                : $$$"""[{"name":"{{{Segment}}}","index":0,"sizeBytes":1,"checksum":"{{{Checksum}}}","eventCount":1,"minSeq":0,"maxSeq":1,"minWitnessedAt":0,"maxWitnessedAt":0}]""",
            "name" => $"\"{Segment}\"",
            "checksum" => $"\"{Checksum}\"",
            "blocks" => """[{"first":0,"last":0}]""",
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        body = body.Replace($"\"{field}\":{original}", $"\"{field}\":{replacement}", StringComparison.Ordinal);
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body);
        });
        using HttpClient client = server.CreateClient();

        if (plan)
        {
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => AtProtoServer.PlanSnapshot(
                new SnapshotRequest(), TestServerBuilder.DefaultUri, "test-key", client,
                cancellationToken: TestContext.Current.CancellationToken));
        }
        else
        {
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => AtProtoServer.ListSegments(
                TestServerBuilder.DefaultUri, "test-key", client,
                cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Theory]
    [InlineData("SegmentNotFound", false)]
    [InlineData("BlockNotFound", true)]
    [InlineData("UnknownEndpoint", false)]
    public async Task DownloadReportsNamedNotFoundErrors(string error, bool block)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 404;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($$$"""{"error":"{{{error}}}","message":"Archive entry is missing"}""");
        });
        using HttpClient client = server.CreateClient();
        await using ArchiveDownload download = new(Segment, block ? 0 : null, Checksum, "test-key",
            TestServerBuilder.DefaultUri, client, 0, new JetstreamMetrics(null));

        if (error == "UnknownEndpoint")
        {
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                download.ReadExactlyAsync(new byte[1], TestContext.Current.CancellationToken));
            Assert.Contains("does not serve", exception.Message, StringComparison.Ordinal);
        }
        else
        {
            HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
                download.ReadExactlyAsync(new byte[1], TestContext.Current.CancellationToken));
            Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
            Assert.Contains(error, exception.Message, StringComparison.Ordinal);
            Assert.Contains("Archive entry is missing", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task DownloadReadWaitsForQuotaBeforeRequestingMoreBytes()
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.Headers.ETag = $"\"{Checksum}:0\"";
            context.Response.Headers["headwind-quota-burst-bytes"] = "2";
            context.Response.Headers["headwind-quota-refill-bytes"] = "1";
            context.Response.Headers["headwind-quota-refill-period-seconds"] = "1";
            context.Response.ContentLength = 3;
            await context.Response.Body.WriteAsync(new byte[] { 1, 2, 3 });
        });
        using HttpClient client = server.CreateClient();
        await using ArchiveDownload download = new(Segment, 0, Checksum, "test-key",
            TestServerBuilder.DefaultUri, client, 0, new JetstreamMetrics(null),
            readTimeout: TimeSpan.FromMilliseconds(100));
        byte[] bytes = new byte[3];
        System.Diagnostics.Stopwatch elapsed = System.Diagnostics.Stopwatch.StartNew();
        await download.ReadExactlyAsync(bytes, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3], bytes);
        Assert.True(elapsed.Elapsed >= TimeSpan.FromMilliseconds(850),
            $"Quota was bypassed: the download completed in {elapsed.Elapsed}.");
    }

    [Fact]
    public async Task DownloadQuotaRefillsUsingTheConfiguredTimeProvider()
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.Headers.ETag = $"\"{Checksum}:0\"";
            context.Response.Headers["headwind-quota-burst-bytes"] = "2";
            context.Response.Headers["headwind-quota-refill-bytes"] = "1";
            context.Response.Headers["headwind-quota-refill-period-seconds"] = "1";
            context.Response.ContentLength = 3;
            await context.Response.Body.WriteAsync(new byte[] { 1, 2, 3 });
        });
        using HttpClient client = server.CreateClient();
        SignalingTimeProvider timeProvider = new();
        await using ArchiveDownload download = new(Segment, 0, Checksum, "test-key",
            TestServerBuilder.DefaultUri, client, 0, new JetstreamMetrics(null), timeProvider: timeProvider);
        byte[] bytes = new byte[3];
        Task read = download.ReadExactlyAsync(bytes, TestContext.Current.CancellationToken);

        await timeProvider.QuotaDelayScheduled.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(2, download.Position);
        Assert.False(read.IsCompleted);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await read.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal([1, 2, 3], bytes);
    }

    [Fact]
    public async Task DownloadResumesAfterAnInterruptedBody()
    {
        int requests = 0;
        using HttpClient client = new(new ArchiveTestHandler(request =>
        {
            int attempt = Interlocked.Increment(ref requests);
            if (attempt == 2)
            {
                Assert.Equal("bytes=2-", request.Headers.Range?.ToString());
                Assert.Equal($"\"{Checksum}:0\"", request.Headers.IfRange?.ToString());
            }

            HttpResponseMessage response = new(attempt == 1 ? HttpStatusCode.OK : HttpStatusCode.PartialContent)
            {
                Content = new StreamContent(attempt == 1
                    ? new InterruptedStream([1, 2])
                    : new ChunkedStream([3, 4]))
            };
            response.Headers.ETag = new EntityTagHeaderValue($"\"{Checksum}:0\"");
            response.Content.Headers.ContentLength = attempt == 1 ? 4 : 2;
            if (attempt == 2)
            {
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(2, 3);
            }

            return response;
        }));
        await using ArchiveDownload download = new(Segment, 0, Checksum, "test-key",
            TestServerBuilder.DefaultUri, client, 0, new JetstreamMetrics(null));
        byte[] bytes = new byte[4];
        await download.ReadExactlyAsync(bytes, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3, 4], bytes);
        Assert.Equal(4, download.Position);
        Assert.Equal(2, requests);
    }

    [Fact]
    public async Task DownloadResumesAfterAStalledBody()
    {
        int requests = 0;
        using HttpClient client = new(new ArchiveTestHandler(request =>
        {
            int attempt = Interlocked.Increment(ref requests);
            if (attempt == 2)
            {
                Assert.Equal("bytes=2-", request.Headers.Range?.ToString());
                Assert.Equal($"\"{Checksum}:0\"", request.Headers.IfRange?.ToString());
            }

            HttpResponseMessage response = new(attempt == 1 ? HttpStatusCode.OK : HttpStatusCode.PartialContent)
            {
                Content = new StreamContent(attempt == 1
                    ? new StallingStream([1, 2]) : new ChunkedStream([3, 4]))
            };
            response.Headers.ETag = new EntityTagHeaderValue($"\"{Checksum}:0\"");
            response.Content.Headers.ContentLength = attempt == 1 ? 4 : 2;
            if (attempt == 2)
            {
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(2, 3, 4);
            }

            return response;
        }));
        await using ArchiveDownload download = new(Segment, 0, Checksum, "test-key",
            TestServerBuilder.DefaultUri, client, 0, new JetstreamMetrics(null),
            readTimeout: TimeSpan.FromMilliseconds(80));

        byte[] bytes = new byte[4];
        await download.ReadExactlyAsync(bytes, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3, 4], bytes);
        Assert.Equal(2, requests);
    }

    [Fact]
    public async Task DownloadReadTimeoutResetsWhenBytesArrive()
    {
        int requests = 0;
        using HttpClient client = new(new ArchiveTestHandler(_ =>
        {
            Interlocked.Increment(ref requests);
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StreamContent(new SlowChunkedStream([1, 2, 3, 4]))
            };
            response.Headers.ETag = new EntityTagHeaderValue($"\"{Checksum}:0\"");
            response.Content.Headers.ContentLength = 4;
            return response;
        }));
        await using ArchiveDownload download = new(Segment, 0, Checksum, "test-key",
            TestServerBuilder.DefaultUri, client, 0, new JetstreamMetrics(null),
            readTimeout: TimeSpan.FromMilliseconds(300));

        byte[] bytes = new byte[4];
        await download.ReadExactlyAsync(bytes, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3, 4], bytes);
        Assert.Equal(1, requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ArchiveReadTimeoutMustBePositive(int milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new JetstreamOptions { ArchiveReadTimeout = TimeSpan.FromMilliseconds(milliseconds) });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new JetstreamOptions { ArchiveReadTimeout = TimeSpan.MaxValue });
    }

    [Fact]
    public async Task CallerCancellationDoesNotResumeStalledDownload()
    {
        int requests = 0;
        TaskCompletionSource stalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using HttpClient client = new(new ArchiveTestHandler(_ =>
        {
            Interlocked.Increment(ref requests);
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StreamContent(new StallingStream([], () => stalled.TrySetResult()))
            };
            response.Headers.ETag = new EntityTagHeaderValue($"\"{Checksum}:0\"");
            response.Content.Headers.ContentLength = 1;
            return response;
        }));
        await using ArchiveDownload download = new(Segment, 0, Checksum, "test-key",
            TestServerBuilder.DefaultUri, client, 0, new JetstreamMetrics(null),
            readTimeout: TimeSpan.FromMinutes(1));
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        Task read = download.ReadExactlyAsync(new byte[1], cancellation.Token);
        try
        {
            await stalled.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
        finally
        {
            await cancellation.CancelAsync();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            read.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task DownloadStopsAfterRepeatedStalledBodies()
    {
        int requests = 0;
        using HttpClient client = new(new ArchiveTestHandler(_ =>
        {
            Interlocked.Increment(ref requests);
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StreamContent(new StallingStream([]))
            };
            response.Headers.ETag = new EntityTagHeaderValue($"\"{Checksum}:0\"");
            response.Content.Headers.ContentLength = 1;
            return response;
        }));
        await using ArchiveDownload download = new(Segment, 0, Checksum, "test-key",
            TestServerBuilder.DefaultUri, client, 0, new JetstreamMetrics(null),
            readTimeout: TimeSpan.FromMilliseconds(40));

        IOException error = await Assert.ThrowsAsync<IOException>(() =>
            download.ReadExactlyAsync(new byte[1], TestContext.Current.CancellationToken));
        Assert.Contains("stalled", error.Message, StringComparison.Ordinal);
        Assert.Equal(4, requests);
    }

    [Theory]
    [InlineData(true, 0, true)]
    [InlineData(true, 1, false)]
    [InlineData(true, -1, false)]
    [InlineData(false, 0, true)]
    [InlineData(false, 1, false)]
    [InlineData(false, -1, false)]
    public async Task InitialPartialDownloadRequiresRangeStartingAtZero(bool block, int rangeStart, bool valid)
    {
        using HttpClient client = new(new ArchiveTestHandler(request =>
        {
            Assert.Null(request.Headers.Range);
            HttpResponseMessage response = new(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent([1, 2])
            };
            response.Headers.ETag = new EntityTagHeaderValue(
                block ? $"\"{Checksum}:0\"" : $"\"{Checksum}\"");
            if (rangeStart >= 0)
            {
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(rangeStart, rangeStart + 1, 4);
            }

            return response;
        }));

        Task<AtProtoHttpResult<Stream>> get = block
            ? AtProtoServer.GetBlock(Segment, 0, TestServerBuilder.DefaultUri, "test-key", client,
                cancellationToken: TestContext.Current.CancellationToken)
            : AtProtoServer.GetSegment(Segment, TestServerBuilder.DefaultUri, "test-key", client,
                cancellationToken: TestContext.Current.CancellationToken);
        if (valid)
        {
            AtProtoHttpResult<Stream> result = await get;
            Assert.True(result.Succeeded);
            await using Stream stream = result.Result;
            Assert.Equal(1, stream.ReadByte());
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => get);
        }
    }

    [Fact]
    public async Task DownloadStopsAfterRepeatedInterruptedBodies()
    {
        int requests = 0;
        using HttpClient client = new(new ArchiveTestHandler(_ =>
        {
            Interlocked.Increment(ref requests);
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StreamContent(new InterruptedStream([]))
            };
            response.Headers.ETag = new EntityTagHeaderValue($"\"{Checksum}:0\"");
            response.Content.Headers.ContentLength = 1;
            return response;
        }));
        await using ArchiveDownload download = new(Segment, 0, Checksum, "test-key",
            TestServerBuilder.DefaultUri, client, 0, new JetstreamMetrics(null));

        await Assert.ThrowsAsync<IOException>(() =>
            download.ReadExactlyAsync(new byte[1], TestContext.Current.CancellationToken));
        Assert.Equal(4, requests);
    }

    [Fact]
    public async Task DownloadLimitsResumesAcrossSuccessfulShortReads()
    {
        int requests = 0;
        using HttpClient client = new(new ArchiveTestHandler(request =>
        {
            int attempt = Interlocked.Increment(ref requests);
            Assert.True(attempt <= 4, "The download exceeded its resume limit.");
            if (attempt > 1)
            {
                Assert.Equal($"bytes={attempt - 1}-", request.Headers.Range?.ToString());
            }

            HttpResponseMessage response = new(attempt == 1 ? HttpStatusCode.OK : HttpStatusCode.PartialContent)
            {
                Content = new StreamContent(new InterruptedStream([checked((byte)attempt)]))
            };
            response.Headers.ETag = new EntityTagHeaderValue($"\"{Checksum}:0\"");
            response.Content.Headers.ContentLength = attempt == 1 ? 10 : 1;
            if (attempt > 1)
            {
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(attempt - 1, attempt - 1, 10);
            }

            return response;
        }));
        await using ArchiveDownload download = new(Segment, 0, Checksum, "test-key",
            TestServerBuilder.DefaultUri, client, 0, new JetstreamMetrics(null));
        byte[] bytes = new byte[10];

        await Assert.ThrowsAsync<IOException>(() =>
            download.ReadExactlyAsync(bytes, TestContext.Current.CancellationToken));
        Assert.Equal([1, 2, 3, 4], bytes[..4]);
        Assert.Equal(4, download.Position);
        Assert.Equal(4, requests);
    }

    [Fact]
    public async Task PlannerRetriesAnHttpDateRetryAfter()
    {
        int plans = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (Interlocked.Increment(ref plans) == 1)
            {
                context.Response.StatusCode = 429;
                context.Response.Headers.RetryAfter = DateTimeOffset.UtcNow.AddSeconds(1).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                return;
            }

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"plannedThroughSeq":0,"sealedTipSeq":0,"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}""");
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        await foreach (JetstreamEvent _ in jetstream.SnapshotAsync(
            new SnapshotRequest { AfterSeq = 0 }, cancellationToken: TestContext.Current.CancellationToken))
        {
        }

        Assert.Equal(2, plans);
    }

    [Theory]
    [InlineData("blocks", 65, false, false)]
    [InlineData("segment", 65, false, false)]
    [InlineData("blocks", 129, false, false)]
    [InlineData("segment", 129, false, false)]
    [InlineData("blocks", 0, true, false)]
    [InlineData("segment", 0, true, false)]
    [InlineData("blocks", 65, false, true)]
    [InlineData("segment", 65, false, true)]
    public async Task SnapshotCanSkipAnInvalidRecordAndResumePastItsBlock(
        string mode, int nestingDepth, bool invalidDid, bool skipRemainder)
    {
        System.Formats.Cbor.CborWriter postWriter = new(System.Formats.Cbor.CborConformanceMode.Canonical);
        postWriter.WriteStartMap(3);
        postWriter.WriteTextString("text");
        postWriter.WriteTextString("hello");
        postWriter.WriteTextString("$type");
        postWriter.WriteTextString("app.bsky.feed.post");
        postWriter.WriteTextString("createdAt");
        postWriter.WriteTextString("2025-01-01T00:00:00.000Z");
        postWriter.WriteEndMap();
        byte[] record = postWriter.Encode();
        byte[] deeplyNestedRecord = new byte[1 + 1 + 4 + nestingDepth + 1];
        deeplyNestedRecord[0] = 0xA1;
        deeplyNestedRecord[1] = 0x64;
        "test"u8.CopyTo(deeplyNestedRecord.AsSpan(2));
        deeplyNestedRecord.AsSpan(6, nestingDepth).Fill(0x81);
        deeplyNestedRecord[^1] = 0xF6;

        using Compressor compressor = new();
        byte[] frame = compressor.Wrap(PostBlock(
            (10, record, TestDid),
            (11, deeplyNestedRecord, invalidDid ? "not-a-did" : TestDid),
            (12, record, TestDid))).ToArray();
        byte[] segment = new byte[256 + 8 + frame.Length];
        "jss0"u8.CopyTo(segment);
        BinaryPrimitives.WriteUInt16LittleEndian(segment.AsSpan(12, 2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(segment.AsSpan(14, 4), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(segment.AsSpan(256, 8), checked((ulong)frame.Length));
        frame.CopyTo(segment.AsSpan(264));
        int plans = 0;
        int downloads = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (context.Request.Path.ToString().EndsWith(".planSnapshot", StringComparison.Ordinal))
            {
                plans++;
                context.Response.ContentType = "application/json";
                string ranges = mode == "blocks" ? ""","blocks":[{"first":0,"last":0}]""" : "";
                await context.Response.WriteAsync(
                    $$$"""{"plannedThroughSeq":12,"sealedTipSeq":12,"segments":[{"name":"{{{Segment}}}","index":0,"checksum":"{{{Checksum}}}","minSeq":10,"maxSeq":12,"mode":"{{{mode}}}"{{{ranges}}}}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":1,"entries":3}}""");
                return;
            }

            downloads++;
            context.Response.Headers.ETag = mode == "blocks" ? $"\"{Checksum}:0\"" : $"\"{Checksum}\"";
            context.Response.ContentType = "application/octet-stream";
            byte[] content = mode == "blocks" ? frame : segment;
            context.Response.ContentLength = content.Length;
            await context.Response.Body.WriteAsync(content);
        });

        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });

        async Task<(List<long?> Sequences, List<SnapshotCheckpoint> Checkpoints, Exception? Error)> Enumerate(
            SnapshotCheckpoint? checkpoint,
            Func<long?, Exception, JetstreamArchiveErrorAction>? onArchiveError = null)
        {
            List<long?> sequences = [];
            List<SnapshotCheckpoint> checkpoints = [];
            Exception? error = null;
            try
            {
                await foreach (JetstreamEvent evt in jetstream.SnapshotAsync(new SnapshotRequest(), checkpoint,
                    onCheckpoint: progress => checkpoints.Add(progress),
                    cancellationToken: TestContext.Current.CancellationToken,
                    onArchiveError: onArchiveError))
                {
                    sequences.Add(evt.Sequence);
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or System.Text.Json.JsonException or
                ArgumentException or NsidFormatException or RecordKeyFormatException or OverflowException)
            {
                error = exception;
            }

            return (sequences, checkpoints, error);
        }

        var firstAttempt = await Enumerate(null);
        Exception firstError = Assert.IsAssignableFrom<Exception>(firstAttempt.Error);
        if (invalidDid)
        {
            Assert.IsType<ArgumentException>(firstError);
        }
        else if (nestingDepth > 128)
        {
            Assert.IsType<InvalidDataException>(firstError);
            Assert.Equal("The value is nested more than 128 levels deep.", firstError.Message);
        }
        else
        {
            Assert.IsAssignableFrom<System.Text.Json.JsonException>(firstError);
        }

        Assert.Equal([10], firstAttempt.Sequences);
        SnapshotCheckpoint lastCheckpoint = Assert.Single(firstAttempt.Checkpoints);
        Assert.Null(lastCheckpoint.SegmentName);
        Assert.Equal(0, lastCheckpoint.NextBlockIndex);

        var resumedAttempt = await Enumerate(lastCheckpoint);
        Assert.Equal(firstError.GetType(), resumedAttempt.Error?.GetType());
        Assert.Equal(firstError.Message, resumedAttempt.Error?.Message);
        Assert.Equal([10], resumedAttempt.Sequences);
        Assert.Empty(resumedAttempt.Checkpoints);

        List<(long Sequence, Type ExceptionType, string Message)> recordErrors = [];
        var skippedAttempt = await Enumerate(null, (sequence, exception) =>
        {
            recordErrors.Add((
                sequence ?? throw new InvalidOperationException("A record failure has no sequence."),
                exception.GetType(), exception.Message));
            return skipRemainder
                ? JetstreamArchiveErrorAction.SkipBlock
                : JetstreamArchiveErrorAction.SkipRecord;
        });
        Assert.Null(skippedAttempt.Error);
        long?[] expectedSequences = skipRemainder ? [10] : [10, 12];
        Assert.Equal(expectedSequences, skippedAttempt.Sequences);
        Assert.Equal([(11L, firstError.GetType(), firstError.Message)], recordErrors);
        SnapshotCheckpoint completedBlock = Assert.Single(skippedAttempt.Checkpoints,
            progress => progress.SegmentName is not null);
        Assert.Equal(Segment, completedBlock.SegmentName);
        Assert.Equal(1, completedBlock.NextBlockIndex);

        var resumedAfterSkip = await Enumerate(completedBlock);
        Assert.Null(resumedAfterSkip.Error);
        Assert.Empty(resumedAfterSkip.Sequences);
        Assert.Equal(4, plans);
        Assert.Equal(mode == "blocks" ? 3 : 4, downloads);
    }

    [Theory]
    [InlineData("blocks")]
    [InlineData("segment")]
    public async Task SnapshotCanSkipAnInvalidBlockAndContinueWithTheNextBlock(string mode)
    {
        System.Formats.Cbor.CborWriter postWriter = new(System.Formats.Cbor.CborConformanceMode.Canonical);
        postWriter.WriteStartMap(3);
        postWriter.WriteTextString("text");
        postWriter.WriteTextString("hello");
        postWriter.WriteTextString("$type");
        postWriter.WriteTextString("app.bsky.feed.post");
        postWriter.WriteTextString("createdAt");
        postWriter.WriteTextString("2025-01-01T00:00:00.000Z");
        postWriter.WriteEndMap();
        byte[] record = postWriter.Encode();

        using Compressor compressor = new();
        byte[] firstFrame = compressor.Wrap(PostBlock((10, record, TestDid))).ToArray();
        byte[] invalidFrame = compressor.Wrap([1, 2, 3]).ToArray();
        byte[] lastFrame = compressor.Wrap(PostBlock((12, record, TestDid))).ToArray();
        byte[][] frames = [firstFrame, invalidFrame, lastFrame];
        byte[] segment = SegmentWithBlocks(frames);
        int plans = 0;
        int downloads = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (context.Request.Path.ToString().EndsWith(".planSnapshot", StringComparison.Ordinal))
            {
                plans++;
                context.Response.ContentType = "application/json";
                string ranges = mode == "blocks" ? ""","blocks":[{"first":0,"last":2}]""" : "";
                await context.Response.WriteAsync(
                    $$$"""{"plannedThroughSeq":12,"sealedTipSeq":12,"segments":[{"name":"{{{Segment}}}","index":0,"checksum":"{{{Checksum}}}","minSeq":10,"maxSeq":12,"mode":"{{{mode}}}"{{{ranges}}}}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":3,"entries":2}}""");
                return;
            }

            downloads++;
            context.Response.ContentType = "application/octet-stream";
            if (mode == "blocks")
            {
                int blockIndex = int.Parse(
                    context.Request.Query["blockIndex"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
                context.Response.Headers.ETag = $"\"{Checksum}:{blockIndex}\"";
                context.Response.ContentLength = frames[blockIndex].Length;
                await context.Response.Body.WriteAsync(frames[blockIndex]);
            }
            else
            {
                context.Response.Headers.ETag = $"\"{Checksum}\"";
                context.Response.ContentLength = segment.Length;
                await context.Response.Body.WriteAsync(segment);
            }
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        List<long?> sequences = [];
        List<(long? Sequence, Type ExceptionType)> archiveErrors = [];
        List<SnapshotCheckpoint> checkpoints = [];
        SnapshotRequest request = new();

        await foreach (JetstreamEvent evt in jetstream.SnapshotAsync(request,
            onCheckpoint: progress => checkpoints.Add(progress),
            onArchiveError: (sequence, exception) =>
            {
                archiveErrors.Add((sequence, exception.GetType()));
                return JetstreamArchiveErrorAction.SkipBlock;
            },
            cancellationToken: TestContext.Current.CancellationToken))
        {
            sequences.Add(evt.Sequence);
        }

        Assert.Equal([10, 12], sequences);
        Assert.Equal([(null, typeof(InvalidDataException))], archiveErrors);
        SnapshotCheckpoint completedBlock = Assert.Single(checkpoints,
            progress => progress.SegmentName is not null && progress.NextBlockIndex == 3);
        Assert.Equal(Segment, completedBlock.SegmentName);
        Assert.Equal(3, completedBlock.NextBlockIndex);

        List<long?> resumedSequences = [];
        await foreach (JetstreamEvent evt in jetstream.SnapshotAsync(request, completedBlock,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            resumedSequences.Add(evt.Sequence);
        }

        Assert.Empty(resumedSequences);
        Assert.Equal(2, plans);
        Assert.Equal(mode == "blocks" ? 3 : 2, downloads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SegmentSnapshotObservesCancellationBetweenBufferedRows(bool resumedSegment)
    {
        System.Formats.Cbor.CborWriter postWriter = new(System.Formats.Cbor.CborConformanceMode.Canonical);
        postWriter.WriteStartMap(3);
        postWriter.WriteTextString("text");
        postWriter.WriteTextString("hello");
        postWriter.WriteTextString("$type");
        postWriter.WriteTextString("app.bsky.feed.post");
        postWriter.WriteTextString("createdAt");
        postWriter.WriteTextString("2025-01-01T00:00:00.000Z");
        postWriter.WriteEndMap();
        byte[] record = postWriter.Encode();
        using Compressor compressor = new();
        byte[] firstFrame = compressor.Wrap(PostBlock((10, record, TestDid))).ToArray();
        byte[] secondFrame = compressor.Wrap(
            PostBlock((11, record, TestDid), (12, record, TestDid))).ToArray();
        byte[] segment = SegmentWithBlocks(firstFrame, secondFrame);
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (context.Request.Path.ToString().EndsWith(".planSnapshot", StringComparison.Ordinal))
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    $$$"""{"plannedThroughSeq":12,"sealedTipSeq":12,"segments":[{"name":"{{{Segment}}}","index":0,"checksum":"{{{Checksum}}}","minSeq":10,"maxSeq":12,"mode":"segment"}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":2,"entries":3}}""");
                return;
            }

            context.Response.Headers.ETag = $"\"{Checksum}\"";
            context.Response.ContentType = "application/octet-stream";
            string range = context.Request.Headers.Range.ToString();
            if (range.StartsWith("bytes=", StringComparison.Ordinal))
            {
                int offset = int.Parse(range.AsSpan("bytes=".Length, range.Length - "bytes=".Length - 1),
                    System.Globalization.CultureInfo.InvariantCulture);
                context.Response.StatusCode = StatusCodes.Status206PartialContent;
                context.Response.Headers.ContentRange =
                    new ContentRangeHeaderValue(offset, segment.Length - 1, segment.Length).ToString();
                context.Response.ContentLength = segment.Length - offset;
                await context.Response.Body.WriteAsync(segment.AsMemory(offset));
            }
            else
            {
                context.Response.ContentLength = segment.Length;
                await context.Response.Body.WriteAsync(segment);
            }
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        SnapshotRequest request = new();
        SnapshotCheckpoint? checkpoint = resumedSegment
            ? new SnapshotCheckpoint
            {
                SealedTipSeq = 12,
                PlanAfterSeq = 0,
                RequestFingerprint = request.Fingerprint(s_server),
                SegmentName = Segment,
                SegmentChecksum = Checksum,
                NextBlockIndex = 1,
                NextByteOffset = 256 + 8 + firstFrame.Length
            }
            : null;
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        IAsyncEnumerator<JetstreamEvent> events = jetstream.SnapshotAsync(request, checkpoint,
            cancellationToken: cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        await using (events.ConfigureAwait(false))
        {
            Assert.True(await events.MoveNextAsync());
            Assert.Equal(resumedSegment ? 11 : 10, events.Current.Sequence);
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await events.MoveNextAsync();
            });
        }
    }

    [Theory]
    [InlineData("blocks")]
    [InlineData("segment")]
    public async Task SnapshotUsesConfiguredHostAndDecodesBothPlanModes(string mode)
    {
        Dictionary<string, long> measurements = [];
        using var meterFactory = new TestMeterFactory();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == JetstreamMetrics.MeterName &&
                ReferenceEquals(instrument.Meter.Scope, meterFactory))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            lock (measurements)
            {
                measurements[instrument.Name] = measurements.GetValueOrDefault(instrument.Name) + measurement;
            }
        });
        listener.Start();
        List<string> requests = [];
        bool rateLimited = false;
        using Compressor compressor = new();
        byte[] frame = compressor.Wrap(TwoRecordBlock()).ToArray();
        byte[] segment = new byte[256 + 8 + frame.Length];
        "jss0"u8.CopyTo(segment);
        BinaryPrimitives.WriteUInt16LittleEndian(segment.AsSpan(12, 2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(segment.AsSpan(14, 4), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(segment.AsSpan(256, 8), checked((ulong)frame.Length));
        frame.CopyTo(segment.AsSpan(264));

        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal(context.Request.Query.ContainsKey("redirected") ? "" : "Bearer test-key",
                context.Request.Headers.Authorization.ToString());
            Assert.DoesNotContain("ignored", context.Request.QueryString.ToString(), StringComparison.Ordinal);
            requests.Add(context.Request.Path.ToString());
            string endpoint = context.Request.Path.ToString().Split('.').Last();
            if (endpoint == "planSnapshot")
            {
                using StreamReader reader = new(context.Request.Body);
                string body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
                Assert.Contains("\"afterSeq\":0", body, StringComparison.Ordinal);
                Assert.Contains("app.bsky.feed.post", body, StringComparison.Ordinal);
                context.Response.ContentType = "application/json";
                string plan = mode == "blocks"
                    ? """{"plannedThroughSeq":42,"sealedTipSeq":42,"segments":[{"name":"seg_0000000000.jss","index":0,"checksum":"0123456789abcdef","minSeq":1,"maxSeq":42,"mode":"blocks","blocks":[{"first":0,"last":0}]}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":1,"entries":1}}"""
                    : """{"plannedThroughSeq":42,"sealedTipSeq":42,"segments":[{"name":"seg_0000000000.jss","index":0,"checksum":"0123456789abcdef","minSeq":1,"maxSeq":42,"mode":"segment"}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":1,"entries":1}}""";
                await context.Response.WriteAsync(plan);
            }
            else
            {
                if (mode == "blocks" && !rateLimited)
                {
                    rateLimited = true;
                    context.Response.StatusCode = 429;
                    context.Response.Headers.RetryAfter = "0";
                    return;
                }

                if (mode == "blocks" && !context.Request.Query.ContainsKey("redirected"))
                {
                    context.Response.StatusCode = 307;
                    context.Response.Headers.Location =
                        $"{TestServerBuilder.DefaultUri}/xrpc/network.bsky.jetstream.getBlock?redirected=1";
                    return;
                }

                context.Response.Headers.ETag = mode == "blocks" ? $"\"{Checksum}:0\"" : $"\"{Checksum}\"";
                context.Response.ContentType = "application/octet-stream";
                context.Response.ContentLength = mode == "blocks" ? frame.Length : segment.Length;
                await context.Response.Body.WriteAsync(mode == "blocks" ? frame : segment);
            }
        });

        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server),
            uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false, MeterFactory = meterFactory });
        SnapshotCheckpoint? saved = null;
        List<JetstreamEvent> events = [];
        await foreach (JetstreamEvent evt in jetstream.SnapshotAsync(
            new SnapshotRequest
            {
                AfterSeq = 0,
                Collections = [new CollectionSelector("app.bsky.feed.post")]
            }, onCheckpoint: progress => saved = progress,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(evt);
        }

        JetstreamCommitEvent received = Assert.IsType<JetstreamCommitEvent>(Assert.Single(events));
        Assert.Equal(10, received.Sequence);
        Assert.Equal("app.bsky.feed.post", received.Commit.Collection.ToString());
        Assert.NotNull(saved);
        Assert.Equal(42, saved.SealedTipSeq);
        Assert.Equal(mode == "blocks" ? 4 : 2, requests.Count);
        Assert.Equal("/xrpc/network.bsky.jetstream.planSnapshot", requests[0]);
        Assert.Equal($"/xrpc/network.bsky.jetstream.get{(mode == "blocks" ? "Block" : "Segment")}", requests[1]);
        Assert.Equal(1, measurements["idunno.atproto.jetstream.total.archive_plans"]);
        Assert.Equal(1, measurements["idunno.atproto.jetstream.total.archive_blocks"]);
        Assert.Equal(1, measurements["idunno.atproto.jetstream.total.archive_segments"]);
        Assert.Equal(mode == "blocks" ? frame.Length : segment.Length,
            measurements["idunno.atproto.jetstream.total.archive_bytes"]);
        Assert.Equal(1, measurements["idunno.atproto.jetstream.total.archive_events"]);
        Assert.Equal(1, measurements["idunno.atproto.jetstream.total.archive_filtered_events"]);
        if (mode == "blocks")
        {
            Assert.Equal(1, measurements["idunno.atproto.jetstream.total.archive_rate_limits"]);
        }
    }

    [Fact]
    public async Task SnapshotFiltersRowsBeyondRequestedBoundAndUsesDidAndCollectionFilters()
    {
        using Compressor compressor = new();
        byte[] frame = compressor.Wrap(TwoRecordBlock()).ToArray();
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (context.Request.Path.ToString().EndsWith(".planSnapshot", StringComparison.Ordinal))
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    """{"plannedThroughSeq":10,"sealedTipSeq":10,"segments":[{"name":"seg_0000000000.jss","index":0,"checksum":"0123456789abcdef","minSeq":1,"maxSeq":11,"mode":"blocks","blocks":[{"first":0,"last":0}]}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":1,"entries":1}}""");
            }
            else
            {
                context.Response.Headers.ETag = $"\"{Checksum}:0\"";
                context.Response.ContentLength = frame.Length;
                await context.Response.Body.WriteAsync(frame);
            }
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        List<long?> received = [];
        await foreach (JetstreamEvent evt in jetstream.SnapshotAsync(
            new SnapshotRequest
            {
                BeforeSeq = 10,
                Dids = [new Did("did:plc:aaaaaaaaaaaaaaaaaaaaaaaa"), new Did(TestDid)],
                Collections = [new CollectionSelector("app.bsky.feed.*")]
            }, cancellationToken: TestContext.Current.CancellationToken))
        {
            received.Add(evt.Sequence);
        }

        Assert.Equal([10], received);
    }

    [Fact]
    public async Task SnapshotKeepsOriginalFiltersWhenCallerChangesLists()
    {
        using Compressor compressor = new();
        byte[] frame = compressor.Wrap(TwoRecordBlock()).ToArray();
        List<JetStreamEventKind> kinds = [JetStreamEventKind.Commit];
        List<Did> dids = [new(TestDid)];
        List<CollectionSelector> collections = [new("app.bsky.feed.post")];
        SnapshotRequest request = new() { Kinds = kinds, Dids = dids, Collections = collections };
        string fingerprint = request.Fingerprint(s_server);
        int plans = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (context.Request.Path.ToString().EndsWith(".planSnapshot", StringComparison.Ordinal))
            {
                plans++;
                using StreamReader reader = new(context.Request.Body);
                string body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
                Assert.Contains("app.bsky.feed.post", body, StringComparison.Ordinal);
                Assert.DoesNotContain("app.bsky.feed.like", body, StringComparison.Ordinal);
                Assert.Contains(TestDid, body, StringComparison.Ordinal);
                Assert.DoesNotContain("did:plc:aaaaaaaaaaaaaaaaaaaaaaaa", body, StringComparison.Ordinal);
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    """{"plannedThroughSeq":11,"sealedTipSeq":11,"segments":[{"name":"seg_0000000000.jss","index":0,"checksum":"0123456789abcdef","minSeq":10,"maxSeq":11,"mode":"blocks","blocks":[{"first":0,"last":0}]}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":1,"entries":2}}""");
            }
            else
            {
                context.Response.Headers.ETag = $"\"{Checksum}:0\"";
                context.Response.ContentLength = frame.Length;
                await context.Response.Body.WriteAsync(frame);
            }
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        SnapshotCheckpoint? saved = null;
        IAsyncEnumerable<JetstreamEvent> snapshot = jetstream.SnapshotAsync(
            request, onCheckpoint: progress => saved = progress,
            cancellationToken: TestContext.Current.CancellationToken);
        kinds.Clear();
        dids[0] = new Did("did:plc:aaaaaaaaaaaaaaaaaaaaaaaa");
        collections[0] = new CollectionSelector("app.bsky.feed.like");

        List<long?> received = [];
        await foreach (JetstreamEvent evt in snapshot)
        {
            received.Add(evt.Sequence);
        }

        Assert.Equal([10], received);
        Assert.Equal(fingerprint, saved?.RequestFingerprint);
        Assert.Equal(1, plans);
    }

    [Fact]
    public async Task SnapshotRejectsPlannerTipBeyondRequestedBound()
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"plannedThroughSeq":11,"sealedTipSeq":11,"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}""");
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (JetstreamEvent _ in jetstream.SnapshotAsync(
                new SnapshotRequest { BeforeSeq = 10 }, cancellationToken: TestContext.Current.CancellationToken))
            {
            }
        });
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(0, -1)]
    [InlineData(-1, 0)]
    public async Task SnapshotRejectsNegativePlannerCursors(long through, long tip)
    {
        int plans = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Interlocked.Increment(ref plans);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($$$"""
                {"plannedThroughSeq":{{{through}}},"sealedTipSeq":{{{tip}}},"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}
                """);
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        SnapshotCheckpoint? checkpoint = null;

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (JetstreamEvent _ in jetstream.SnapshotAsync(new SnapshotRequest(),
                onCheckpoint: progress => checkpoint = progress,
                cancellationToken: TestContext.Current.CancellationToken))
            {
                Assert.Fail("An invalid plan must not deliver archive events.");
            }
        });
        Assert.Null(checkpoint);
        Assert.Equal(1, plans);
    }

    [Theory]
    [InlineData(0, 10, 11)]
    [InlineData(5, 10, 20)]
    public async Task SnapshotRejectsPlanAdvancingBeyondSealedTip(long after, long tip, long through)
    {
        int plans = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Interlocked.Increment(ref plans);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($$$"""
                {"plannedThroughSeq":{{{through}}},"sealedTipSeq":{{{tip}}},"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}
                """);
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        SnapshotCheckpoint? checkpoint = null;

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (JetstreamEvent _ in jetstream.SnapshotAsync(new SnapshotRequest { AfterSeq = after },
                onCheckpoint: progress => checkpoint = progress,
                cancellationToken: TestContext.Current.CancellationToken))
            {
                Assert.Fail("An invalid plan must not deliver archive events.");
            }
        });
        Assert.Null(checkpoint);
        Assert.Equal(1, plans);
    }

    [Theory]
    [InlineData(50, 40, 40)]
    [InlineData(50, 40, 50)]
    [InlineData(40, 40, 40)]
    public async Task SnapshotAfterSealedTipProducesResumableEmptyCheckpoint(long after, long tip, long through)
    {
        int plans = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Interlocked.Increment(ref plans);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($$$"""
                {"plannedThroughSeq":{{{through}}},"sealedTipSeq":{{{tip}}},"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}
                """);
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        SnapshotRequest request = new() { AfterSeq = after };
        SnapshotCheckpoint? checkpoint = null;
        await foreach (JetstreamEvent _ in jetstream.SnapshotAsync(request,
            onCheckpoint: progress => checkpoint = progress,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            Assert.Fail("No archive events exist after the sealed tip.");
        }

        Assert.NotNull(checkpoint);
        Assert.Equal(after, checkpoint.PlanAfterSeq);
        Assert.Equal(tip, checkpoint.SealedTipSeq);
        if (after > tip)
        {
            Assert.Throws<ArgumentException>(() => jetstream.SnapshotAsync(request,
                checkpoint with { PlanAfterSeq = after + 1 },
                cancellationToken: TestContext.Current.CancellationToken));
            Assert.Throws<ArgumentException>(() => jetstream.SnapshotAsync(request,
                checkpoint with { PlanAfterSeq = tip + 1 },
                cancellationToken: TestContext.Current.CancellationToken));
        }
        Assert.NotNull(jetstream.ReplayAsync(request, checkpoint,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.NotNull(jetstream.ReplayAsync(request, checkpoint with { LiveAfterSeq = after },
            cancellationToken: TestContext.Current.CancellationToken));
        await foreach (JetstreamEvent _ in jetstream.SnapshotAsync(request, checkpoint,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            Assert.Fail("A resumed empty snapshot must remain empty.");
        }

        Assert.Equal(1, plans);
    }

    [Fact]
    public async Task SnapshotRejectsFutureCursorPlanContainingSegments()
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($$$"""
                {"plannedThroughSeq":40,"sealedTipSeq":40,"segments":[{"name":"{{{Segment}}}","index":0,"checksum":"{{{Checksum}}}","minSeq":1,"maxSeq":40,"mode":"blocks","blocks":[{"first":0,"last":0}]}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":1,"entries":1}}
                """);
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (JetstreamEvent _ in jetstream.SnapshotAsync(new SnapshotRequest { AfterSeq = 50 },
                cancellationToken: TestContext.Current.CancellationToken))
            {
            }
        });
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task SnapshotRejectsBlocksPlanWithoutRanges(string ranges)
    {
        int downloads = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (!context.Request.Path.ToString().EndsWith(".planSnapshot", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref downloads);
                return;
            }

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($$$"""
                {"plannedThroughSeq":42,"sealedTipSeq":42,"segments":[{"name":"{{{Segment}}}","index":0,"checksum":"{{{Checksum}}}","minSeq":1,"maxSeq":42,"mode":"blocks","blocks":{{{ranges}}}}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":0,"entries":1}}
                """);
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (JetstreamEvent _ in jetstream.SnapshotAsync(new SnapshotRequest(),
                cancellationToken: TestContext.Current.CancellationToken))
            {
            }
        });

        Assert.Contains("no block ranges", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, downloads);
    }

    [Fact]
    public async Task MissingKeyFailsBeforeAnyRequest()
    {
        using AtProtoJetstream jetstream = new(options: new JetstreamOptions { UseCompression = false });
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (JetstreamEvent _ in jetstream.SnapshotAsync(
                new SnapshotRequest(), cancellationToken: TestContext.Current.CancellationToken))
            {
            }
        });
        Assert.Contains("API key", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("blocks", "\"fedcba9876543210:0\"", "\"0123456789abcdef:0\"", "0")]
    [InlineData("blocks", null, "\"0123456789abcdef:0\"", "0")]
    [InlineData("segment", "\"fedcba9876543210\"", "\"0123456789abcdef\"", "(whole segment)")]
    [InlineData("segment", null, "\"0123456789abcdef\"", "(whole segment)")]
    public async Task SnapshotIdentifiesMismatchedDownloadEtag(
        string mode, string? responseEtag, string expectedEtag, string block)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (context.Request.Path.ToString().EndsWith(".planSnapshot", StringComparison.Ordinal))
            {
                context.Response.ContentType = "application/json";
                string plan = mode == "blocks"
                    ? """{"plannedThroughSeq":42,"sealedTipSeq":42,"segments":[{"name":"seg_0000000000.jss","index":0,"checksum":"0123456789abcdef","minSeq":1,"maxSeq":42,"mode":"blocks","blocks":[{"first":0,"last":0}]}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":1,"entries":1}}"""
                    : """{"plannedThroughSeq":42,"sealedTipSeq":42,"segments":[{"name":"seg_0000000000.jss","index":0,"checksum":"0123456789abcdef","minSeq":1,"maxSeq":42,"mode":"segment"}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":1,"entries":1}}""";
                await context.Response.WriteAsync(plan);
            }
            else
            {
                if (responseEtag is not null)
                {
                    context.Response.Headers.ETag = responseEtag;
                }

                context.Response.ContentLength = 1;
                await context.Response.Body.WriteAsync(new byte[1]);
            }
        });

        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server),
            uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (JetstreamEvent _ in jetstream.SnapshotAsync(
                new SnapshotRequest(), cancellationToken: TestContext.Current.CancellationToken))
            {
            }
        });

        Assert.Contains(Segment, error.Message, StringComparison.Ordinal);
        Assert.Contains($"block {block}, offset 0", error.Message, StringComparison.Ordinal);
        Assert.Contains($"planned checksum '{Checksum}'", error.Message, StringComparison.Ordinal);
        Assert.Contains($"expected ETag {expectedEtag}", error.Message, StringComparison.Ordinal);
        Assert.Contains($"response ETag {responseEtag ?? "(missing)"}", error.Message, StringComparison.Ordinal);
        Assert.Contains("HTTP 200", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("test-key", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RangedDownloadFollowsSignedRedirectWithoutForwardingCredentials()
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (!context.Request.Query.ContainsKey("redirected"))
            {
                Assert.Equal("Bearer test-key", context.Request.Headers.Authorization.ToString());
                context.Response.StatusCode = 307;
                context.Response.Headers.Location =
                    $"{TestServerBuilder.DefaultUri}/xrpc/network.bsky.jetstream.getBlock?redirected=1";
                return;
            }

            Assert.Empty(context.Request.Headers.Authorization.ToString());
            Assert.Equal("bytes=3-", context.Request.Headers.Range.ToString());
            Assert.Equal($"\"{Checksum}:7\"", context.Request.Headers.IfRange.ToString());
            context.Response.StatusCode = 206;
            context.Response.Headers.ContentRange = "bytes 3-5/6";
            context.Response.Headers.ETag = $"\"{Checksum}:7\"";
            context.Response.ContentLength = 3;
            byte[] body = [4, 5, 6];
            await context.Response.Body.WriteAsync(body);
        });

        using HttpClient client = server.CreateClient();
        AtProtoHttpResult<Stream> result = await AtProtoServer.GetBlock(
            Segment, 7, TestServerBuilder.DefaultUri, "test-key", client,
            offset: 3, etag: $"\"{Checksum}:7\"", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.PartialContent, result.StatusCode);
        await using Stream stream = result.Result;
        byte[] bytes = new byte[3];
        await stream.ReadExactlyAsync(bytes, TestContext.Current.CancellationToken);
        Assert.Equal([4, 5, 6], bytes);
    }

    [Fact]
    public async Task DownloadRejectsRemotePlaintextRedirectFromLoopback()
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, context =>
        {
            context.Response.StatusCode = 307;
            context.Response.Headers.Location = "http://archive.example/download?signature=secret";
            return Task.CompletedTask;
        });
        using HttpClient client = server.CreateClient();

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() => AtProtoServer.GetBlock(
            Segment, 0, new Uri("http://localhost:1234"), "test-key", client,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("invalid download redirect", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("signature=secret", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrossOriginRedirectRequiresSdkOwnedClient(bool trusted)
    {
        int requests = 0;
        using HttpClient client = new(new ArchiveTestHandler(request =>
        {
            if (Interlocked.Increment(ref requests) == 1)
            {
                Assert.Equal("Bearer test-key", request.Headers.Authorization?.ToString());
                HttpResponseMessage redirect = new(HttpStatusCode.TemporaryRedirect);
                redirect.Headers.Location = new Uri("https://archive.example/download?signature=secret");
                return redirect;
            }

            Assert.Equal("archive.example", request.RequestUri?.Host);
            Assert.Null(request.Headers.Authorization);
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1])
            };
            response.Headers.ETag = new EntityTagHeaderValue($"\"{Checksum}:0\"");
            return response;
        }));

        if (trusted)
        {
            AtProtoHttpResult<Stream> result = await AtProtoServer.GetBlockCore(
                Segment, 0, TestServerBuilder.DefaultUri, "test-key", client, 0, null, null,
                allowCrossOriginRedirect: true, TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded);
            await using Stream stream = result.Result;
            Assert.Equal(1, stream.ReadByte());
            Assert.Equal(2, requests);
        }
        else
        {
            InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                AtProtoServer.GetBlock(Segment, 0, TestServerBuilder.DefaultUri, "test-key", client,
                    cancellationToken: TestContext.Current.CancellationToken));
            Assert.Contains("invalid download redirect", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("signature=secret", error.Message, StringComparison.Ordinal);
            Assert.Equal(1, requests);
        }
    }

    [Fact]
    public async Task SdkOwnedClientRejectsCrossOriginPlaintextRedirect()
    {
        int requests = 0;
        using HttpClient client = new(new ArchiveTestHandler(_ =>
        {
            Interlocked.Increment(ref requests);
            HttpResponseMessage redirect = new(HttpStatusCode.TemporaryRedirect);
            redirect.Headers.Location = new Uri("http://localhost:2345/download");
            return redirect;
        }));

        await Assert.ThrowsAsync<InvalidDataException>(() => AtProtoServer.GetBlockCore(
            Segment, 0, new Uri("http://localhost:1234"), "test-key", client, 0, null, null,
            allowCrossOriginRedirect: true, TestContext.Current.CancellationToken));
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task DownloadRejectsAClientThatAutomaticallyFollowsRedirects()
    {
        Uri redirected = new("http://archive.example/download");
        using HttpClient client = new(new ArchiveTestHandler(request =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, redirected),
                Content = new ByteArrayContent([1])
            }));

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() => AtProtoServer.GetBlock(
            Segment, 0, TestServerBuilder.DefaultUri, "test-key", client,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("disable automatic redirects", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(redirected.ToString(), error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 0)]
    public async Task ResumedSegmentRejectsInvalidHeader(int version, int blockCount)
    {
        using Compressor compressor = new();
        byte[] frame = compressor.Wrap(TwoRecordBlock()).ToArray();
        byte[] segment = new byte[256 + 8 + frame.Length];
        "jss0"u8.CopyTo(segment);
        BinaryPrimitives.WriteUInt16LittleEndian(segment.AsSpan(12, 2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(segment.AsSpan(14, 4), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(segment.AsSpan(256, 8), checked((ulong)frame.Length));
        frame.CopyTo(segment.AsSpan(264));
        bool resuming = false;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (context.Request.Path.ToString().EndsWith(".planSnapshot", StringComparison.Ordinal))
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    """{"plannedThroughSeq":11,"sealedTipSeq":11,"segments":[{"name":"seg_0000000000.jss","index":0,"checksum":"0123456789abcdef","minSeq":1,"maxSeq":11,"mode":"segment"}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":1,"entries":1}}""");
                return;
            }

            context.Response.Headers.ETag = $"\"{Checksum}\"";
            if (resuming)
            {
                byte[] header = segment[..256];
                BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(12, 2), checked((ushort)version));
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(14, 4), checked((uint)blockCount));
                context.Response.ContentLength = header.Length;
                await context.Response.Body.WriteAsync(header);
            }
            else
            {
                context.Response.ContentLength = segment.Length;
                await context.Response.Body.WriteAsync(segment);
            }
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        SnapshotCheckpoint? checkpoint = null;
        SnapshotRequest request = new();
        await foreach (JetstreamEvent _ in jetstream.SnapshotAsync(request,
            onCheckpoint: progress => checkpoint = progress,
            cancellationToken: TestContext.Current.CancellationToken))
        {
        }

        Assert.NotNull(checkpoint);
        Assert.Equal(1, checkpoint.NextBlockIndex);
        resuming = true;
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (JetstreamEvent _ in jetstream.SnapshotAsync(request, checkpoint,
                cancellationToken: TestContext.Current.CancellationToken))
            {
            }
        });
    }

    [Theory]
    [InlineData("blocks")]
    [InlineData("segment")]
    public async Task SnapshotRejectsCheckpointBeyondPlannedSegment(string mode)
    {
        int downloads = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (context.Request.Path.ToString().EndsWith(".planSnapshot", StringComparison.Ordinal))
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(mode == "blocks"
                    ? """{"plannedThroughSeq":11,"sealedTipSeq":11,"segments":[{"name":"seg_0000000000.jss","index":0,"checksum":"0123456789abcdef","minSeq":1,"maxSeq":11,"mode":"blocks","blocks":[{"first":0,"last":0}]}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":1,"entries":1}}"""
                    : """{"plannedThroughSeq":11,"sealedTipSeq":11,"segments":[{"name":"seg_0000000000.jss","index":0,"checksum":"0123456789abcdef","minSeq":1,"maxSeq":11,"mode":"segment"}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":1,"entries":1}}""");
                return;
            }

            downloads++;
            context.Response.Headers.ETag = $"\"{Checksum}\"";
            byte[] header = new byte[256];
            "jss0"u8.CopyTo(header);
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(12, 2), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(14, 4), 1);
            context.Response.ContentLength = header.Length;
            await context.Response.Body.WriteAsync(header);
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        SnapshotRequest request = new();
        SnapshotCheckpoint checkpoint = new()
        {
            SealedTipSeq = 11, PlanAfterSeq = 0, RequestFingerprint = request.Fingerprint(s_server),
            SegmentName = Segment, SegmentChecksum = Checksum, NextBlockIndex = 2,
            NextByteOffset = mode == "segment" ? 256 : 0
        };

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (JetstreamEvent _ in jetstream.SnapshotAsync(request, checkpoint,
                cancellationToken: TestContext.Current.CancellationToken))
            {
            }
        });
        Assert.Contains("checkpoint block index exceeds", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(mode == "segment" ? 1 : 0, downloads);
    }

    [Fact]
    public void SnapshotRejectsReversedSequenceBoundsBeforePlanning()
    {
        using AtProtoJetstream jetstream = new(options: new JetstreamOptions { ApiKey = "test-key" });
        SnapshotRequest request = new() { AfterSeq = 12, BeforeSeq = 11 };

        Assert.Throws<ArgumentException>(() => jetstream.SnapshotAsync(request,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => jetstream.ReplayAsync(request,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PlanPagesKeepTheOriginalTipWhenResumingPastACompletedBlock()
    {
        List<(long After, long? Before)> plans = [];
        int downloads = 0;
        using Compressor compressor = new();
        byte[] frame = compressor.Wrap(TwoRecordBlock()).ToArray();
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (context.Request.Path.ToString().EndsWith(".planSnapshot", StringComparison.Ordinal))
            {
                using StreamReader reader = new(context.Request.Body);
                string body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
                using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(body);
                long after = document.RootElement.GetProperty("afterSeq").GetInt64();
                long? before = document.RootElement.TryGetProperty("beforeSeq", out System.Text.Json.JsonElement bound)
                    ? bound.GetInt64() : null;
                plans.Add((after, before));
                context.Response.ContentType = "application/json";
                string plan = after == 0
                    ? """{"plannedThroughSeq":15,"sealedTipSeq":30,"segments":[{"name":"seg_0000000000.jss","index":0,"checksum":"0123456789abcdef","minSeq":1,"maxSeq":15,"mode":"blocks","blocks":[{"first":0,"last":0}]}],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":1,"entries":1}}"""
                    : """{"plannedThroughSeq":30,"sealedTipSeq":30,"segments":[],"stats":{"segmentsExamined":0,"segmentsMatched":0,"blocksMatched":0,"entries":0}}""";
                await context.Response.WriteAsync(plan);
            }
            else
            {
                downloads++;
                context.Response.Headers.ETag = $"\"{Checksum}:0\"";
                context.Response.ContentLength = frame.Length;
                await context.Response.Body.WriteAsync(frame);
            }
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server),
            uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        SnapshotCheckpoint? completedBlock = null;
        List<long?> received = [];
        SnapshotRequest request = new() { AfterSeq = 0 };
        await foreach (JetstreamEvent evt in jetstream.SnapshotAsync(request, onCheckpoint: progress =>
        {
            if (progress.SegmentName is not null)
            {
                completedBlock = progress;
            }
        }, cancellationToken: TestContext.Current.CancellationToken))
        {
            received.Add(evt.Sequence);
        }

        Assert.Equal([10, 11], received);
        Assert.Equal(30, completedBlock?.SealedTipSeq);
        Assert.Equal(1, completedBlock?.NextBlockIndex);
        await foreach (JetstreamEvent evt in jetstream.SnapshotAsync(request, completedBlock,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            received.Add(evt.Sequence);
        }

        Assert.Equal([10, 11], received);
        Assert.Equal(1, downloads);
        Assert.Equal([(0, null), (15, 30), (0, 30), (15, 30)], plans);
        SnapshotCheckpoint saved = Assert.IsType<SnapshotCheckpoint>(completedBlock);
        Assert.Throws<ArgumentException>(() => jetstream.SnapshotAsync(
            request with { Collections = [new CollectionSelector("app.bsky.feed.post")] }, saved,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => jetstream.SnapshotAsync(
            request with { AfterSeq = 1 }, saved, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => jetstream.SnapshotAsync(request, saved with
        {
            RequestFingerprint = null
        }, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("blocks")]
    [InlineData("segment")]
    public async Task LaterPlanPageDoesNotRepeatRowsBeforeItsCursor(string mode)
    {
        List<JetStreamEventKind> kinds = [JetStreamEventKind.Commit];
        List<Did> dids = [new(TestDid)];
        List<CollectionSelector> collections = [new("app.bsky.feed.post"), new("app.bsky.feed.like")];
        SnapshotRequest request = new() { Kinds = kinds, Dids = dids, Collections = collections };
        string fingerprint = request.Fingerprint(s_server);
        using Compressor compressor = new();
        byte[] frame = compressor.Wrap(TwoRecordBlock()).ToArray();
        byte[] segment = new byte[256 + 8 + frame.Length];
        "jss0"u8.CopyTo(segment);
        BinaryPrimitives.WriteUInt16LittleEndian(segment.AsSpan(12, 2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(segment.AsSpan(14, 4), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(segment.AsSpan(256, 8), checked((ulong)frame.Length));
        frame.CopyTo(segment.AsSpan(264));
        List<long> plans = [];
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (context.Request.Path.ToString().EndsWith(".planSnapshot", StringComparison.Ordinal))
            {
                using StreamReader reader = new(context.Request.Body);
                string body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
                Assert.Contains("app.bsky.feed.post", body, StringComparison.Ordinal);
                Assert.Contains("app.bsky.feed.like", body, StringComparison.Ordinal);
                Assert.Contains(TestDid, body, StringComparison.Ordinal);
                using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(body);
                long after = document.RootElement.GetProperty("afterSeq").GetInt64();
                plans.Add(after);
                context.Response.ContentType = "application/json";
                string ranges = mode == "blocks" ? ""","blocks":[{"first":0,"last":0}]""" : "";
                await context.Response.WriteAsync(
                    $$$"""{"plannedThroughSeq":{{{(after == 0 ? 10 : 11)}}},"sealedTipSeq":11,"segments":[{"name":"seg_0000000000.jss","index":0,"checksum":"{{{Checksum}}}","minSeq":10,"maxSeq":11,"mode":"{{{mode}}}"{{{ranges}}} }],"stats":{"segmentsExamined":1,"segmentsMatched":1,"blocksMatched":1,"entries":2} }""");
            }
            else
            {
                context.Response.Headers.ETag = mode == "blocks" ? $"\"{Checksum}:0\"" : $"\"{Checksum}\"";
                byte[] content = mode == "blocks" ? frame : segment;
                context.Response.ContentLength = content.Length;
                await context.Response.Body.WriteAsync(content);
            }
        });
        using AtProtoJetstream jetstream = new(
            httpClientFactory: new TestHttpClientFactory(server), uri: s_server,
            options: new JetstreamOptions { ApiKey = "test-key", UseCompression = false });
        List<long?> received = [];
        await foreach (JetstreamEvent evt in jetstream.SnapshotAsync(request, onCheckpoint: progress =>
        {
            Assert.Equal(fingerprint, progress.RequestFingerprint);
            if (progress.PlanAfterSeq == 10)
            {
                kinds.Clear();
                dids[0] = new Did("did:plc:aaaaaaaaaaaaaaaaaaaaaaaa");
                collections.Clear();
            }
        },
            cancellationToken: TestContext.Current.CancellationToken))
        {
            received.Add(evt.Sequence);
        }

        Assert.Equal([0, 10], plans);
        Assert.Equal([10, 11], received);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArchivePlanRejectsOversizedJson(bool declaredLength)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            if (declaredLength)
            {
                context.Response.ContentLength = 8 * 1024 * 1024 + 1;
            }
            else
            {
                await context.Response.Body.WriteAsync(
                    new byte[8 * 1024 * 1024 + 1], TestContext.Current.CancellationToken);
            }
        });
        using HttpClient client = server.CreateClient();
        await Assert.ThrowsAsync<InvalidDataException>(() => AtProtoServer.PlanSnapshot(
            new SnapshotRequest(), TestServerBuilder.DefaultUri, "test-key", client,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ArchiveMetricServerTagOmitsUserInfoAndRequestQuery()
    {
        Uri service = new("wss://user:password@jetstream.example:443/private?token=secret");
        Assert.Equal("wss://jetstream.example", AtProtoJetstream.ArchiveServerTag(service));
    }

    private sealed class ArchiveTestHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class InterruptedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position >= Length)
            {
                throw new IOException("The response body was interrupted.");
            }

            return base.ReadAsync(buffer[..Math.Min(buffer.Length, checked((int)(Length - Position)))], cancellationToken);
        }
    }

    private sealed class ChunkedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], cancellationToken);
    }

    private sealed class StallingStream(byte[] bytes, Action? onStalled = null) : MemoryStream(bytes)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position >= Length)
            {
                onStalled?.Invoke();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return await base.ReadAsync(buffer[..Math.Min(buffer.Length, checked((int)(Length - Position)))], cancellationToken);
        }
    }

    private sealed class SlowChunkedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken);
            return await base.ReadAsync(buffer[..1], cancellationToken);
        }
    }

    private static byte[] TwoRecordBlock()
    {
        byte[] record = Convert.FromHexString("A164746573746178");
        byte[] did = Encoding.UTF8.GetBytes(TestDid);
        byte[] post = Encoding.UTF8.GetBytes("app.bsky.feed.post");
        byte[] like = Encoding.UTF8.GetBytes("app.bsky.feed.like");
        byte[] rkey = Encoding.UTF8.GetBytes("3mfrqvim56e25");
        byte[] rev = Encoding.UTF8.GetBytes("3mpksbjhx5s26");
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(2U);
        writer.Write(10UL);
        writer.Write(11UL);
        writer.Write(123456L);
        writer.Write(123457L);
        writer.Write(0L);
        writer.Write(0L);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write(checked((byte)post.Length));
        writer.Write(checked((byte)like.Length));
        writer.Write(checked((ushort)did.Length));
        writer.Write(checked((ushort)did.Length));
        writer.Write(checked((byte)rkey.Length));
        writer.Write(checked((byte)rkey.Length));
        writer.Write(checked((byte)rev.Length));
        writer.Write(checked((byte)rev.Length));
        writer.Write(checked((uint)record.Length));
        writer.Write(checked((uint)record.Length));
        writer.Write(post);
        writer.Write(like);
        writer.Write(did);
        writer.Write(did);
        writer.Write(rkey);
        writer.Write(rkey);
        writer.Write(rev);
        writer.Write(rev);
        writer.Write(record);
        writer.Write(record);
        return stream.ToArray();
    }

    private static byte[] SegmentWithBlocks(params byte[][] frames)
    {
        int length = 256 + frames.Sum(frame => 8 + frame.Length);
        byte[] segment = new byte[length];
        "jss0"u8.CopyTo(segment);
        BinaryPrimitives.WriteUInt16LittleEndian(segment.AsSpan(12, 2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(segment.AsSpan(14, 4), checked((uint)frames.Length));
        int offset = 256;
        foreach (byte[] frame in frames)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(segment.AsSpan(offset, 8), checked((ulong)frame.Length));
            offset += 8;
            frame.CopyTo(segment.AsSpan(offset));
            offset += frame.Length;
        }

        return segment;
    }

    private static byte[] PostBlock(params (long Sequence, byte[] Record, string Did)[] records)
    {
        byte[][] dids = records.Select(record => Encoding.UTF8.GetBytes(record.Did)).ToArray();
        byte[] collection = Encoding.UTF8.GetBytes("app.bsky.feed.post");
        byte[] rkey = Encoding.UTF8.GetBytes("3mfrqvim56e25");
        byte[] rev = Encoding.UTF8.GetBytes("3mpksbjhx5s26");
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(checked((uint)records.Length));
        foreach ((long sequence, _, _) in records)
        {
            writer.Write(checked((ulong)sequence));
        }

        for (int index = 0; index < records.Length; index++)
        {
            writer.Write(123456L);
        }

        for (int index = 0; index < records.Length; index++)
        {
            writer.Write(0L);
        }

        for (int index = 0; index < records.Length; index++)
        {
            writer.Write((byte)1);
        }

        for (int index = 0; index < records.Length; index++)
        {
            writer.Write(checked((byte)collection.Length));
        }

        for (int index = 0; index < records.Length; index++)
        {
            writer.Write(checked((ushort)dids[index].Length));
        }

        for (int index = 0; index < records.Length; index++)
        {
            writer.Write(checked((byte)rkey.Length));
        }

        for (int index = 0; index < records.Length; index++)
        {
            writer.Write(checked((byte)rev.Length));
        }

        foreach ((_, byte[] payload, _) in records)
        {
            writer.Write(checked((uint)payload.Length));
        }

        for (int index = 0; index < records.Length; index++)
        {
            writer.Write(collection);
        }

        for (int index = 0; index < records.Length; index++)
        {
            writer.Write(dids[index]);
        }

        for (int index = 0; index < records.Length; index++)
        {
            writer.Write(rkey);
        }

        for (int index = 0; index < records.Length; index++)
        {
            writer.Write(rev);
        }

        foreach ((_, byte[] payload, _) in records)
        {
            writer.Write(payload);
        }

        return stream.ToArray();
    }
}
