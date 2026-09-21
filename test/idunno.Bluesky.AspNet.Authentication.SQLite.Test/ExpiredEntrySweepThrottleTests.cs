// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Authentication.SQLite.Test;

public class ExpiredEntrySweepThrottleTests
{
    private static readonly TimeSpan s_shortInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan s_afterShortIntervalElapses = TimeSpan.FromMilliseconds(250);

    [Fact]
    public void ASweepIsNotClaimableUntilAnIntervalHasElapsed()
    {
        ExpiredEntrySweepThrottle throttle = new(TimeSpan.FromMinutes(5), "interval");

        Assert.False(throttle.TryClaimSweep());
    }

    [Fact]
    public async Task ASweepIsClaimableOnceAnIntervalHasElapsed()
    {
        ExpiredEntrySweepThrottle throttle = new(s_shortInterval, "interval");

        await Task.Delay(s_afterShortIntervalElapses, TestContext.Current.CancellationToken);

        Assert.True(throttle.TryClaimSweep());
    }

    [Fact]
    public async Task ClaimingASweepConsumesTheInterval()
    {
        ExpiredEntrySweepThrottle throttle = new(s_shortInterval, "interval");

        await Task.Delay(s_afterShortIntervalElapses, TestContext.Current.CancellationToken);

        Assert.True(throttle.TryClaimSweep());
        Assert.False(throttle.TryClaimSweep());
    }

    [Fact]
    public async Task OnlyOneOfManyConcurrentCallersClaimsTheSameInterval()
    {
        long intervalMilliseconds = (long)s_shortInterval.TotalMilliseconds;
        long now = 0;

        ExpiredEntrySweepThrottle throttle = new(s_shortInterval, "interval", now: () => Volatile.Read(ref now));

        // Advance the clock past the first interval so the sweep is claimable, then hold it steady
        // so exactly one of the concurrent callers can win the interval.
        Volatile.Write(ref now, intervalMilliseconds);

        using Barrier barrier = new(32);
        bool[] claims = new bool[32];

        await Task.WhenAll(Enumerable.Range(0, 32).Select(index => Task.Run(
            () =>
            {
                barrier.SignalAndWait(TestContext.Current.CancellationToken);
                claims[index] = throttle.TryClaimSweep();
            },
            TestContext.Current.CancellationToken)));

        Assert.Equal(1, claims.Count(claimed => claimed));
    }

    [Fact]
    public void AZeroIntervalDisablesSweeping()
    {
        ExpiredEntrySweepThrottle throttle = new(TimeSpan.Zero, "interval");

        Assert.False(throttle.IsEnabled);
        Assert.False(throttle.TryClaimSweep());
    }

    [Fact]
    public void APositiveIntervalEnablesSweeping()
    {
        ExpiredEntrySweepThrottle throttle = new(s_shortInterval, "interval");

        Assert.True(throttle.IsEnabled);
    }

    [Fact]
    public void ANegativeIntervalIsRejected()
    {
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExpiredEntrySweepThrottle(TimeSpan.FromSeconds(-1), "interval"));

        Assert.Equal("interval", exception.ParamName);
    }
}
