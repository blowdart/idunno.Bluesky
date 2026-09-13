// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;

using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Authentication;

internal sealed class EphemeralCorrelationStateCache : ICorrelationStateCache
{
    internal const int DefaultSizeLimit = 1024;

    private static volatile bool s_warned;
    private static volatile bool s_capacityWarned;

    private static MemoryCache? s_cache;
    private static int s_configuredSizeLimit;

#if NET9_0_OR_GREATER
    private static readonly Lock s_warnedLock = new ();
    private static readonly Lock s_takeLock = new ();
    private static readonly Lock s_cacheLock = new ();
#else
    private static readonly object s_warnedLock = new();
    private static readonly object s_takeLock = new();
    private static readonly object s_cacheLock = new();
#endif

    private static readonly TimeSpan s_defaultSlidingExpiration = new(0, 0, 15, 0);

    [SuppressMessage("Major Code Smell", "S3010:Static fields should not be updated in constructors", Justification = "Used to ensure the emphermal warning is only logged once")]
    public EphemeralCorrelationStateCache(
        ILoggerFactory loggerFactory,
        TimeSpan? entryTimeToLive = null,
        int? sizeLimit = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sizeLimit ?? DefaultSizeLimit, 0);

        Logger = loggerFactory.CreateLogger<EphemeralCorrelationStateCache>();
        EntryTTL = entryTimeToLive ?? s_defaultSlidingExpiration;

        EnsureCache(sizeLimit ?? DefaultSizeLimit, Logger);

        if (!s_warned)
        {
            lock (s_warnedLock)
            {
                if (!s_warned)
                {
                    s_warned = true;
                    Logger.UsingInMemoryCorrelationCacheWarning();
                }
            }
        }
    }

    private static MemoryCache Cache => s_cache!;

    private TimeSpan EntryTTL { get; } = new(0, 15, 0);

    private ILogger<EphemeralCorrelationStateCache> Logger { get; set; }

    /// <inheritdoc/>
    public CorrelationStateCacheEvents Events { get; set; } = new CorrelationStateCacheEvents();

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null"/>.</exception>
    public async Task AddOAuthLoginState(Guid correlationId, OAuthLoginState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(DateTime.UtcNow.Add(EntryTTL))
            .SetSize(1);

        // Eviction for capacity fails a login which is still in flight, so it needs to be reported rather than
        // looking like a user who took too long over the authorization server.
        cacheOptions.RegisterPostEvictionCallback(OnStateEvicted, Logger);

        CorrelationStateSettingContext context = new(state.ToJson());
        await Events.PreStoring(context).ConfigureAwait(false);

        Cache.Set($"{correlationId}", context.State, cacheOptions);
    }

    /// <inheritdoc/>
    public async Task<OAuthLoginState?> GetOAuthLoginState(Guid correlationId)
    {
        string? encodedState = Cache.Get($"{correlationId}") as string;

        return await Decode(encodedState).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<OAuthLoginState?> TakeOAuthLoginState(Guid correlationId)
    {
        string? encodedState;

        // The read and the removal are held together so two concurrent callers cannot both be given the same state.
        lock (s_takeLock)
        {
            encodedState = Cache.Get($"{correlationId}") as string;
            Cache.Remove($"{correlationId}");
        }

        return await Decode(encodedState).ConfigureAwait(false);
    }

    private static void EnsureCache(int sizeLimit, ILogger logger)
    {
        lock (s_cacheLock)
        {
            if (s_cache is null)
            {
                s_configuredSizeLimit = sizeLimit;
                s_cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimit });
            }
            else if (s_configuredSizeLimit != sizeLimit)
            {
                logger.EphemeralStoreSizeLimitIgnored(nameof(EphemeralCorrelationStateCache), sizeLimit, s_configuredSizeLimit);
            }
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A post eviction callback must not throw, as MemoryCache gives the exception nowhere to go")]
    private static void OnStateEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (reason != EvictionReason.Capacity || state is not ILogger logger || s_capacityWarned)
        {
            return;
        }

        // Reaching the limit compacts the cache, evicting a proportion of it rather than a single entry, so report
        // this once for the process rather than once for every login the compaction removed.
        lock (s_warnedLock)
        {
            if (s_capacityWarned)
            {
                return;
            }

            s_capacityWarned = true;
        }

        try
        {
            logger.EphemeralCorrelationStateCacheCapacityReached(s_configuredSizeLimit);
        }
        catch (Exception)
        {
            // Deliberately ignored. This runs on a thread pool thread after the entry has already gone, so there is
            // nothing to recover and nowhere for an exception to propagate to; a failed diagnostic must not crash the process.
        }
    }

    private async Task<OAuthLoginState?> Decode(string? encodedState)    {
        if (string.IsNullOrEmpty(encodedState))
        {
            return null;
        }

        try
        {
            CorrelationStateRetrievedContext context = new(encodedState);
            await Events.PostRetrieval(context).ConfigureAwait(false);

            return OAuthLoginState.FromJson(context.State);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            // The state cannot be unprotected, which a key ring change or a tampered entry would both cause.
            // There is no state to return and no way to tell the two apart here, so the login simply fails.
            return null;
        }
    }

    /// <inheritdoc/>
    public Task RemoveCorrelationState(Guid correlationId)
    {
        Cache.Remove($"{correlationId}");
        return Task.CompletedTask;
    }
}
