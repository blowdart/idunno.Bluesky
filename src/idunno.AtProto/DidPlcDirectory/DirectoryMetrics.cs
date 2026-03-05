// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace idunno.AtProto.DidPlcDirectory
{
    /// <summary>
    /// Directory Agent metrics.
    /// </summary>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Metric names are typically lower case.")]
    public class DirectoryMetrics
    {
        // Follows boundaries from http.server.request.duration/http.client.request.duration
        private static readonly IReadOnlyList<double> s_shortSecondsBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10];

        // For non-DI scenarios, see https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#best-practices
        private static readonly Meter s_meter = new(MeterName, MeterVersion);

        /// <summary>
        /// Creates a new instance of <see cref="DirectoryMetrics"/> using the provided <see cref="IMeterFactory"/> to create the underlying <see cref="Meter"/>.
        /// </summary>
        /// <param name="meterFactory">An optional <see cref="IMeterFactory"/> to use for creating the underlying <see cref="Meter"/>.</param>
        [SuppressMessage(
            "Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "but IMeterFactory automatically manages the lifetime of any Meter objects it creates, disposing them when the DI container is disposed.")]
        public DirectoryMetrics(IMeterFactory? meterFactory)
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
            nameof(TotalRequests),
            nameof(FailedRequests),
            nameof(SuccessfulRequests),
            nameof(RequestDuration)
            )]
        private void Initialize(Meter meter)
        {
            TotalRequests = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.requests.total",
                description: "Total requests sent",
                unit: "{requests}");

            FailedRequests = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.requests.total.failed",
                description: "Total failed requests",
                unit: "{requests}");

            SuccessfulRequests = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.requests.total.succeeded",
                description: "Total succeeded requests",
                unit: "{requests}");

            RequestDuration = meter.CreateHistogram<double>(
                name: $"{MeterName.ToLowerInvariant()}.request.duration",
                description: "Request duration",
                unit: "s",
                advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = s_shortSecondsBucketBoundaries });
        }

        /// <summary>
        /// Gets the meter name publishing metrics.
        /// </summary>
        public static string MeterName => "idunno.AtProto.Directory";

        /// <summary>
        /// Gets the current version of the meter.
        /// </summary>
        public static string MeterVersion => "1.0.0";

        internal Counter<long> TotalRequests { get; private set; }

        internal Counter<long> FailedRequests { get; private set; }

        internal Counter<long> SuccessfulRequests { get; private set; }

        internal Histogram<double> RequestDuration { get; private set; }
    }
}
