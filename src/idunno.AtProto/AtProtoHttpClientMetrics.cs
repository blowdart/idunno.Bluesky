// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace idunno.AtProto
{
    /// <summary>
    /// AtProtoAgent metrics.
    /// </summary>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Metric names are typically lower case.")]
    public static class AtProtoHttpClientMetrics
    {
        // Follows boundaries from http.server.request.duration/http.client.request.duration
        private static readonly IReadOnlyList<double> s_shortSecondsBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10];

        /// <summary>
        /// Gets the meter name publishing metrics.
        /// </summary>
        public static string MeterName => "idunno.AtProto.AtProtoHttpClient";

        /// <summary>
        /// Gets the current version of the meter.
        /// </summary>
        public static string MeterVersion => "1.0.0";

        internal static readonly Meter s_meter = new(MeterName, MeterVersion);

        internal static Counter<long> RequestsSent
        {
            get;
        } = s_meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.requests.total",
            description: "Total requests sent",
            unit: "Requests");

        internal static Counter<long> ResponsesReceived
        {
            get;
        } = s_meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.responses.total",
            description: "Total responses received",
            unit: "Requests");

        internal static Counter<long> SuccessfulRequests
        {
            get;
        } = s_meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.requests.total.successful",
            description: "Total successful requests",
            unit: "Requests");

        internal static Counter<long> FailedRequests
        {
            get;
        } = s_meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.requests.total.failure",
            description: "Total failed requests",
            unit: "Requests");

        internal static Counter<long> DPoPRetries
        {
            get;
        } = s_meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.requests.total.dpop_retry",
            description: "Total request retries due to DPoP nonce rotation",
            unit: "Requests");

        internal static Counter<long> DeserializationFailures
        {
            get;
        } = s_meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.requests.total.deserialization_failure",
            description: "Total Deserialization failures",
            unit: "Requests");

        internal static Counter<long> CreateBlob
        {
            get;
        } = s_meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.requests.total.blob_post_request",
            description: "Total POST Blob requests",
            unit: "Requests");

        internal static Counter<long> GetRequests
        {
            get;
        } = s_meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.requests.total.get_request",
            description: "Total GET requests",
            unit: "Requests");

        internal static Counter<long> PostRequests
        {
            get;
        } = s_meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.requests.total.post_request",
            description: "Total POST requests",
            unit: "Requests");

        internal static Histogram<double> RequestDuration
        {
            get;
        } = s_meter.CreateHistogram<double>(
            name: $"{MeterName.ToLowerInvariant()}.request.duration",
            description: "Request duration",
            unit: "Milliseconds",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = s_shortSecondsBucketBoundaries });
    }
}
