// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace idunno.AtProto;

/// <summary>
/// Metrics for a <see cref="DidHandleCache"/>.
/// </summary>
public sealed class DidHandleCacheMetrics
{
    // For non-DI scenarios, see https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#best-practices
    private static readonly Meter s_meter = new(MeterName, MeterVersion);

    /// <summary>
    /// Creates a new instance of <see cref="DidHandleCacheMetrics"/>.
    /// </summary>
    /// <param name="meterFactory">The <see cref="IMeterFactory"/>, if any, to use to create meters.</param>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = " IMeterFactory automatically manages the lifetime of any Meter objects it creates")]
    internal DidHandleCacheMetrics(IMeterFactory? meterFactory)
    {
        Initialize(meterFactory is null ? s_meter : meterFactory.Create(MeterName, MeterVersion));
    }

    /// <summary>
    /// Gets the meter name publishing metrics.
    /// </summary>
    public static string MeterName => "idunno.AtProto.DidHandleCache";

    /// <summary>
    /// Gets the current version of the meter.
    /// </summary>
    public static string MeterVersion => "1.0.0";

    internal Counter<long> Hits { get; private set; }

    internal Counter<long> Misses { get; private set; }

    internal Counter<long> CoalescedLookups { get; private set; }

    internal Counter<long> RejectedLookups { get; private set; }

    internal Counter<long> InvalidHandles { get; private set; }

    internal Counter<long> Invalidations { get; private set; }

    [MemberNotNull(
        nameof(Hits),
        nameof(Misses),
        nameof(CoalescedLookups),
        nameof(RejectedLookups),
        nameof(InvalidHandles),
        nameof(Invalidations))]
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Guidelines suggest all lower case.")]
    private void Initialize(Meter meter)
    {
        string prefix = MeterName.ToLowerInvariant();

        Hits = meter.CreateCounter<long>(
            name: $"{prefix}.total.hits",
            unit: "{lookups}",
            description: "Total number of handle lookups answered from the cache, including cached failures.");

        Misses = meter.CreateCounter<long>(
            name: $"{prefix}.total.misses",
            unit: "{lookups}",
            description: "Total number of handle lookups which started a resolution.");

        CoalescedLookups = meter.CreateCounter<long>(
            name: $"{prefix}.total.coalesced_lookups",
            unit: "{lookups}",
            description: "Total number of handle lookups which waited for a resolution already in progress for the same DID.");

        RejectedLookups = meter.CreateCounter<long>(
            name: $"{prefix}.total.rejected_lookups",
            unit: "{lookups}",
            description: "Total number of handle lookups which returned an uncached invalid handle because too many lookups were pending, or a resolution slot did not become free in time.");

        InvalidHandles = meter.CreateCounter<long>(
            name: $"{prefix}.total.invalid_handles",
            unit: "{resolutions}",
            description: "Total number of resolutions which could not resolve or verify a handle.");

        Invalidations = meter.CreateCounter<long>(
            name: $"{prefix}.total.invalidations",
            unit: "{invalidations}",
            description: "Total number of times a DID was invalidated.");
    }
}
