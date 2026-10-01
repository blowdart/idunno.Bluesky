// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

namespace idunno.AtProto.Test;

/// <summary>
/// Measures the bytes the current thread allocates while running a hot path, for the allocation budget tests.
/// </summary>
/// <remarks>
/// <para>Allocations are counted on the current thread only, so a path must complete synchronously for its allocations to
/// be attributed to it, whatever other tests are running in parallel. Each measurement runs the path once first, so JIT
/// compilation and one time static initialisation are not counted.</para>
/// </remarks>
internal static class AllocationMeasurement
{
    /// <summary>
    /// Runs <paramref name="action"/> once to warm up, then again while counting allocations, and returns the bytes
    /// allocated per unit of work.
    /// </summary>
    /// <param name="units">The number of units of work, such as messages or items, that one run of <paramref name="action"/> performs.</param>
    /// <param name="action">The work to measure.</param>
    /// <returns>The bytes allocated per unit of work.</returns>
    public static long PerUnit(int units, Action action)
    {
        action();

        long start = GC.GetAllocatedBytesForCurrentThread();
        action();

        return (GC.GetAllocatedBytesForCurrentThread() - start) / units;
    }

    /// <summary>
    /// Runs <paramref name="action"/> once to warm up, then again while counting allocations, and returns the bytes
    /// allocated per unit of work and whether every call completed synchronously.
    /// </summary>
    /// <param name="units">The number of calls to make.</param>
    /// <param name="action">The work to measure, given the index of the call.</param>
    /// <returns>The bytes allocated per call, and whether every call completed synchronously.</returns>
    public static async Task<(long PerUnit, bool CompletedSynchronously)> PerUnitAsync(int units, Func<int, Task> action)
    {
        for (int i = 0; i < units; i++)
        {
            await action(i);
        }

        bool completedSynchronously = true;
        long start = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < units; i++)
        {
            Task task = action(i);
            completedSynchronously &= task.IsCompleted;
            await task;
        }

        return ((GC.GetAllocatedBytesForCurrentThread() - start) / units, completedSynchronously);
    }

    /// <summary>
    /// Records a measurement when benchmarks\capture.ps1 has asked for them, so it can suggest new budgets.
    /// </summary>
    /// <param name="path">The name of the measured path.</param>
    /// <param name="perUnit">The bytes allocated per unit of work.</param>
    public static void Report(string path, long perUnit)
    {
        if (Environment.GetEnvironmentVariable("IDUNNO_ALLOCATION_REPORT") is { Length: > 0 } reportDirectory)
        {
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(
                Path.Combine(reportDirectory, $"net{Environment.Version.Major}.{path}.txt"),
                perUnit.ToString(CultureInfo.InvariantCulture));
        }
    }
}
