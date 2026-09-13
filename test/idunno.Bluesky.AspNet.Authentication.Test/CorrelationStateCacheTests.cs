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
    public async Task AddThenGetRoundTripsTheLoginState()
    {
        ICorrelationStateCache cache = CreateCache();
        Guid correlationId = Guid.NewGuid();

        OAuthLoginState state = TestData.LoginState(correlationId, codeVerifier: "the-code-verifier");
        await cache.AddOAuthLoginState(correlationId, state);

        OAuthLoginState? retrieved = await cache.GetOAuthLoginState(correlationId);

        Assert.NotNull(retrieved);
        Assert.Equal(state, retrieved);
        Assert.Equal("the-code-verifier", retrieved.CodeVerifier);
    }

    [Fact]
    public async Task AddThrowsWhenTheStateIsNull()
    {
        ICorrelationStateCache cache = CreateCache();

        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.AddOAuthLoginState(Guid.NewGuid(), null!));
    }

    [Fact]
    public async Task GetReturnsNullForACorrelationIdWhichWasNeverStored()
    {
        ICorrelationStateCache cache = CreateCache();

        Assert.Null(await cache.GetOAuthLoginState(Guid.NewGuid()));
    }

    [Fact]
    public async Task TakeReturnsTheStateAndConsumesIt()
    {
        ICorrelationStateCache cache = CreateCache();
        Guid correlationId = Guid.NewGuid();

        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId));

        Assert.NotNull(await cache.TakeOAuthLoginState(correlationId));

        // Login state is single use, so a replayed callback must not be given the same state again.
        Assert.Null(await cache.TakeOAuthLoginState(correlationId));
        Assert.Null(await cache.GetOAuthLoginState(correlationId));
    }

    [Fact]
    public async Task TakeReturnsNullForACorrelationIdWhichWasNeverStored()
    {
        ICorrelationStateCache cache = CreateCache();

        Assert.Null(await cache.TakeOAuthLoginState(Guid.NewGuid()));
    }

    [Fact]
    public async Task RemoveCorrelationStateRemovesTheState()
    {
        ICorrelationStateCache cache = CreateCache();
        Guid correlationId = Guid.NewGuid();

        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId));
        await cache.RemoveCorrelationState(correlationId);

        Assert.Null(await cache.GetOAuthLoginState(correlationId));
    }

    [Fact]
    public async Task TakeConsumesAnEntryEvenWhenItsStateCannotBeRead()
    {
        ICorrelationStateCache cache = CreateCache();
        Guid correlationId = Guid.NewGuid();

        CorrelationStateCacheEvents readable =
            new DataProtectingCorrelationStateCacheEvents(new EphemeralDataProtectionProvider());

        cache.Events = readable;
        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId));
        Assert.NotNull(await cache.GetOAuthLoginState(correlationId));

        // A different provider means a different key ring, which is what a rolled or lost key looks like to the cache.
        cache.Events = new DataProtectingCorrelationStateCacheEvents(new EphemeralDataProtectionProvider());
        Assert.Null(await cache.TakeOAuthLoginState(correlationId));

        // An entry which could not be read must still have been consumed, so it cannot be presented a second time,
        // even by a caller whose key ring could read it a moment ago.
        cache.Events = readable;
        Assert.Null(await cache.GetOAuthLoginState(correlationId));
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

        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId));

        cache.Events = new CorrelationStateCacheEvents();

        Assert.Null(await cache.GetOAuthLoginState(correlationId));
        Assert.Null(await cache.TakeOAuthLoginState(correlationId));
    }

    [Fact]
    public async Task StateWhichCannotBeUnprotectedFailsTheLoginRatherThanThrowing()
    {
        ICorrelationStateCache cache = CreateCache();
        Guid correlationId = Guid.NewGuid();

        cache.Events = new DataProtectingCorrelationStateCacheEvents(new EphemeralDataProtectionProvider());
        await cache.AddOAuthLoginState(correlationId, TestData.LoginState(correlationId));

        // A different provider means a different key ring, which is what a rolled or lost key looks like to the cache.
        cache.Events = new DataProtectingCorrelationStateCacheEvents(new EphemeralDataProtectionProvider());

        Assert.Null(await cache.GetOAuthLoginState(correlationId));
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
        await cache.AddOAuthLoginState(correlationId, state);

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
        await cache.GetOAuthLoginState(correlationId);

        cache.Events = protectingEvents;
        OAuthLoginState? retrieved = await cache.GetOAuthLoginState(correlationId);

        Assert.NotNull(retrieved);
        Assert.Equal(state, retrieved);

        // The user's delegates run inside the protection, so both legs see the same plaintext.
        Assert.Equal(seenWhenStoring, seenWhenRetrieved);
        Assert.Contains("a-very-secret-verifier", seenWhenStoring, StringComparison.Ordinal);

        // What was actually stored is protected, so the code verifier is not sitting in the cache in the clear.
        Assert.NotNull(persisted);
        Assert.DoesNotContain("a-very-secret-verifier", persisted, StringComparison.Ordinal);
    }
}
