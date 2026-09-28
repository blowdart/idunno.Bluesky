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

using ZstdSharp;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class JetstreamArchiveTests
{
    private const string Checksum = "0123456789abcdef";
    private const string Segment = "seg_0000000000.jss";
    private const string TestDid = "did:plc:g6ylltenitt4tp27bpwalh7b";
    private static readonly Uri s_server = new("wss://test.internal:443/some/path?ignored=1");

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
            TestServerBuilder.DefaultUri, client, 0, new JetstreamMetrics(null));
        byte[] bytes = new byte[3];
        System.Diagnostics.Stopwatch elapsed = System.Diagnostics.Stopwatch.StartNew();
        await download.ReadExactlyAsync(bytes, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3], bytes);
        Assert.True(elapsed.Elapsed >= TimeSpan.FromMilliseconds(850),
            $"Quota was bypassed: the download completed in {elapsed.Elapsed}.");
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
}
