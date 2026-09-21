// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Authentication.SQLite;

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
    private readonly long _intervalMilliseconds;
    private readonly Func<long> _now;
    private long _nextSweepAt;

    /// <summary>
    /// Creates a new instance of <see cref="ExpiredEntrySweepThrottle"/>.
    /// </summary>
    /// <param name="interval">How long to wait between sweeps. <see cref="TimeSpan.Zero"/> disables sweeping.</param>
    /// <param name="paramName">The name of the parameter <paramref name="interval"/> was supplied as.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is negative.</exception>
    internal ExpiredEntrySweepThrottle(TimeSpan interval, string paramName)
        : this(interval, paramName, static () => Environment.TickCount64)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="ExpiredEntrySweepThrottle"/> with an injectable time source.
    /// </summary>
    /// <param name="interval">How long to wait between sweeps. <see cref="TimeSpan.Zero"/> disables sweeping.</param>
    /// <param name="paramName">The name of the parameter <paramref name="interval"/> was supplied as.</param>
    /// <param name="now">A function returning the current time as milliseconds, used in place of <see cref="Environment.TickCount64"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is negative.</exception>
    internal ExpiredEntrySweepThrottle(TimeSpan interval, string paramName, Func<long> now)
    {
        if (interval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(paramName, interval, "The sweep interval cannot be negative.");
        }

        _now = now;
        _intervalMilliseconds = (long)interval.TotalMilliseconds;
        _nextSweepAt = _now() + _intervalMilliseconds;
    }

    /// <summary>
    /// Gets a value indicating whether sweeping is enabled.
    /// </summary>
    internal bool IsEnabled => _intervalMilliseconds > 0;

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

        long now = _now();
        long nextSweepAt = Interlocked.Read(ref _nextSweepAt);

        if (now < nextSweepAt)
        {
            return false;
        }

        return Interlocked.CompareExchange(ref _nextSweepAt, now + _intervalMilliseconds, nextSweepAt) == nextSweepAt;
    }
}
