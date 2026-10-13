// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Samples.BotSdk;

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Checks sample eligibility and bounded replay generation recovery.</summary>
public sealed class ReplayEligibilityTests
{
    /// <summary>Uses the durable witness boundary, even when the post was authored earlier.</summary>
    /// <param name="seconds">The witnessed-time offset, or a missing timestamp.</param>
    /// <param name="eligible">Whether a reply should be selected.</param>
    [Theory]
    [InlineData(null, false)]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void SampleUsesWitnessedTimeRatherThanPostCreationTime(int? seconds, bool eligible)
    {
        var item = TestRecords.Event("#beans") with
        {
            WitnessedAt = seconds is int offset ? TestRecords.Start.AddSeconds(offset) : null
        };
        BeansBotPolicy policy = new();
        Assert.Equal(eligible ? 1 : 0, policy.Evaluate(item, TestRecords.Start).Count);
    }

    /// <summary>Resets independent recovery episodes without allowing repeated prefixes to evade the bound.</summary>
    [Fact]
    public void GenerationRecoveryResetsOnlyBeyondPreviouslyHandledProgress()
    {
        GenerationRecovery recovery = new();
        recovery.Observe(10);
        for (int incident = 0; incident < 10; incident++)
        {
            for (int retry = 1; retry <= 5; retry++)
            {
                recovery.Retry();
                recovery.Observe(10 + incident);
                recovery.Observe(9);
                Assert.Equal(retry, recovery.Retries);
            }

            recovery.Observe(11 + incident);
            Assert.Equal(0, recovery.Retries);
        }
    }
}
