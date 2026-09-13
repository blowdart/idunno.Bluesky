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
                if (!s_warned)
                {
                    s_warned = true;
                    Logger.UsingInMemoryCorrelationCacheWarning();
                }
            }
        }
    }

    private static MemoryCache Cache { get; set; }

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

        CorrelationStateSettingContext context = new(state.ToJson());
        await Events.PreStoring(context).ConfigureAwait(false);

        Cache.Set($"{correlationId}", context.State, cacheOptions);
    }

    /// <inheritdoc/>
    public async Task<OAuthLoginState?> GetOAuthLoginState(Guid correlationId)
    {
        string? encodedState = Cache.Get($"{correlationId}") as string;

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
