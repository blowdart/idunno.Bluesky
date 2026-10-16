// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

namespace idunno.Bluesky.BotSdk;

/// <summary>Tracks newly confirmed actions and developer-mode simulations during the current process lifetime.</summary>
/// <param name="startedAt">The process start time displayed in the summary.</param>
public sealed class ActionStatistics(DateTimeOffset startedAt)
{
    private long _count;

    /// <summary>Records a newly committed action outcome.</summary>
    public void RecordCompletion() => Interlocked.Increment(ref _count);

    /// <summary>Gets a human-readable action count and process start time.</summary>
    public string Summary
    {
        get
        {
            long count = Interlocked.Read(ref _count);
            string noun = count == 1 ? "action" : "actions";

            return $"{count.ToString(CultureInfo.InvariantCulture)} {noun} completed since {startedAt.ToString("u", CultureInfo.InvariantCulture)} \U0001f525";
        }
    }
}
