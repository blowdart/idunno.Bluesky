// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Records process-local hosting measurements without subject, source or cursor cardinality.</summary>
internal sealed class HostingMetrics : IDisposable
{
    /// <summary>Identifies the hosting meter for instrumentation and OpenTelemetry collection.</summary>
    internal const string MeterName = "idunno.Bluesky.AspNet.Labeler";

    private readonly Meter _meter;
    private readonly Counter<long> _frames;
    private readonly UpDownCounter<long> _subscriptions;
    private readonly Histogram<long> _pending;
    private readonly Counter<long> _failures;
    private readonly Counter<long> _rejections;
    private readonly Histogram<double> _queries;

    /// <summary>Initializes a new instance of the <see cref="HostingMetrics"/> class.</summary>
    /// <param name="factory">The DI-owned factory that coordinates this component's meter lifetime.</param>
    public HostingMetrics(IMeterFactory factory)
    {
        _meter = factory.Create(MeterName);
        _frames = _meter.CreateCounter<long>("idunno.bluesky.labeler.host.frames", "{frame}");
        _subscriptions = _meter.CreateUpDownCounter<long>("idunno.bluesky.labeler.host.subscriptions", "{connection}");
        _pending = _meter.CreateHistogram<long>("idunno.bluesky.labeler.host.pending", "{label}");
        _failures = _meter.CreateCounter<long>("idunno.bluesky.labeler.host.failures", "{failure}");
        _rejections = _meter.CreateCounter<long>("idunno.bluesky.labeler.host.rejections", "{rejection}");
        _queries = _meter.CreateHistogram<double>("idunno.bluesky.labeler.host.query.duration", "s");
    }

    /// <summary>Disposes this component's instruments when its service provider is torn down.</summary>
    public void Dispose() => _meter.Dispose();

    /// <summary>Counts completed socket writes, never attempted or abandoned frames.</summary>
    internal void Frame() => _frames.Add(1);

    /// <summary>Tracks admitted connection ownership, including release after failed upgrades.</summary>
    internal void Subscription(long delta) => _subscriptions.Add(delta);

    /// <summary>Samples durable publication backlog without summing shared database state across hosts.</summary>
    internal void Pending(long count) => _pending.Record(count);

    /// <summary>Counts operational failures using only fixed server-selected categories.</summary>
    internal void Failure(string operation) => _failures.Add(1, new KeyValuePair<string, object?>("operation", operation));

    /// <summary>Counts admission failures and invalid input without tagging client-provided values.</summary>
    internal void Rejection(string reason) => _rejections.Add(1, new KeyValuePair<string, object?>("reason", reason));

    /// <summary>Measures query work with the same clock used by request deadlines.</summary>
    internal void Query(TimeSpan elapsed) => _queries.Record(elapsed.TotalSeconds);
}
