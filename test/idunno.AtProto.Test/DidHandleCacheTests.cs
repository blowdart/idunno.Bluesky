// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Reflection;

using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Time.Testing;

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
        FakeTimeProvider timeProvider = new();
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
    public async Task HandlesDoNotExpireWhileTheTimeProviderHasNotAdvanced()
    {
        FakeTimeProvider timeProvider = new();
        TestResolver resolver = new(_ => Task.FromResult(s_handle));
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions { Duration = TimeSpan.FromMilliseconds(50), TimeProvider = timeProvider });

        await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);

        Assert.True(cache.TryGetCachedHandle(s_did, out _));
        Assert.Equal(1, resolver.Calls);
    }

    [Fact]
    public async Task AnInvalidHandleIsCachedForTheFailedResolutionDuration()
    {
        FakeTimeProvider timeProvider = new();
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
        FakeTimeProvider timeProvider = new();
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
        FakeTimeProvider timeProvider = new();
        TestResolver resolver = new(async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return s_handle;
        });
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions
        {
            ResolutionTimeout = TimeSpan.FromMilliseconds(50),
            TimeProvider = timeProvider
        });

        ValueTask<Handle> lookup = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));

        Assert.Equal(Handle.Invalid, await lookup);
    }

    [Fact]
    public async Task AResolverWhichIgnoresCancellationIsBoundedByTheTimeout()
    {
        FakeTimeProvider timeProvider = new();
        TaskCompletionSource<Handle> neverCompletes = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestResolver resolver = new(_ => neverCompletes.Task);
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions
        {
            ResolutionTimeout = TimeSpan.FromMilliseconds(50),
            TimeProvider = timeProvider
        });

        ValueTask<Handle> lookup = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));

        Assert.Equal(Handle.Invalid, await lookup);
    }

    [Fact]
    public async Task DisposingCancelsAResolverWhichIgnoresCancellation()
    {
        TaskCompletionSource<Handle> neverCompletes = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestResolver resolver = new(_ => neverCompletes.Task);
        DidHandleCache cache = new(resolver.ResolveAsync, null);

        ValueTask<Handle> lookup = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);

        cache.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lookup.AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(nameof(DidHandleCacheMetrics.Hits), "idunno.atproto.didhandlecache.total.hits")]
    [InlineData(nameof(DidHandleCacheMetrics.Misses), "idunno.atproto.didhandlecache.total.misses")]
    [InlineData(nameof(DidHandleCacheMetrics.CoalescedLookups), "idunno.atproto.didhandlecache.total.coalesced_lookups")]
    [InlineData(nameof(DidHandleCacheMetrics.RejectedLookups), "idunno.atproto.didhandlecache.total.rejected_lookups")]
    [InlineData(nameof(DidHandleCacheMetrics.InvalidHandles), "idunno.atproto.didhandlecache.total.invalid_handles")]
    [InlineData(nameof(DidHandleCacheMetrics.Invalidations), "idunno.atproto.didhandlecache.total.invalidations")]
    public void CounterIsPublishedUnderItsExpectedName(string propertyName, string instrumentName)
    {
        // Instrument names are a contract with whatever is scraping them, so a rename should not pass silently.
        using RecordingMeterFactory meterFactory = new();
        DidHandleCacheMetrics metrics = new(meterFactory);
        using MeasurementRecorder recorder = new(meterFactory);

        Counter<long> counter = (Counter<long>)typeof(DidHandleCacheMetrics)
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(metrics)!;
        counter.Add(1);

        Assert.Equal(1, recorder.Total(instrumentName));
        Assert.Equal([DidHandleCacheMetrics.MeterName], meterFactory.CreatedMeterNames);
    }

    [Fact]
    public async Task LookupsAndInvalidationsEmitMetrics()
    {
        using RecordingMeterFactory meterFactory = new();
        using MeasurementRecorder recorder = new(meterFactory);
        TaskCompletionSource<Handle> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestResolver resolver = new(_ => completion.Task);
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions { MeterFactory = meterFactory });

        ValueTask<Handle> first = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        ValueTask<Handle> second = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        completion.SetResult(s_handle);
        await first;
        await second;
        await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);

        Assert.Equal(1, recorder.Total(nameof(DidHandleCacheMetrics.Misses)));
        Assert.Equal(1, recorder.Total(nameof(DidHandleCacheMetrics.CoalescedLookups)));
        Assert.Equal(1, recorder.Total(nameof(DidHandleCacheMetrics.Hits)));
        Assert.Equal(0, recorder.Total(nameof(DidHandleCacheMetrics.InvalidHandles)));
        Assert.Equal(0, recorder.Total(nameof(DidHandleCacheMetrics.Invalidations)));

        cache.Invalidate(s_did);

        Assert.Equal(1, recorder.Total(nameof(DidHandleCacheMetrics.Invalidations)));
    }

    [Fact]
    public async Task LookupDurationAndPendingLookupCountAreRecorded()
    {
        using RecordingMeterFactory meterFactory = new();
        using MeasurementRecorder recorder = new(meterFactory);
        TaskCompletionSource<Handle> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestResolver resolver = new(_ => completion.Task);
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions { MeterFactory = meterFactory });

        ValueTask<Handle> lookup = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);

        Assert.Equal(1, recorder.Total("idunno.atproto.didhandlecache.pending_lookups"));

        completion.SetResult(s_handle);
        Assert.Equal(s_handle, await lookup);

        Assert.Equal(0, recorder.Total("idunno.atproto.didhandlecache.pending_lookups"));
        Assert.Equal(1, recorder.MeasurementCount("idunno.atproto.didhandlecache.lookup.duration"));
    }

    [Fact]
    public async Task AnInvalidHandleEmitsAMetric()
    {
        using RecordingMeterFactory meterFactory = new();
        using MeasurementRecorder recorder = new(meterFactory);
        TestResolver resolver = new(_ => Task.FromResult(Handle.Invalid));
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions { MeterFactory = meterFactory });

        await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);

        Assert.Equal(1, recorder.Total(nameof(DidHandleCacheMetrics.InvalidHandles)));
        Assert.Equal(1, recorder.Total(nameof(DidHandleCacheMetrics.Misses)));
        Assert.Equal(1, recorder.Total(nameof(DidHandleCacheMetrics.Hits)));
    }

    [Fact]
    public async Task LookupsAreRejectedButNotCachedOnceSizeLookupsArePending()
    {
        using RecordingMeterFactory meterFactory = new();
        using MeasurementRecorder recorder = new(meterFactory);
        TaskCompletionSource<Handle> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestResolver resolver = new(_ => completion.Task);
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions { Size = 1, MeterFactory = meterFactory });

        ValueTask<Handle> first = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        ValueTask<Handle> shared = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        ValueTask<Handle> rejected = cache.ResolveHandleAsync(s_otherDid, TestContext.Current.CancellationToken);

        Assert.True(rejected.IsCompleted);
        Assert.Equal(Handle.Invalid, await rejected);
        Assert.Equal(1, resolver.Calls);
        Assert.Equal(1, recorder.Total(nameof(DidHandleCacheMetrics.RejectedLookups)));
        Assert.Equal(1, recorder.Total(nameof(DidHandleCacheMetrics.CoalescedLookups)));
        Assert.False(cache.TryGetCachedHandle(s_otherDid, out _));

        completion.SetResult(s_handle);

        Assert.Equal(s_handle, await first);
        Assert.Equal(s_handle, await shared);
        Assert.Equal(s_handle, await cache.ResolveHandleAsync(s_otherDid, TestContext.Current.CancellationToken));
        Assert.Equal(2, resolver.Calls);
    }

    [Fact]
    public async Task LookupsOrphanedByAnInvalidationStillCountAsPending()
    {
        TaskCompletionSource<Handle> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestResolver resolver = new(_ => completion.Task);
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions { Size = 1 });

        ValueTask<Handle> orphaned = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        cache.Invalidate(s_did);

        Assert.Equal(Handle.Invalid, await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken));
        Assert.Equal(Handle.Invalid, await cache.ResolveHandleAsync(s_otherDid, TestContext.Current.CancellationToken));
        Assert.Equal(1, resolver.Calls);

        completion.SetResult(s_handle);

        Assert.Equal(s_handle, await orphaned);
        Assert.False(cache.TryGetCachedHandle(s_did, out _));
    }

    [Fact]
    public async Task AtMostMaximumConcurrentResolutionsRunAtOnce()
    {
        TaskCompletionSource<Handle> firstCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Handle otherHandle = new("other.example.com");
        int calls = 0;
        TestResolver resolver = new(_ => Interlocked.Increment(ref calls) == 1 ? firstCompletion.Task : Task.FromResult(otherHandle));
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions { MaximumConcurrentResolutions = 1 });

        ValueTask<Handle> first = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        ValueTask<Handle> waiting = cache.ResolveHandleAsync(s_otherDid, TestContext.Current.CancellationToken);

        Assert.Equal(1, resolver.Calls);
        Assert.False(waiting.IsCompleted);

        firstCompletion.SetResult(s_handle);

        Assert.Equal(s_handle, await first);
        Assert.Equal(otherHandle, await waiting);
        Assert.Equal(2, resolver.Calls);
    }

    [Fact]
    public async Task ATimedOutResolverWhichIgnoresCancellationHoldsItsSlotUntilItFinishes()
    {
        using RecordingMeterFactory meterFactory = new();
        using MeasurementRecorder recorder = new(meterFactory);
        FakeTimeProvider timeProvider = new();
        TaskCompletionSource<Handle> ignoresCancellation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        TestResolver resolver = new(_ => Interlocked.Increment(ref calls) == 1 ? ignoresCancellation.Task : Task.FromResult(s_handle));
        Did thirdDid = new("did:web:third.example.com");
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions
        {
            MaximumConcurrentResolutions = 1,
            ResolutionTimeout = TimeSpan.FromMilliseconds(50),
            MeterFactory = meterFactory,
            TimeProvider = timeProvider
        });

        ValueTask<Handle> timedOut = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        Assert.Equal(Handle.Invalid, await timedOut);

        ValueTask<Handle> blocked = cache.ResolveHandleAsync(s_otherDid, TestContext.Current.CancellationToken);
        Assert.False(blocked.IsCompleted);
        Assert.Equal(1, resolver.Calls);

        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        Assert.Equal(Handle.Invalid, await blocked);
        Assert.Equal(1, resolver.Calls);
        Assert.Equal(1, recorder.Total(nameof(DidHandleCacheMetrics.RejectedLookups)));

        ignoresCancellation.SetResult(s_handle);

        Assert.Equal(s_handle, await cache.ResolveHandleAsync(thirdDid, TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        Assert.Equal(2, resolver.Calls);
    }

    [Fact]
    public async Task AResolverWhichThrowsSynchronouslyReleasesItsSlot()
    {
        int calls = 0;
        TestResolver resolver = new(_ => Interlocked.Increment(ref calls) == 1 ? throw new InvalidOperationException("Resolver failed.") : Task.FromResult(s_handle));
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions { MaximumConcurrentResolutions = 1 });

        Assert.Equal(Handle.Invalid, await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken));
        Assert.Equal(s_handle, await cache.ResolveHandleAsync(s_otherDid, TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ALookupWhichTimesOutWaitingToStartIsRejectedButNotCached()
    {
        using RecordingMeterFactory meterFactory = new();
        using MeasurementRecorder recorder = new(meterFactory);
        FakeTimeProvider timeProvider = new();
        TaskCompletionSource<Handle> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestResolver resolver = new(_ => completion.Task);
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions
        {
            MaximumConcurrentResolutions = 1,
            ResolutionTimeout = TimeSpan.FromMilliseconds(50),
            MeterFactory = meterFactory,
            TimeProvider = timeProvider
        });

        ValueTask<Handle> first = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);
        ValueTask<Handle> waiting = cache.ResolveHandleAsync(s_otherDid, TestContext.Current.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMilliseconds(50));

        Assert.Equal(Handle.Invalid, await first);
        Assert.Equal(Handle.Invalid, await waiting);
        Assert.Equal(1, resolver.Calls);
        Assert.Equal(1, recorder.Total(nameof(DidHandleCacheMetrics.RejectedLookups)));
        Assert.False(cache.TryGetCachedHandle(s_otherDid, out _));

        completion.SetResult(s_handle);

        Assert.Equal(s_handle, await cache.ResolveHandleAsync(s_otherDid, TestContext.Current.CancellationToken));
        Assert.Equal(2, resolver.Calls);
    }

    [Fact]
    public async Task InvalidatingOtherDidsDoesNotStopAHandleBeingCached()
    {
        TaskCompletionSource<Handle> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestResolver resolver = new(_ => completion.Task);
        using DidHandleCache cache = new(resolver.ResolveAsync, null);

        ValueTask<Handle> lookup = cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken);

        for (int i = 0; i < 4096; i++)
        {
            cache.Invalidate(new Did($"did:web:invalidated{i}.example.com"));
        }

        completion.SetResult(s_handle);

        Assert.Equal(s_handle, await lookup);
        Assert.True(cache.TryGetCachedHandle(s_did, out Handle? cached));
        Assert.Equal(s_handle, cached);
    }

    [Fact]
    public async Task ALookupCompletesWhenLoggingThrows()
    {
        TestResolver resolver = new(_ => Task.FromResult(Handle.Invalid));
        using DidHandleCache cache = new(resolver.ResolveAsync, new DidHandleCacheOptions { LoggerFactory = new ThrowingLoggerFactory() });

        Assert.Equal(Handle.Invalid, await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        Assert.False(cache.TryGetCachedHandle(s_did, out _));
        Assert.Equal(Handle.Invalid, await cache.ResolveHandleAsync(s_did, TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        Assert.Equal(2, resolver.Calls);
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
        Assert.Equal(32, options.MaximumConcurrentResolutions);
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
        () => new DidHandleCacheOptions { MaximumConcurrentResolutions = 0 },
        () => new DidHandleCacheOptions { MaximumConcurrentResolutions = -1 },
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

    private sealed class ThrowingLoggerFactory : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new ThrowingLogger();

        public void Dispose()
        {
        }
    }

    private sealed class ThrowingLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("Logging failed.");
    }

    // Records the counters published by meters created from a specific meter factory, keyed by both instrument name and the
    // name of the DidHandleCacheMetrics property which created them.
    private sealed class MeasurementRecorder : IDisposable
    {
        private static readonly Dictionary<string, string> s_propertyNames = new()
        {
            ["idunno.atproto.didhandlecache.total.hits"] = nameof(DidHandleCacheMetrics.Hits),
            ["idunno.atproto.didhandlecache.total.misses"] = nameof(DidHandleCacheMetrics.Misses),
            ["idunno.atproto.didhandlecache.total.coalesced_lookups"] = nameof(DidHandleCacheMetrics.CoalescedLookups),
            ["idunno.atproto.didhandlecache.total.rejected_lookups"] = nameof(DidHandleCacheMetrics.RejectedLookups),
            ["idunno.atproto.didhandlecache.total.invalid_handles"] = nameof(DidHandleCacheMetrics.InvalidHandles),
            ["idunno.atproto.didhandlecache.total.invalidations"] = nameof(DidHandleCacheMetrics.Invalidations),
        };

        private readonly ConcurrentDictionary<string, long> _totals = new();
        private readonly ConcurrentDictionary<string, int> _measurementCounts = new();
        private readonly MeterListener _listener = new();

        public MeasurementRecorder(IMeterFactory meterFactory)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Scope == meterFactory)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
            {
                _totals.AddOrUpdate(instrument.Name, measurement, (_, total) => total + measurement);

                if (s_propertyNames.TryGetValue(instrument.Name, out string? propertyName))
                {
                    _totals.AddOrUpdate(propertyName, measurement, (_, total) => total + measurement);
                }
            });

            _listener.SetMeasurementEventCallback<double>((instrument, _, _, _) =>
            {
                _measurementCounts.AddOrUpdate(instrument.Name, 1, (_, count) => count + 1);
            });

            _listener.Start();
        }

        public long Total(string name) => _totals.GetValueOrDefault(name);

        public int MeasurementCount(string name) => _measurementCounts.GetValueOrDefault(name);

        public void Dispose() => _listener.Dispose();
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
