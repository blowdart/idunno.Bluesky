// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.OAuthCallback;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry.Metrics;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods for registering OAuth callback server metrics.
/// </summary>
public static class CallbackServerMetricsExtensions
{
    /// <summary>
    /// Enables instrumentation data collection for the local OAuth callback server.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static MeterProviderBuilder AddAtProtoOAuthCallbackMetrics(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(CallbackServerMetrics.MeterName);
    }
}
