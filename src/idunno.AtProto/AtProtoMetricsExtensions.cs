// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.DidPlcDirectory;
using idunno.AtProto.Jetstream;
using idunno.DidPlcDirectory;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry.Metrics;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods to simplify registering of the various idunno.AtProto instrumentation meters.
/// </summary>
public static class AtProtoMetricsExtensions
{
    /// <summary>
    /// Enables the instrumentation data collection for <see cref="AtProtoHttpClient"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static MeterProviderBuilder AddAtProtoHttpClientMetrics(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(AtProtoHttpClientMetrics.MeterName);
    }

    /// <summary>
    /// Enables the instrumentation data collection for <see cref="DirectoryServer"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static MeterProviderBuilder AddAtProtoDirectoryMetrics(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddMeter(DirectoryMetrics.MeterName);
    }

    /// <summary>
    /// Enables the instrumentation data collection for <see cref="AtProtoJetstream"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static MeterProviderBuilder AddAtProtoJetstreamMetrics(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddMeter(JetstreamMetrics.MeterName);
    }
}
