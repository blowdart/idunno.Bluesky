// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.AspNet.Labeler;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry.Metrics;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>Provides extensions for collecting labeler hosting instrumentation.</summary>
public static class LabelerMetricsExtensions
{
    /// <summary>Enables collection of labeler hosting metrics.</summary>
    /// <param name="builder">The metrics provider builder.</param>
    /// <returns>The builder for chaining additional configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks><para>This extension selects the hosting meter. Configure readers and exporters separately.</para></remarks>
    public static MeterProviderBuilder AddBlueskyLabelerMetrics(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(HostingMetrics.MeterName);
    }
}
