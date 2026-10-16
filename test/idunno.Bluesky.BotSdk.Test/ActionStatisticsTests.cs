// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Checks the per-process action count and plain-text home page summary.</summary>
public sealed class ActionStatisticsTests
{
    /// <summary>Uses a fixed UTC startup date and resets the count for each new run.</summary>
    [Fact]
    public void ShowsCurrentRunCountAndStartupDate()
    {
        DateTimeOffset startedAt = new(2026, 10, 8, 13, 14, 26, TimeSpan.FromHours(1));
        ActionStatistics statistics = new(startedAt);
        Assert.Equal("0 actions completed since 2026-10-08 12:14:26Z \U0001f525", statistics.Summary);

        statistics.RecordCompletion();
        Assert.Equal("1 action completed since 2026-10-08 12:14:26Z \U0001f525", statistics.Summary);
        statistics.RecordCompletion();
        Assert.Equal("2 actions completed since 2026-10-08 12:14:26Z \U0001f525", statistics.Summary);
        Assert.StartsWith("0 actions completed since ", new ActionStatistics(startedAt.AddHours(1)).Summary);
    }

    /// <summary>Preserves increments while HTTP requests read the summary concurrently.</summary>
    [Fact]
    public void CountsConcurrentCompletions()
    {
        ActionStatistics statistics = new(TestRecords.Start);
        Parallel.For(0, 1000, _ =>
        {
            statistics.RecordCompletion();
            Assert.Contains(" completed since ", statistics.Summary);
        });

        Assert.StartsWith("1000 actions completed since ", statistics.Summary);
    }
}
