// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.BotSdk;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry.Metrics;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>Enables collection of the bot runtime's instrumentation.</summary>
public static class BotMetricsExtensions
{
    /// <summary>Enables collection of host-owned Bluesky bot runtime metrics.</summary>
    /// <param name="builder">The meter provider builder.</param>
    /// <returns>The builder for chained registration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks><para>The storage-backed Prometheus endpoint is separate from this instrumentation.</para></remarks>
    public static MeterProviderBuilder AddBlueskyBotMetrics(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(BotMetrics.MeterName);
    }
}
