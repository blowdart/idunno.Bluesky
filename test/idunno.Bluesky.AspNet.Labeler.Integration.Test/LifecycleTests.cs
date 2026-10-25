// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using System.Formats.Cbor;
using System.Net;
using System.Net.WebSockets;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

/// <summary>Uses explicit operation barriers and fake time to exercise hosting lifetime and resource boundaries.</summary>
public class LifecycleTests
{
    private const string Meter = "idunno.Bluesky.AspNet.Labeler";
    private const string Prefix = "idunno.bluesky.labeler.host.";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueryDeadlineCoversMaintenanceRefreshAndGateWait(bool waitingForGate)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Host.BeforeMaintenanceRefreshAsync = async cancellation =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellation);
        };
        Task<HttpResponseMessage> first = fixture.Client.GetAsync(
            "/xrpc/com.atproto.label.queryLabels?uriPatterns=*", token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
        await fixture.Clock.WaitForTimerAsync(TimeSpan.FromSeconds(10));
        Task<HttpResponseMessage> request = waitingForGate
            ? fixture.Client.GetAsync("/xrpc/com.atproto.label.queryLabels?uriPatterns=*", token)
            : first;
        if (waitingForGate)
        {
            await fixture.Clock.WaitForTimerAsync(TimeSpan.FromSeconds(10));
        }
        fixture.Clock.Advance(TimeSpan.FromSeconds(10));
        try
        {
            using HttpResponseMessage response = await request.WaitAsync(TimeSpan.FromSeconds(30), token);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains("deadline", await response.Content.ReadAsStringAsync(token));
        }
        finally
        {
            release.TrySetResult();
            using HttpResponseMessage firstResponse = await first;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestartDuringReadyToReopenDrainsBeforeVerifyingAndActivatingItsKey(bool mismatchedKey)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey originalKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using LabelSigningKey replacementKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using LabelSigningKey otherKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        TestKeyRing originalRing = new(originalKey, replacementKey);
        await using TestServerBuilder original = await TestServerBuilder.CreateAsync(signingKeyRing: originalRing);
        ILabelMaintenanceStore maintenance = original.Services.GetRequiredService<ILabelMaintenanceStore>();
        const string operation = "restart-ready-rotation";
        await maintenance.BeginRotationAsync(
            operationId: operation,
            previousKeyId: "active",
            replacementKeyId: "replacement",
            replacementPublicKey: replacementKey.ExportPublicKeyMultibase(),
            cancellationToken: token);
        await original.StopAsync();
        await maintenance.CaptureRotationBoundaryAsync(operation, cancellationToken: token);
        await maintenance.VerifyRotationKeyPublicationAsync(
            operation, replacementKey.ExportPublicKeyMultibase(), cancellationToken: token);
        TestKeyRing restartedRing = new(originalKey, mismatchedKey ? otherKey : replacementKey);
        await using TestServerBuilder restarted = await TestServerBuilder.CreateAsync(
            database: original.DatabasePath,
            signingKeyRing: restartedRing);
        LabelMaintenanceState drained = await maintenance.GetMaintenanceStateAsync(token);
        Assert.True(drained.AllHostsDrained);
        Assert.Equal("active", restartedRing.ActiveKeyId);
        using HttpResponseMessage unavailable = await restarted.Client.GetAsync(
            "/xrpc/com.atproto.label.queryLabels?uriPatterns=*", token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        LabelMaintenanceState activated = await maintenance.GetMaintenanceStateAsync(token);
        Assert.Equal(mismatchedKey ? "active" : "replacement", restartedRing.ActiveKeyId);
        Assert.Equal(!mismatchedKey, Assert.Single(activated.Hosts, participant => !participant.Stopped).KeyActivated);
        if (!mismatchedKey)
        {
            await maintenance.CompleteRotationAsync(operation, "replacement", cancellationToken: token);
            restarted.Clock.Advance(TimeSpan.FromSeconds(1));
            await restarted.Clock.WaitForTimerAsync(TimeSpan.FromSeconds(1));
            using HttpResponseMessage reopened = await restarted.Client.GetAsync(
                "/xrpc/com.atproto.label.queryLabels?uriPatterns=*", token);
            Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
        }
    }

    [Fact]
    public async Task AdministrativeIssuanceLeasePreventsDrainAcknowledgement()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync();
        ILabelMaintenanceStore maintenance = fixture.Services.GetRequiredService<ILabelMaintenanceStore>();
        IDisposable lease = Assert.IsAssignableFrom<IDisposable>(fixture.Host.TryAdmitAdministrativeIssuance());
        try
        {
            await maintenance.BeginRotationAsync(
                operationId: "issuer-drain",
                previousKeyId: "active",
                replacementKeyId: "replacement",
                replacementPublicKey: "zreplacement",
                cancellationToken: token);
            fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            await fixture.Clock.WaitForTimerAsync(TimeSpan.FromSeconds(1));
            Assert.False((await maintenance.GetMaintenanceStateAsync(token)).AllHostsDrained);
            Assert.Null(fixture.Host.TryAdmitAdministrativeIssuance());
        }
        finally
        {
            lease.Dispose();
        }

        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        await fixture.Clock.WaitForTimerAsync(TimeSpan.FromSeconds(1));
        Assert.True((await maintenance.GetMaintenanceStateAsync(token)).AllHostsDrained);
    }

    /// <summary>Models host-owned keys without transferring ownership to the test host.</summary>
    internal sealed class TestKeyRing(LabelSigningKey original, LabelSigningKey replacement) : ILabelSigningKeyRing
    {
        public string ActiveKeyId { get; private set; } = "active";
        public LabelSigningKey GetSigningKey(string keyId) => keyId switch
        {
            "active" => original,
            "replacement" => replacement,
            _ => throw new KeyNotFoundException(keyId)
        };
        public string GetPublicKeyMultibase(string keyId) => GetSigningKey(keyId).ExportPublicKeyMultibase();
        public Task ActivateAsync(string keyId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = GetSigningKey(keyId);
            ActiveKeyId = keyId;
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HealthReportsDependencyFailureWithoutLeakingDetailsOrFailingLiveness(bool stoppedPublisher)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync();
        using HttpResponseMessage healthy = await fixture.Client.GetAsync("/health/labeler/ready", token);
        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
        Assert.Equal("Healthy", await healthy.Content.ReadAsStringAsync(token));
        if (stoppedPublisher)
        {
            await fixture.Host.StopAsync(token);
        }
        else
        {
            await fixture.CorruptSchemaAsync();
        }

        using HttpResponseMessage unhealthy = await fixture.Client.GetAsync("/health/labeler/ready", token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unhealthy.StatusCode);
        Assert.Equal("Unhealthy", await unhealthy.Content.ReadAsStringAsync(token));
        using HttpResponseMessage live = await fixture.Client.GetAsync("/health/labeler/live", token);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MaintenanceReturnsRetryableUnavailableResponsesAndClosesSubscriptionsForReconnect(bool network)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(network: network);
        using ClientWebSocket nativeSocket = new();
        if (network)
        {
            await nativeSocket.ConnectAsync(fixture.SocketAddress, token);
        }

        using WebSocket existing = network ? nativeSocket : await fixture.ConnectAsync();
        ILabelMaintenanceStore maintenance = fixture.Services.GetRequiredService<ILabelMaintenanceStore>();
        await maintenance.BeginRotationAsync(
            operationId: "maintenance-http-boundary",
            previousKeyId: "active",
            replacementKeyId: "replacement",
            replacementPublicKey: "zreplacement",
            cancellationToken: token);

        using HttpResponseMessage query = await fixture.Client.GetAsync(
            "/xrpc/com.atproto.label.queryLabels?uriPatterns=*",
            token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, query.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(1), query.Headers.RetryAfter?.Delta);
        Assert.Contains("ServiceUnavailable", await query.Content.ReadAsStringAsync(token));

        using HttpRequestMessage upgrade = new(HttpMethod.Get, "/xrpc/com.atproto.label.subscribeLabels");
        upgrade.Headers.TryAddWithoutValidation("Connection", "Upgrade");
        upgrade.Headers.TryAddWithoutValidation("Upgrade", "websocket");
        upgrade.Headers.TryAddWithoutValidation("Sec-WebSocket-Version", "13");
        upgrade.Headers.TryAddWithoutValidation("Sec-WebSocket-Key", "dGhlIHNhbXBsZSBub25jZQ==");
        using HttpResponseMessage newSubscription = await fixture.Client.SendAsync(upgrade, token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, newSubscription.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(1), newSubscription.Headers.RetryAfter?.Delta);

        using CancellationTokenSource watchdog = CancellationTokenSource.CreateLinkedTokenSource(token);
        watchdog.CancelAfter(TimeSpan.FromSeconds(30));
        byte[] buffer = new byte[1];
        ValueWebSocketReceiveResult close = await existing.ReceiveAsync(buffer.AsMemory(), watchdog.Token);
        Assert.Equal(WebSocketMessageType.Close, close.MessageType);
        Assert.Equal((WebSocketCloseStatus)1012, existing.CloseStatus);
        await existing.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledged.", watchdog.Token);
    }

    [Fact]
    public async Task MaintenanceCloseDeadlineReleasesAnUnresponsivePeer()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(network: true);
        IMeterFactory factory = fixture.Services.GetRequiredService<IMeterFactory>();
        MetricCollector<long> connections = new(factory, Meter, Prefix + "subscriptions");
        using ClientWebSocket socket = new();
        await socket.ConnectAsync(fixture.SocketAddress, token);
        ILabelMaintenanceStore maintenance = fixture.Services.GetRequiredService<ILabelMaintenanceStore>();
        await maintenance.BeginRotationAsync(
            operationId: "unresponsive-close",
            previousKeyId: "active",
            replacementKeyId: "replacement",
            replacementPublicKey: "zreplacement",
            cancellationToken: token);
        using HttpResponseMessage unavailable = await fixture.Client.GetAsync(
            "/xrpc/com.atproto.label.queryLabels?uriPatterns=*", token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        using CancellationTokenSource watchdog = CancellationTokenSource.CreateLinkedTokenSource(token);
        watchdog.CancelAfter(TimeSpan.FromSeconds(30));
        byte[] buffer = new byte[1];
        ValueWebSocketReceiveResult close = await socket.ReceiveAsync(buffer.AsMemory(), watchdog.Token);
        Assert.Equal(WebSocketMessageType.Close, close.MessageType);
        Assert.Equal((WebSocketCloseStatus)1012, socket.CloseStatus);
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        await connections.WaitForMeasurementsAsync(2, watchdog.Token);
        Assert.Equal(0, connections.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
    }

    [Fact]
    public async Task QueryDeadlineCancelsAnInProgressRead()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Host.BeforeReadAsync = async cancellation =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
        };
        Task<HttpResponseMessage> request = fixture.Client.GetAsync("/xrpc/com.atproto.label.queryLabels?uriPatterns=*", token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
        fixture.Clock.Advance(TimeSpan.FromSeconds(10));
        using HttpResponseMessage response = await request;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("ServiceUnavailable", await response.Content.ReadAsStringAsync(token));
    }

    [Fact]
    public async Task ClientCancellationReleasesAnInProgressQuery()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Host.BeforeReadAsync = async cancellation =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
        };
        using CancellationTokenSource cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<HttpResponseMessage> request = fixture.Client.GetAsync("/xrpc/com.atproto.label.queryLabels?uriPatterns=*", cancelled.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    [Fact]
    public async Task SendDeadlineTerminatesSlowConsumerAndReleasesAdmission()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(initialize: store => SeedAsync(store, key));
        IMeterFactory factory = fixture.Services.GetRequiredService<IMeterFactory>();
        MetricCollector<long> failures = new(factory, Meter, Prefix + "failures");
        MetricCollector<long> connections = new(factory, Meter, Prefix + "subscriptions");
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Host.BeforeSendAsync = async cancellation =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
        };
        using WebSocket socket = await fixture.ConnectAsync("?cursor=0");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
        fixture.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal("ConsumerTooSlow", Notice(await ReceiveAsync(socket)));
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Acknowledged.", token);
        await fixture.StopAsync();
        using CancellationTokenSource watchdog = new(TimeSpan.FromSeconds(30));
        await connections.WaitForMeasurementsAsync(2, watchdog.Token);
        Assert.Equal(0, connections.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
        Assert.Contains(failures.GetMeasurementSnapshot(), measurement =>
            measurement.Tags.Any(tag => tag.Key == "operation" && Equals(tag.Value, "send_deadline")));
    }

    [Fact]
    public async Task CrossProviderPublicationDuringReplayAppearsAfterTheCapturedTip()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(initialize: store => SeedAsync(store, key));
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int sends = 0;
        fixture.Host.BeforeSendAsync = async cancellation =>
        {
            if (Interlocked.Increment(ref sends) == 1)
            {
                entered.SetResult();
                await release.Task.WaitAsync(cancellation);
            }
        };
        using WebSocket socket = await fixture.ConnectAsync("?cursor=0");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
        using ServiceProvider other = fixture.OtherProvider();
        ILabelStore store = other.GetRequiredService<ILabelStore>();
        await store.AppendAsync("second", [Signed(key, "did:plc:second", fixture.Clock.GetUtcNow())], cancellationToken: token);
        await store.PublishAsync(cancellationToken: token);
        release.SetResult();
        Assert.Equal(1, Sequence(await ReceiveAsync(socket)));
        await fixture.Clock.WaitForTimerAsync(TimeSpan.FromSeconds(1));
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, Sequence(await ReceiveAsync(socket)));
    }

    [Fact]
    public async Task NoCursorIsLiveOnlyAndRestartPublishesPendingHistory()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.Secp256k1);
        // Startup receives committed, unpublished history; it must publish before its first idle poll.
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(initialize: store => SeedAsync(store, key));
        Assert.Equal(1, (await fixture.Store.GetStateAsync(token)).PublishedThrough);
        using WebSocket socket = await fixture.ConnectAsync();
        await fixture.Clock.WaitForTimerAsync(TimeSpan.FromSeconds(1));
        using ServiceProvider other = fixture.OtherProvider();
        ILabelStore store = other.GetRequiredService<ILabelStore>();
        await store.AppendAsync("second", [Signed(key, "did:plc:second", fixture.Clock.GetUtcNow())], cancellationToken: token);
        await store.PublishAsync(cancellationToken: token);
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, Sequence(await ReceiveAsync(socket)));
    }

    [Fact]
    public async Task RecreatedHostRetainsStreamIdentityQueryCursorAndPendingPublication()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await using TestServerBuilder original = await TestServerBuilder.CreateAsync(initialize: async store =>
        {
            await store.AppendAsync("initial",
            [
                Signed(key, "did:plc:first", DateTimeOffset.UnixEpoch),
                Signed(key, "did:plc:second", DateTimeOffset.UnixEpoch)
            ], cancellationToken: token);
        });
        using HttpResponseMessage first = await original.Client.GetAsync("/xrpc/com.atproto.label.queryLabels?uriPatterns=*&limit=1", token);
        using System.Text.Json.JsonDocument page = System.Text.Json.JsonDocument.Parse(await first.Content.ReadAsByteArrayAsync(token));
        string cursor = page.RootElement.GetProperty("cursor").GetString()!;
        Guid streamId = (await original.Store.GetStateAsync(token)).StreamId;
        await original.StopAsync();
        await original.Store.AppendAsync("pending", [Signed(key, "did:plc:third", DateTimeOffset.UnixEpoch)], cancellationToken: token);
        await using TestServerBuilder restarted = await TestServerBuilder.CreateAsync(database: original.DatabasePath);
        LabelStoreState state = await restarted.Store.GetStateAsync(token);
        Assert.Equal(streamId, state.StreamId);
        Assert.Equal(3, state.PublishedThrough);
        using HttpResponseMessage next = await restarted.Client.GetAsync(
            "/xrpc/com.atproto.label.queryLabels?uriPatterns=*&cursor=" + Uri.EscapeDataString(cursor), token);
        using System.Text.Json.JsonDocument remainder = System.Text.Json.JsonDocument.Parse(await next.Content.ReadAsByteArrayAsync(token));
        Assert.Equal("did:plc:second", Assert.Single(remainder.RootElement.GetProperty("labels").EnumerateArray()).GetProperty("uri").GetString());
        Assert.False(remainder.RootElement.TryGetProperty("cursor", out _));
        using WebSocket socket = await restarted.ConnectAsync("?cursor=2");
        Assert.Equal(3, Sequence(await ReceiveAsync(socket)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrunedCursorRecoversAtRetainedBoundaryIncludingReadRace(bool race)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(initialize: async store =>
        {
            await store.AppendAsync("labels",
            [
                Signed(key, "did:plc:first", DateTimeOffset.UnixEpoch),
                Signed(key, "did:plc:second", DateTimeOffset.UnixEpoch),
                Signed(key, "did:plc:third", DateTimeOffset.UnixEpoch)
            ], cancellationToken: token);
        });
        if (race)
        {
            fixture.Host.BeforeReadAsync = _ => fixture.PruneAsync(2);
        }
        else
        {
            await fixture.PruneAsync(2);
        }

        using WebSocket socket = await fixture.ConnectAsync("?cursor=1");
        Assert.Equal("OutdatedCursor", Notice(await ReceiveAsync(socket)));
        Assert.Equal(3, Sequence(await ReceiveAsync(socket)));
    }

    [Fact]
    public async Task AdmissionAndShutdownReleaseConnectionsAndPublishMetrics()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync(
            options: new LabelerHostingOptions { MaximumSubscriptions = 1 },
            network: true);
        IMeterFactory factory = fixture.Services.GetRequiredService<IMeterFactory>();
        MetricCollector<long> rejections = new(factory, Meter, Prefix + "rejections");
        MetricCollector<long> connections = new(factory, Meter, Prefix + "subscriptions");
        using ClientWebSocket socket = new();
        await socket.ConnectAsync(fixture.SocketAddress, token);
        await fixture.Clock.WaitForTimerAsync(TimeSpan.FromSeconds(1));
        using HttpRequestMessage excess = new(HttpMethod.Get, fixture.SocketAddress.AbsolutePath);
        excess.Headers.TryAddWithoutValidation("Connection", "Upgrade");
        excess.Headers.TryAddWithoutValidation("Upgrade", "websocket");
        excess.Headers.TryAddWithoutValidation("Sec-WebSocket-Key", "dGhlIHNhbXBsZSBub25jZQ==");
        excess.Headers.TryAddWithoutValidation("Sec-WebSocket-Version", "13");
        using HttpResponseMessage response = await fixture.Client.SendAsync(excess, token);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(1), response.Headers.RetryAfter!.Delta);
        Task stopping = fixture.StopAsync();
        byte[] closeBuffer = new byte[1];
        using CancellationTokenSource closeWatchdog = CancellationTokenSource.CreateLinkedTokenSource(token);
        closeWatchdog.CancelAfter(TimeSpan.FromSeconds(30));
        ValueWebSocketReceiveResult close = await socket.ReceiveAsync(closeBuffer.AsMemory(), closeWatchdog.Token);
        Assert.Equal(WebSocketMessageType.Close, close.MessageType);
        Assert.Equal((WebSocketCloseStatus)1012, socket.CloseStatus);
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledged.", closeWatchdog.Token);
        await stopping;
        using CancellationTokenSource watchdog = new(TimeSpan.FromSeconds(30));
        await connections.WaitForMeasurementsAsync(2, watchdog.Token);
        Assert.Equal(0, connections.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
        Assert.Equal(1, Assert.Single(rejections.GetMeasurementSnapshot()).Value);
    }

    [Fact]
    public async Task MetricNamesAndQueryPublicationEmissionsRemainStable()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TestServerBuilder fixture = await TestServerBuilder.CreateAsync();
        IMeterFactory factory = fixture.Services.GetRequiredService<IMeterFactory>();
        MetricCollector<double> duration = new(factory, Meter, Prefix + "query.duration");
        MetricCollector<long> pending = new(factory, Meter, Prefix + "pending");
        MetricCollector<long> frames = new(factory, Meter, Prefix + "frames");
        using HttpResponseMessage response = await fixture.Client.GetAsync("/xrpc/com.atproto.label.queryLabels?uriPatterns=*", token);
        using CancellationTokenSource watchdog = new(TimeSpan.FromSeconds(30));
        await duration.WaitForMeasurementsAsync(1, watchdog.Token);
        Assert.Equal(0, Assert.Single(duration.GetMeasurementSnapshot()).Value);
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        await pending.WaitForMeasurementsAsync(1, watchdog.Token);
        Assert.Equal(0, Assert.Single(pending.GetMeasurementSnapshot()).Value);
        using WebSocket socket = await fixture.ConnectAsync("?cursor=999");
        Assert.Equal("FutureCursor", Notice(await ReceiveAsync(socket)));
        await frames.WaitForMeasurementsAsync(1, watchdog.Token);
        Assert.Equal(1, Assert.Single(frames.GetMeasurementSnapshot()).Value);
    }

    /// <summary>Queues one event without publication so startup recovery is exercised by every seeded fixture.</summary>
    private static async Task SeedAsync(ILabelStore store, LabelSigningKey key) =>
        await store.AppendAsync("first", [Signed(key, "did:plc:first", DateTimeOffset.UnixEpoch)], cancellationToken: TestContext.Current.CancellationToken);

    /// <summary>Builds signed data with an explicit test-owned timestamp, never a wall-clock deadline.</summary>
    private static SignedLabelSnapshot Signed(LabelSigningKey key, string uri, DateTimeOffset timestamp) =>
        new(key.CreateLabel(
            source: TestServerBuilder.s_issuer,
            uri: uri,
            value: "spam",
            creationTimestamp: timestamp));

    /// <summary>Reads one bounded binary message with a hang watchdog unrelated to fake-time assertions.</summary>
    private static async Task<byte[]> ReceiveAsync(WebSocket socket)
    {
        byte[] buffer = new byte[64 * 1024];
        using CancellationTokenSource watchdog = new(TimeSpan.FromSeconds(30));
        ValueWebSocketReceiveResult result = await socket.ReceiveAsync(buffer.AsMemory(), watchdog.Token);
        Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
        Assert.True(result.EndOfMessage);
        return buffer[..result.Count];
    }

    /// <summary>Decodes the sequence independently of host helpers and the SDK event reader.</summary>
    private static long Sequence(byte[] frame)
    {
        CborReader reader = new(frame, allowMultipleRootLevelValues: true);
        reader.SkipValue();
        reader.ReadStartMap();
        Assert.Equal("seq", reader.ReadTextString());
        return reader.ReadInt64();
    }

    /// <summary>Extracts an informational or error name while accepting only standard two-part framing.</summary>
    private static string Notice(byte[] frame)
    {
        CborReader reader = new(frame, allowMultipleRootLevelValues: true);
        reader.SkipValue();
        reader.ReadStartMap();
        Assert.Contains(reader.ReadTextString(), new[] { "name", "error" });
        return reader.ReadTextString();
    }
}
