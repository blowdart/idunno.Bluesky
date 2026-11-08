// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;

using idunno.Bluesky.AspNet.Labeler;

using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

public class DatabaseRetryTests
{
    [Fact]
    public async Task RuntimeDatabaseOperationContinuesRetryingUntilStorageRecovers()
    {
        SignalClock clock = new();
        int attempts = 0;
        int retryNotifications = 0;
        Task<int> operation = DatabaseRetry.ExecuteAsync(
            _ =>
            {
                if (++attempts <= 2)
                {
                    throw new SimulatedDatabaseException();
                }

                return Task.FromResult(42);
            },
            clock,
            NullLogger.Instance,
            "test runtime",
            TestContext.Current.CancellationToken,
            () => retryNotifications++);

        await clock.WaitForTimerAsync(TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(1));
        await clock.WaitForTimerAsync(TimeSpan.FromSeconds(2));
        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(42, await operation);
        Assert.Equal(3, attempts);
        Assert.Equal(2, retryNotifications);
    }

    [Fact]
    public async Task StartupDatabaseOperationRetriesFiveTimesAtThirtySecondIntervals()
    {
        SignalClock clock = new();
        int attempts = 0;
        Task<int> operation = DatabaseRetry.ExecuteStartupAsync(
            _ =>
            {
                if (++attempts <= 5)
                {
                    throw new SimulatedDatabaseException();
                }

                return Task.FromResult(42);
            },
            clock,
            NullLogger.Instance,
            "test startup",
            TestContext.Current.CancellationToken);

        for (int retry = 0; retry < 5; retry++)
        {
            await clock.WaitForTimerAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(retry + 1, attempts);
            clock.Advance(TimeSpan.FromSeconds(30));
        }

        Assert.Equal(42, await operation);
        Assert.Equal(6, attempts);
    }

    [Fact]
    public async Task StartupDatabaseOperationStopsAfterFiveRetries()
    {
        SignalClock clock = new();
        int attempts = 0;
        Task<int> operation = DatabaseRetry.ExecuteStartupAsync<int>(
            _ =>
            {
                attempts++;
                throw new SimulatedDatabaseException();
            },
            clock,
            NullLogger.Instance,
            "test startup",
            TestContext.Current.CancellationToken);

        for (int retry = 0; retry < 5; retry++)
        {
            await clock.WaitForTimerAsync(TimeSpan.FromSeconds(30));
            clock.Advance(TimeSpan.FromSeconds(30));
        }

        await Assert.ThrowsAsync<SimulatedDatabaseException>(() => operation);
        Assert.Equal(6, attempts);
    }

    private sealed class SimulatedDatabaseException() : DbException
    {
    }
}
