// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace idunno.AtProto.OAuthCallback;

/// <summary>
/// Publishes metrics for the OAuth callback server.
/// </summary>
[SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Metric names are typically lower case.")]
public static class CallbackServerMetrics
{
    private static readonly Meter s_meter = new(MeterName, MeterVersion);
    private static readonly Counter<long> s_callbacks = s_meter.CreateCounter<long>(
        name: $"{MeterName.ToLowerInvariant()}.callbacks.total",
        description: "OAuth callbacks completed by the local callback server.",
        unit: "{callbacks}");
    private static readonly Histogram<double> s_callbackWaitDuration = s_meter.CreateHistogram<double>(
        name: $"{MeterName.ToLowerInvariant()}.callback.wait.duration",
        description: "Time spent waiting for an OAuth callback.",
        unit: "s");
    private static readonly Counter<long> s_rejectedRequests = s_meter.CreateCounter<long>(
        name: $"{MeterName.ToLowerInvariant()}.requests.rejected.total",
        description: "Requests rejected by the local OAuth callback server.",
        unit: "{requests}");

    /// <summary>
    /// Gets the meter name publishing callback server metrics.
    /// </summary>
    public static string MeterName => "idunno.AtProto.OAuthCallback";

    /// <summary>
    /// Gets the current version of the meter.
    /// </summary>
    public static string MeterVersion => "1.0.0";

    internal static void RecordCallbackCompletion(string outcome, long startTimestamp)
    {
        s_callbacks.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

        if (startTimestamp != 0)
        {
            s_callbackWaitDuration.Record(
                Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
                new KeyValuePair<string, object?>("outcome", outcome));
        }
    }

    internal static void RecordRejectedRequest(string reason) =>
        s_rejectedRequests.Add(1, new KeyValuePair<string, object?>("reason", reason));
}
