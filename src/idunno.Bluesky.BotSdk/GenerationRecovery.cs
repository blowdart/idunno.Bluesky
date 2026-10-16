// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.BotSdk;

/// <summary>Bounds generation retries until replay handles a previously unseen sequence.</summary>
internal sealed class GenerationRecovery
{
    private long _highestProcessed;

    /// <summary>Gets the number of generation retries since forward progress.</summary>
    public int Retries { get; private set; }

    /// <summary>Records a generation recovery attempt.</summary>
    public void Retry() => Retries++;

    /// <summary>Resets recovery after handling a sequence beyond the high-water mark.</summary>
    /// <param name="processed">The successfully handled sequence.</param>
    public void Observe(long processed)
    {
        if (processed > _highestProcessed)
        {
            // Keep this high-water mark across enumerations: replaying the same prefix
            // after a stale checkpoint must not reset a repeated generation failure.
            _highestProcessed = processed;
            Retries = 0;
        }
    }
}
