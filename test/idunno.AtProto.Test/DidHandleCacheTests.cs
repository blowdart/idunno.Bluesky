// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class DidHandleCacheTests
{
    private static readonly Did s_did = new("did:plc:ewvi7nxzyoun6zhxsrcy6jgr");
    private static readonly Did s_otherDid = new("did:plc:g6ylltenitt4tp27bpwalh7b");
    private static readonly Handle s_handle = new("example.com");

    [Fact]
    public async Task AVerifiedHandleIsResolvedOnceAndCached()
    {
        TestResolver resolver = new(_ => Task.FromResult(s_handle));
        using DidHandleCache cache = new(resolver.ResolveAsync, null);

        Assert.False(cache.TryGetCachedHandle(s_did, out _));

        Assert.Equal(s_handle, await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken));
        Assert.Equal(s_handle, await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken));

        Assert.Equal(1, resolver.Calls);
        Assert.True(cache.TryGetCachedHandle(s_did, out Handle? cached));
        Assert.Equal(s_handle, cached);
        Assert.False(cache.TryGetCachedHandle(s_otherDid, out _));
    }

    [Fact]
    public async Task HandlesExpireAfterTheirDuration()
    {
        ManualTimeProvider timeProvider = new();
        TestResolver resolver = new(_ => Task.FromResult(s_handle));
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions { Duration = TimeSpan.FromMinutes(10), TimeProvider = timeProvider });

        await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(9));
        Assert.True(cache.TryGetCachedHandle(s_did, out _));

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        Assert.False(cache.TryGetCachedHandle(s_did, out _));

        await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        Assert.Equal(2, resolver.Calls);
    }

    [Fact]
    public async Task AnInvalidHandleIsCachedForTheFailedResolutionDuration()
    {
        ManualTimeProvider timeProvider = new();
        TestResolver resolver = new(_ => Task.FromResult(Handle.Invalid));
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions { FailedResolutionDuration = TimeSpan.FromMinutes(1), TimeProvider = timeProvider });

        Assert.Equal(Handle.Invalid, await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken));
        Assert.Equal(Handle.Invalid, await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken));
        Assert.Equal(1, resolver.Calls);

        Assert.True(cache.TryGetCachedHandle(s_did, out Handle? cached));
        Assert.Equal(Handle.Invalid, cached);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        Assert.False(cache.TryGetCachedHandle(s_did, out _));

        await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        Assert.Equal(2, resolver.Calls);
    }

    [Fact]
    public async Task AnInvalidHandleIsNotCachedForLongerThanTheDuration()
    {
        ManualTimeProvider timeProvider = new();
        TestResolver resolver = new(_ => Task.FromResult(Handle.Invalid));
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions
        {
            Duration = TimeSpan.FromSeconds(10),
            FailedResolutionDuration = TimeSpan.FromMinutes(1),
            TimeProvider = timeProvider
        });

        await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);

        timeProvider.Advance(TimeSpan.FromSeconds(10));
        Assert.False(cache.TryGetCachedHandle(s_did, out _));
    }

    [Fact]
    public async Task AResolverWhichThrowsResultsInAnInvalidHandle()
    {
        TestResolver resolver = new(_ => throw new HttpRequestException("Failed"));
        using DidHandleCache cache = new(resolver.ResolveAsync, null);

        Assert.Equal(Handle.Invalid, await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken));
        Assert.True(cache.TryGetCachedHandle(s_did, out Handle? cached));
        Assert.Equal(Handle.Invalid, cached);
    }

    [Fact]
    public async Task AResolutionWhichTimesOutResultsInAnInvalidHandle()
    {
        TestResolver resolver = new(async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return s_handle;
        });
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions { ResolutionTimeout = TimeSpan.FromMilliseconds(50) });

        Assert.Equal(Handle.Invalid, await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentLookupsShareOneResolution()
    {
        TaskCompletionSource<Handle> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestResolver resolver = new(_ => completion.Task);
        using DidHandleCache cache = new(resolver.ResolveAsync, null);

        ValueTask<Handle> first = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        ValueTask<Handle> second = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);

        completion.SetResult(s_handle);

        Assert.Equal(s_handle, await first);
        Assert.Equal(s_handle, await second);
        Assert.Equal(1, resolver.Calls);
    }

    [Fact]
    public async Task CancellingALookupDoesNotCancelTheSharedResolution()
    {
        TaskCompletionSource<Handle> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestResolver resolver = new(_ => completion.Task);
        using DidHandleCache cache = new(resolver.ResolveAsync, null);
        using CancellationTokenSource cancellationTokenSource = new();

        ValueTask<Handle> cancelled = cache.ResolveHandleAsync(s_did, cancellationTokenSource.Token);
        ValueTask<Handle> waiting = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);

        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await cancelled);
        Assert.False(resolver.LastCancellationToken.IsCancellationRequested);

        completion.SetResult(s_handle);

        Assert.Equal(s_handle, await waiting);
        Assert.True(cache.TryGetCachedHandle(s_did, out _));
    }

    [Fact]
    public async Task AnAlreadyCancelledLookupThrowsWithoutResolving()
    {
        TestResolver resolver = new(_ => Task.FromResult(s_handle));
        using DidHandleCache cache = new(resolver.ResolveAsync, null);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await cache.ResolveHandleAsync(s_did, new CancellationToken(canceled: true)));
        Assert.Equal(0, resolver.Calls);
    }

    [Fact]
    public async Task AHandleResolvedWhilstItsDidIsInvalidatedIsReturnedButNotCached()
    {
        TaskCompletionSource<Handle> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestResolver resolver = new(_ => completion.Task);
        using DidHandleCache cache = new(resolver.ResolveAsync, null);

        ValueTask<Handle> lookup = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);

        cache.Invalidate(s_did);
        completion.SetResult(s_handle);

        Assert.Equal(s_handle, await lookup);
        Assert.False(cache.TryGetCachedHandle(s_did, out _));
    }

    [Fact]
    public async Task ALookupAfterAnInvalidationStartsANewResolution()
    {
        TaskCompletionSource<Handle> stale = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Handle updated = new("updated.example.com");
        int calls = 0;
        TestResolver resolver = new(_ => Interlocked.Increment(ref calls) == 1 ? stale.Task : Task.FromResult(updated));
        using DidHandleCache cache = new(resolver.ResolveAsync, null);

        ValueTask<Handle> before = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        cache.Invalidate(s_did);
        Handle after = await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);

        stale.SetResult(s_handle);

        Assert.Equal(s_handle, await before);
        Assert.Equal(updated, after);
        Assert.Equal(2, resolver.Calls);
        Assert.True(cache.TryGetCachedHandle(s_did, out Handle? cached));
        Assert.Equal(updated, cached);
    }

    [Fact]
    public async Task InvalidateRemovesACachedHandle()
    {
        TestResolver resolver = new(_ => Task.FromResult(s_handle));
        using DidHandleCache cache = new(resolver.ResolveAsync, null);

        await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        await cache.ResolveHandleAsync(s_otherDid, TestContext.Current.CancellationToken);

        cache.Invalidate(s_did);

        Assert.False(cache.TryGetCachedHandle(s_did, out _));
        Assert.True(cache.TryGetCachedHandle(s_otherDid, out _));
    }

    [Fact]
    public async Task DisposingCancelsResolutionsInProgress()
    {
        TestResolver resolver = new(async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return s_handle;
        });
        DidHandleCache cache = new(resolver.ResolveAsync, null);

        ValueTask<Handle> lookup = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);

        cache.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await lookup);
        Assert.True(resolver.LastCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task ADisposedCacheThrowsButIgnoresInvalidation()
    {
        DidHandleCache cache = new(new TestResolver(_ => Task.FromResult(s_handle)).ResolveAsync, null);
        cache.Dispose();
        cache.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken));
        Assert.Throws<ObjectDisposedException>(() => cache.TryGetCachedHandle(s_did, out _));
        cache.Invalidate(s_did);
    }

    [Fact]
    public async Task NullArgumentsThrow()
    {
        using DidHandleCache cache = new(new TestResolver(_ => Task.FromResult(s_handle)).ResolveAsync, null);

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await cache.ResolveHandleAsync(null!, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => cache.TryGetCachedHandle(null!, out _));
        Assert.Throws<ArgumentNullException>(() => cache.Invalidate(null!));
    }

    [Fact]
    public void OptionsHaveTheDocumentedDefaults()
    {
        DidHandleCacheOptions options = new();

        Assert.Equal(100_000, options.Size);
        Assert.Equal(TimeSpan.FromHours(1), options.Duration);
        Assert.Equal(TimeSpan.FromMinutes(1), options.FailedResolutionDuration);
        Assert.Equal(TimeSpan.FromSeconds(30), options.ResolutionTimeout);
        Assert.Same(TimeProvider.System, options.TimeProvider);
        Assert.Null(options.PlcDirectory);
        Assert.Null(options.HttpClient);
        Assert.Null(options.LoggerFactory);
        Assert.Null(options.MeterFactory);
    }

    public static TheoryData<Func<DidHandleCacheOptions>> InvalidOptions => new()
    {
        () => new DidHandleCacheOptions { Size = 0 },
        () => new DidHandleCacheOptions { Duration = TimeSpan.Zero },
        () => new DidHandleCacheOptions { Duration = TimeSpan.FromDays(100) },
        () => new DidHandleCacheOptions { FailedResolutionDuration = TimeSpan.FromSeconds(-1) },
        () => new DidHandleCacheOptions { FailedResolutionDuration = TimeSpan.FromDays(100) },
        () => new DidHandleCacheOptions { ResolutionTimeout = TimeSpan.Zero },
        () => new DidHandleCacheOptions { ResolutionTimeout = TimeSpan.FromDays(100) },
        () => new DidHandleCacheOptions { TimeProvider = null! },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void InvalidOptionsThrow(Func<DidHandleCacheOptions> create)
    {
        Assert.ThrowsAny<ArgumentException>(() => create());
    }

    private sealed class TestResolver(Func<CancellationToken, Task<Handle>> resolve)
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<Handle> ResolveAsync(Did did, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            LastCancellationToken = cancellationToken;
            return resolve(cancellationToken);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}

[ExcludeFromCodeCoverage]
internal sealed class RecordingDidHandleResolver : IDidHandleResolver
{
    public ConcurrentQueue<Did> Invalidated { get; } = new();

    public ValueTask<Handle> ResolveHandleAsync(Did did, CancellationToken cancellationToken = default) => ValueTask.FromResult(Handle.Invalid);

    public bool TryGetCachedHandle(Did did, [NotNullWhen(true)] out Handle? handle)
    {
        handle = null;
        return false;
    }

    public void Invalidate(Did did) => Invalidated.Enqueue(did);
}
