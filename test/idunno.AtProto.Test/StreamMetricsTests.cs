// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

using idunno.AtProto.Firehose;
using idunno.AtProto.Jetstream;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace idunno.AtProto.Test;

public class StreamMetricsTests
{
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
