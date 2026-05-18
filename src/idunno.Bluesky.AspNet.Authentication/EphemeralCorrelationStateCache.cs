// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.AtProto.Authentication;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Authentication;

internal sealed class EphemeralCorrelationStateCache : ICorrelationStateCache
{
    const string CorrelationPrefix = "_blueskyLoginCorrelation";

    private static volatile bool s_warned;

#if NET9_0_OR_GREATER
    private static readonly Lock s_warnedLock = new ();
#else
    private static readonly object s_warnedLock = new();
#endif

    private static readonly TimeSpan s_defaultSlidingExpiration = new(0, 0, 15, 0);

    static EphemeralCorrelationStateCache()
    {
        MemoryCacheOptions cacheOptions = new()
        {
            SizeLimit = 1024
        };

        Cache = new MemoryCache(cacheOptions);
    }

    [SuppressMessage("Major Code Smell", "S3010:Static fields should not be updated in constructors", Justification = "Used to ensure the emphermal warning is only logged once")]
    public EphemeralCorrelationStateCache(
        ILoggerFactory loggerFactory,
        TimeSpan? entryTimeToLive = null)

    {
        Logger = loggerFactory.CreateLogger<EphemeralCorrelationStateCache>();
        EntryTTL = entryTimeToLive ?? s_defaultSlidingExpiration;

        if (!s_warned)
        {
            lock (s_warnedLock)
            {
                s_warned = true;
                Logger.UsingInMemoryCorrelationCacheWarning();
            }
        }
    }

    private static MemoryCache Cache { get; set; }

    private TimeSpan EntryTTL { get; } = new(0, 15, 0);

    private ILogger<EphemeralCorrelationStateCache> Logger { get; set; }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null"/>.</exception>
    public async Task AddOAuthLoginState(Guid correlationId, OAuthLoginState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(DateTime.UtcNow.Add(EntryTTL))
            .SetSize(1);

        Cache.Set($"{CorrelationPrefix}{correlationId}", state.ToJson(), cacheOptions);
    }

    /// <inheritdoc/>
    public async Task<OAuthLoginState?> GetOAuthLoginState(Guid correlationId)
    {
        string? encodedState = Cache.Get($"{CorrelationPrefix}{correlationId}") as string;

        if (string.IsNullOrEmpty(encodedState))
        {
            return null;
        }

        try
        {
            return OAuthLoginState.FromJson(encodedState);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
