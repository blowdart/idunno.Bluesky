// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

/// <summary>Verifies that the public extension selects hosting instruments in the OpenTelemetry pipeline.</summary>
public class LabelerMetricsExtensionsTests
{
    [Fact]
    public void RejectsNullBuilder()
    {
        Assert.Throws<ArgumentNullException>(
            "builder",
            () => LabelerMetricsExtensions.AddBlueskyLabelerMetrics(null!));
    }

    [Fact]
    public void ReturnsTheBuilder()
    {
        MeterProviderBuilder builder = Sdk.CreateMeterProviderBuilder();

        Assert.Same(builder, builder.AddBlueskyLabelerMetrics());
    }

    [Fact]
    public void CollectsHostingAndOptionalModerationMetricsOnly()
    {
        using RecordingMetricExporter exporter = new();
        using PeriodicExportingMetricReader reader = new(exporter, exportIntervalMilliseconds: int.MaxValue);
        using MeterProvider provider = Sdk.CreateMeterProviderBuilder()
            .AddBlueskyLabelerMetrics()
            .AddReader(reader)
            .Build();
        ServiceCollection services = new();
        services.AddMetrics();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using HostingMetrics hosting = new(serviceProvider.GetRequiredService<IMeterFactory>());
        using Meter moderationMeter = serviceProvider.GetRequiredService<IMeterFactory>().Create(LabelerModerationHost.MeterName);
        Counter<long> reports = moderationMeter.CreateCounter<long>("idunno.bluesky.labeler.moderation.reports");
        using Meter otherMeter = new("idunno.Bluesky.AspNet.Labeler.Integration.Test.Unregistered");
        Counter<long> otherCounter = otherMeter.CreateCounter<long>("test.unregistered.messages");

        hosting.Frame();
        reports.Add(1);
        otherCounter.Add(1);

        Assert.True(provider.ForceFlush());
        Assert.Contains("idunno.bluesky.labeler.host.frames", exporter.MetricNames);
        Assert.Contains("idunno.bluesky.labeler.moderation.reports", exporter.MetricNames);
        Assert.DoesNotContain("test.unregistered.messages", exporter.MetricNames);
    }

    /// <summary>Captures exported names to verify meter selection, which direct metric listeners bypass.</summary>
    private sealed class RecordingMetricExporter : BaseExporter<Metric>
    {
        /// <summary>Gets the instrument names observed during explicit collection.</summary>
        public HashSet<string> MetricNames { get; } = [];

        /// <summary>Captures instrument names emitted by the configured metrics provider.</summary>
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
