// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;

using idunno.AtProto.Firehose;
using idunno.AtProto.Repo;

using Microsoft.Extensions.Diagnostics.Metrics.Testing;

using static idunno.AtProto.Integration.Test.FirehoseTestData;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public sealed class FirehoseSignatureVerifierTests : IDisposable
{
    private static readonly byte[] s_data = [1, 2, 3, 4];

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _rotatedKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ManualTimeProvider _time = new();
    private readonly FirehoseMetrics _metrics = new(null);
    private readonly Did _did = new(TestDid);
    private int _resolutions;
    private Func<DidDocument?> _document;

    public FirehoseSignatureVerifierTests()
    {
        _document = () => DidDocumentFor(TestDid, SigningKeyVerifier.RepositorySigningKeyFragment, _key);
    }

    public void Dispose()
    {
        _key.Dispose();
        _rotatedKey.Dispose();
    }

    [Fact]
    public async Task CachedKeysExpire()
    {
        using FirehoseSignatureVerifier verifier = CreateVerifier();
        using MetricCollector<long> hits = new(_metrics.SigningKeyCacheHits);
        using MetricCollector<long> misses = new(_metrics.SigningKeyCacheMisses);

        await Verify(verifier, _key);
        await Verify(verifier, _key);
        _time.Advance(TimeSpan.FromMinutes(59));
        await Verify(verifier, _key);

        Assert.Equal(1, _resolutions);

        _time.Advance(TimeSpan.FromMinutes(1));
        await Verify(verifier, _key);

        Assert.Equal(2, _resolutions);
        Assert.Equal(2, hits.GetMeasurementSnapshot().EvaluateAsCounter());
        Assert.Equal(2, misses.GetMeasurementSnapshot().EvaluateAsCounter());
    }

    [Fact]
    public async Task CachedKeysDoNotExpireWhileTheTimeProviderHasNotAdvanced()
    {
        using FirehoseSignatureVerifier verifier = CreateVerifier(new FirehoseOptions { SigningKeyCacheDuration = TimeSpan.FromMilliseconds(50) });

        await Verify(verifier, _key);
        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        await Verify(verifier, _key);

        Assert.Equal(1, _resolutions);
    }

    [Fact]
    public async Task InvalidatingADidResolvesItAgain()
    {
        using FirehoseSignatureVerifier verifier = CreateVerifier();

        await Verify(verifier, _key);
        verifier.Invalidate(_did);
        await Verify(verifier, _key);

        Assert.Equal(2, _resolutions);
    }

    [Fact]
    public async Task RotatedKeysAreRefreshedAtMostOncePerInterval()
    {
        using FirehoseSignatureVerifier verifier = CreateVerifier();
        using MetricCollector<long> refreshes = new(_metrics.SigningKeyRefreshes);

        await Verify(verifier, _key);
        _document = () => DidDocumentFor(TestDid, SigningKeyVerifier.RepositorySigningKeyFragment, _rotatedKey);

        await Assert.ThrowsAsync<InvalidDataException>(() => Verify(verifier, _rotatedKey));
        Assert.Equal(1, _resolutions);

        _time.Advance(FirehoseSignatureVerifier.MinimumRefreshInterval);
        await Verify(verifier, _rotatedKey);
        Assert.Equal(2, _resolutions);

        await Assert.ThrowsAsync<InvalidDataException>(() => Verify(verifier, _key));
        await Assert.ThrowsAsync<InvalidDataException>(() => Verify(verifier, _key));
        Assert.Equal(2, _resolutions);
        Assert.Equal(1, refreshes.GetMeasurementSnapshot().EvaluateAsCounter());
    }

    [Fact]
    public async Task FailedRefreshesKeepTheCachedKeyAndCountAsARefresh()
    {
        using FirehoseSignatureVerifier verifier = CreateVerifier();

        await Verify(verifier, _key);
        _time.Advance(FirehoseSignatureVerifier.MinimumRefreshInterval);
        _document = () => throw new HttpRequestException("resolution failed");

        await Assert.ThrowsAsync<InvalidDataException>(() => Verify(verifier, _rotatedKey));
        Assert.Equal(2, _resolutions);

        await Verify(verifier, _key);
        _time.Advance(FirehoseSignatureVerifier.MinimumRefreshInterval - TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<InvalidDataException>(() => Verify(verifier, _rotatedKey));
        await Verify(verifier, _key);
        Assert.Equal(2, _resolutions);

        _time.Advance(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<InvalidDataException>(() => Verify(verifier, _rotatedKey));
        Assert.Equal(3, _resolutions);
    }

    [Fact]
    public async Task FailedResolutionsAreCachedBriefly()
    {
        _document = () => throw new HttpRequestException("resolution failed");
        using FirehoseSignatureVerifier verifier = CreateVerifier();

        await Assert.ThrowsAsync<InvalidDataException>(() => Verify(verifier, _key));
        await Assert.ThrowsAsync<InvalidDataException>(() => Verify(verifier, _key));
        Assert.Equal(1, _resolutions);

        _document = () => DidDocumentFor(TestDid, SigningKeyVerifier.RepositorySigningKeyFragment, _key);
        _time.Advance(FirehoseSignatureVerifier.FailedResolutionDuration);
        await Verify(verifier, _key);

        Assert.Equal(2, _resolutions);
    }

    [Fact]
    public async Task KeysResolvedWhilstTheirDidIsInvalidatedAreNotCached()
    {
        FirehoseSignatureVerifier? verifier = null;
        int resolutions = 0;

        using (verifier = new FirehoseSignatureVerifier(
            (_, _) =>
            {
                if (Interlocked.Increment(ref resolutions) == 1)
                {
                    // An #identity event on another subscription arrives whilst the key is being resolved.
                    verifier!.Invalidate(_did);
                }

                return Task.FromResult(_document());
            },
            new FirehoseOptions(),
            _metrics,
            _time))
        {
            await Verify(verifier, _key);
            await Verify(verifier, _key);
            await Verify(verifier, _key);
        }

        Assert.Equal(2, resolutions);
    }

    [Fact]
    public async Task KeysAreCachedPerFragment()
    {
        using FirehoseSignatureVerifier verifier = CreateVerifier();

        await Verify(verifier, _key);
        await Assert.ThrowsAsync<InvalidDataException>(() => Verify(verifier, _key, SigningKeyVerifier.LabelSigningKeyFragment));

        Assert.Equal(2, _resolutions);
    }

    [Fact]
    public async Task KeysAreNotCachedWhenCachingIsDisabled()
    {
        using FirehoseSignatureVerifier verifier = CreateVerifier(new FirehoseOptions { CacheSigningKeys = false });

        await Verify(verifier, _key);
        await Verify(verifier, _key);

        Assert.False(verifier.IsCaching);
        Assert.Equal(2, _resolutions);
    }

    private FirehoseSignatureVerifier CreateVerifier(FirehoseOptions? options = null) => new(
        (_, _) =>
        {
            Interlocked.Increment(ref _resolutions);
            return Task.FromResult(_document());
        },
        options ?? new FirehoseOptions(),
        _metrics,
        _time);

    private Task Verify(FirehoseSignatureVerifier verifier, ECDsa signer, string fragment = SigningKeyVerifier.RepositorySigningKeyFragment) =>
        verifier.VerifyAsync(_did, fragment, s_data, Sign(signer, s_data), "test", [], TestContext.Current.CancellationToken);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}