// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using System.Threading.Channels;

using idunno.AtProto.Firehose;
using idunno.AtProto.Jetstream;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Time.Testing;

namespace idunno.AtProto.Test;

public class StreamMetricsTests
{
    private static JetstreamEvent Event(long sequence) => new()
    {
        Did = new Did("did:plc:g6ylltenitt4tp27bpwalh7b"),
        Kind = JetStreamEventKind.Unknown,
        TimeStamp = 0,
        Sequence = sequence
    };

    [Theory]
    [InlineData("drain")]
    [InlineData("cancel")]
    [InlineData("fault")]
    [InlineData("stop")]
    public async Task JetstreamQueueMetricsAggregateAndRecordOnlyPendingCapacityWaits(string finish)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using ServiceProvider services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        IMeterFactory factory = services.GetRequiredService<IMeterFactory>();
        using MetricCollector<long> queued = new(factory, JetstreamMetrics.MeterName, "idunno.atproto.jetstream.stream.queue.count");
        using MetricCollector<long> capacities = new(factory, JetstreamMetrics.MeterName, "idunno.atproto.jetstream.stream.buffer.capacity");
        using MetricCollector<long> waits = new(factory, JetstreamMetrics.MeterName, "idunno.atproto.jetstream.total.stream.capacity_waits");
        using MetricCollector<double> durations = new(factory, JetstreamMetrics.MeterName, "idunno.atproto.jetstream.stream.capacity_wait.duration");
        FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        KeyValuePair<string, object?> serverTag = new("server", "wss://example.com");
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        using LiveEventQueue first = new(1, new JetstreamMetrics(factory), time, serverTag, cancellation.Token);
        using LiveEventQueue second = new(3, new JetstreamMetrics(factory), time, serverTag, token);
        await first.WriteAsync(Event(1));
        await second.WriteAsync(Event(10));
        await second.WriteAsync(Event(11));
        Assert.Equal(4, capacities.GetMeasurementSnapshot().Sum(m => m.Value));
        Assert.Equal(3, queued.GetMeasurementSnapshot().Sum(m => m.Value));
        Assert.Empty(waits.GetMeasurementSnapshot());
        Assert.Empty(durations.GetMeasurementSnapshot());

        Task pending = first.WriteAsync(Event(2));
        Assert.False(pending.IsCompleted);
        Assert.Equal(1, waits.GetMeasurementSnapshot().Sum(m => m.Value));
        Assert.Equal(3, queued.GetMeasurementSnapshot().Sum(m => m.Value));
        time.Advance(TimeSpan.FromSeconds(3));
        switch (finish)
        {
            case "drain":
                Assert.True(first.TryRead(out JetstreamEvent? item));
                Assert.Equal(1, item?.Sequence);
                await pending.WaitAsync(TimeSpan.FromSeconds(30), token);
                Assert.True(first.TryRead(out item));
                Assert.Equal(2, item?.Sequence);
                break;
            case "cancel":
                await cancellation.CancelAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(30), token));
                break;
            case "fault":
                IOException fault = new("Interrupted");
                first.Complete(fault);
                Assert.Same(fault, await Assert.ThrowsAsync<IOException>(() => pending.WaitAsync(TimeSpan.FromSeconds(30), token)));
                Assert.True(first.TryRead(out _));
                Assert.Same(fault, await Assert.ThrowsAsync<IOException>(() => first.WaitToReadAsync(token).AsTask()));
                break;
            case "stop":
                first.Stop();
                await pending.WaitAsync(TimeSpan.FromSeconds(30), token);
                break;
        }

        Assert.Equal(3, Assert.Single(durations.GetMeasurementSnapshot()).Value);
        first.Stop();
        first.Stop();
        Assert.Equal(3, capacities.GetMeasurementSnapshot().Sum(m => m.Value));
        Assert.Equal(2, queued.GetMeasurementSnapshot().Sum(m => m.Value));
        Assert.True(second.TryRead(out JetstreamEvent? secondItem));
        Assert.Equal(10, secondItem?.Sequence);
        Assert.Equal(1, queued.GetMeasurementSnapshot().Sum(m => m.Value));
        second.Stop();
        Assert.Equal(0, capacities.GetMeasurementSnapshot().Sum(m => m.Value));
        Assert.Equal(0, queued.GetMeasurementSnapshot().Sum(m => m.Value));
        Assert.All(capacities.GetMeasurementSnapshot(), measurement =>
            Assert.Equal("wss://example.com", measurement.Tags["server"]));
    }

    [Fact]
    public async Task JetstreamQueuePropagatesMissingSequenceAsAnInvalidDataError()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LiveEventQueue queue = new(1, new JetstreamMetrics(null), TimeProvider.System,
            new KeyValuePair<string, object?>("server", "wss://example.com"), token);
        await queue.DeliverAsync(Event(1) with { Sequence = null });
        await Assert.ThrowsAsync<InvalidDataException>(() => queue.WaitToReadAsync(token).AsTask());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JetstreamQueueDeliveryAcceptsNormalCompletionWithoutDiscardingQueuedEvents(bool waitingForCapacity)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LiveEventQueue queue = new(1, new JetstreamMetrics(null), TimeProvider.System,
            new KeyValuePair<string, object?>("server", "wss://example.com"), token);
        await queue.DeliverAsync(Event(1));
        Task? pending = waitingForCapacity ? queue.DeliverAsync(Event(2)) : null;
        if (pending is not null)
        {
            Assert.False(pending.IsCompleted);
        }

        queue.Complete();
        if (pending is not null)
        {
            await pending.WaitAsync(TimeSpan.FromSeconds(30), token);
        }

        await queue.DeliverAsync(Event(3)).WaitAsync(TimeSpan.FromSeconds(30), token);
        Assert.True(queue.TryRead(out JetstreamEvent? item));
        Assert.Equal(1, item?.Sequence);
        Assert.False(await queue.WaitToReadAsync(token));
    }

    [Theory]
    [InlineData("io")]
    [InlineData("invalid-data")]
    [InlineData("channel-closed")]
    public async Task JetstreamQueueDeliveryPropagatesGenuineChannelFaults(string faultKind)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using LiveEventQueue queue = new(1, new JetstreamMetrics(null), TimeProvider.System,
            new KeyValuePair<string, object?>("server", "wss://example.com"), token);
        await queue.DeliverAsync(Event(1));
        Task pending = queue.DeliverAsync(Event(2));
        Assert.False(pending.IsCompleted);
        Exception fault = faultKind switch
        {
            "invalid-data" => new InvalidDataException("Invalid event."),
            "channel-closed" => new ChannelClosedException(),
            _ => new IOException("Interrupted.")
        };
        queue.Complete(fault);
        queue.Complete();
        Exception error = await Assert.ThrowsAnyAsync<Exception>(() => pending.WaitAsync(TimeSpan.FromSeconds(30), token));
        Assert.Same(fault, error);
        Assert.True(queue.TryRead(out _));
        error = await Assert.ThrowsAnyAsync<Exception>(() => queue.WaitToReadAsync(token).AsTask());
        Assert.Same(fault, error);
    }

    [Fact]
    public void FirehoseConnectionAndMessageMetricsTrackLiveness()
    {
        using ServiceProvider services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
        using MetricCollector<long> activeConnections = new(meterFactory, FirehoseMetrics.MeterName, "idunno.atproto.firehose.connections.active");
        using MetricCollector<long> openedConnections = new(meterFactory, FirehoseMetrics.MeterName, "idunno.atproto.firehose.total.connections_opened");
        using MetricCollector<long> closedConnections = new(meterFactory, FirehoseMetrics.MeterName, "idunno.atproto.firehose.total.connections_closed");
        using MetricCollector<double> connectionDuration = new(meterFactory, FirehoseMetrics.MeterName, "idunno.atproto.firehose.connection.duration");
        using MetricCollector<double> messageInterarrivalDuration = new(meterFactory, FirehoseMetrics.MeterName, "idunno.atproto.firehose.message.interarrival.duration");
        FirehoseMetrics metrics = new(meterFactory);
        object connection = new();
        KeyValuePair<string, object?> serverTag = new("server", "wss://example.com");

        metrics.RecordConnectionOpened(connection, serverTag);
        metrics.RecordMessageReceived(connection, serverTag);
        metrics.RecordMessageReceived(connection, serverTag);
        metrics.RecordConnectionClosed(connection, serverTag);

        Assert.Equal(0, activeConnections.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
        Assert.Equal(1, openedConnections.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
        Assert.Equal(1, closedConnections.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
        Assert.Single(connectionDuration.GetMeasurementSnapshot());
        Assert.Single(messageInterarrivalDuration.GetMeasurementSnapshot());
    }

    [Fact]
    public void JetstreamConnectionAndMessageMetricsTrackLiveness()
    {
        using ServiceProvider services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
        using MetricCollector<long> activeConnections = new(meterFactory, JetstreamMetrics.MeterName, "idunno.atproto.jetstream.connections.active");
        using MetricCollector<long> openedConnections = new(meterFactory, JetstreamMetrics.MeterName, "idunno.atproto.jetstream.total.connections_opened");
        using MetricCollector<long> closedConnections = new(meterFactory, JetstreamMetrics.MeterName, "idunno.atproto.jetstream.total.connections_closed");
        using MetricCollector<double> connectionDuration = new(meterFactory, JetstreamMetrics.MeterName, "idunno.atproto.jetstream.connection.duration");
        using MetricCollector<double> messageInterarrivalDuration = new(meterFactory, JetstreamMetrics.MeterName, "idunno.atproto.jetstream.message.interarrival.duration");
        JetstreamMetrics metrics = new(meterFactory);
        object connection = new();
        KeyValuePair<string, object?> serverTag = new("server", "wss://example.com");

        metrics.RecordConnectionOpened(connection, serverTag);
        metrics.RecordMessageReceived(connection, serverTag);
        metrics.RecordMessageReceived(connection, serverTag);
        metrics.RecordConnectionClosed(connection, serverTag);

        Assert.Equal(0, activeConnections.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
        Assert.Equal(1, openedConnections.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
        Assert.Equal(1, closedConnections.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
        Assert.Single(connectionDuration.GetMeasurementSnapshot());
        Assert.Single(messageInterarrivalDuration.GetMeasurementSnapshot());
    }
}
