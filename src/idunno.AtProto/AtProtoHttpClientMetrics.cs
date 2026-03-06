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
    public class AtProtoHttpClientMetrics
    {
        // Follows boundaries from http.server.request.duration/http.client.request.duration
        private static readonly IReadOnlyList<double> s_shortSecondsBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10];

        // For non-DI scenarios, see https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#best-practices
        private static readonly Meter s_meter = new (MeterName, MeterVersion);

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClientMetrics"/> using the provided <see cref="IMeterFactory"/> to create the underlying <see cref="Meter"/>.
        /// </summary>
        /// <param name="meterFactory">An optional <see cref="IMeterFactory"/> to use for creating the underlying <see cref="Meter"/>.</param>
        [SuppressMessage(
            "Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "but IMeterFactory automatically manages the lifetime of any Meter objects it creates, disposing them when the DI container is disposed.")]
        public AtProtoHttpClientMetrics(IMeterFactory? meterFactory)
        {
            if (meterFactory == null)
            {
                Initialize(s_meter);
            }
            else
            {
                Initialize(meterFactory.Create(MeterName, MeterVersion));
            }
        }

        [MemberNotNull(
            nameof(RequestsSent),
            nameof(ResponsesReceived),
            nameof(SuccessfulRequests),
            nameof(FailedRequests),
            nameof(DPoPRetries),
            nameof(DeserializationFailures),
            nameof(RequestDuration),
            nameof(XrpcRequests)
            )]
        private void Initialize(Meter meter)
        {
            RequestsSent = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.requests.total",
                description: "Total requests sent",
                unit: "{requests}");

            ResponsesReceived = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.responses.total",
                description: "Total responses received",
                unit: "{responses}");

            SuccessfulRequests = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.requests.total.successful",
                description: "Total successful requests",
                unit: "{requests}");

            FailedRequests = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.requests.total.failure",
                description: "Total failed requests",
                unit: "{requests}");

            DPoPRetries =  meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.requests.total.dpop_retry",
                description: "Total request retries due to DPoP nonce rotation",
                unit: "{requests}");

            DeserializationFailures = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.responses.total.deserialization_failure",
                description: "Total Deserialization failures",
                unit: "{requests}");

            RequestDuration = meter.CreateHistogram<double>(
                name: $"{MeterName.ToLowerInvariant()}.request.duration",
                description: "Request duration",
                unit: "s",
                advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = s_shortSecondsBucketBoundaries });

            XrpcRequests = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.requests.total.xrpc_request",
                description: "Total XRPC requests",
                unit: "{requests}");
        }

        /// <summary>
        /// Gets the meter name publishing metrics.
        /// </summary>
        public static string MeterName => "idunno.AtProto.AtProtoHttpClient";

        /// <summary>
        /// Gets the current version of the meter.
        /// </summary>
        public static string MeterVersion => "1.0.0";

        internal Counter<long> RequestsSent { get; private set; }

        internal Counter<long> ResponsesReceived { get; private set; }

        internal Counter<long> SuccessfulRequests { get; private set; }

        internal Counter<long> FailedRequests { get; private set; }

        internal Counter<long> DPoPRetries { get; private set; }

        internal Counter<long> DeserializationFailures { get; private set; }

        internal Histogram<double> RequestDuration { get; private set; }

        internal Counter<long> XrpcRequests { get; private set; }
    }
}
