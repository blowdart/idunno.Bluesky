// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Time.Testing;

namespace idunno.Bluesky.AspNet.Authentication.SQLite.Test;

public class ExpiredEntrySweepThrottleTests
{
    private static readonly TimeSpan s_shortInterval = TimeSpan.FromMilliseconds(50);

    [Fact]
    public void ASweepIsNotClaimableUntilAnIntervalHasElapsed()
    {
        ExpiredEntrySweepThrottle throttle = new(TimeSpan.FromMinutes(5), "interval");

        Assert.False(throttle.TryClaimSweep());
    }

    [Fact]
    public void ASweepIsClaimableOnceAnIntervalHasElapsed()
    {
        FakeTimeProvider timeProvider = new();
        ExpiredEntrySweepThrottle throttle = new(s_shortInterval, "interval", timeProvider);

        timeProvider.Advance(s_shortInterval);

        Assert.True(throttle.TryClaimSweep());
    }

    [Fact]
    public void ClaimingASweepConsumesTheInterval()
    {
        FakeTimeProvider timeProvider = new();
        ExpiredEntrySweepThrottle throttle = new(s_shortInterval, "interval", timeProvider);

        timeProvider.Advance(s_shortInterval);

        Assert.True(throttle.TryClaimSweep());
        Assert.False(throttle.TryClaimSweep());
    }

    [Fact]
    public async Task OnlyOneOfManyConcurrentCallersClaimsTheSameInterval()
    {
        FakeTimeProvider timeProvider = new();
        ExpiredEntrySweepThrottle throttle = new(s_shortInterval, "interval", dueImmediately: true, timeProvider);
        timeProvider.Advance(s_shortInterval);

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
    public void AMaximumIntervalDoesNotOverflow()
    {
        FakeTimeProvider timeProvider = new();
        ExpiredEntrySweepThrottle throttle = new(TimeSpan.MaxValue, "interval", timeProvider);

        Assert.False(throttle.TryClaimSweep());

        timeProvider.Advance(TimeSpan.FromDays(365 * 100));

        Assert.False(throttle.TryClaimSweep());
    }

    [Fact]
    public void AMaximumIntervalThatIsDueImmediatelyIsClaimableOnce()
    {
        FakeTimeProvider timeProvider = new();
        ExpiredEntrySweepThrottle throttle = new(TimeSpan.MaxValue, "interval", dueImmediately: true, timeProvider);

        Assert.True(throttle.TryClaimSweep());
        Assert.False(throttle.TryClaimSweep());
    }

    [Fact]
    public void ASweepDueImmediatelyStartsANewInterval()
    {
        FakeTimeProvider timeProvider = new();
        ExpiredEntrySweepThrottle throttle = new(s_shortInterval, "interval", dueImmediately: true, timeProvider);

        Assert.True(throttle.TryClaimSweep());

        timeProvider.Advance(s_shortInterval - TimeSpan.FromTicks(1));
        Assert.False(throttle.TryClaimSweep());

        timeProvider.Advance(TimeSpan.FromTicks(1));
        Assert.True(throttle.TryClaimSweep());
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
