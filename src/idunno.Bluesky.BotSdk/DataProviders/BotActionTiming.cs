// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.BotSdk.DataProviders;

/// <summary>Exposes committed action timings from one storage provider instance.</summary>
internal interface IBotActionTimingSource
{
    /// <summary>Gets the source of newly committed terminal action timings.</summary>
    BotActionTiming Timing { get; }
}

/// <summary>Publishes end-to-end timings for newly committed terminal action outcomes.</summary>
/// <remarks><para>Unknown enqueue times and negative wall-clock intervals do not produce observations.</para></remarks>
internal sealed class BotActionTiming
{
    /// <summary>Occurs after a terminal outcome commits, supplying its status and enqueue-to-completion duration in seconds.</summary>
    internal event Action<string, double>? Completed;

    /// <summary>Publishes a committed terminal timing when its durable enqueue time and clock interval are known.</summary>
    /// <param name="status">The terminal delivery status.</param>
    /// <param name="enqueuedAt">The durable enqueue time, or <see langword="null"/> for a migrated action with unknown history.</param>
    /// <param name="completedAt">The completion time captured from the provider's transaction clock.</param>
    internal void Record(string status, DateTimeOffset? enqueuedAt, DateTimeOffset completedAt)
    {
        if (status is not ("sent" or "not-sent" or "simulated") ||
            enqueuedAt is not DateTimeOffset enqueue || completedAt < enqueue)
        {
            return;
        }

        Completed?.Invoke(status, (completedAt - enqueue).TotalSeconds);
    }
}
