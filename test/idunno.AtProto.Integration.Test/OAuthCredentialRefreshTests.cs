// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.IdentityModel.Tokens;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class OAuthCredentialRefreshTests
{
    private const string DomainName = "oauth.test.internal";
    private const string ServerDid = $"did:web:{DomainName}";
    private const string Authority = $"https://{DomainName}/";
    private const string AccountDid = "did:plc:oauthaccount";
    private const string OtherDid = "did:plc:someoneelse";

    /// <summary>
    /// A public signing key for the test authorization server's discovery document. Duende will not load a discovery
    /// document which does not offer a key set.
    /// </summary>
    private static readonly string s_signingKeyJson = CreatePublicSigningKeyJson();

    private static string CreatePublicSigningKeyJson()
    {
        using RSA rsa = RSA.Create(2048);

        RSAParameters parameters = rsa.ExportParameters(false);

        string modulus = Base64UrlEncoder.Encode(parameters.Modulus!);
        string exponent = Base64UrlEncoder.Encode(parameters.Exponent!);

        return $$"""{"kty":"RSA","use":"sig","alg":"RS256","kid":"test","n":"{{modulus}}","e":"{{exponent}}"}""";
    }

    [Fact]
    public async Task ARefreshWhichIssuesATokenForTheSameAccountSucceeds()
    {
        OAuthTestServer server = new();
        using AtProtoAgent agent = CreateAgent(server);

        await Login(agent);

        Assert.True(await agent.RefreshCredentials(TestContext.Current.CancellationToken));

        Assert.NotNull(agent.Credentials);
        Assert.Equal(new Did(AccountDid), agent.Credentials.Did);
        Assert.Equal(server.LastIssuedRefreshToken, agent.Credentials.RefreshToken);
    }

    [Fact]
    public async Task ANonceUpdateDuringRefreshDoesNotDiscardTheRefreshedCredentials()
    {
        OAuthTestServer server = new() { GateRefresh = true };
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);
        Task<bool> refresh = agent.RefreshCredentials(TestContext.Current.CancellationToken);

        await server.RefreshEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

        originalCredentials.DPoPNonce = "rotatedNonce";
        await agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken);

        server.ReleaseRefresh.TrySetResult();

        Assert.True(await refresh);
        DPoPAccessCredentials refreshedCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);
        Assert.NotSame(originalCredentials, refreshedCredentials);
        Assert.Equal("rotatedNonce", refreshedCredentials.DPoPNonce);
    }

    [Fact]
    public async Task AStaleNonceUpdateAfterRefreshDoesNotReplaceTheRefreshedCredentials()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);
        Assert.True(await agent.RefreshCredentials(TestContext.Current.CancellationToken));

        DPoPAccessCredentials refreshedCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);
        int credentialsUpdatedCount = 0;
        agent.CredentialsUpdated += (_, _) => credentialsUpdatedCount++;

        originalCredentials.DPoPNonce = "staleNonce";
        await agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken);

        Assert.Same(refreshedCredentials, agent.Credentials);
        Assert.Equal(0, credentialsUpdatedCount);
    }

    [Fact]
    public async Task ASuspendedNonceNotificationCannotPersistCredentialsSupersededByARefresh()
    {
        OAuthTestServer server = new() { GateRefresh = true };
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        TaskCompletionSource releaseFirstNotification = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource firstNotificationEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AccessCredentials? lastPersistedCredentials = null;
        int notificationCount = 0;

        agent.CredentialsUpdatedAsync = async (e, _) =>
        {
            if (Interlocked.Increment(ref notificationCount) == 1)
            {
                firstNotificationEntered.TrySetResult();
                await releaseFirstNotification.Task;
            }

            lastPersistedCredentials = e.AccessCredentials;
        };

        originalCredentials.DPoPNonce = "rotatedNonce";
        Task notification = agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken);

        await firstNotificationEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

        TaskCompletionSource refreshNotificationQueued = new(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.CredentialNotificationQueueing = queued =>
        {
            if (!ReferenceEquals(queued, originalCredentials))
            {
                refreshNotificationQueued.TrySetResult();
            }
        };

        Task<bool> refresh = agent.RefreshCredentials(TestContext.Current.CancellationToken);

        await server.RefreshEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        server.ReleaseRefresh.TrySetResult();

        // Wait until the refresh has published its credentials and queued their notification behind the suspended one,
        // so that the notification for the superseded credentials is the one which would otherwise complete last.
        await refreshNotificationQueued.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.NotSame(originalCredentials, agent.Credentials);

        releaseFirstNotification.TrySetResult();

        await notification;
        Assert.True(await refresh);

        // The refreshed credentials must be the last ones a handler was given, otherwise a handler which persists
        // them would leave a spent refresh token in durable storage.
        Assert.Equal(2, notificationCount);
        Assert.Same(agent.Credentials, lastPersistedCredentials);
    }

    [Fact]
    public async Task ACancelledRefreshStillNotifiesCredentialsItHasAlreadyCommitted()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        TaskCompletionSource releaseFirstNotification = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource firstNotificationEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AccessCredentials? lastPersistedCredentials = null;
        int notificationCount = 0;

        agent.CredentialsUpdatedAsync = async (e, _) =>
        {
            if (Interlocked.Increment(ref notificationCount) == 1)
            {
                firstNotificationEntered.TrySetResult();
                await releaseFirstNotification.Task;
            }

            lastPersistedCredentials = e.AccessCredentials;
        };

        originalCredentials.DPoPNonce = "rotatedNonce";
        Task notification = agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken);

        await firstNotificationEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

        using CancellationTokenSource refreshCancellation = new();
        Task<bool> refresh = agent.RefreshCredentials(refreshCancellation.Token);

        // Wait until the refresh has committed its credentials, then cancel the caller. The notification for
        // credentials the agent is already using must still be raised, otherwise the suspended handler would be the
        // last to write and would persist the refresh token the server has spent.
        while (ReferenceEquals(agent.Credentials, originalCredentials))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        await refreshCancellation.CancelAsync();

        releaseFirstNotification.TrySetResult();

        await notification;
        Assert.True(await refresh);

        Assert.Equal(2, notificationCount);
        Assert.Same(agent.Credentials, lastPersistedCredentials);
    }

    [Fact]
    public async Task AHandlerWhichRefreshesBeforePersistingIsGivenTheRefreshedCredentialsLast()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        List<AccessCredentials> persisted = [];
        bool refreshed = false;

        agent.CredentialsUpdatedAsync = async (e, cancellationToken) =>
        {
            // A handler which calls back into the agent before it persists what it was given. The refresh replaces the
            // credentials this handler is holding, so its notification has to be the last one raised.
            if (!refreshed)
            {
                refreshed = true;
                Assert.True(await agent.RefreshCredentials(cancellationToken));
            }

            persisted.Add(e.AccessCredentials);
        };

        originalCredentials.DPoPNonce = "rotatedNonce";
        await agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken);

        Assert.Equal(2, persisted.Count);
        Assert.Same(originalCredentials, persisted[0]);
        Assert.Same(agent.Credentials, persisted[1]);
    }

    [Fact]
    public async Task AHandlerWhichRefreshesAndThenThrowsStillHasTheRefreshedCredentialsNotified()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        List<AccessCredentials> persisted = [];
        bool refreshed = false;

        agent.CredentialsUpdatedAsync = async (e, cancellationToken) =>
        {
            // A handler which spends the refresh token by calling back into the agent, and then fails. The credentials
            // the refresh issued must still reach a handler, otherwise nothing can persist them.
            if (!refreshed)
            {
                refreshed = true;
                Assert.True(await agent.RefreshCredentials(cancellationToken));

                throw new InvalidOperationException("Persistence failed.");
            }

            persisted.Add(e.AccessCredentials);
        };

        originalCredentials.DPoPNonce = "rotatedNonce";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken));

        AccessCredentials refreshedCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        Assert.Same(refreshedCredentials, Assert.Single(persisted));
    }

    [Fact]
    public async Task ARefreshAHandlerStartedWithoutAwaitingItStillNotifiesTheCredentialsItCommits()
    {
        OAuthTestServer server = new() { GateRefresh = true };
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        List<AccessCredentials> persisted = [];
        Task<bool>? refresh = null;

        agent.CredentialsUpdatedAsync = (e, _) =>
        {
            // A handler which starts a refresh and returns without awaiting it. The refresh runs on, and completes,
            // after the notification it was started from has finished, so the credentials it commits have to be
            // notified rather than handed to a notification which is no longer running.
            refresh ??= agent.RefreshCredentials(CancellationToken.None);

            persisted.Add(e.AccessCredentials);

            return Task.CompletedTask;
        };

        originalCredentials.DPoPNonce = "rotatedNonce";
        await agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken);

        Assert.NotNull(refresh);

        await server.RefreshEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        server.ReleaseRefresh.TrySetResult();

        Assert.True(await refresh);

        Assert.Equal(2, persisted.Count);
        Assert.Same(originalCredentials, persisted[0]);
        Assert.Same(agent.Credentials, persisted[1]);
    }

    [Fact]
    public async Task ALogoutDoesNotRaiseUnauthenticatedUntilASuspendedCredentialsHandlerHasFinished()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        TaskCompletionSource handlerEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseHandler = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> order = [];

        agent.CredentialsUpdatedAsync = async (_, _) =>
        {
            handlerEntered.TrySetResult();
            await releaseHandler.Task;

            lock (order)
            {
                order.Add("persisted");
            }
        };

        agent.Unauthenticated += (_, _) =>
        {
            lock (order)
            {
                order.Add("unauthenticated");
            }
        };

        originalCredentials.DPoPNonce = "rotatedNonce";
        Task notification = agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken);

        await handlerEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

        // The handler is held until the logout has actually cleared the credentials, which is the point an unordered
        // Unauthenticated would be raised from, so the two are not merely racing.
        Task releaseWhenSessionEnded = Task.Run(
            async () =>
            {
                while (agent.Credentials is not null)
                {
                    await Task.Delay(1, TestContext.Current.CancellationToken);
                }

                releaseHandler.TrySetResult();
            },
            TestContext.Current.CancellationToken);

        Task logout = agent.Logout(TestContext.Current.CancellationToken);

        await notification;
        await logout;
        await releaseWhenSessionEnded;

        // A subscriber which discards its stored credentials when the session ends can only discard the write the
        // suspended handler made if the session ending reaches it afterwards.
        Assert.Equal(["persisted", "unauthenticated"], order);
        Assert.Null(agent.Credentials);
    }

    [Fact]
    public async Task AHandlerWhichLogsOutDoesNotDeadlockTheAgent()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        bool unauthenticated = false;

        agent.Unauthenticated += (_, _) => unauthenticated = true;

        agent.CredentialsUpdatedAsync = async (_, cancellationToken) =>
        {
            // Logout raised from inside the notification it would otherwise be ordered behind. There is nothing left
            // to wait for, so it has to be raised inline rather than waiting on a queue the caller is holding.
            await agent.Logout(cancellationToken);
        };

        originalCredentials.DPoPNonce = "rotatedNonce";

        await agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.True(unauthenticated);
        Assert.Null(agent.Credentials);
    }

    [Fact]
    public async Task AHandlerWhichAwaitsALogoutPersistsBeforeUnauthenticatedIsRaised()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        List<string> order = [];

        agent.Unauthenticated += (_, _) => order.Add("unauthenticated");

        agent.CredentialsUpdatedAsync = async (_, cancellationToken) =>
        {
            // A handler which ends the session and then persists what it was given. The session ending has to reach a
            // subscriber after that write, otherwise nothing tells it to discard the ended session.
            await agent.Logout(cancellationToken);

            order.Add("persisted");
        };

        originalCredentials.DPoPNonce = "rotatedNonce";

        await agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(["persisted", "unauthenticated"], order);
        Assert.Null(agent.Credentials);
    }

    [Fact]
    public async Task ANotificationForSupersededCredentialsDoesNotDisplaceADeferredOneForCurrentCredentials()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        List<AccessCredentials> persisted = [];
        bool refreshed = false;

        agent.CredentialsUpdatedAsync = async (e, cancellationToken) =>
        {
            if (!refreshed)
            {
                refreshed = true;

                // The refresh publishes and defers its credentials, which the drain has to raise. The notification
                // raised afterwards carries the credentials that refresh replaced, so it must not displace them.
                Assert.True(await agent.RefreshCredentials(cancellationToken));

                await agent.RaiseCredentialsUpdated(originalCredentials, credentialsCommitted: true, cancellationToken);
            }

            persisted.Add(e.AccessCredentials);
        };

        originalCredentials.DPoPNonce = "rotatedNonce";
        await agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken);

        // The credentials the refresh issued have to be the last ones a handler was given, otherwise durable storage
        // keeps a spent refresh token.
        Assert.Equal(2, persisted.Count);
        Assert.Same(originalCredentials, persisted[0]);
        Assert.Same(agent.Credentials, persisted[1]);
    }

    [Fact]
    public async Task ARefreshWhichIssuesATokenForADifferentAccountIsRejected()
    {
        OAuthTestServer server = new() { IssuedDid = new Did(OtherDid) };
        using AtProtoAgent agent = CreateAgent(server);

        await Login(agent);

        AccessCredentials credentialsBeforeRefresh = agent.Credentials!;

        await Assert.ThrowsAsync<SecurityTokenValidationException>(
            () => agent.RefreshCredentials(TestContext.Current.CancellationToken));

        // The credentials the agent presents must still be the ones it was authenticated with, rather than the ones
        // issued for another actor.
        Assert.Same(credentialsBeforeRefresh, agent.Credentials);
        Assert.Equal(new Did(AccountDid), agent.Credentials!.Did);

        bool unauthenticatedEventRaised = false;
        agent.Unauthenticated += (_, _) => unauthenticatedEventRaised = true;

        Assert.False(await agent.RefreshCredentials(TestContext.Current.CancellationToken));
        Assert.Null(agent.Credentials);
        Assert.True(unauthenticatedEventRaised);
    }

    [Fact]
    public async Task ARefreshOfASuppliedCredentialWhichIssuesATokenForADifferentAccountIsRejected()
    {
        OAuthTestServer server = new() { IssuedDid = new Did(OtherDid) };
        using AtProtoAgent agent = CreateAgent(server);

        DPoPAccessCredentials credentials = CreateCredentials();

        await Assert.ThrowsAsync<SecurityTokenValidationException>(
            () => agent.RefreshCredentials(credentials, TestContext.Current.CancellationToken));

        Assert.Null(agent.Credentials);
    }

    [Fact]
    public async Task ALoginWhichReplacesASessionRaisesUnauthenticatedForItBeforeAuthenticatedForTheNewOne()
    {
        OAuthTestServer server = new();
        using AtProtoAgent agent = CreateAgent(server);

        await Login(agent);

        List<string> events = [];

        agent.Unauthenticated += (_, _) => events.Add("unauthenticated");
        agent.Authenticated += (_, _) => events.Add("authenticated");

        await Login(agent);

        Assert.Equal(["unauthenticated", "authenticated"], events);
    }

    [Fact]
    public async Task ALoginDoesNotRaiseAuthenticatedUntilASuspendedCredentialsHandlerHasFinished()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        TaskCompletionSource notificationEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseNotification = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> events = [];

        agent.CredentialsUpdatedAsync = async (_, _) =>
        {
            notificationEntered.TrySetResult();

            // Held until the login has published its credentials, which is the instant an unordered Authenticated
            // would be raised, so that the event has every opportunity to overtake this handler.
            await releaseNotification.Task;

            lock (events)
            {
                events.Add("persisted");
            }
        };

        agent.Authenticated += (_, _) =>
        {
            lock (events)
            {
                events.Add("authenticated");
            }
        };

        originalCredentials.DPoPNonce = "rotatedNonce";
        Task notification = agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken);

        await notificationEntered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Cleared directly, so that the login has no session to end and the ordering of Authenticated is the only
        // thing keeping it behind the handler which is still running.
        agent.Credentials = null;

        Task<bool> login = agent.Login(CreateCredentials(), TestContext.Current.CancellationToken);

        while (agent.Credentials is null)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        releaseNotification.TrySetResult();

        Assert.True(await login.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        await notification.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        lock (events)
        {
            Assert.Equal(["persisted", "authenticated"], events);
        }
    }

    [Fact]
    public async Task AFailedLogoutStillRaisesUnauthenticated()
    {
        OAuthTestServer server = new() { RevocationSucceeds = false };
        using AtProtoAgent agent = CreateAgent(server);

        await Login(agent);

        bool unauthenticatedEventRaised = false;
        agent.Unauthenticated += (_, _) => unauthenticatedEventRaised = true;

        await Assert.ThrowsAsync<LogoutException>(() => agent.Logout(TestContext.Current.CancellationToken));

        // The credentials are discarded whether or not the revocation succeeded, so a subscriber holding them in
        // durable storage has to be told, otherwise the next start restores a session the server has rejected.
        Assert.Null(agent.Credentials);
        Assert.True(unauthenticatedEventRaised);
    }

    [Fact]
    public async Task ARefreshOfACredentialForADifferentAccountDoesNotRePointTheAgent()
    {
        OAuthTestServer server = new() { IssuedDid = new Did(OtherDid) };
        using AtProtoAgent agent = CreateAgent(server);

        await Login(agent);

        AccessCredentials credentialsBeforeRefresh = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        int refreshesBefore = server.RefreshCount;

        Assert.False(await agent.RefreshCredentials(CreateCredentials(new Did(OtherDid)), TestContext.Current.CancellationToken));

        Assert.Same(credentialsBeforeRefresh, agent.Credentials);
        Assert.Equal(new Did(AccountDid), agent.Credentials!.Did);

        // Declined before the token endpoint was called, so the supplied credential has not been spent and remains
        // usable. Exchanging it and then discarding the result would destroy the caller's stored session.
        Assert.Equal(refreshesBefore, server.RefreshCount);
    }

    [Fact]
    public async Task ALoginAfterASessionEndsDoesNotTreatItsRefreshTokenAsAlreadySpent()
    {
        OAuthTestServer server = new();
        using AtProtoAgent agent = CreateAgent(server);

        await Login(agent);

        Assert.True(await agent.RefreshCredentials(TestContext.Current.CancellationToken));

        // The session ends by a route which does not go through logout, leaving the tokens it exchanged remembered.
        agent.Credentials = null;

        Assert.True(await agent.Login(CreateCredentials(), TestContext.Current.CancellationToken));

        int refreshesBefore = server.RefreshCount;

        Assert.True(await agent.RefreshCredentials(TestContext.Current.CancellationToken));

        // The new session's refresh token has the same value as the ended session's, so if the exchanges the old
        // session made are still remembered this refresh is wrongly short circuited as already spent.
        Assert.Equal(refreshesBefore + 1, server.RefreshCount);
    }

    [Fact]
    public async Task ASessionWhichEndedBeforeAnotherBeganIsReportedBeforeIt()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        // Spend the refresh token on an exchange which cannot complete, so the next refresh finds it already spent,
        // ends the session, and raises the session ending after it has released the refresh semaphore. That is the
        // window a login can commit in.
        server.IssuedDid = new Did(OtherDid);

        await Assert.ThrowsAsync<SecurityTokenValidationException>(
            () => agent.RefreshCredentials(TestContext.Current.CancellationToken));

        server.IssuedDid = new Did(AccountDid);

        List<string> order = [];

        agent.Unauthenticated += (_, _) =>
        {
            lock (order)
            {
                order.Add("unauthenticated");
            }
        };

        agent.Authenticated += (_, _) =>
        {
            lock (order)
            {
                order.Add("authenticated");
            }
        };

        TaskCompletionSource sessionEndQueued = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseSessionEnd = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Holds the session ending between the refresh committing it and the event being raised, which is the window a
        // login can commit in and, without commit ordered delivery, announce itself first.
        agent.SessionEventQueueing = async () =>
        {
            if (sessionEndQueued.TrySetResult())
            {
                await releaseSessionEnd.Task;
            }
        };

        Task<bool> refresh = agent.RefreshCredentials(TestContext.Current.CancellationToken);

        await sessionEndQueued.Task.WaitAsync(TestContext.Current.CancellationToken);

        Task<bool> login = agent.Login(CreateCredentials(), TestContext.Current.CancellationToken);

        await WaitFor(() => agent.Credentials is not null, TestContext.Current.CancellationToken);

        releaseSessionEnd.TrySetResult();

        Assert.False(await refresh);
        Assert.True(await login);

        // The session ended before the new one began, so it has to be reported first. A subscriber which discards its
        // stored credentials when a session ends would otherwise discard the session which is actually current.
        Assert.Equal(["unauthenticated", "authenticated"], order);
        Assert.NotNull(agent.Credentials);
    }

    [Fact]
    public async Task ARefreshWhichRestoresASessionNotifiesItsCredentialsEvenWhenAuthenticatedThrows()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        List<AccessCredentials> persisted = [];

        agent.CredentialsUpdatedAsync = (e, _) =>
        {
            persisted.Add(e.AccessCredentials);
            return Task.CompletedTask;
        };

        agent.Authenticated += (_, _) => throw new InvalidOperationException("Session start handling failed.");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.RefreshCredentials(CreateCredentials(), TestContext.Current.CancellationToken));

        // The credentials are already live in the agent and the token they replaced has been spent, so they have to be
        // notified even though the session start handler failed, otherwise storage keeps a token the server has burnt.
        // The notification can be queued behind one which is still running, so it is waited for rather than sampled.
        await WaitFor(() => persisted.Count == 1, TestContext.Current.CancellationToken);

        AccessCredentials refreshedCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        Assert.Same(refreshedCredentials, Assert.Single(persisted));
    }

    /// <summary>
    /// Waits for <paramref name="condition"/> to become true, so that a test does not depend on a notification which is
    /// queued behind another having been delivered by the time the call which caused it returns.
    /// </summary>
    private static async Task WaitFor(Func<bool> condition, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(10);

        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(10, cancellationToken);
        }

        Assert.True(condition(), "The expected condition was not reached before the timeout expired.");
    }

    [Fact]
    public async Task ARefreshWhichRestoresASessionRaisesAuthenticated()
    {
        OAuthTestServer server = new();
        using AtProtoAgent agent = CreateAgent(server);

        Did? authenticatedAs = null;
        agent.Authenticated += (_, e) => authenticatedAs = e.Did;

        Assert.True(await agent.RefreshCredentials(CreateCredentials(), TestContext.Current.CancellationToken));

        Assert.True(agent.IsAuthenticated);
        Assert.Equal(new Did(AccountDid), authenticatedAs);
    }

    [Fact]
    public async Task AStaleNonceUpdateForACredentialTypeOfYourOwnDoesNotReplaceNewerCredentials()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        CustomDPoPCredentials staleCredentials = CreateCustomCredentials("staleRefreshToken");
        agent.Credentials = staleCredentials;

        CustomDPoPCredentials currentCredentials = CreateCustomCredentials("currentRefreshToken");
        agent.Credentials = currentCredentials;

        int credentialsUpdatedCount = 0;
        agent.CredentialsUpdated += (_, _) => credentialsUpdatedCount++;

        staleCredentials.DPoPNonce = "staleNonce";
        await agent.NotifyCredentialsUpdated(staleCredentials, TestContext.Current.CancellationToken);

        // A credential type of your own is DPoP bound without deriving from DPoPAccessCredentials, so it used to take
        // an unguarded path which published the caller's snapshot, restoring the refresh token it was holding.
        Assert.Same(currentCredentials, agent.Credentials);
        Assert.Equal(0, credentialsUpdatedCount);
    }

    private static CustomDPoPCredentials CreateCustomCredentials(string refreshToken) =>
        new(new Uri($"https://{DomainName}"), CreateAccessJwt(new Did(AccountDid)), refreshToken);

    /// <summary>
    /// A DPoP bound credential which does not derive from <see cref="DPoPAccessCredentials"/>, as a caller is free to
    /// write, since <see cref="AccessCredentials"/> and <see cref="IDPoPBoundCredential"/> are both public.
    /// </summary>
    private sealed class CustomDPoPCredentials(Uri service, string accessJwt, string refreshToken)
        : AccessCredentials(service, AuthenticationType.OAuth, accessJwt, refreshToken), IDPoPBoundCredential
    {
        public string DPoPProofKey { get; set; } = JwtBuilder.CreateProofKey();

        public string DPoPNonce { get; set; } = "nonce";
    }

    private static DPoPAccessCredentials CreateCredentials() => CreateCredentials(new Did(AccountDid));

    private static DPoPAccessCredentials CreateCredentials(Did did) =>
        new(service: new Uri($"https://{DomainName}"),
            accessJwt: CreateAccessJwt(did),
            refreshToken: "initialRefreshToken",
            dPoPProofKey: JwtBuilder.CreateProofKey(),
            dPoPNonce: "nonce");

    private static string CreateAccessJwt(Did did) =>
        JwtBuilder.CreateJwt(did, issuer: Authority, audience: ServerDid, scope: "atproto");

    private static async Task Login(AtProtoAgent agent)
    {
        Assert.True(await agent.Login(CreateCredentials(), TestContext.Current.CancellationToken));
        Assert.True(agent.IsAuthenticated);
    }

    private static AtProtoAgent CreateAgent(OAuthTestServer server) =>
        new OAuthTestAgent(server)
        {
            Options = new AtProtoAgentOptions
            {
                OAuthOptions = new OAuthOptions("https://client.test/clientMetadata.json")
            }
        };

    /// <summary>
    /// An agent whose OAuth exchanges are routed at the in memory test server rather than the network.
    /// </summary>
    private sealed class OAuthTestAgent(OAuthTestServer server) : AtProtoAgent(
        new Uri($"https://{DomainName}"),
        new TestHttpClientFactory(server.TestServer),
        new AtProtoAgentOptions
        {
            PlcDirectoryServer = new Uri($"https://{DomainName}"),
            OAuthOptions = new OAuthOptions("https://client.test/clientMetadata.json")
        })
    {
        private readonly OAuthTestServer _server = server;

        internal Task NotifyCredentialsUpdated(AtProtoCredential credentials, CancellationToken cancellationToken) =>
            InternalOnCredentialsUpdatedCallBack(credentials, cancellationToken);

        /// <summary>
        /// Raises a credential update notification directly, bypassing the freshness check the DPoP nonce callback
        /// applies, so that a notification which reaches the queue later than the credentials which replaced it can
        /// be reproduced.
        /// </summary>
        internal Task RaiseCredentialsUpdated(AccessCredentials credentials, bool credentialsCommitted, CancellationToken cancellationToken) =>
            RaiseCredentialsUpdatedAsync(credentials, credentialsCommitted, cancellationToken);

        public override OAuthClient CreateOAuthClient() =>
            new(httpClientConfigurator: httpClient => httpClient,
                innerHandlerFactory: _server.TestServer.CreateHandler,
                loggerFactory: null,
                options: Options?.OAuthOptions);
    }

    /// <summary>
    /// A test server which serves authorization server discovery, a token endpoint and enough of a personal data
    /// server for an OAuth credential refresh to complete.
    /// </summary>
    private sealed class OAuthTestServer
    {
        private int _tokenSerialNumber;

        internal bool GateRefresh { get; set; }

        internal OAuthTestServer()
        {
            TestServer = TestServerBuilder.CreateServer(DomainName, Handle);
        }

        internal TestServer TestServer { get; }

        internal TaskCompletionSource RefreshEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseRefresh { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets or sets the DID the token endpoint issues tokens for. Defaults to the account being refreshed.
        /// </summary>
        internal Did IssuedDid { get; set; } = new Did(AccountDid);

        /// <summary>
        /// Gets or sets whether the revocation endpoint accepts a token, so that a logout which fails at the server
        /// can be exercised.
        /// </summary>
        internal bool RevocationSucceeds { get; set; } = true;

        internal string? LastIssuedRefreshToken { get; private set; }

        /// <summary>
        /// Gets the number of times the token endpoint has been asked to exchange a refresh token, so that a refresh
        /// which should have been declined before reaching the server can be distinguished from one which was made
        /// and then discarded.
        /// </summary>
        internal int RefreshCount => _refreshCount;

        private int _refreshCount;

        private async Task Handle(HttpContext context)
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            switch (request.Path)
            {
                case "/.well-known/oauth-protected-resource":
                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""{"authorization_servers":["{{Authority}}"]}""");
                    return;

                case "/.well-known/oauth-authorization-server":
                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""
                        {
                          "issuer": "https://{{DomainName}}",
                          "authorization_endpoint": "https://{{DomainName}}/authorize",
                          "token_endpoint": "https://{{DomainName}}/token",
                          "jwks_uri": "https://{{DomainName}}/jwks",
                          "revocation_endpoint": "https://{{DomainName}}/revoke",
                          "response_types_supported": [ "code" ],
                          "grant_types_supported": [ "authorization_code", "refresh_token" ]
                        }
                        """);
                    return;

                case "/revoke" when request.Method == HttpMethod.Post.Method:
                    if (!RevocationSucceeds)
                    {
                        response.StatusCode = StatusCodes.Status400BadRequest;
                        return;
                    }

                    response.ContentType = "application/json";
                    await response.WriteAsync("{}");
                    return;

                case "/jwks":
                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""{"keys":[{{s_signingKeyJson}}]}""");
                    return;

                case "/token" when request.Method == HttpMethod.Post.Method:
                    Interlocked.Increment(ref _refreshCount);

                    if (GateRefresh)
                    {
                        RefreshEntered.TrySetResult();
                        await ReleaseRefresh.Task.ConfigureAwait(false);
                    }

                    LastIssuedRefreshToken = $"refreshToken{Interlocked.Increment(ref _tokenSerialNumber)}";

                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""
                        {
                          "access_token": "{{CreateAccessJwt(IssuedDid)}}",
                          "token_type": "DPoP",
                          "expires_in": 900,
                          "refresh_token": "{{LastIssuedRefreshToken}}",
                          "scope": "atproto"
                        }
                        """);
                    return;

                case "/xrpc/com.atproto.server.describeServer":
                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""{"did":"{{ServerDid}}","availableUserDomains":[".{{DomainName}}"]}""");
                    return;

                default:
                    response.StatusCode = StatusCodes.Status404NotFound;
                    return;
            }
        }
    }
}
