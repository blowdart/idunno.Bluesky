// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using idunno.AtProto.Repo;

using Microsoft.Extensions.Caching.Memory;

namespace idunno.AtProto.Firehose;

/// <summary>
/// Resolves signing keys and verifies signatures for firehose events, optionally caching the keys it resolves.
/// </summary>
/// <remarks>
/// <para>This is only created when signature verification is on, which is not the default. The cache bounds memory but not the
/// number of resolutions: a malicious server can thrash it by sending events from more distinct DIDs than the cache holds, or by
/// preceding each event with an <c>#identity</c> event for the same DID, which invalidates its keys. Either makes most events
/// resolve a DID document. This is documented rather than prevented.</para>
/// </remarks>
internal sealed class FirehoseSignatureVerifier : IDisposable
{
    /// <summary>
    /// How long a failure to resolve a usable key is remembered, so a DID which cannot be resolved is not resolved again for every event.
    /// </summary>
    internal static TimeSpan FailedResolutionDuration { get; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The minimum time between re-resolving a cached key because a signature failed to verify against it.
    /// </summary>
    internal static TimeSpan MinimumRefreshInterval { get; } = TimeSpan.FromMinutes(5);

    private static readonly string[] s_fragments = [SigningKeyVerifier.RepositorySigningKeyFragment, SigningKeyVerifier.LabelSigningKeyFragment];

    // Invalidation generations, striped by DID so they take bounded memory. A resolution which began before its DID's stripe was
    // invalidated is not cached, so a key resolved before an #identity event cannot be cached after it. Unrelated DIDs sharing a
    // stripe only cost an extra resolution.
    private const int GenerationStripes = 256;

    private readonly long[] _generations = new long[GenerationStripes];

#if NET9_0_OR_GREATER
    private readonly Lock _cacheLock = new();
#else
    private readonly object _cacheLock = new();
#endif

    private readonly Func<Did, CancellationToken, Task<DidDocument?>> _resolver;
    private readonly FirehoseMetrics? _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly MemoryCache? _cache;
    private readonly TimeSpan _cacheDuration;

    /// <summary>
    /// Creates a new instance of <see cref="FirehoseSignatureVerifier"/> which resolves every key it needs, without caching.
    /// </summary>
    /// <param name="resolver">The function used to resolve DID documents.</param>
    internal FirehoseSignatureVerifier(Func<Did, CancellationToken, Task<DidDocument?>> resolver) : this(resolver, null, null, null)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="FirehoseSignatureVerifier"/>.
    /// </summary>
    /// <param name="resolver">The function used to resolve DID documents.</param>
    /// <param name="options">The options configuring the signing key cache, or <see langword="null"/> to disable caching.</param>
    /// <param name="metrics">The metrics to record cache activity against, if any.</param>
    /// <param name="timeProvider">The time provider used to expire cache entries, or <see langword="null"/> for the system clock.</param>
    internal FirehoseSignatureVerifier(
        Func<Did, CancellationToken, Task<DidDocument?>> resolver,
        FirehoseOptions? options,
        FirehoseMetrics? metrics,
        TimeProvider? timeProvider)
    {
        _resolver = resolver;
        _metrics = metrics;
        _timeProvider = timeProvider ?? TimeProvider.System;

        if (options is { CacheSigningKeys: true })
        {
            _cacheDuration = options.SigningKeyCacheDuration;
            _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = options.SigningKeyCacheSize });
        }
    }

    /// <summary>
    /// Gets a value indicating whether resolved keys are cached.
    /// </summary>
    internal bool IsCaching => _cache is not null;

    /// <summary>
    /// Verifies <paramref name="signature"/> over <paramref name="signedData"/> with the key <paramref name="fragment"/> of <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The DID whose key signed the data.</param>
    /// <param name="fragment">The verification method fragment.</param>
    /// <param name="signedData">The signed bytes.</param>
    /// <param name="signature">The signature.</param>
    /// <param name="subject">A description of what was signed, for error messages.</param>
    /// <param name="documents">DID documents already resolved while decoding the current event, keyed by DID. Only used when keys are not cached.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidDataException">The DID document cannot be resolved, has no usable key, or the signature is invalid.</exception>
    public async Task VerifyAsync(
        Did did,
        string fragment,
        byte[] signedData,
        byte[] signature,
        string subject,
        Dictionary<Did, DidDocument?> documents,
        CancellationToken cancellationToken)
    {
        if (_cache is null)
        {
            if (!documents.TryGetValue(did, out DidDocument? document))
            {
                document = await ResolveDocumentAsync(did, cancellationToken).ConfigureAwait(false);
                documents[did] = document;
            }

            Verify(SigningKeyVerifier.GetKey(document, did, fragment), signedData, signature, subject);
            return;
        }

        (Did, string) cacheKey = (did, fragment);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (_cache.TryGetValue(cacheKey, out CachedKey? cached) && cached is not null && cached.ExpiresAt > now)
        {
            _metrics?.SigningKeyCacheHits.Add(1);
        }
        else
        {
            _metrics?.SigningKeyCacheMisses.Add(1);
            cached = await ResolveAndCacheAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            Verify(cached.GetKey(did), signedData, signature, subject);
        }
        catch (InvalidDataException) when (cached.Key is not null && now - cached.ResolvedAt >= MinimumRefreshInterval)
        {
            // The key may have been rotated since it was cached, so it is resolved again, at most once per refresh interval
            // so a stream of bad signatures cannot turn into a stream of DID resolutions.
            _metrics?.SigningKeyRefreshes.Add(1);
            cached = await ResolveAndCacheAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            Verify(cached.GetKey(did), signedData, signature, subject);
        }
    }

    /// <summary>
    /// Removes any cached keys for <paramref name="did"/>, so they are resolved again the next time they are needed.
    /// </summary>
    /// <param name="did">The DID whose keys are removed.</param>
    /// <remarks>
    /// <para>This is not rate limited, so a malicious server can send an <c>#identity</c> event before every event for a DID to force
    /// a resolution each time.</para>
    /// </remarks>
    public void Invalidate(Did did)
    {
        if (_cache is null)
        {
            return;
        }

        lock (_cacheLock)
        {
            _generations[Stripe(did)]++;

            foreach (string fragment in s_fragments)
            {
                _cache.Remove((did, fragment));
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _cache?.Dispose();

    private static void Verify(SigningKey key, byte[] signedData, byte[] signature, string subject)
    {
        try
        {
            key.Verify(signedData, signature, subject);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException($"The {subject} signature could not be verified.", exception);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A resolver failure makes the event unverifiable rather than ending the stream.")]
    private async Task<DidDocument?> ResolveDocumentAsync(Did did, CancellationToken cancellationToken)
    {
        try
        {
            return await _resolver(did, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidDataException($"The DID document for '{did}' could not be resolved.", exception);
        }
    }

    private static int Stripe(Did did) => (int)((uint)did.GetHashCode() % GenerationStripes);

    private async Task<CachedKey> ResolveAndCacheAsync((Did Did, string Fragment) cacheKey, CancellationToken cancellationToken)
    {
        int stripe = Stripe(cacheKey.Did);
        long generation;

        lock (_cacheLock)
        {
            generation = _generations[stripe];
        }

        CachedKey entry;

        try
        {
            DidDocument? document = await ResolveDocumentAsync(cacheKey.Did, cancellationToken).ConfigureAwait(false);
            entry = new CachedKey(SigningKeyVerifier.GetKey(document, cacheKey.Did, cacheKey.Fragment), null, _timeProvider.GetUtcNow(), _cacheDuration);
        }
        catch (InvalidDataException exception)
        {
            TimeSpan duration = _cacheDuration < FailedResolutionDuration ? _cacheDuration : FailedResolutionDuration;
            entry = new CachedKey(null, exception, _timeProvider.GetUtcNow(), duration);
        }

        lock (_cacheLock)
        {
            // The key is still used for this verification, but is not cached if the DID was invalidated whilst it was resolved.
            if (_generations[stripe] == generation)
            {
                _cache!.Set(cacheKey, entry, new MemoryCacheEntryOptions
                {
                    Size = 1,
                    AbsoluteExpirationRelativeToNow = entry.ExpiresAt - entry.ResolvedAt
                });
            }
        }

        return entry;
    }

    private sealed class CachedKey(SigningKey? key, InvalidDataException? failure, DateTimeOffset resolvedAt, TimeSpan duration)
    {
        public SigningKey? Key { get; } = key;

        public DateTimeOffset ResolvedAt { get; } = resolvedAt;

        public DateTimeOffset ExpiresAt { get; } = resolvedAt + duration;

        public SigningKey GetKey(Did did) =>
            Key ?? throw new InvalidDataException($"The signing key for '{did}' could not be resolved.", failure);
    }
}