// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Authentication.MySQL;

/// <summary>
/// Decides how often a store should delete the rows it has allowed to expire.
/// </summary>
/// <remarks>
/// <para>
///   Expiry is only ever a read filter, so expired rows stay readable to nothing but still occupy the table. Sweeping
///   them on write keeps the tables bounded without requiring an operator to schedule anything, but sweeping on every
///   write would put a table scoped delete in the path of every sign in. This throttles the sweep so that at most one
///   runs per interval per store instance.
/// </para>
/// <para>
///   The first sweep is scheduled one interval after construction rather than on the first write, so that restarting a
///   fleet of application servers does not have all of them sweep at once.
/// </para>
/// </remarks>
internal sealed class ExpiredEntrySweepThrottle
{
    private readonly TimeSpan _interval;
    private readonly TimeProvider _timeProvider;
    private long _lastSweepAt;
    private int _dueImmediately;

    /// <summary>
    /// Creates a new instance of <see cref="ExpiredEntrySweepThrottle"/>.
    /// </summary>
    /// <param name="interval">How long to wait between sweeps. <see cref="TimeSpan.Zero"/> disables sweeping.</param>
    /// <param name="paramName">The name of the parameter <paramref name="interval"/> was supplied as.</param>
    /// <param name="timeProvider">A provider for monotonic timestamps, or <see langword="null"/> to use <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is negative.</exception>
    internal ExpiredEntrySweepThrottle(TimeSpan interval, string paramName, TimeProvider? timeProvider = null)
    {
        if (interval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(paramName, interval, "The sweep interval cannot be negative.");
        }

        _interval = interval;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lastSweepAt = _timeProvider.GetTimestamp();
    }

    /// <summary>
    /// Creates a new instance of <see cref="ExpiredEntrySweepThrottle"/> whose current interval can be primed to be
    /// immediately due. Intended for tests.
    /// </summary>
    /// <param name="interval">How long to wait between sweeps. <see cref="TimeSpan.Zero"/> disables sweeping.</param>
    /// <param name="paramName">The name of the parameter <paramref name="interval"/> was supplied as.</param>
    /// <param name="dueImmediately">When <see langword="true"/>, the first sweep is claimable immediately rather than one interval after construction.</param>
    /// <param name="timeProvider">A provider for monotonic timestamps, or <see langword="null"/> to use <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is negative.</exception>
    internal ExpiredEntrySweepThrottle(TimeSpan interval, string paramName, bool dueImmediately, TimeProvider? timeProvider = null)
        : this(interval, paramName, timeProvider)
    {
        _dueImmediately = dueImmediately ? 1 : 0;
    }

    /// <summary>
    /// Gets a value indicating whether sweeping is enabled.
    /// </summary>
    internal bool IsEnabled => _interval > TimeSpan.Zero;

    /// <summary>
    /// Gets a value indicating whether the caller should sweep, claiming the current interval when it should.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the caller has claimed the sweep for the current interval, otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    ///   At most one concurrent caller is given <see langword="true"/> for any one interval. A caller which loses the
    ///   exchange does not retry, as the caller which won it is about to sweep on its behalf.
    /// </para>
    /// </remarks>
    internal bool TryClaimSweep()
    {
        if (!IsEnabled)
        {
            return false;
        }

        long lastSweepAt = Interlocked.Read(ref _lastSweepAt);
        long now = _timeProvider.GetTimestamp();

        if (Interlocked.CompareExchange(ref _dueImmediately, 0, 1) == 1)
        {
            return Interlocked.CompareExchange(ref _lastSweepAt, now, lastSweepAt) == lastSweepAt;
        }

        // Comparing elapsed time, rather than adding the interval to a timestamp, cannot overflow however large the interval is.
        if (_timeProvider.GetElapsedTime(lastSweepAt, now) < _interval)
        {
            return false;
        }

        return Interlocked.CompareExchange(ref _lastSweepAt, now, lastSweepAt) == lastSweepAt;
    }
}
