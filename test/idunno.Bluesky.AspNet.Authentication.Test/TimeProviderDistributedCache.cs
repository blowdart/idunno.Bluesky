// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Caching.Distributed;

namespace idunno.Bluesky.AspNet.Authentication.Test;

internal sealed class TimeProviderDistributedCache(TimeProvider timeProvider) : IDistributedCache
{
    private readonly object _lock = new();
    private readonly Dictionary<string, CacheEntry> _entries = [];

    public byte[]? Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out CacheEntry? entry))
            {
                return null;
            }

            DateTimeOffset now = timeProvider.GetUtcNow();

            if (entry.IsExpired(now))
            {
                _entries.Remove(key);
                return null;
            }

            entry.LastAccessed = now;

            return entry.Value.ToArray();
        }
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        return Task.FromResult(Get(key));
    }

    public void Refresh(string key)
    {
        _ = Get(key);
    }

    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Refresh(key);

        return Task.CompletedTask;
    }

    public void Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_lock)
        {
            _entries.Remove(key);
        }
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Remove(key);

        return Task.CompletedTask;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        if (options.AbsoluteExpiration is not null && options.AbsoluteExpirationRelativeToNow is not null)
        {
            throw new ArgumentException("Only one absolute expiration may be set.", nameof(options));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset? absoluteExpiration = options.AbsoluteExpirationRelativeToNow is TimeSpan relativeExpiration
            ? now.Add(relativeExpiration)
            : options.AbsoluteExpiration;

        lock (_lock)
        {
            _entries[key] = new CacheEntry(
                value.ToArray(),
                absoluteExpiration,
                options.SlidingExpiration,
                now);
        }
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Set(key, value, options);

        return Task.CompletedTask;
    }

    private sealed class CacheEntry(
        byte[] value,
        DateTimeOffset? absoluteExpiration,
        TimeSpan? slidingExpiration,
        DateTimeOffset lastAccessed)
    {
        internal byte[] Value { get; } = value;

        internal DateTimeOffset LastAccessed { get; set; } = lastAccessed;

        internal bool IsExpired(DateTimeOffset now) =>
            (absoluteExpiration is DateTimeOffset absolute && now >= absolute) ||
            (slidingExpiration is TimeSpan sliding && now - LastAccessed >= sliding);
    }
}
