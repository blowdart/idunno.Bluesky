// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

using Microsoft.AspNetCore.DataProtection;

namespace idunno.Bluesky.AspNet.Authentication.Test;

/// <summary>
/// The behaviour every <see cref="ICorrelationStateCache"/> implementation in the package is expected to share.
/// </summary>
public abstract class CorrelationStateCacheTests
{
    protected abstract ICorrelationStateCache CreateCache();

    [Fact]
    public async Task AddThenPeekRoundTripsTheLoginState()
    {
        ICorrelationStateCache cache = CreateCache();
        Guid correlationId = Guid.NewGuid();

        OAuthLoginState state = TestData.LoginState(correlationId, codeVerifier: "the-code-verifier");
        await cache.AddOAuthLoginState(correlationId, state, TestContext.Current.CancellationToken);

        OAuthLoginState? retrieved = await cache.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal(state, retrieved);
        Assert.Equal("the-code-verifier", retrieved.CodeVerifier);
    }

    [Fact]
    public async Task AddThrowsWhenTheStateIsNull()
    {
        ICorrelationStateCache cache = CreateCache();

        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.AddOAuthLoginState(Guid.NewGuid(), null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PeekReturnsNullForACorrelationIdWhichWasNeverStored()
    {
        ICorrelationStateCache cache = CreateCache();

        Assert.Null(await cache.PeekOAuthLoginState(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TakeReturnsTheStateAndConsumesIt()
    {
        ICorrelationStateCache cache = CreateCache();
        Guid correlationId = Guid.NewGuid();

        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId), TestContext.Current.CancellationToken);

        Assert.NotNull(await cache.TakeOAuthLoginState(correlationId, TestContext.Current.CancellationToken));

        // Login state is single use, so a replayed callback must not be given the same state again.
        Assert.Null(await cache.TakeOAuthLoginState(correlationId, TestContext.Current.CancellationToken));
        Assert.Null(await cache.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TakeReturnsNullForACorrelationIdWhichWasNeverStored()
    {
        ICorrelationStateCache cache = CreateCache();

        Assert.Null(await cache.TakeOAuthLoginState(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveCorrelationStateRemovesTheState()
    {
        ICorrelationStateCache cache = CreateCache();
        Guid correlationId = Guid.NewGuid();

        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId), TestContext.Current.CancellationToken);
        await cache.RemoveCorrelationState(correlationId, TestContext.Current.CancellationToken);

        Assert.Null(await cache.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TakeConsumesAnEntryEvenWhenItsStateCannotBeRead()
    {
        ICorrelationStateCache cache = CreateCache();
        Guid correlationId = Guid.NewGuid();

        CorrelationStateCacheEvents readable =
            new DataProtectingCorrelationStateCacheEvents(new EphemeralDataProtectionProvider());

        cache.Events = readable;
        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId), TestContext.Current.CancellationToken);
        Assert.NotNull(await cache.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken));

        // A different provider means a different key ring, which is what a rolled or lost key looks like to the cache.
        cache.Events = new DataProtectingCorrelationStateCacheEvents(new EphemeralDataProtectionProvider());
        Assert.Null(await cache.TakeOAuthLoginState(correlationId, TestContext.Current.CancellationToken));

        // An entry which could not be read must still have been consumed, so it cannot be presented a second time,
        // even by a caller whose key ring could read it a moment ago.
        cache.Events = readable;
        Assert.Null(await cache.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StateWhichIsNotValidJsonFailsTheLoginRatherThanThrowing()
    {
        ICorrelationStateCache cache = CreateCache();
        Guid correlationId = Guid.NewGuid();

        cache.Events = new CorrelationStateCacheEvents
        {
            OnStoring = context =>
            {
                context.ReplaceState("{ not json");
                return Task.CompletedTask;
            }
        };

        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId), TestContext.Current.CancellationToken);

        cache.Events = new CorrelationStateCacheEvents();

        Assert.Null(await cache.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken));
        Assert.Null(await cache.TakeOAuthLoginState(correlationId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StateWhichCannotBeUnprotectedFailsTheLoginRatherThanThrowing()
    {
        ICorrelationStateCache cache = CreateCache();
        Guid correlationId = Guid.NewGuid();

        cache.Events = new DataProtectingCorrelationStateCacheEvents(new EphemeralDataProtectionProvider());
        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId), TestContext.Current.CancellationToken);

        // A different provider means a different key ring, which is what a rolled or lost key looks like to the cache.
        cache.Events = new DataProtectingCorrelationStateCacheEvents(new EphemeralDataProtectionProvider());

        Assert.Null(await cache.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StoringEventSeesTheStateBeforeItIsProtectedAndRetrievalSeesItAfterItIsUnprotected()
    {
        ICorrelationStateCache cache = CreateCache();
        Guid correlationId = Guid.NewGuid();

        string? seenWhenStoring = null;
        string? seenWhenRetrieved = null;
        string? persisted = null;

        cache.Events = new DataProtectingCorrelationStateCacheEvents(new EphemeralDataProtectionProvider())
        {
            OnStoring = context =>
            {
                seenWhenStoring = context.State;
                return Task.CompletedTask;
            },
            OnRetrieved = context =>
            {
                seenWhenRetrieved = context.State;
                return Task.CompletedTask;
            }
        };

        OAuthLoginState state = TestData.LoginState(correlationId, codeVerifier: "a-very-secret-verifier");
        await cache.AddOAuthLoginState(correlationId, state, TestContext.Current.CancellationToken);

        // Capture what actually went into the cache by reading it back without the protecting events in place.
        CorrelationStateCacheEvents protectingEvents = cache.Events;
        cache.Events = new CorrelationStateCacheEvents
        {
            OnRetrieved = context =>
            {
                persisted = context.State;
                return Task.CompletedTask;
            }
        };
        await cache.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken);

        cache.Events = protectingEvents;
        OAuthLoginState? retrieved = await cache.PeekOAuthLoginState(correlationId, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal(state, retrieved);

        // The user's delegates run inside the protection, so both legs see the same plaintext.
        Assert.Equal(seenWhenStoring, seenWhenRetrieved);
        Assert.Contains("a-very-secret-verifier", seenWhenStoring, StringComparison.Ordinal);

        // What was actually stored is protected, so the code verifier is not sitting in the cache in the clear.
        Assert.NotNull(persisted);
        Assert.DoesNotContain("a-very-secret-verifier", persisted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryOperationHonoursACancelledToken()
    {
        // A cache backed by a database does its work over a connection, so a request which has gone away should not
        // leave the operation running. The in process caches have nothing to cancel, but they are held to the same
        // contract so a caller can rely on it whichever store is registered.
        ICorrelationStateCache cache = CreateCache();
        Guid correlationId = Guid.NewGuid();

        using CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();
        CancellationToken cancelled = cancellationTokenSource.Token;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId), cancelled));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.PeekOAuthLoginState(correlationId, cancelled));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.TakeOAuthLoginState(correlationId, cancelled));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cache.RemoveCorrelationState(correlationId, cancelled));
    }
}
