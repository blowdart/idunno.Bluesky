// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Bluesky Authentication metrics.
/// </summary>
[SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Metric names are typically lower case.")]
public class BlueskyAuthenticationMetrics
{
    private static readonly IReadOnlyList<double> s_refreshWaitBucketBoundaries = [1, 5, 7.5, 10, 12.5, 15, 17.5, 20, 25, 30, 35, 40, 45, 60];

    // For non-DI scenarios, see https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#best-practices
    private static readonly Meter s_meter = new(MeterName, MeterVersion);

    /// <summary>
    /// Creates a new instance of <see cref="BlueskyAuthenticationMetrics"/> using the provided <see cref="IMeterFactory"/> to create the underlying <see cref="Meter"/>.
    /// </summary>
    /// <param name="meterFactory">An optional <see cref="IMeterFactory"/> to use for creating the underlying <see cref="Meter"/>.</param>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "but IMeterFactory automatically manages the lifetime of any Meter objects it creates, disposing them when the DI container is disposed.")]
    public BlueskyAuthenticationMetrics(IMeterFactory? meterFactory)
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
        nameof(AccessTokensRefreshed),
        nameof(AccessTokensRefreshFailures),
        nameof(AccessTokenRefreshWaits),
        nameof(AccessTokenRefreshWaitDuration),
        nameof(DataProtectionFailures),
        nameof(ProfileCacheMisses),
        nameof(SigninsTotal),
        nameof(SigninsFailed)
        )]
    private void Initialize(Meter meter)
    {
        AccessTokensRefreshed = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.tokenrefreshes.total",
            description: "Total access tokens refreshed",
            unit: "{refreshes}");

        AccessTokensRefreshFailures = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.tokenrefreshfailures.total",
            description: "Total access tokens refresh failures",
            unit: "{refreshes}");

        AccessTokenRefreshWaits = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.tokenrefreshwaits.total",
            description: "Total waits during token refresh as another refresh is in progress",
            unit: "{waits}");

        AccessTokenRefreshWaitDuration = meter.CreateHistogram<double>(
            name: $"{MeterName.ToLowerInvariant()}.tokenrefreshwaits.duration",
            description: "Duration of waits during token refresh as another refresh is in progress",
            unit: "s",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = s_refreshWaitBucketBoundaries });

        DataProtectionFailures = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.dataprotection.failures.total",
            description: "Total data protection failures",
            unit: "{failures}");

        ProfileCacheMisses = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.profilecache.misses.total",
            description: "Total profile cache misses",
            unit: "{misses}");

        SigninsTotal = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.signins.total",
            description: "Total sign-ins",
            unit: "{signins}");

        SigninsFailed = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.signins.failed.total",
            description: "Total failed sign-ins",
            unit: "{signins}");
    }

    /// <summary>
    /// Gets the meter name publishing metrics.
    /// </summary>
    public static string MeterName => "idunno.Bluesky.AspNet.Authentication";

    /// <summary>
    /// Gets the current version of the meter.
    /// </summary>
    public static string MeterVersion => "1.0.0";

    internal Counter<long> AccessTokensRefreshed { get; private set; }

    internal Counter<long> AccessTokensRefreshFailures { get; private set; }

    internal Counter<long> AccessTokenRefreshWaits { get; private set; }

    internal Histogram<double> AccessTokenRefreshWaitDuration { get; private set; }

    internal Counter<long> DataProtectionFailures { get; private set; }

    internal Counter<long> ProfileCacheMisses { get; private set; }

    internal Counter<long> SigninsTotal { get; private set; }

    internal Counter<long> SigninsFailed { get; private set; }
}
