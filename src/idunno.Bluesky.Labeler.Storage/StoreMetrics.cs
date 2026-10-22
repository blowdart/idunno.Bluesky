// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace idunno.Bluesky.Labeler.Storage;

internal sealed class StoreMetrics
{
    private static readonly Meter s_meter = new(LabelPersistenceMetrics.MeterName, LabelPersistenceMetrics.MeterVersion);
    private readonly Histogram<double> _duration;
    private readonly Counter<long> _operations;
    private readonly UpDownCounter<long> _active;
    private readonly Counter<long> _labels;
    private readonly Counter<long> _bytes;
    private readonly Histogram<long> _backlog;
    private readonly Histogram<double> _age;
    private readonly string _provider;
    internal bool BytesEnabled => _bytes.Enabled;

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "IMeterFactory owns and disposes its meters.")]
    internal StoreMetrics(string provider, IMeterFactory? factory)
    {
        _provider = provider;
        Meter meter = factory is null ? s_meter : factory.Create(LabelPersistenceMetrics.MeterName, LabelPersistenceMetrics.MeterVersion);
        _duration = meter.CreateHistogram<double>("idunno.bluesky.labeler.operation.duration", "s",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5, 15] });
        _operations = meter.CreateCounter<long>("idunno.bluesky.labeler.operations", "{operation}");
        _active = meter.CreateUpDownCounter<long>("idunno.bluesky.labeler.operations.active", "{operation}");
        _labels = meter.CreateCounter<long>("idunno.bluesky.labeler.labels", "{label}");
        _bytes = meter.CreateCounter<long>("idunno.bluesky.labeler.bytes", "By");
        _backlog = meter.CreateHistogram<long>("idunno.bluesky.labeler.publication.pending", "{label}");
        _age = meter.CreateHistogram<double>("idunno.bluesky.labeler.publication.oldest", "s");
    }

    private TagList Tags(string operation, string outcome) => new()
    {
        { "provider", _provider }, { "operation", operation }, { "outcome", outcome }
    };

    internal long Start(string operation)
    {
        _active.Add(1, Tags(operation, "active"));
        return Stopwatch.GetTimestamp();
    }

    internal void Stop(string operation, string outcome, long start)
    {
        _active.Add(-1, Tags(operation, "active"));
        _operations.Add(1, Tags(operation, outcome));
        _duration.Record(Stopwatch.GetElapsedTime(start).TotalSeconds, Tags(operation, outcome));
    }

    internal void Event(string operation, string outcome) => _operations.Add(1, Tags(operation, outcome));

    internal void Labels(string operation, long count, long bytes)
    {
        _labels.Add(count, Tags(operation, "success"));
        _bytes.Add(bytes, Tags(operation, "success"));
    }

    internal void Snapshot(LabelStoreSnapshot snapshot)
    {
        _backlog.Record(snapshot.PendingLabels, Tags("snapshot", "success"));
        _age.Record(snapshot.OldestPendingAge.TotalSeconds, Tags("snapshot", "success"));
    }
}
