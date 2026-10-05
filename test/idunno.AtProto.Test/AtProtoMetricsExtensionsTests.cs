// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

using idunno.AtProto.Firehose;

using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace idunno.AtProto.Test;

public class AtProtoMetricsExtensionsTests
{
    [Fact]
    public void AddAtProtoFirehoseMetricsRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(
            "builder",
            () => AtProtoMetricsExtensions.AddAtProtoFirehoseMetrics(null!));
    }

    [Fact]
    public void AddAtProtoFirehoseMetricsReturnsTheBuilder()
    {
        MeterProviderBuilder builder = Sdk.CreateMeterProviderBuilder();

        Assert.Same(builder, builder.AddAtProtoFirehoseMetrics());
    }

    [Fact]
    public void AddAtProtoFirehoseMetricsCollectsOnlyTheFirehoseMeter()
    {
        using RecordingMetricExporter exporter = new();
        using PeriodicExportingMetricReader reader = new(exporter);
        using MeterProvider provider = Sdk.CreateMeterProviderBuilder()
            .AddAtProtoFirehoseMetrics()
            .AddReader(reader)
            .Build();
        using Meter firehoseMeter = new(FirehoseMetrics.MeterName, FirehoseMetrics.MeterVersion);
        using Meter otherMeter = new("idunno.AtProto.Test.Unregistered");
        Counter<long> firehoseCounter = firehoseMeter.CreateCounter<long>("test.firehose.messages");
        Counter<long> otherCounter = otherMeter.CreateCounter<long>("test.unregistered.messages");

        firehoseCounter.Add(1);
        otherCounter.Add(1);

        Assert.True(provider.ForceFlush());
        Assert.Contains("test.firehose.messages", exporter.MetricNames);
        Assert.DoesNotContain("test.unregistered.messages", exporter.MetricNames);
    }

    private sealed class RecordingMetricExporter : BaseExporter<Metric>
    {
        public HashSet<string> MetricNames { get; } = [];

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (Metric metric in batch)
            {
                MetricNames.Add(metric.Name);
            }

            return ExportResult.Success;
        }
    }
}
