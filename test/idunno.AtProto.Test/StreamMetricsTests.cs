// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using idunno.AtProto.Firehose;
using idunno.AtProto.Jetstream;

namespace idunno.AtProto.Test;

public class StreamMetricsTests
{
    [Fact]
    public void FirehoseConnectionAndMessageMetricsTrackLiveness()
    {
        using RecordingMeterFactory meterFactory = new();
        using MeasurementRecorder recorder = new(meterFactory);
        FirehoseMetrics metrics = new(meterFactory);
        object connection = new();
        KeyValuePair<string, object?> serverTag = new("server", "wss://example.com");

        metrics.RecordConnectionOpened(connection, serverTag);
        metrics.RecordMessageReceived(connection, serverTag);
        metrics.RecordMessageReceived(connection, serverTag);
        metrics.RecordConnectionClosed(connection, serverTag);

        Assert.Equal(0, recorder.Total("idunno.atproto.firehose.connections.active"));
        Assert.Equal(1, recorder.Total("idunno.atproto.firehose.total.connections_opened"));
        Assert.Equal(1, recorder.Total("idunno.atproto.firehose.total.connections_closed"));
        Assert.Equal(1, recorder.MeasurementCount("idunno.atproto.firehose.connection.duration"));
        Assert.Equal(1, recorder.MeasurementCount("idunno.atproto.firehose.message.interarrival.duration"));
    }

    [Fact]
    public void JetstreamConnectionAndMessageMetricsTrackLiveness()
    {
        using RecordingMeterFactory meterFactory = new();
        using MeasurementRecorder recorder = new(meterFactory);
        JetstreamMetrics metrics = new(meterFactory);
        object connection = new();
        KeyValuePair<string, object?> serverTag = new("server", "wss://example.com");

        metrics.RecordConnectionOpened(connection, serverTag);
        metrics.RecordMessageReceived(connection, serverTag);
        metrics.RecordMessageReceived(connection, serverTag);
        metrics.RecordConnectionClosed(connection, serverTag);

        Assert.Equal(0, recorder.Total("idunno.atproto.jetstream.connections.active"));
        Assert.Equal(1, recorder.Total("idunno.atproto.jetstream.total.connections_opened"));
        Assert.Equal(1, recorder.Total("idunno.atproto.jetstream.total.connections_closed"));
        Assert.Equal(1, recorder.MeasurementCount("idunno.atproto.jetstream.connection.duration"));
        Assert.Equal(1, recorder.MeasurementCount("idunno.atproto.jetstream.message.interarrival.duration"));
    }

    private sealed class MeasurementRecorder : IDisposable
    {
        private readonly ConcurrentDictionary<string, long> _totals = new();
        private readonly ConcurrentDictionary<string, int> _measurementCounts = new();
        private readonly MeterListener _listener = new();

        public MeasurementRecorder(IMeterFactory meterFactory)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Scope == meterFactory)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
            {
                _totals.AddOrUpdate(instrument.Name, measurement, (_, total) => total + measurement);
            });
            _listener.SetMeasurementEventCallback<double>((instrument, _, _, _) =>
            {
                _measurementCounts.AddOrUpdate(instrument.Name, 1, (_, count) => count + 1);
            });

            _listener.Start();
        }

        public long Total(string name) => _totals.GetValueOrDefault(name);

        public int MeasurementCount(string name) => _measurementCounts.GetValueOrDefault(name);

        public void Dispose() => _listener.Dispose();
    }
}
