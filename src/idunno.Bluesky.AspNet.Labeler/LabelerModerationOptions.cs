// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Configures explicit Xenon report admission independently of remote CLI administration.</summary>
public sealed class LabelerModerationOptions
{
    /// <summary>Gets the maximum concurrent report requests admitted before authentication.</summary>
    /// <value>The per-process limit, initially 16.</value>
    public int MaximumConcurrentReports { get; init; } = 16;

    /// <summary>Gets the maximum report requests admitted in each one-minute window.</summary>
    /// <value>The per-process limit, initially 120, including rejected authenticated submissions.</value>
    public int ReportsPerMinute { get; init; } = 120;

    /// <summary>Gets the maximum declaration-observation age.</summary>
    /// <value>The shared persisted freshness, initially 30 minutes, from one millisecond to one day in whole milliseconds.</value>
    public TimeSpan VerificationFreshness { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Gets the maximum accepted reporter service-token lifetime.</summary>
    /// <value>The positive lifetime, initially 15 minutes.</value>
    public TimeSpan MaximumServiceTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Rejects unbounded admission or incompatible verification configuration before serving traffic.</summary>
    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumConcurrentReports, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaximumConcurrentReports, 1024);
        ArgumentOutOfRangeException.ThrowIfLessThan(ReportsPerMinute, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ReportsPerMinute, 100000);
        ArgumentOutOfRangeException.ThrowIfLessThan(VerificationFreshness, TimeSpan.FromMilliseconds(1));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(VerificationFreshness, TimeSpan.FromDays(1));
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            VerificationFreshness.Ticks % TimeSpan.TicksPerMillisecond,
            0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumServiceTokenLifetime, TimeSpan.Zero);
    }
}
