// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using idunno.AtProto;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Implements an in-memory profile cache.
/// </summary>
public sealed class EphermealProfileCache : IProfileCache
{
    private static volatile bool s_warned;

#if NET9_0_OR_GREATER
    private static readonly Lock s_warnedLock = new ();
#else
    private static readonly object s_warnedLock = new();
#endif

    static EphermealProfileCache()
    {
        MemoryCacheOptions cacheOptions = new()
        {
            SizeLimit = 1024
        };

        Cache = new MemoryCache(cacheOptions);
    }

    /// <summary>
    /// Creates a new instance of <see cref="EphermealProfileCache"/>.
    /// </summary>
    /// <param name="loggerFactory">The logger to create loggers from.</param>
    /// <param name="entryTimeToLive">The time to live for cache entries.</param>
    [SuppressMessage("Major Code Smell", "S3010:Static fields should not be updated in constructors", Justification = "Used to ensure the emphermal warning is only logged once")]
    public EphermealProfileCache(
        ILoggerFactory loggerFactory,
        TimeSpan? entryTimeToLive = null)
    {
        Logger = loggerFactory.CreateLogger<EphermealProfileCache>();
        EntryTTL = entryTimeToLive ?? new(0, 0, 15, 0);

        if (!s_warned)
        {
            lock (s_warnedLock)
            {
                s_warned = true;
                Logger.UsingInMemoryProfileCacheWarning();
            }
        }
    }

    private static MemoryCache Cache { get; set; }

    private TimeSpan EntryTTL { get; set; }

    private ILogger<EphermealProfileCache> Logger { get; set; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is <see langword="null" />./</exception>
    public async Task Add(Did did, ProfileCacheEntry profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        MemoryCacheEntryOptions options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(DateTime.UtcNow.Add(EntryTTL))
            .SetSize(1);

        Cache.Set($"{did}", profile, options);
    }

    /// <inheritdoc/>
    public async Task<ProfileCacheEntry?> GetCachedValue(Did did)
    {
        if (Cache.Get($"{did}") is not ProfileCacheEntry result)
        {
            return null;
        }

        return result;
    }
}
