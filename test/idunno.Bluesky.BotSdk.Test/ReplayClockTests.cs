// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Checks bounded replay recovery deadlines before and after SDK timer completion.</summary>
public sealed class ReplayClockTests
{
    /// <summary>Retains the resume margin after a long wait fires, even if its timer is then disposed.</summary>
    [Fact]
    public void LongWaitRetainsBoundedResumeMargin()
    {
        FakeTimeProvider time = new(TestRecords.Start);
        ReplayClock clock = new(time);
        DateTimeOffset ordinary = TestRecords.Start.AddMinutes(5);
        DateTimeOffset resume = TestRecords.Start.AddMinutes(11);
        int fired = 0;
        using ITimer timer = clock.CreateTimer(_ => fired++, null, TimeSpan.FromMinutes(10), Timeout.InfiniteTimeSpan);
        Assert.Equal(resume, clock.Deadline(ordinary));
        time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(1, fired);
        timer.Dispose();
        Assert.Equal(resume, clock.Deadline(ordinary));
        time.Advance(TimeSpan.FromSeconds(59));
        Assert.True(clock.Deadline(ordinary) > time.GetUtcNow());
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(time.GetUtcNow(), clock.Deadline(ordinary));
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(clock.Deadline(ordinary) < time.GetUtcNow());
    }

    /// <summary>Does not grant a resume margin when an outstanding wait is cancelled or disposed.</summary>
    /// <param name="disposal">The timer cancellation operation.</param>
    [Theory]
    [InlineData("dispose")]
    [InlineData("async-dispose")]
    [InlineData("change")]
    public async Task CancelledWaitDoesNotExtendDeadline(string disposal)
    {
        FakeTimeProvider time = new(TestRecords.Start);
        ReplayClock clock = new(time);
        DateTimeOffset ordinary = TestRecords.Start.AddMinutes(5);
        int fired = 0;
        using ITimer timer = clock.CreateTimer(_ => fired++, null, TimeSpan.FromMinutes(10), Timeout.InfiniteTimeSpan);
        if (disposal == "async-dispose")
        {
            await timer.DisposeAsync();
        }
        else if (disposal == "change")
        {
            Assert.True(timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan));
        }
        else
        {
            timer.Dispose();
        }

        Assert.Equal(ordinary, clock.Deadline(ordinary));
        time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(0, fired);
        Assert.Equal(ordinary, clock.Deadline(ordinary));
    }
}
