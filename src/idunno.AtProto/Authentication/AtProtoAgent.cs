// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Timers;

using idunno.AtProto.Authentication;
using idunno.AtProto.Events;
using idunno.AtProto.Server;
using idunno.Security;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace idunno.AtProto;

// Credential storage, refresh, and the notifications and events which report them.
//
// This describes private machinery, so it is a plain comment rather than a documentation comment; none of it belongs
// in the published API documentation. A longer walkthrough, with diagrams, is in
// devnotes/credential-lifecycle-and-locking.md.
//
// Why any of this is serialised.
//
// A subscriber typically persists what it is given, so the order it is told things in decides what ends up in durable
// storage. The last write wins, and the agent has several things which can change credentials at once: a background
// refresh, a manual refresh, a DPoP nonce rotation forced by the server, a login, and a logout. Left unordered, a
// handler which started first can finish last and leave storage holding a refresh token the server has already spent,
// or a session which has ended, with nothing following it to repair the damage. On the next start the agent restores
// those credentials and cannot authenticate.
//
// The four pieces of state.
//
//   _credentialLock guards the agent credentials and everything which describes them, and is only ever held for
//   straight line work. Never await, raise an event, or call a handler while holding it.
//
//   _credentialRefreshSemaphore allows one credential exchange at a time, so two refreshes cannot both present the
//   same refresh token, and a logout cannot revoke tokens a refresh is part way through replacing.
//
//   _credentialNotificationSemaphore allows one notification or session event at a time, which is what gives a
//   subscriber a single ordered view of the session.
//
//   _credentialGeneration counts every change to the credentials, so work which started against one set can tell,
//   when it finishes, that it is looking at history.
//
// Lock ordering.
//
// The refresh semaphore may be taken before the notification semaphore, and either may be taken before
// _credentialLock. Never the other way around. In practice this means a notification or session event is raised after
// the refresh semaphore has been released, which is also what stops a handler which calls back into the agent from
// deadlocking it.
//
// How a change reaches a subscriber.
//
// Credentials are published under _credentialLock, by TryPublishRefreshedCredentials for a refresh or directly for a
// login or logout. Whatever made the change then calls RaiseCredentialsUpdatedAsync, or RaiseAuthenticatedAsync or
// RaiseUnauthenticatedAsync for a session starting or ending. Each waits its turn on the notification semaphore, and a
// notification is dropped rather than raised if the agent no longer holds the credentials it carries, because whatever
// replaced them is raising a notification of its own.
//
// Reentrancy.
//
// A handler may call straight back into the agent, and a login, logout or refresh it performs produces something else
// to raise while the notification which triggered it still holds the semaphore. Waiting would deadlock, and raising it
// inline would put it ahead of the handler which caused it, letting that handler finish afterwards and persist what it
// was already holding. So a reentrant raise is parked, in _deferredCredentialNotification or _deferredSessionEvents,
// and drained by the notification which is already running once its handler returns. Reentrancy is detected with
// _raisingCredentialNotification.
//
// Committed credentials.
//
// Once an exchange has happened the refresh token the agent was holding is spent, whatever happens next. A
// notification for credentials in that state is marked committed, cannot be cancelled, and is drained even if the
// handler which triggered it throws, because it is the only opportunity to persist tokens which actually work.
//
// Adding code here.
//
// Anything which changes the credentials should publish them under _credentialLock and then raise through the helpers
// above rather than calling OnCredentialsUpdatedAsync, OnAuthenticated or OnUnauthenticated directly. Raising one of
// those directly bypasses the ordering and is how every bug this machinery exists to prevent gets reintroduced. A path
// which ends a session because something failed should use ClearCredentialsAndRaiseUnauthenticatedAsync, so the
// credentials are not discarded silently.
public partial class AtProtoAgent
{
#if NET9_0_OR_GREATER
    /// <summary>
    /// Guards the agent credentials, the generation which tracks changes to them, and the deferred notification state.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Held only for straight line work. Nothing awaits, raises an event or calls a handler whilst holding it, as a
    ///   handler is free to call back into the agent and would deadlock against itself.
    /// </para>
    /// <para>
    ///   Checks made against the credentials are only meaningful if the decision they lead to happens under the same
    ///   acquisition. Reading the credentials, releasing the lock and then acting on what was read allows the thing
    ///   which was checked to change in between.
    /// </para>
    /// </remarks>
    private readonly Lock _credentialLock = new();

    /// <summary>
    /// Guards the background credential refresh timer. Unrelated to <see cref="_credentialLock"/>, and may be taken
    /// whilst holding it.
    /// </summary>
    private readonly Lock _timerLock = new();
#else
    /// <summary>
    /// Guards the agent credentials, the generation which tracks changes to them, and the deferred notification state.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Held only for straight line work. Nothing awaits, raises an event or calls a handler whilst holding it, as a
    ///   handler is free to call back into the agent and would deadlock against itself.
    /// </para>
    /// <para>
    ///   Checks made against the credentials are only meaningful if the decision they lead to happens under the same
    ///   acquisition. Reading the credentials, releasing the lock and then acting on what was read allows the thing
    ///   which was checked to change in between.
    /// </para>
    /// </remarks>
    private readonly object _credentialLock = new();

    /// <summary>
    /// Guards the background credential refresh timer. Unrelated to <see cref="_credentialLock"/>, and may be taken
    /// whilst holding it.
    /// </summary>
    private readonly object _timerLock = new();
#endif

    /// <summary>
    /// Serialises credential refreshes.
    /// </summary>
    /// <remarks>
    /// <para>Deliberately not disposed. Disposing it whilst a refresh holds it makes the release which ends that refresh
    /// throw, turning a disposal which races a refresh into an exception out of <see cref="RefreshCredentials(CancellationToken)"/>.
    /// Nothing here uses <see cref="SemaphoreSlim.AvailableWaitHandle"/>, which is the only thing its disposal frees.</para>
    /// </remarks>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposing it whilst a refresh holds it makes the release which ends that refresh throw.")]
    private readonly SemaphoreSlim _credentialRefreshSemaphore = new(1, 1);

    /// <summary>
    /// Serialises credential update notifications, so a handler which persists them cannot have an older set of
    /// credentials written after a newer set.
    /// </summary>
    /// <remarks>
    /// <para>Deliberately not disposed, for the same reason <see cref="_credentialRefreshSemaphore"/> is not.</para>
    /// </remarks>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposing it whilst a notification holds it makes the release which ends that notification throw.")]
    private readonly SemaphoreSlim _credentialNotificationSemaphore = new(1, 1);

    /// <summary>
    /// Set whilst a credential update notification is being raised on the current asynchronous flow.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   A handler is free to call back into the agent, and a call which refreshes credentials or rotates the DPoP
    ///   nonce raises another notification. Waiting on <see cref="_credentialNotificationSemaphore"/> for a notification
    ///   raised from inside one which already holds it would deadlock the agent, so a reentrant notification is handed
    ///   to <see cref="_deferredCredentialNotification"/> instead.
    /// </para>
    /// <para>
    ///   The value is a scope rather than a flag because an <see cref="AsyncLocal{T}"/> flows into any work a handler
    ///   starts, including work it does not await. Such work can run after the notification it was started from has
    ///   finished, when there is no longer anything holding the semaphore to drain a deferred notification, so the
    ///   scope is marked inactive as the notification finishes and a reentrant notification is only deferred while
    ///   the scope it flowed from is still active.
    /// </para>
    /// </remarks>
    private readonly AsyncLocal<CredentialNotificationScope?> _raisingCredentialNotification = new();

    /// <summary>
    /// Marks the extent of a credential update notification, so that work which flowed out of it can tell whether it
    /// is still running.
    /// </summary>
    private sealed class CredentialNotificationScope
    {
        /// <summary>
        /// Gets or sets a value indicating whether the notification this scope was created for is still running.
        /// </summary>
        /// <remarks>Guarded by <see cref="_credentialLock"/>.</remarks>
        public bool Active { get; set; } = true;
    }

    /// <summary>
    /// Credentials raised from inside a notification which is still running, to be notified once it has finished.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Guarded by <see cref="_credentialLock"/>. Only the newest credentials are held: an older reentrant update is
    ///   of no interest to a handler which is about to be given a newer one.
    /// </para>
    /// </remarks>
    private AccessCredentials? _deferredCredentialNotification;

    /// <summary>
    /// Whether <see cref="_deferredCredentialNotification"/> holds credentials the agent has already published, whose
    /// notification is the only opportunity to persist them.
    /// </summary>
    /// <remarks>
    /// <para>Guarded by <see cref="_credentialLock"/>.</para>
    /// </remarks>
    private bool _deferredCredentialNotificationCommitted;

    /// <summary>
    /// Session start and end events raised from inside a credential update notification which is still running, to be
    /// raised once that notification, and everything queued behind it, has finished.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Guarded by <see cref="_credentialLock"/>. Held as a queue rather than a single event because a handler can end
    ///   one session and start another, and a subscriber which is told they happened in the other order would discard
    ///   the credentials for the session which is actually current.
    /// </para>
    /// </remarks>
    private readonly Queue<Action> _deferredSessionEvents = new();

    /// <summary>
    /// Called when a credential update notification is about to wait its turn behind any notification already being
    /// raised, so that a test can synchronise on the notification queue rather than on elapsed time.
    /// </summary>
    internal Action<AccessCredentials>? CredentialNotificationQueueing { get; set; }

    private AccessCredentials? _credentials;

    /// <summary>
    /// Incremented every time the agent credentials are published or cleared, so an operation which started against one
    /// set of credentials can tell that they have been replaced whilst it was running.
    /// </summary>
    /// <remarks>
    /// <para>Guarded by <see cref="_credentialLock"/>.</para>
    /// </remarks>
    private long _credentialGeneration;

    /// <summary>
    /// The number of recently exchanged refresh tokens remembered by <see cref="HasRefreshTokenAlreadyBeenExchanged(string, string)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Remembering only the most recently exchanged token would let a caller holding an older credential present a
    ///   token which has already been spent, which is exactly what the check exists to prevent.
    /// </para>
    /// </remarks>
    private const int MaximumRememberedRefreshTokens = 4;

    private readonly Queue<string> _exchangedRefreshTokens = new(MaximumRememberedRefreshTokens);

#if NET9_0_OR_GREATER
    private readonly Lock _exchangedRefreshTokenLock = new();
#else
    private readonly object _exchangedRefreshTokenLock = new();
#endif

    internal readonly bool _enableTokenRefresh = true;
    private readonly TimeSpan _refreshAccessTokenInterval = new(1, 0, 0);
    private readonly TimeSpan _backgroundRefreshRetryInterval = new(0, 1, 0);

    /// <summary>
    /// The delay used when an access token is already at or close to expiry when the refresh timer is started.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The refresh is scheduled rather than run inline because <see cref="StartTokenRefreshTimer"/> is itself called
    ///   from inside a refresh, whilst the refresh semaphore is still held. Running it inline would recurse, and a server
    ///   issuing short lived access tokens would drive an unbounded chain of immediate refreshes with no delay between them.
    /// </para>
    /// </remarks>
    private readonly TimeSpan _immediateRefreshInterval = new(0, 0, 1);

    private System.Timers.Timer? _credentialRefreshTimer;

    /// <summary>
    /// Gets the current credentials for the agent, if any.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when setting the credentials on a disposed agent.</exception>
    /// <remarks>
    /// <para>
    ///   Setting this property replaces the agent credentials and nothing else. It raises no <see cref="Authenticated"/>,
    ///   <see cref="Unauthenticated"/> or credentials updated notification, and it is not ordered against a notification
    ///   which is already running, so a subscriber persisting credentials is not told about the change and can write the
    ///   credentials it was already holding afterwards.
    /// </para>
    /// <para>
    ///   Prefer <see cref="Login(string, string, string?, Uri?, CancellationToken)"/>, <see cref="Logout(CancellationToken)"/>
    ///   and <see cref="RefreshCredentials(AtProtoCredential, CancellationToken)"/>, which raise the matching events and
    ///   order them behind any notification in flight. Credentials assigned here are still published under the same lock,
    ///   so an in flight refresh which started before the assignment is discarded rather than allowed to overwrite it.
    /// </para>
    /// </remarks>
    public AccessCredentials? Credentials
    {
        get
        {
            if (_atProtoAgentDisposed)
            {
                return null;
            }

            lock (_credentialLock)
            {
                return _credentials;
            }
        }

        set
        {
            ObjectDisposedException.ThrowIf(_atProtoAgentDisposed, this);

            lock (_credentialLock)
            {
                _credentials = value;
                _credentialGeneration++;

                if (_credentials is not null)
                {
                    Service = _credentials.Service;
                }
                else
                {
                    Service = OriginalService;
                }
            }
        }
    }

    /// <summary>
    /// Gets the generation of the agent credentials, which changes every time they are published or cleared.
    /// </summary>
    private long CredentialGeneration
    {
        get
        {
            lock (_credentialLock)
            {
                return _credentialGeneration;
            }
        }
    }

    /// <summary>
    /// Publishes <paramref name="refreshedCredentials"/> as the agent credentials, but only if the agent credentials have
    /// not been replaced since <paramref name="expectedGeneration"/> was read.
    /// </summary>
    /// <param name="refreshedCredentials">The credentials a refresh produced.</param>
    /// <param name="expectedGeneration">The <see cref="CredentialGeneration"/> read when the refresh started.</param>
    /// <param name="sessionStarted">
    /// Whether the credentials started a session on an agent which did not have one, as a refresh of stored credentials
    /// on a newly created agent does.
    /// </param>
    /// <returns><see langword="true"/> if the credentials were published, otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    ///   A refresh spans a call to the authorization server, and a logout or a login can complete whilst it is in flight.
    ///   Publishing unconditionally would let a refresh which started before a logout re-establish the session the logout
    ///   ended, with tokens the revocation never saw, or overwrite the credentials a login had just installed, leaving the
    ///   agent running as the previous account.
    /// </para>
    /// <para>
    ///   The check and the write happen under the same lock the generation is incremented under, so a refresh cannot
    ///   observe an unchanged generation and then publish over a change made immediately afterwards.
    /// </para>
    /// <para>
    ///   Credentials for a different actor to the one the agent is authenticated as are never published. A refresh of a
    ///   credential the caller supplied is not necessarily a refresh of the agent's own session, and re-pointing the
    ///   agent, and everything built on it, at another account as a side effect of refreshing a stored credential would
    ///   be silent.
    /// </para>
    /// </remarks>
    private bool TryPublishRefreshedCredentials(AccessCredentials refreshedCredentials, long expectedGeneration, out bool sessionStarted)
    {
        ArgumentNullException.ThrowIfNull(refreshedCredentials);

        sessionStarted = false;

        lock (_credentialLock)
        {
            // The generation moved, so a login, a logout or another refresh landed while this one was talking to the
            // server. Whatever did that is more recent than this, so these credentials are history and are dropped.
            // The refresh token they replaced is spent either way, which is why the caller treats this as a failure
            // and ends the session rather than retrying.
            if (_atProtoAgentDisposed || _credentialGeneration != expectedGeneration)
            {
                return false;
            }

            // Refreshing a credential the caller supplied is not necessarily refreshing the agent's own session, so
            // the result is only published if it belongs to the account the agent is authenticated as.
            if (_credentials is AccessCredentials currentCredentials &&
                currentCredentials.Did is Did currentDid &&
                currentDid != refreshedCredentials.Did)
            {
                Logger.RefreshedCredentialsDiscardedAsTheyAreForADifferentActor(
                    _logger,
                    refreshedCredentials.Did,
                    currentDid);

                return false;
            }

            // Reported so the caller can raise Authenticated. Refreshing a stored credential on a fresh agent is how
            // a session is restored across a restart, and that starts a session just as a login does.
            sessionStarted = _credentials is null;

            // The nonce belongs to the connection rather than to the tokens, so the newest one the agent has seen is
            // carried across to the credentials replacing them. Without this the refreshed credentials start with the
            // nonce the refresh began with, and a rotation which happened during the refresh is lost, costing a
            // rejected request and a retry on the next call. Only carried when the proof key and service match, as a
            // nonce is only meaningful for the binding it was issued against.
            if (_credentials is DPoPAccessCredentials currentDPoPCredentials &&
                refreshedCredentials is DPoPAccessCredentials refreshedDPoPCredentials &&
                currentDPoPCredentials.Service == refreshedDPoPCredentials.Service &&
                string.Equals(currentDPoPCredentials.DPoPProofKey, refreshedDPoPCredentials.DPoPProofKey, StringComparison.Ordinal))
            {
                refreshedDPoPCredentials.DPoPNonce = currentDPoPCredentials.DPoPNonce;
            }

            _credentials = refreshedCredentials;
            _credentialGeneration++;
            Service = refreshedCredentials.Service;

            return true;
        }
    }

    /// <summary>
    /// Gets the current <see cref="Did"/> of the agent's authenticated session, if any, otherwise returns <see langword="null"/>.
    /// </summary>
    public Did? Did
    {
        get
        {
            if (_atProtoAgentDisposed)
            {
                return null;
            }

            Did? did = null;
            lock (_credentialLock)
            {
                if (_credentials is AccessCredentials accessCredentials)
                {
                    did = accessCredentials.Did;
                }
            }

            return did;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the agent has current authentication tokens.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The value is a snapshot. Another thread can log the agent out, or a background refresh can replace the
    ///   credentials, between this being read and <see cref="Credentials"/> being read. Callers which need both must
    ///   read <see cref="Credentials"/> into a local and test that, rather than relying on the guarantee this
    ///   property gives the compiler.
    /// </para>
    /// </remarks>
    [MemberNotNullWhen(true, nameof(Credentials))]
    [MemberNotNullWhen(true, nameof(Did))]
    public override bool IsAuthenticated
    {
        get
        {
            if (_atProtoAgentDisposed)
            {
                return false;
            }

            lock (_credentialLock)
            {
                return _credentials is IAccessCredential accessCredential &&
                    accessCredential.Did is not null &&
                    accessCredential.ExpiresOn > DateTimeOffset.UtcNow;
            }
        }
    }

    /// <summary>
    /// Gets a flag indicating whether the agent has access credentials.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The value is a snapshot. Another thread can log the agent out, or a background refresh can replace the
    ///   credentials, between this being read and <see cref="Credentials"/> being read. Callers which need both must
    ///   read <see cref="Credentials"/> into a local and test that, rather than relying on the guarantee this
    ///   property gives the compiler.
    /// </para>
    /// </remarks>
    [MemberNotNullWhen(true, nameof(Credentials))]
    [MemberNotNullWhen(true, nameof(Did))]
    public bool HasCredentials
    {
        get
        {
            if (_atProtoAgentDisposed)
            {
                return false;
            }

            lock (_credentialLock)
            {
                return _credentials is IAccessCredential accessCredential && accessCredential.Did is not null;
            }
        }
    }

    /// <summary>
    /// Called internally by an <see cref="AtProtoHttpClient{TResult}"/> if the credentials were updated.
    /// </summary>
    /// <param name="credentials">The new credentials</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="credentials"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    ///   In practice this reports a DPoP nonce rotation. The server hands back a new nonce on a request made with an
    ///   old one, and the agent has to record it so the retry, and everything after it, is accepted.
    /// </para>
    /// <para>
    ///   The nonce is the only part which changes, so it is written into the credentials the agent already holds rather
    ///   than replacing them. Replacing them with the snapshot the request was made with is what
    ///   <see href="https://github.com/blowdart/idunno.Bluesky/issues/559">issue 559</see> was: a request which started
    ///   before a refresh carries the tokens from before it, so publishing that snapshot restored a spent refresh
    ///   token and changed the generation, which in turn made the refresh discard the credentials it had just been
    ///   issued. The session then had no usable tokens at all.
    /// </para>
    /// </remarks>
    protected internal virtual async Task InternalOnCredentialsUpdatedCallBack(AtProtoCredential credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        if (credentials is AccessCredentials updatedAccessCredentials && credentials is IDPoPBoundCredential updatedDPoPCredential)
        {
            AccessCredentials credentialsToNotify;

            lock (_credentialLock)
            {
                // The request this nonce came from was made with a snapshot of the credentials taken when it started,
                // and a refresh or a logout can have happened since. Everything except the nonce therefore has to still
                // match what the agent holds. If any of it does not, the nonce belongs to a session, or a set of tokens,
                // which is over, and applying it would corrupt the current ones. The agent is left alone, and the
                // request is left to fail and retry, which will collect a nonce for the credentials in use.
                if (_atProtoAgentDisposed ||
                    _credentials is not AccessCredentials currentCredentials ||
                    currentCredentials is not IDPoPBoundCredential currentDPoPCredential ||
                    !string.Equals(currentCredentials.AccessJwt, updatedAccessCredentials.AccessJwt, StringComparison.Ordinal) ||
                    !string.Equals(currentCredentials.RefreshToken, updatedAccessCredentials.RefreshToken, StringComparison.Ordinal) ||
                    !string.Equals(currentDPoPCredential.DPoPProofKey, updatedDPoPCredential.DPoPProofKey, StringComparison.Ordinal) ||
                    currentCredentials.Service != updatedAccessCredentials.Service)
                {
                    return;
                }

                // Mutated in place rather than published, so the generation does not change and a refresh which is in
                // flight is not made to think its credentials have been superseded. A handler already holding this
                // instance sees the new nonce without being told anything.
                if (!ReferenceEquals(currentCredentials, updatedAccessCredentials) &&
                    !string.Equals(currentDPoPCredential.DPoPNonce, updatedDPoPCredential.DPoPNonce, StringComparison.Ordinal))
                {
                    currentDPoPCredential.DPoPNonce = updatedDPoPCredential.DPoPNonce;
                }

                credentialsToNotify = currentCredentials;
            }

            Logger.OnCredentialUpdatedCallbackCalled(_logger);

            // Not committed: nothing has been exchanged, so if this notification is dropped or cancelled the agent's
            // credentials still work and the nonce is simply collected again on the next request.
            await RaiseCredentialsUpdatedAsync(credentialsToNotify, credentialsCommitted: false, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Anything else is rejected rather than published. Only a DPoP bound access credential drives this
            // callback, and publishing a shape which is not one would mean assigning the caller's snapshot without the
            // freshness check above, which is exactly the stale assignment issue 559 was.
            Logger.OnCredentialUpdatedCallbackCalledWithUnexpectedCredentialType(_logger);
        }
    }

    /// <summary>
    /// Raises <see cref="AtProtoAgent.CredentialsUpdated"/> and awaits <see cref="AtProtoAgent.CredentialsUpdatedAsync"/> for
    /// <paramref name="credentials"/>, provided the agent still holds them.
    /// </summary>
    /// <param name="credentials">The credentials the notification is being raised for.</param>
    /// <param name="credentialsCommitted">
    ///   <see langword="true"/> if <paramref name="credentials"/> have already been published as the agent's credentials
    ///   and a handler is the only opportunity to persist them, otherwise <see langword="false"/>.
    /// </param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    ///   A handler which persists what it is given must never write an older set of credentials over a newer set. A DPoP
    ///   nonce update and a credential refresh can be notified concurrently, so without serialisation a nonce notification
    ///   which started first can finish last and write a refresh token the server has already spent over the one which
    ///   replaced it, leaving a restart with unusable credentials.
    /// </para>
    /// <para>
    ///   Notifications are therefore raised one at a time, and one whose credentials the agent has since replaced is
    ///   dropped rather than raised: whatever replaced them has its own notification, and the credentials in hand no
    ///   longer describe the session.
    /// </para>
    /// <para>
    ///   Once credentials have been committed their notification cannot be cancelled. Dropping it would leave an older
    ///   notification queued ahead of it free to persist credentials the server has already replaced, with nothing left
    ///   to follow and repair them.
    /// </para>
    /// <para>
    ///   A handler is free to call back into the agent, and a call which refreshes credentials raises a notification of
    ///   its own. Raising that inline would let the outer handler, holding the credentials which have just been
    ///   replaced, finish last and persist them, so a reentrant notification is deferred and raised once the handler it
    ///   came from has finished. A deferred notification for committed credentials is raised even if the handler it was
    ///   deferred from fails, as the refresh token it replaced has already been spent.
    /// </para>
    /// <para>
    ///   Internal rather than private so that a test can raise a notification directly, bypassing the freshness check
    ///   the DPoP nonce callback applies.
    /// </para>
    /// </remarks>
    internal async Task RaiseCredentialsUpdatedAsync(AccessCredentials credentials, bool credentialsCommitted, CancellationToken cancellationToken)
    {
        if (credentialsCommitted)
        {
            cancellationToken = CancellationToken.None;
        }

        if (_raisingCredentialNotification.Value is CredentialNotificationScope scope)
        {
            bool deferred = false;

            lock (_credentialLock)
            {
                // Work a handler started but did not await keeps the scope it flowed from, so the notification it
                // raises has to be checked against that scope still running. Deferring to a notification which has
                // already finished would leave nothing to raise it.
                //
                // The credentials also have to still be the agent's. The slot holds only the newest deferral, and a
                // notification can reach it later than one for credentials which replaced it, so without this a stale
                // set could overwrite a newer one and be dropped as stale when the drain raised it, losing both.
                if (scope.Active && ReferenceEquals(_credentials, credentials))
                {
                    _deferredCredentialNotification = credentials;
                    _deferredCredentialNotificationCommitted = _deferredCredentialNotificationCommitted || credentialsCommitted;
                    deferred = true;
                }
                else if (scope.Active)
                {
                    // Nothing to raise, but the deferral is still accounted for: whatever replaced these credentials
                    // has a notification of its own, so returning here drops only the superseded set.
                    deferred = true;
                }
            }

            if (deferred)
            {
                return;
            }
        }

        CredentialNotificationQueueing?.Invoke(credentials);

        // Cancellation is checked here, before the queue, rather than inside it. A notification which has waited its
        // turn is raised to completion, because abandoning it part way would leave the subscriber holding whatever the
        // notification ahead of it wrote.
        await _credentialNotificationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        // From here this call owns the queue, and is responsible for raising anything a handler defers into it before
        // it releases the semaphore. Nothing else can drain those deferrals: a later caller is still blocked on the
        // semaphore, and the handler which parked them has, by definition, already returned.
        CredentialNotificationScope notificationScope = new();

        _raisingCredentialNotification.Value = notificationScope;

        try
        {
            AccessCredentials? credentialsToRaise = credentials;

            try
            {
                // Raise, then take whatever the handler deferred and raise that too, until nothing is left. A handler
                // which refreshes before it persists therefore sees the refreshed credentials last, which is what a
                // subscriber ends up storing.
                while (credentialsToRaise is not null)
                {
                    AccessCredentials raising = credentialsToRaise;

                    await RaiseCredentialsUpdatedIfCurrent(raising, cancellationToken).ConfigureAwait(false);

                    credentialsToRaise = TakeDeferredCredentialNotification(raising, committedOnly: false, notificationScope);
                }

                // Raised last, after everything a handler queued, so a handler which ended or replaced the session
                // cannot persist it afterwards. A subscriber which discards its stored credentials therefore discards
                // the last thing written for the session which is over.
                RaiseDeferredSessionEvents(swallowExceptions: false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // A handler which failed may have already triggered a refresh, spending the refresh token the agent
                // was holding. Whatever replaced it has to reach a handler, otherwise nothing is left which can
                // persist it and durable storage keeps a token the server will not honour.
                await DrainCommittedCredentialNotifications(credentialsToRaise, notificationScope).ConfigureAwait(false);

                // The session has already started or ended, so a subscriber is told even though the notification failed.
                RaiseDeferredSessionEvents(swallowExceptions: true);

                throw;
            }
        }
        finally
        {
            _raisingCredentialNotification.Value = null;

            lock (_credentialLock)
            {
                // Closing the scope makes any later reentrant raise take the queue instead of deferring here, and the
                // slots are emptied because nothing after this point can drain them. In the normal case they are
                // already empty; anything still present was parked by work which outlived its handler and arrived
                // after the loop had stopped looking, and is dropped in preference to being raised out of order or
                // never raised at all.
                notificationScope.Active = false;
                _deferredCredentialNotification = null;
                _deferredCredentialNotificationCommitted = false;
                _deferredSessionEvents.Clear();
            }

            _credentialNotificationSemaphore.Release();
        }
    }

    /// <summary>
    /// Raises the credential update notification for <paramref name="credentials"/>, provided the agent still holds them.
    /// </summary>
    /// <param name="credentials">The credentials the notification is being raised for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    private async Task RaiseCredentialsUpdatedIfCurrent(AccessCredentials credentials, CancellationToken cancellationToken)
    {
        bool agentStillHoldsCredentials;

        lock (_credentialLock)
        {
            // The deferral slot is deliberately left alone. The drain loop empties it before each raise, and work
            // which outlived a handler can fill it at any point, so clearing it here would discard a notification
            // which nothing else is going to raise.
            agentStillHoldsCredentials = !_atProtoAgentDisposed && ReferenceEquals(_credentials, credentials);
        }

        if (agentStillHoldsCredentials)
        {
            await OnCredentialsUpdatedAsync(
                new CredentialsUpdatedEventArgs(credentials.Did, credentials.Service, credentials),
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Takes the credentials deferred by a notification raised from inside the one for <paramref name="justRaised"/>, if any.
    /// </summary>
    /// <param name="justRaised">The credentials whose notification has just finished.</param>
    /// <param name="committedOnly">Whether to take the deferred credentials only if the agent has already published them.</param>
    /// <param name="scopeToCloseWhenEmpty">
    /// The scope to mark inactive if nothing is deferred, so that work which flowed out of the notification and raises
    /// one after this point takes the queue rather than deferring to a notification which is about to finish.
    /// </param>
    /// <returns>The credentials to raise a notification for, or <see langword="null"/> if there are none.</returns>
    private AccessCredentials? TakeDeferredCredentialNotification(
        AccessCredentials justRaised,
        bool committedOnly,
        CredentialNotificationScope? scopeToCloseWhenEmpty = null)
    {
        lock (_credentialLock)
        {
            AccessCredentials? deferred = _deferredCredentialNotification;

            _deferredCredentialNotification = null;

            bool committed = _deferredCredentialNotificationCommitted;

            _deferredCredentialNotificationCommitted = false;

            // A handler is given the credentials themselves, so a reentrant update to the instance it has just been
            // handed, a DPoP nonce rotation for example, needs no notification of its own.
            if (deferred is null || ReferenceEquals(deferred, justRaised) || (committedOnly && !committed))
            {
                // Closed under the same lock the deferral is taken under, so anything raising a notification either
                // deferred before this point, and is taken above, or finds the scope closed and queues instead.
                if (scopeToCloseWhenEmpty is not null)
                {
                    scopeToCloseWhenEmpty.Active = false;
                }

                return null;
            }

            return deferred;
        }
    }

    /// <summary>
    /// Raises notifications for any credentials the agent has already published which were deferred by a handler which
    /// then failed, swallowing any exception a handler throws so the original failure is the one which propagates.
    /// </summary>
    /// <param name="justRaised">The credentials whose notification failed, if any.</param>
    /// <param name="scope">The scope of the notification which failed, closed once there is nothing left to drain.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    private async Task DrainCommittedCredentialNotifications(AccessCredentials? justRaised, CredentialNotificationScope scope)
    {
        AccessCredentials? credentialsToRaise = justRaised is null ? null : TakeDeferredCredentialNotification(justRaised, committedOnly: true, scope);

        while (credentialsToRaise is not null)
        {
            AccessCredentials raising = credentialsToRaise;

            try
            {
                await RaiseCredentialsUpdatedIfCurrent(raising, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Logger.CommittedCredentialsNotificationThrew(_logger, exception);
            }

            credentialsToRaise = TakeDeferredCredentialNotification(raising, committedOnly: true, scope);
        }
    }

    /// <summary>
    /// Raises <see cref="Unauthenticated"/> for a session which has ended, ordered behind any credential update
    /// notification which is still running.
    /// </summary>
    /// <param name="e">The event arguments describing the session which has ended.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    private Task RaiseUnauthenticatedAsync(UnauthenticatedEventArgs e) => RaiseSessionEventAsync(() => OnUnauthenticated(e));

    /// <summary>
    /// Raises <see cref="Authenticated"/> for a session which has started, ordered behind any credential update
    /// notification which is still running.
    /// </summary>
    /// <param name="e">The event arguments describing the session which has started.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    private Task RaiseAuthenticatedAsync(AuthenticatedEventArgs e) => RaiseSessionEventAsync(() => OnAuthenticated(e));

    /// <summary>
    /// Raises a session starting or ending, ordered behind any credential update notification which is still running.
    /// </summary>
    /// <param name="raise">The action which raises the event.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    ///   The credentials a notification carries are checked against the agent before the handler is given them, but a
    ///   handler which suspends can still be running when the session it was told about is replaced or ends. Raising
    ///   the event immediately would let that handler finish afterwards and write credentials for a session which is no
    ///   longer current back to durable storage, with nothing following it to repair them. Ordering the event behind
    ///   the notification queue makes it the last thing a handler sees, so a subscriber which discards or replaces its
    ///   stored credentials discards the stale write with them.
    /// </para>
    /// <para>
    ///   A session started or ended from inside a handler is deferred rather than raised inline, so that the handler
    ///   which caused it, and anything it queued, still runs first. It cannot be waited on either, as the notification
    ///   it came from holds the semaphore it would wait for.
    /// </para>
    /// </remarks>
    private async Task RaiseSessionEventAsync(Action raise)
    {
        bool deferred = false;

        lock (_credentialLock)
        {
            if (_raisingCredentialNotification.Value is { Active: true })
            {
                _deferredSessionEvents.Enqueue(raise);
                deferred = true;
            }
        }

        if (deferred)
        {
            return;
        }

        await _credentialNotificationSemaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            raise();
        }
        finally
        {
            _credentialNotificationSemaphore.Release();
        }
    }

    /// <summary>
    /// Raises the session events deferred by a login or logout called from inside a credential update notification,
    /// in the order they happened.
    /// </summary>
    /// <param name="swallowExceptions">
    /// Whether to swallow an exception thrown by a subscriber, so that a failure which is already propagating stays
    /// the one which reaches the caller.
    /// </param>
    private void RaiseDeferredSessionEvents(bool swallowExceptions)
    {
        while (true)
        {
            Action? raise;

            lock (_credentialLock)
            {
                raise = _deferredSessionEvents.Count == 0 ? null : _deferredSessionEvents.Dequeue();
            }

            if (raise is null)
            {
                return;
            }

            if (!swallowExceptions)
            {
                raise();

                continue;
            }

            try
            {
                raise();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Logger.DeferredSessionEventThrew(_logger, exception);
            }
        }
    }

    /// <summary>
    /// Clears the agent credentials and raises <see cref="Unauthenticated"/> for the session they belonged to, if any.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    ///   Used by the paths which end a session because an operation failed. The credentials are discarded either way,
    ///   as an agent which kept them would report itself as authenticated against a session it can no longer use, but
    ///   discarding them silently leaves a subscriber holding them in durable storage with nothing to say they are
    ///   finished with, so the next start restores a session the server has already rejected.
    /// </para>
    /// </remarks>
    private async Task ClearCredentialsAndRaiseUnauthenticatedAsync()
    {
        UnauthenticatedEventArgs? endedSession;

        lock (_credentialLock)
        {
            endedSession = _credentials is AccessCredentials ended && ended.Did is Did did
                ? new UnauthenticatedEventArgs(did, ended.Service)
                : null;

            _credentials = null;
            _credentialGeneration++;
            Service = OriginalService;
        }

        ForgetExchangedRefreshTokens();

        if (endedSession is not null)
        {
            await RaiseUnauthenticatedAsync(endedSession).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Clears the agent credentials if it is still holding <paramref name="refreshToken"/>, which the server has spent
    /// without the agent receiving anything in return.
    /// </summary>
    /// <param name="refreshToken">The refresh token which has already been exchanged.</param>
    /// <param name="unauthenticatedEventArgs">The event args to raise for the session which has ended, if any.</param>
    /// <returns><see langword="true"/> if the credentials were cleared, otherwise <see langword="false"/>.</returns>
    private bool TryClearCredentialsForSpentRefreshToken(string refreshToken, out UnauthenticatedEventArgs? unauthenticatedEventArgs)
    {
        bool cleared;

        lock (_credentialLock)
        {
            if (_credentials is not AccessCredentials currentCredentials ||
                !string.Equals(currentCredentials.RefreshToken, refreshToken, StringComparison.Ordinal))
            {
                unauthenticatedEventArgs = null;
                return false;
            }

            _credentials = null;
            _credentialGeneration++;
            Service = OriginalService;

            unauthenticatedEventArgs = currentCredentials.Did is Did did
                ? new UnauthenticatedEventArgs(did, currentCredentials.Service)
                : null;

            cleared = true;
        }

        // The remembered tokens record exchanges made by the session which has just ended, so they say nothing about
        // whatever session comes next. Left in place, a later login whose refresh token happened to match one of them
        // would have its first refresh treated as already spent and be cleared immediately.
        ForgetExchangedRefreshTokens();

        return cleared;
    }

    /// <summary>
    /// Gets a value indicating whether a refresh of a credential issued to <paramref name="expectedDid"/> would be a refresh
    /// of an account other than the one the agent is currently authenticated as.
    /// </summary>
    /// <param name="expectedDid">The <see cref="Did"/> the credential being refreshed was issued to, if it is known.</param>
    /// <param name="currentDid">The <see cref="Did"/> the agent is currently authenticated as, when the actors differ.</param>
    /// <returns><see langword="true"/> if the actors are known to differ, otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    ///   Checked before the token endpoint is called, and not only afterwards in
    ///   <see cref="TryPublishRefreshedCredentials(AccessCredentials, long, out bool)"/>. By the time the result comes back
    ///   the refresh token has been exchanged and recorded as spent, so rejecting only at that point would leave the
    ///   caller's stored session destroyed rather than untouched: the old token no longer works and its replacement has
    ///   been discarded. Declining up front leaves the supplied credential exactly as it was and still usable elsewhere.
    /// </para>
    /// <para>
    ///   The check is unavoidably advisory. A credential which carries no <see cref="Did"/> cannot be attributed to an
    ///   actor without exchanging it, so the post-response check remains as the backstop for that, for a racing login and
    ///   for an authorization server which answers with a token for the wrong subject.
    /// </para>
    /// </remarks>
    private bool IsRefreshForADifferentActor(Did? expectedDid, [NotNullWhen(true)] out Did? currentDid)
    {
        currentDid = null;

        if (expectedDid is null)
        {
            return false;
        }

        lock (_credentialLock)
        {
            if (_credentials is AccessCredentials currentCredentials &&
                currentCredentials.Did is Did agentDid &&
                agentDid != expectedDid)
            {
                currentDid = agentDid;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Announces credentials a refresh has already committed to the agent, raising <see cref="Authenticated"/> first when
    /// the refresh started a session.
    /// </summary>
    /// <param name="credentials">The credentials the agent is now holding.</param>
    /// <param name="sessionStarted">Whether the refresh started a session, rather than replacing the credentials of one.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    ///   The credentials are live in the agent before either event is raised, and the refresh token they replaced has
    ///   already been spent, so the credentials notification is the only thing which can bring durable storage back into
    ///   step. It therefore runs even when an <see cref="Authenticated"/> subscriber throws; without that a subscriber
    ///   failing on the session start would leave storage holding a refresh token the server will no longer honour.
    /// </para>
    /// <para>
    ///   The original subscriber exception is propagated once the notification has been made. A failure of the
    ///   notification itself is logged rather than thrown, so it cannot displace the exception the caller needs to see.
    /// </para>
    /// </remarks>
    private async Task RaiseSessionStartAndCredentialsUpdatedAsync(AccessCredentials credentials, bool sessionStarted, CancellationToken cancellationToken)
    {
        try
        {
            if (sessionStarted)
            {
                await RaiseAuthenticatedAsync(new AuthenticatedEventArgs(
                    credentials.Did,
                    credentials.Service,
                    credentials)).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            try
            {
                await RaiseCredentialsUpdatedAsync(credentials, credentialsCommitted: true, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception notificationException) when (notificationException is not OutOfMemoryException)
            {
                Logger.CommittedCredentialsNotificationThrew(_logger, notificationException);
            }

            throw;
        }

        await RaiseCredentialsUpdatedAsync(credentials, credentialsCommitted: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new instance of <see cref="OAuthClient"/>.
    /// </summary>
    /// <returns>The new <see cref="OAuthClient"/> instance.</returns>
    /// <remarks>
    /// <para>
    ///   This is the single place the agent builds the client it talks to an authorization server with, so overriding it
    ///   replaces the transport for every OAuth exchange the agent performs, including the background credential refresh.
    /// </para>
    /// </remarks>
    public virtual OAuthClient CreateOAuthClient()
    {
        return new OAuthClient(ConfigureHttpClient, OAuthProxyHttpMessageHandlerBuilder, LoggerFactory, Options?.OAuthOptions)
        {
            MaximumResponseSize = MaximumResponseSize
        };
    }

    /// <summary>
    /// Creates a new instance of <see cref="OAuthClient"/>.
    /// </summary>
    /// <param name="state">The state to restore in the <see cref="OAuthClient"/>.</param>
    /// <returns>The new <see cref="OAuthClient"/> instance, with the state restored from <paramref name="state"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null"/>.</exception>
    public OAuthClient CreateOAuthClient(OAuthLoginState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var oAuthClient = new OAuthClient(ConfigureHttpClient, OAuthProxyHttpMessageHandlerBuilder, LoggerFactory, Options?.OAuthOptions)
        {
            State = state,
            MaximumResponseSize = MaximumResponseSize
        };

        return oAuthClient;
    }

    /// <summary>
    /// Builds a preconfigured <see cref="HttpMessageHandler"/> to use for making requests during the OAuth flow, with SSRF protections in place.
    /// If a proxy is configured in <see cref="Options"/>, the handler will be configured to route requests through the proxy while still applying SSRF protections
    /// to the ultimate endpoints being called.
    /// If <see cref="OAuthOptions.AllowInsecureProtocols"/> or <see cref="OAuthOptions.AllowLoopback"/> is set, or the OAuth return URI configured in
    /// <see cref="Options"/> is using HTTP or is a loopback address, the handler will be configured to allow insecure protocols or
    /// loopback addresses respectively, but will still apply SSRF protections to all other endpoints.
    /// </summary>
    /// <returns>A preconfigured <see cref="HttpMessageHandler"/> instance.</returns>
    private HttpMessageHandler OAuthProxyHttpMessageHandlerBuilder()
    {
        SslClientAuthenticationOptions? sslOptions = null;

        if (_httpClientOptions?.CheckCertificateRevocationList == false)
        {
            sslOptions = new SslClientAuthenticationOptions
            {
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            };
        }

        bool allowInsecureProtocols = Options?.OAuthOptions?.AllowInsecureProtocolsOnTheWire == true;
        bool allowLoopback = Options?.OAuthOptions?.AllowLoopbackOnTheWire == true;

        if (_httpClientOptions is not null && _httpClientOptions.ProxyUri is not null)
        {
            ProxiedSsrfOptions options = new()
            {
                ConnectTimeout = _httpClientOptions.Timeout,
                AllowedSchemes = allowInsecureProtocols ? ["https", "http"] : ["https"],
                AllowLoopback = allowLoopback,
                AutomaticDecompression = DecompressionMethods.All,
                SslOptions = sslOptions,
                Proxy = new WebProxy(_httpClientOptions.ProxyUri)
            };

            return new ProxiedSsrfDelegatingHandler(
                options: options,
                loggerFactory: LoggerFactory);
        }
        else
        {
            SsrfOptions options = new()
            {
                ConnectTimeout = _httpClientOptions?.Timeout,
                AllowedSchemes = allowInsecureProtocols ? ["https", "http"] : ["https"],
                AllowLoopback = allowLoopback,
                AutomaticDecompression = DecompressionMethods.All,
                SslOptions = sslOptions
            };


            return SsrfSocketsHttpHandlerFactory.Create(
                options: options,
                loggerFactory: LoggerFactory);
        }
    }

    /// <summary>
    /// Builds an OAuth authorization URI for starting the OAuth flow.
    /// </summary>
    /// <param name="oAuthClient">An instance of <paramref name="oAuthClient"/> to build the URI in.</param>
    /// <param name="handle">The handle to authorize for.</param>
    /// <param name="scopes">A collection of scopes to request. Defaults to "atproto".</param>
    /// <param name="returnUri">The URI the OAuth server should post back to when it has authorized the application.</param>
    /// <param name="uriExtraParameters">Any extra parameters to attach to the URI.</param>
    /// <param name="stateExtraProperties">Any extra properties to save in state.</param>
    /// <param name="validateDiscoveredEndpoints">Flag indicating whether to validate discovered endpoints.</param>
    /// <param name="validatePds">A callback to validate the PDS URI discovered for <paramref name="handle"/>. If <paramref name="validateDiscoveredEndpoints" /> is <see langword="true"/> and this parameter is not specified, a default validation callback will be used that implements simple SSRF protections.</param>
    /// <param name="validateAuthorizationServer">A callback to validate the authorization server discovered for the PDS for <paramref name="handle"/>. If <paramref name="validateDiscoveredEndpoints" /> is <see langword="true"/> and this parameter is not specified, a default validation callback will be used that implements simple SSRF protections.</param>
    /// <param name="allowInsecureProtocols">Flag indicating whether HTTP is allowed for authorization servers and personal data servers.</param>
    /// <param name="allowLoopback">Flag indicating whether loopback addresses are allowed for authorization servers and personal data servers.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="oAuthClient"/> or <paramref name="handle"/> is <see langword="null"/>, or
    /// <paramref name="scopes"/> or <paramref name="returnUri"/>is not specified and is not configured on the agent options.
    /// </exception>
    /// <exception cref="OAuthException">
    /// Thrown when the OAuth options on the agent have not been configured, or
    /// the specified <paramref name="handle"/> cannot be resolved, or
    /// the PDS for the specified <paramref name="handle"/> cannot be resolved, or
    /// the authorization server for <paramref name="handle"/> cannot be discovered.
    /// </exception>
    /// <remarks>
    /// <para>
    ///   <paramref name="allowInsecureProtocols"/> and <paramref name="allowLoopback"/> apply to the validation of the endpoints
    ///   discovered for <paramref name="handle"/>, and are combined with <see cref="OAuthOptions.AllowInsecureProtocols"/> and
    ///   <see cref="OAuthOptions.AllowLoopback"/>, which apply to both that validation and the transport the requests are then
    ///   made over. Setting either option is therefore enough to reach an authorization server or personal data server over HTTP
    ///   or on the loopback interface, as the localhost client development setup at
    ///   <see href="https://atproto.com/specs/oauth#clients">atproto.com</see> needs.
    /// </para>
    /// <para>
    ///   Passing these parameters alone relaxes only the validation, so an endpoint the agent will then refuse to connect to can
    ///   still be admitted, unless the configured OAuth return uri already allows it. A return uri which itself uses HTTP or is a
    ///   loopback address relaxes the transport, because an application whose own callback is not served over HTTPS is by
    ///   definition not in a position to require it, but it does not relax the validation.
    /// </para>
    /// </remarks>
    public async Task<Uri> BuildOAuth2LoginUri(
        OAuthClient oAuthClient,
        Handle handle,
        IEnumerable<string>? scopes = null,
        Uri? returnUri = null,
        IEnumerable<KeyValuePair<string, string>>? uriExtraParameters = null,
        Dictionary<string, string>? stateExtraProperties = null,
        bool validateDiscoveredEndpoints = true,
        Func<Uri, bool, bool, ILoggerFactory?, CancellationToken, Task<bool>>? validatePds = null,
        Func<Uri, bool, bool, ILoggerFactory?, CancellationToken, Task<bool>>? validateAuthorizationServer = null,
        bool allowInsecureProtocols = false,
        bool allowLoopback = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(oAuthClient);
        ArgumentNullException.ThrowIfNull(handle);

        if (Options is null || Options.OAuthOptions is null)
        {
            throw new OAuthException("OAuth options are not configured.");
        }

        returnUri ??= Options.OAuthOptions.ReturnUri;
        ArgumentNullException.ThrowIfNull(returnUri);

        Options.OAuthOptions.Validate();

        scopes ??= Options.OAuthOptions.Scopes;
        ArgumentNullException.ThrowIfNull(scopes);

        if (validateDiscoveredEndpoints)
        {
            validatePds ??= SecurityHelpers.DefaultDiscoveryUriValidator;
            validateAuthorizationServer ??= SecurityHelpers.DefaultDiscoveryUriValidator;
        }

        allowInsecureProtocols = allowInsecureProtocols || Options.OAuthOptions.AllowInsecureProtocols;
        allowLoopback = allowLoopback || Options.OAuthOptions.AllowLoopback;

        Did? did = await ResolveHandle(handle, cancellationToken).ConfigureAwait(false) ?? throw new OAuthException("Could not resolve DID");
        Uri? pds = await ResolvePds(did, cancellationToken).ConfigureAwait(false) ?? throw new OAuthException($"Could not resolve PDS for {did}.");

        if (validatePds is not null &&
            !await validatePds.Invoke(pds, allowInsecureProtocols, allowLoopback, LoggerFactory, cancellationToken).ConfigureAwait(false))
        {
            throw new OAuthException($"The discovered PDS {pds} did not pass validation.");
        }

        Uri? authorizationServer = await ResolveAuthorizationServer(pds, cancellationToken).ConfigureAwait(false) ?? throw new OAuthException($"Could not discover authorization server for {handle}.");

        if (validateAuthorizationServer is not null &&
            !await validateAuthorizationServer.Invoke(authorizationServer, allowInsecureProtocols, allowLoopback, LoggerFactory, cancellationToken).ConfigureAwait(false))
        {
            throw new OAuthException($"The discovered authorization server {authorizationServer} did not pass validation.");
        }

        return await oAuthClient.BuildOAuth2LoginUri(
            service: pds,
            returnUri: returnUri,
            authority: authorizationServer,
            scopes: scopes,
            handle: handle,
            uriExtraParameters: uriExtraParameters,
            stateExtraProperties: stateExtraProperties,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds an OAuth logout URI.
    /// </summary>
    /// <param name="oAuthClient">An instance of <paramref name="oAuthClient"/> to build the URI in.</param>
    /// <param name="returnUri">The URI the oauth server should post back to when it has authorized the application.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="returnUri"/>is not specified and is not configured on the agent.<see cref="Options"/>.
    /// </exception>
    /// <exception cref="OAuthException">
    /// Thrown when the OAuth options on the agent have not been configured, or
    /// the current agent credentials are not OAuth credentials.
    /// </exception>

    internal async Task<Uri> BuildOAuth2LogoutUri(
        OAuthClient oAuthClient,
        Uri? returnUri = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        if (Credentials is not DPoPAccessCredentials credentials)
        {
            throw new OAuthException("Session credentials are not DPoP access credentials.");
        }

        ArgumentNullException.ThrowIfNull(oAuthClient);
        if (Options is null || Options.OAuthOptions is null)
        {
            throw new OAuthException("OAuth options are not configured.");
        }

        Uri? pds = await ResolvePds(Credentials.Did, cancellationToken).ConfigureAwait(false) ?? throw new OAuthException($"Could not resolve PDS for {Credentials.Did}.");
        Uri? authorizationServer = await ResolveAuthorizationServer(pds, cancellationToken).ConfigureAwait(false) ?? throw new OAuthException($"Could not discover authorization server for {Credentials.Did}.");

        returnUri ??= Options.OAuthOptions.ReturnUri;
        ArgumentNullException.ThrowIfNull(returnUri);

        Options.OAuthOptions.Validate();

        return await oAuthClient.BuildOAuth2LogoutUri(
            credentials,
            authority: authorizationServer,
            returnUri: returnUri,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Processes the login response received from the client URI generated from CreateOAuth2StartUri() and sets the agent credentials if successful.
    /// </summary>
    /// <param name="oAuthClient">An instance of <paramref name="oAuthClient"/> to build the URI in.</param>
    /// <param name="callbackData">The data returned to the callback URI</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="OAuthException">Thrown when the internal state of this instance is faulty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="oAuthClient"/> is <see langword="null"/>.</exception>
    public async Task<bool> ProcessOAuth2LoginResponse(OAuthClient oAuthClient, string callbackData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(oAuthClient);
        DPoPAccessCredentials? accessCredentials = await oAuthClient.ProcessOAuth2LoginResponse(callbackData, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (accessCredentials is not null)
        {
            return await Login(accessCredentials, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Get a <see cref="ServiceCredential"/> on behalf of the requesting DID for the specified <paramref name="service"/>.
    /// </summary>
    /// <param name="service">The server to request the service authentication from.</param>
    /// <param name="lxm">Lexicon (XRPC) method to bind the requested token to</param>
    /// <param name="expiry">An optional length of the time the token should be valid for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when any of <paramref name="service"/> or <paramref name="lxm"/> are <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="expiry"/> is specified but is zero or negative.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ServiceCredential>> GetServiceAuth(
        Uri service,
        Nsid lxm,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(lxm);

        // Get the server description so we can get the DID of the server.
        AtProtoHttpResult<ServerDescription> serverDescriptionResult = await DescribeServer(service, cancellationToken).ConfigureAwait(false);
        if (serverDescriptionResult.Succeeded)
        {
            return await GetServiceAuth(service, serverDescriptionResult.Result.Did, lxm, expiry, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Logger.GetServiceAuthCannotGetServiceDescription(_logger, service);
            return new AtProtoHttpResult<ServiceCredential>(
                null,
                serverDescriptionResult.StatusCode,
                serverDescriptionResult.HttpResponseHeaders,
                serverDescriptionResult.AtErrorDetail,
                serverDescriptionResult.RateLimit);
        }
    }

    /// <summary>
    /// Get a <see cref="ServiceCredential"/> on behalf of the requesting DID for the requested <paramref name="audience"/>.
    /// </summary>
    /// <param name="service">The server to request the service authentication from.</param>
    /// <param name="audience">The DID of the service that the token will be used to authenticate with.</param>
    /// <param name="lxm">Lexicon (XRPC) method to bind the requested token to</param>
    /// <param name="expiry">An optional length of the time the token should be valid for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when any of <paramref name="service"/>, <paramref name="audience"/> or <paramref name="lxm"/> are <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="expiry"/> is specified but is zero or negative.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ServiceCredential>> GetServiceAuth(
        Uri service,
        Did audience,
        Nsid lxm,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(audience);
        ArgumentNullException.ThrowIfNull(lxm);

        if (expiry is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiry.Value.TotalSeconds, 0);
        }

        using (_logger.BeginScope($"GetServiceAuth()"))
        {
            if (expiry is not null)
            {
                Logger.RequestingServiceAuthToken(_logger, Service, audience, expiry.Value.ToString("c"), lxm);
            }
            else
            {
                Logger.RequestingServiceAuthTokenNoExpirySpecified(_logger, Service, audience, lxm);
            }

            if (!IsAuthenticated)
            {
                Logger.GetServiceAuthFailedAsSessionIsAnonymous(_logger, Service);

                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<ServiceCredential> serviceCredentialResult = await AtProtoServer.GetServiceAuth(
                audience: audience,
                expiry: expiry,
                lxm: lxm,
                service: service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                loggerFactory: LoggerFactory,
                credentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                maximumResponseSize: MaximumResponseSize,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (serviceCredentialResult.Succeeded && serviceCredentialResult.Result.AccessJwt is not null)
            {
                TimeSpan expiresIn = GetTimeToJwtTokenExpiry(serviceCredentialResult.Result.AccessJwt);
                Logger.ServiceAuthTokenAcquired(_logger, service, audience, expiresIn.ToString("c"), lxm);
            }
            else
            {
                Logger.ServiceAuthTokenAcquisitionFailed(
                    _logger,
                    service,
                    Did,
                    audience,
                    lxm,
                    serviceCredentialResult.StatusCode,
                    serviceCredentialResult.AtErrorDetail?.Error,
                    serviceCredentialResult.AtErrorDetail?.Message);
            }

            return serviceCredentialResult;
        }
    }

    /// <summary>
    /// Gets information about the session associated with the access token provided.
    /// </summary>
    /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<Session>> GetSession(
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            Logger.GetSessionFailedAsSessionIsAnonymous(_logger, Service);

            throw new AuthenticationRequiredException();
        }

        return await GetSession(Credentials, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets information about the session associated with the access token provided.
    /// </summary>
    /// <param name="accessCredentials">The access credentials to authenticate with.</param>
    /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="accessCredentials"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="accessCredentials"/>'s AccessJwt is <see langword="null"/> or empty.</exception>
    public async Task<AtProtoHttpResult<Session>> GetSession(
        AccessCredentials accessCredentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentException.ThrowIfNullOrEmpty(accessCredentials.AccessJwt);

        return await AtProtoServer.GetSession(accessCredentials, HttpClient, InternalOnCredentialsUpdatedCallBack, LoggerFactory, MaximumResponseSize, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the token endpoint <see cref="Uri"/> for the specified <paramref name="authorizationServer"/>.
    /// </summary>
    /// <param name="authorizationServer">The <see cref="Uri"/> of the the authorization server whose token endpoint uri should be retrieved.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="authorizationServer"/> is <see langword="null"/>.</exception>
    public async Task<Uri?> GetTokenEndpoint(Uri authorizationServer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorizationServer);

        Uri? tokenEndpoint = null;

        if (!cancellationToken.IsCancellationRequested)
        {
            using (Stream responseStream = await HttpClient.GetStreamAsync(new Uri($"https://{authorizationServer.Host}/.well-known/oauth-authorization-server"), cancellationToken).ConfigureAwait(false))
            using (JsonDocument protectedResultMetadata = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (!cancellationToken.IsCancellationRequested && protectedResultMetadata is not null)
                {
                    string? tokenEndpointValue = protectedResultMetadata.RootElement.GetProperty("token_endpoint").GetString();

                    if (!cancellationToken.IsCancellationRequested && !string.IsNullOrWhiteSpace(tokenEndpointValue))
                    {
                        tokenEndpoint = new Uri(tokenEndpointValue);
                    }
                }
            }
        }

        return tokenEndpoint;
    }

    /// <summary>
    /// Resolves the revocation endpoint <see cref="Uri"/> for the specified <paramref name="authorizationServer"/>.
    /// </summary>
    /// <param name="authorizationServer">The <see cref="Uri"/> of the the authorization server whose token endpoint uri should be retrieved.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="authorizationServer"/> is <see langword="null"/>.</exception>
    public async Task<Uri?> GetRevocationEndpoint(Uri authorizationServer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorizationServer);

        Uri? tokenEndpoint = null;

        if (!cancellationToken.IsCancellationRequested)
        {
            using (Stream responseStream = await HttpClient.GetStreamAsync(new Uri($"https://{authorizationServer.Host}/.well-known/oauth-authorization-server"), cancellationToken).ConfigureAwait(false))
            using (JsonDocument protectedResultMetadata = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (!cancellationToken.IsCancellationRequested && protectedResultMetadata is not null)
                {
                    string? tokenEndpointValue = protectedResultMetadata.RootElement.GetProperty("revocation_endpoint").GetString();

                    if (!cancellationToken.IsCancellationRequested && !string.IsNullOrWhiteSpace(tokenEndpointValue))
                    {
                        tokenEndpoint = new Uri(tokenEndpointValue);
                    }
                }
            }
        }

        return tokenEndpoint;
    }

    /// <summary>
    /// Authenticates to and creates a session on the <paramref name="service"/> with the specified <paramref name="identifier"/> and <paramref name="password"/>.
    /// </summary>
    /// <param name="identifier">The identifier used to authenticate.</param>
    /// <param name="password">The password used to authenticated.</param>
    /// <param name="authFactorToken">An optional multi factory authentication code.</param>
    /// <param name="service">The service to authenticate to.</param>
    /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="identifier" /> or <paramref name="password"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identifier" /> or <paramref name="password"/> is <see langword="null"/> or empty.</exception>
    public async Task<AtProtoHttpResult<bool>> Login(
        string identifier,
        string password,
        string? authFactorToken = null,
        Uri? service = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        AtIdentifier atIdentifier = AtIdentifier.Create(identifier);

        Handle? handle = atIdentifier as Handle;
        if (handle is not null)
        {
            return await Login(
                handle: handle,
                password: password,
                authFactorToken: authFactorToken,
                service: service,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        Did? did = atIdentifier as Did;
        if (did is not null)
        {
            return await Login(
                did: did,
                password: password,
                authFactorToken: authFactorToken,
                service: service,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        throw new ArgumentException($"{identifier} is not a valid handle or did", nameof(identifier));
    }

    /// <summary>
    /// Authenticates to and creates a session on the <paramref name="service"/> with the specified <paramref name="handle"/> and <paramref name="password"/>.
    /// </summary>
    /// <param name="handle">The handle used to authenticate.</param>
    /// <param name="password">The password used to authenticated.</param>
    /// <param name="authFactorToken">An optional multi factory authentication code.</param>
    /// <param name="service">The service to authenticate to.</param>
    /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle" /> or <paramref name="password"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="password"/> is empty.</exception>
    /// <exception cref="SecurityTokenValidationException">Thrown when the token returned from the server is invalid.</exception>
    public async Task<AtProtoHttpResult<bool>> Login(
        Handle handle,
        string password,
        string? authFactorToken = null,
        Uri? service = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        using (_logger.BeginScope($"Handle/Password login for {handle}"))
        {
            bool succeeded = false;

            StopTokenRefreshTimer();

            try
            {
                if (service is null)
                {
                    Did? userDid = await ResolveHandle(handle, cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (userDid is null || cancellationToken.IsCancellationRequested)
                    {
                        return new AtProtoHttpResult<bool>(
                            false,
                            HttpStatusCode.NotFound,
                            null,
                            new AtErrorDetail() { Error = "HandleNotResolvable", Message = "Handle could not be resolved to a DID." });
                    }

                    Uri? pds = null;

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        pds = await ResolvePds(userDid, cancellationToken).ConfigureAwait(false);
                    }

                    if (pds is null || cancellationToken.IsCancellationRequested)
                    {
                        return new AtProtoHttpResult<bool>(
                            false,
                            HttpStatusCode.NotFound,
                            null,
                            new AtErrorDetail() { Error = "PdsNotResolvable", Message = $"Could not resolve a PDS for {userDid}." });
                    }

                    service = pds;
                }

                Logger.CreateSessionCalled(_logger, handle.ToString(), service);

                AtProtoHttpResult<Session> createSessionResult =
                    await AtProtoServer.CreateSession(
                        handle.ToString(),
                        password,
                        authFactorToken,
                        service,
                        httpClient: HttpClient,
                        loggerFactory: LoggerFactory,
                        maximumResponseSize: MaximumResponseSize,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                Logger.CreateSessionReturned(_logger, createSessionResult.StatusCode);

                if (createSessionResult.Succeeded)
                {
                    if (!await ValidateJwtToken(createSessionResult.Result.AccessJwt, createSessionResult.Result.Did, service).ConfigureAwait(false))
                    {
                        Logger.CreateSessionJwtValidationFailed(_logger);
                        throw new SecurityTokenValidationException("The issued access token could not be validated.");
                    }

                    AuthenticationType authenticationType = AuthenticationType.UsernamePassword;
                    if (!string.IsNullOrWhiteSpace(authFactorToken))
                    {
                        authenticationType = AuthenticationType.UsernamePasswordAuthFactorToken;
                    }

                    AccessCredentials accessCredentials = new(
                            service,
                            authenticationType,
                            createSessionResult.Result.AccessJwt,
                            createSessionResult.Result.RefreshJwt);

                    await InternalLogin(accessCredentials).ConfigureAwait(false);

                    succeeded = true;

                    return new AtProtoHttpResult<bool>()
                    {
                        Result = true,
                        StatusCode = createSessionResult.StatusCode,
                        AtErrorDetail = createSessionResult.AtErrorDetail,
                        RateLimit = createSessionResult.RateLimit
                    };
                }
                else
                {
                    Logger.CreateSessionFailed(_logger, createSessionResult.StatusCode);

                    StopTokenRefreshTimer();

                    await ClearCredentialsAndRaiseUnauthenticatedAsync().ConfigureAwait(false);

                    return new AtProtoHttpResult<bool>
                    {
                        Result = false,
                        StatusCode = createSessionResult.StatusCode,
                        AtErrorDetail = createSessionResult.AtErrorDetail,
                        RateLimit = createSessionResult.RateLimit
                    };
                }
            }
            finally
            {
                RestoreTokenRefreshTimerAfterFailure(succeeded);
            }
        }
    }

    /// <summary>
    /// Authenticates to and creates a session on the <paramref name="service"/> with the specified <paramref name="did"/> and <paramref name="password"/>.
    /// </summary>
    /// <param name="did">The identifier used to authenticate.</param>
    /// <param name="password">The password used to authenticated.</param>
    /// <param name="authFactorToken">An optional multi factory authentication code.</param>
    /// <param name="service">The service to authenticate to.</param>
    /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="did" /> or <paramref name="password"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="SecurityTokenValidationException">Thrown when the token returned from the server is invalid.</exception>
    public async Task<AtProtoHttpResult<bool>> Login(
        Did did,
        string password,
        string? authFactorToken = null,
        Uri? service = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        using (_logger.BeginScope($"Did/Password login for {did}"))
        {
            bool succeeded = false;

            StopTokenRefreshTimer();

            try
            {
                if (service is null)
                {
                    Uri? pds = null;

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        pds = await ResolvePds(did, cancellationToken).ConfigureAwait(false);
                    }

                    if (pds is null || cancellationToken.IsCancellationRequested)
                    {
                        return new AtProtoHttpResult<bool>(
                            false,
                            HttpStatusCode.NotFound,
                            null,
                            new AtErrorDetail() { Error = "PdsNotResolvable", Message = $"Could not resolve a PDS for {did}." });
                    }

                    service = pds;
                }

                Logger.CreateSessionCalled(_logger, did.ToString(), service);

                AtProtoHttpResult<Session> createSessionResult =
                    await AtProtoServer.CreateSession(
                        did,
                        password,
                        authFactorToken,
                        service,
                        httpClient: HttpClient,
                        loggerFactory: LoggerFactory,
                        maximumResponseSize: MaximumResponseSize,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                Logger.CreateSessionReturned(_logger, createSessionResult.StatusCode);

                if (createSessionResult.Succeeded)
                {
                    if (!await ValidateJwtToken(createSessionResult.Result.AccessJwt, createSessionResult.Result.Did, service).ConfigureAwait(false))
                    {
                        Logger.CreateSessionJwtValidationFailed(_logger);
                        throw new SecurityTokenValidationException("The issued access token could not be validated.");
                    }

                    AuthenticationType authenticationType = AuthenticationType.UsernamePassword;
                    if (!string.IsNullOrWhiteSpace(authFactorToken))
                    {
                        authenticationType = AuthenticationType.UsernamePasswordAuthFactorToken;
                    }

                    AccessCredentials accessCredentials = new(
                            service,
                            authenticationType,
                            createSessionResult.Result.AccessJwt,
                            createSessionResult.Result.RefreshJwt);

                    await InternalLogin(accessCredentials).ConfigureAwait(false);

                    succeeded = true;

                    return new AtProtoHttpResult<bool>()
                    {
                        Result = true,
                        StatusCode = createSessionResult.StatusCode,
                        AtErrorDetail = createSessionResult.AtErrorDetail,
                        RateLimit = createSessionResult.RateLimit
                    };
                }
                else
                {
                    Logger.CreateSessionFailed(_logger, createSessionResult.StatusCode);

                    StopTokenRefreshTimer();

                    await ClearCredentialsAndRaiseUnauthenticatedAsync().ConfigureAwait(false);

                    return new AtProtoHttpResult<bool>
                    {
                        Result = false,
                        StatusCode = createSessionResult.StatusCode,
                        AtErrorDetail = createSessionResult.AtErrorDetail,
                        RateLimit = createSessionResult.RateLimit
                    };
                }
            }
            finally
            {
                RestoreTokenRefreshTimerAfterFailure(succeeded);
            }
        }
    }


    /// <summary>
    /// Sets the agent credentials to the specified <paramref name="accessCredentials"/>.
    /// </summary>
    /// <param name="accessCredentials"><see cref="AccessCredentials"/> to use when authenticating to the service.</param>
    /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessCredentials"/> or any of its properties are <see langword="null"/>.</exception>
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Matching other login methods, so keeping cancellationToken for ease of use.")]
    public async Task<bool> Login(
        AccessCredentials accessCredentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(accessCredentials.AccessJwt);
        ArgumentNullException.ThrowIfNull(accessCredentials.Did);
        ArgumentNullException.ThrowIfNull(accessCredentials.RefreshToken);
        ArgumentNullException.ThrowIfNull(accessCredentials.Service);

        if (accessCredentials is DPoPAccessCredentials dPoPAccessCredentials)
        {
            return await Login(dPoPAccessCredentials, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        Logger.AgentAuthenticatedWithOAuthCredentials(_logger, accessCredentials.Did, accessCredentials.Service);

        await InternalLogin(accessCredentials).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Sets the agent credentials to the specified <paramref name="dPoPAccessCredentials"/>.
    /// </summary>
    /// <param name="dPoPAccessCredentials"><see cref="DPoPAccessCredentials"/> to use when authenticating to the service.</param>
    /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dPoPAccessCredentials"/> or any of its properties are <see langword="null"/>.</exception>
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Matching other login methods.")]
    public async Task<bool> Login(
        DPoPAccessCredentials dPoPAccessCredentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dPoPAccessCredentials);
        ArgumentNullException.ThrowIfNull(dPoPAccessCredentials.AccessJwt);
        ArgumentNullException.ThrowIfNull(dPoPAccessCredentials.Did);
        ArgumentNullException.ThrowIfNull(dPoPAccessCredentials.RefreshToken);
        ArgumentNullException.ThrowIfNull(dPoPAccessCredentials.Service);
        ArgumentNullException.ThrowIfNull(dPoPAccessCredentials.DPoPProofKey);
        ArgumentNullException.ThrowIfNull(dPoPAccessCredentials.DPoPNonce);

        Logger.AgentAuthenticatedWithDPoPOAuthCredentials(_logger, dPoPAccessCredentials.Did, dPoPAccessCredentials.Service);

        await InternalLogin(dPoPAccessCredentials).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Clears the internal session state used by the agent and tells the service for this agent's current session to cancel the session.
    /// </summary>
    /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <exception cref="CredentialException">Thrown when the current agent authentication state does not have enough information to call the DeleteSession API.</exception>
    /// <exception cref="LogoutException">Thrown when the DeleteSession API call fails.</exception>
    /// <exception cref="OAuthException">Thrown if the OAuth configuration on the agent is not specified or is not configured on the agent options.</exception>
    /// <remarks>
    /// <para>
    ///   The logout runs whilst the refresh semaphore is held, so a credential refresh cannot revoke one set of tokens and
    ///   then have another set published over the cleared credentials, leaving the agent authenticated against a session the
    ///   logout ended. A refresh which is already in flight when the logout starts finds the credentials have moved on and
    ///   discards what it was issued.
    /// </para>
    /// </remarks>
    public async Task Logout(CancellationToken cancellationToken = default)
    {
        await _credentialRefreshSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await InternalLogout(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _credentialRefreshSemaphore.Release();
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    private async Task InternalLogout(CancellationToken cancellationToken = default)
    {
        // The agent credentials are read once. Logout spans several awaits, and re-reading the property would let a
        // concurrent logout, or a background refresh landing part way through, revoke one credential having checked
        // another, or null the property and leave the later reads throwing a NullReferenceException.
        AccessCredentials? credentials = Credentials;

        if (credentials is null)
        {
            return;
        }

        if (credentials.AuthenticationType != AuthenticationType.UsernamePassword &&
            credentials.AuthenticationType != AuthenticationType.UsernamePasswordAuthFactorToken)
        {
            // Call revocation openid-connect endpoint
            if (credentials is not DPoPAccessCredentials accessCredentials)
            {
                throw new CredentialException("Credential type is OAuth but it cannot be converted to DPoPAccessCredentials.");
            }

            Uri? authorizationService = await ResolveAuthorizationServer(accessCredentials.Service, cancellationToken).ConfigureAwait(false) ??
                throw new CredentialException($"Could not resolve authorization server for {accessCredentials.Service}");

            Uri? revocationEndpoint = await GetRevocationEndpoint(authorizationService, cancellationToken).ConfigureAwait(false) ??
                throw new CredentialException($"Could not resolve revocation endpoint for {authorizationService}");

            if (Options is null || Options.OAuthOptions is null)
            {
                throw new OAuthException("OAuth options are not configured.");
            }

            Options.OAuthOptions.Validate();

            string scopeString = string.Join(" ", Options.OAuthOptions.Scopes.Where(s => !string.IsNullOrEmpty(s)));

            string clientId = Options.OAuthOptions.ClientId;

            // Special case the client ID if it matches localhost to add the desired scope as query string parameters.
            // See Localhost Client Development at https://atproto.com/specs/oauth#clients.
            if (clientId == "http://localhost")
            {
                clientId = QueryHelpers.AddQueryString(clientId, "scope", scopeString);
            }

            Logger.LogoutCalled(_logger, credentials.Did, credentials.Service);

            bool succeeded = false;

            StopTokenRefreshTimer();

            try
            {
                AtProtoHttpClient<EmptyResponse> revokeRequest = new(LoggerFactory) { MaximumResponseSize = MaximumResponseSize };

                DPoPRevokeCredentials dPoPRevokeCredentials = new(
                    accessCredentials.Service,
                    accessCredentials.RefreshToken,
                    accessCredentials.DPoPProofKey,
                    string.Empty);

                // Revocation credential specific callback to update the DPoP nonce in credentials if the nonce needs updating ,
                // so that automatic retry in AtProtoHttpClient will have the updated nonce for the retry attempt.
                Task logoutCredentialsUpdated(AtProtoCredential credentials, CancellationToken token)
                {
                    ArgumentNullException.ThrowIfNull(credentials);

                    if (credentials is DPoPRevokeCredentials refreshedCredentials)
                    {
                        dPoPRevokeCredentials.DPoPNonce = refreshedCredentials.DPoPNonce;

                        Logger.OnCredentialUpdatedCallbackCalled(_logger);
                    }
                    else
                    {
                        throw new CredentialException("Logout credentials updated callback was called with credentials of an unexpected type.");
                    }

                    return Task.CompletedTask;
                }

                // First revoke the refresh token, then revoke the access token.
                using (var formData = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("token", accessCredentials.RefreshToken),
                        new KeyValuePair<string, string>("token_type_hint", "refresh_token"),
                        new KeyValuePair<string, string>("client_id", clientId),
                    ]))
                {
                    AtProtoHttpResult<EmptyResponse> revokeResponse = await revokeRequest.Post(
                        service: authorizationService,
                        endpoint: revocationEndpoint.AbsolutePath,
                        record: formData,
                        jsonSerializerOptions: AtProtoServer.AtProtoJsonSerializerOptions,
                        credentials: dPoPRevokeCredentials,
                        onCredentialsUpdated: logoutCredentialsUpdated,
                        httpClient: HttpClient,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (!revokeResponse.Succeeded)
                    {
                        Logger.RevokeFailed(_logger, credentials.Did, credentials.Service, revokeResponse.StatusCode, "refresh_token");

                        // The credentials are discarded even though the revocation failed, so that a failed logout does not
                        // leave an agent which still reports itself as authenticated. This matches the behaviour of a failed
                        // DeleteSession() below.
                        await ClearCredentialsAndRaiseUnauthenticatedAsync().ConfigureAwait(false);

                        throw new LogoutException()
                        {
                            StatusCode = revokeResponse.StatusCode,
                            Error = revokeResponse.AtErrorDetail
                        };
                    }
                }

                // Now revoke the access token.
                // Some authorization servers may not require this second call if revoking the refresh token also invalidates the access token,
                // but some may require both to be revoked to ensure the session is fully revoked, so calling both to be safe.
                dPoPRevokeCredentials.Token = accessCredentials.AccessJwt;

                using (var formData = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("token", accessCredentials.AccessJwt),
                        new KeyValuePair<string, string>("token_type_hint", "access_token"),
                        new KeyValuePair<string, string>("client_id", clientId),
                    ]))
                {
                    AtProtoHttpResult<EmptyResponse> revokeResponse = await revokeRequest.Post(
                        service: authorizationService,
                        endpoint: revocationEndpoint.AbsolutePath,
                        record: formData,
                        jsonSerializerOptions: AtProtoServer.AtProtoJsonSerializerOptions,
                        credentials: dPoPRevokeCredentials,
                        onCredentialsUpdated: logoutCredentialsUpdated,
                        httpClient: HttpClient,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (!revokeResponse.Succeeded)
                    {
                        Logger.RevokeFailed(_logger, credentials.Did, credentials.Service, revokeResponse.StatusCode, "access_token");

                        // The refresh token has already been revoked by this point, so the session is over whatever happens
                        // to the access token. Holding on to the credentials would leave the agent reporting itself as
                        // authenticated against a session which no longer exists.
                        await ClearCredentialsAndRaiseUnauthenticatedAsync().ConfigureAwait(false);

                        throw new LogoutException()
                        {
                            StatusCode = revokeResponse.StatusCode,
                            Error = revokeResponse.AtErrorDetail
                        };
                    }
                }

                succeeded = true;

                await ClearCredentialsAndRaiseUnauthenticatedAsync().ConfigureAwait(false);
            }
            finally
            {
                RestoreTokenRefreshTimerAfterFailure(succeeded);
            }
        }
        else
        {
            if (credentials.Did is null || credentials.Service is null || credentials.RefreshToken is null)
            {
                throw new CredentialException("agent.Credentials is missing information needed to call DeleteSession");
            }

            Logger.LogoutCalled(_logger, credentials.Did, credentials.Service);

            bool succeeded = false;

            StopTokenRefreshTimer();

            try
            {
                // Take the refresh token value from credentials and make it a specific refresh token.
                RefreshCredential refreshCredential = new(credentials);

                AtProtoHttpResult<EmptyResponse> deleteSessionResult =
                    await AtProtoServer.DeleteSession(refreshCredential, HttpClient, LoggerFactory, MaximumResponseSize, cancellationToken).ConfigureAwait(false);

                if (deleteSessionResult.Succeeded)
                {
                    succeeded = true;

                    await ClearCredentialsAndRaiseUnauthenticatedAsync().ConfigureAwait(false);
                }
                else
                {
                    Logger.LogoutFailed(_logger, credentials.Did, credentials.Service, deleteSessionResult.StatusCode);

                    await ClearCredentialsAndRaiseUnauthenticatedAsync().ConfigureAwait(false);

                    throw new LogoutException()
                    {
                        StatusCode = deleteSessionResult.StatusCode,
                        Error = deleteSessionResult.AtErrorDetail
                    };
                }
            }
            finally
            {
                RestoreTokenRefreshTimerAfterFailure(succeeded);
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Acquires a new access and refresh token for the agent, and updates the agent <see cref="Credentials"/>.
    /// </summary>
    /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when agent is not authenticated.</exception>
    /// <exception cref="CredentialException">Thrown when agent credentials are not valid for refreshing.</exception>
    /// <exception cref="SecurityTokenValidationException">
    ///   Thrown when the issued access token could not be validated, or was issued for a different actor to the one the
    ///   agent is currently authenticated as.
    /// </exception>
    /// <remarks>
    /// <para>
    ///   The agent <see cref="Credentials"/> are read once. Reading them repeatedly would allow a background refresh
    ///   landing part way through to have the type checked on one credential and the refresh performed with another.
    /// </para>
    /// </remarks>
    public async Task<bool> RefreshCredentials(CancellationToken cancellationToken = default)
    {
        AccessCredentials? credentials = Credentials;

        if (credentials is null || credentials.RefreshToken is null)
        {
            Logger.RefreshCredentialsFailedNoSession(_logger);
            throw new AuthenticationRequiredException();
        }

        if (credentials.AuthenticationType == AuthenticationType.UsernamePassword ||
            credentials.AuthenticationType == AuthenticationType.UsernamePasswordAuthFactorToken)
        {
            return await RefreshSessionIssuedCredentials(credentials, credentials.Did, cancellationToken).ConfigureAwait(false);
        }
        else if (credentials.AuthenticationType == AuthenticationType.OAuth)
        {
            if (credentials is not DPoPAccessCredentials accessCredentials)
            {
                throw new CredentialException("Credential type is OAuth but it cannot be converted to DPoPAccessCredentials.");
            }

            DPoPRefreshCredential refreshCredential = new(accessCredentials.Service, accessCredentials.RefreshToken, accessCredentials.DPoPProofKey, accessCredentials.DPoPNonce);

            return await RefreshOAuthIssuedCredentials(refreshCredential, accessCredentials.Did, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new CredentialException(credentials);
        }
    }

    /// <summary>
    /// Acquires a new access and refresh token for the specified <paramref name="credential"/>, and updates the agent <see cref="Credentials"/>.
    /// </summary>
    /// <param name="credential">The credential to refresh.</param>
    /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when agent is not authenticated.</exception>
    /// <exception cref="CredentialException">Thrown when agent credentials are not valid for refreshing.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="credential"/> is <see langword="null"/>.</exception>
    /// <exception cref="SecurityTokenValidationException">
    ///   Thrown when the issued access token could not be validated, or was issued for a different actor to the one
    ///   <paramref name="credential"/> identifies.
    /// </exception>
    public async Task<bool> RefreshCredentials(AtProtoCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        // The DID the credential was issued for, where the caller supplied something which carries one, so that the tokens
        // the refresh token is exchanged for can be checked to belong to the same actor.
        Did? expectedDid = (credential as AccessCredentials)?.Did;

        // Create refresh credentials if passed access credentials.
        if (credential is DPoPAccessCredentials dPoPAccessCredentials)
        {
            credential = new DPoPRefreshCredential(dPoPAccessCredentials);
        }
        else if (credential is AccessCredentials accessCredentials)
        {
            credential = new RefreshCredential(accessCredentials);
        }

        return credential switch
        {
            DPoPRefreshCredential dPoPRefreshCredential => await RefreshOAuthIssuedCredentials(dPoPRefreshCredential, expectedDid, cancellationToken).ConfigureAwait(false),
            RefreshCredential refreshCredential => await RefreshSessionIssuedCredentials(refreshCredential, expectedDid, cancellationToken).ConfigureAwait(false),
            _ => throw new CredentialException(credential, "Cannot refresh credentials of this type."),
        };
    }

    /// <summary>
    /// Resolves the authorization server <see cref="Uri"/> for the specified <paramref name="handle"/>.
    /// </summary>
    /// <param name="handle">The handle of the account to resolve the authorization server for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="handle"/> is <see langword="null"/> or white space.</exception>
    public async Task<Uri?> ResolveAuthorizationServer(string handle, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);

        Did? did = await ResolveHandle(handle, cancellationToken).ConfigureAwait(false) ?? throw new ArgumentException($"{handle} cannot be resolved to a DID", nameof(handle));
        Uri? pds = await ResolvePds(did, cancellationToken).ConfigureAwait(false) ?? throw new ArgumentException($"PDS cannot be discovered for {handle}.", nameof(handle));
        Logger.ResolveAuthorizationServerCalled(_logger, pds);

        return await ResolveAuthorizationServer(pds, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the authorization server <see cref="Uri"/> for the specified <paramref name="pds"/>.
    /// </summary>
    /// <param name="pds">The <see cref="Uri"/> of the PDS to resolve the authorization server for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pds"/> is <see langword="null"/>.</exception>
    public async Task<Uri?> ResolveAuthorizationServer(Uri pds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pds);

        Logger.ResolveAuthorizationServerCalled(_logger, pds);

        Uri? authorizationServer = null;

        if (!cancellationToken.IsCancellationRequested)
        {
            // The PDS port, if any, must be preserved, otherwise discovery is performed against the wrong endpoint.
            Uri protectedResourceMetadataUri = new UriBuilder(
                Uri.UriSchemeHttps,
                pds.Host,
                pds.IsDefaultPort ? -1 : pds.Port,
                "/.well-known/oauth-protected-resource").Uri;

            bool allowInsecureProtocols = Options?.OAuthOptions?.ReturnUri is not null && Options.OAuthOptions.ReturnUri.Scheme == Uri.UriSchemeHttp;

            using (Stream responseStream = await HttpClient.GetStreamAsync(protectedResourceMetadataUri, cancellationToken).ConfigureAwait(false))
            using (JsonDocument protectedResultMetadata = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (!cancellationToken.IsCancellationRequested &&
                    protectedResultMetadata is not null &&
                    protectedResultMetadata.RootElement.TryGetProperty("authorization_servers", out JsonElement authorizationServers) &&
                    authorizationServers.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement authorizationServerElement in authorizationServers.EnumerateArray())
                    {
                        string? serverUri = authorizationServerElement.ValueKind == JsonValueKind.String ? authorizationServerElement.GetString() : null;

                        // The metadata comes from the PDS, so an entry which is not a usable, appropriately secured
                        // absolute URI is skipped rather than being turned into an exception or an insecure endpoint.
                        if (!string.IsNullOrEmpty(serverUri) &&
                            Uri.TryCreate(serverUri, UriKind.Absolute, out Uri? candidateAuthorizationServer) &&
                            (candidateAuthorizationServer.Scheme == Uri.UriSchemeHttps ||
                             (allowInsecureProtocols && candidateAuthorizationServer.Scheme == Uri.UriSchemeHttp)))
                        {
                            authorizationServer = candidateAuthorizationServer;
                            break;
                        }

                        Logger.ResolveAuthorizationServerSkippedEntry(_logger, pds, serverUri ?? string.Empty);
                    }
                }
            }
        }

        if (authorizationServer is not null)
        {
            Logger.ResolveAuthorizationServerDiscovered(_logger, pds, authorizationServer);
        }
        else
        {
            Logger.ResolveAuthorizationServerFailed(_logger, pds);
        }

        return authorizationServer;
    }

    private static TimeSpan GetTimeToJwtTokenExpiry(string jwt)
    {
        if (string.IsNullOrEmpty(jwt))
        {
            throw new ArgumentNullException(nameof(jwt));
        }

        JsonWebToken token = new(jwt);

        DateTimeOffset validUntil = DateTime.SpecifyKind(token.ValidTo, DateTimeKind.Utc);
        TimeSpan validityPeriod = validUntil - DateTimeOffset.UtcNow;

        return validityPeriod;
    }

    /// <summary>
    /// Publishes <paramref name="accessCredentials"/> as the agent credentials and starts background refresh for them.
    /// </summary>
    /// <param name="accessCredentials">The credentials the agent has just been authenticated with.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessCredentials"/>, or any of the properties it is read for, is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    ///   The credentials are published whilst the refresh semaphore is held, so a refresh of the previous credentials
    ///   cannot land between them being installed and the refresh timer being started for them. A refresh which is
    ///   already past that point when this runs is discarded when it finds the credentials have moved on.
    /// </para>
    /// </remarks>
    private async Task InternalLogin(AccessCredentials accessCredentials)
    {
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(accessCredentials.AccessJwt);
        ArgumentNullException.ThrowIfNull(accessCredentials.Did);
        ArgumentNullException.ThrowIfNull(accessCredentials.RefreshToken);
        ArgumentNullException.ThrowIfNull(accessCredentials.Service);

        UnauthenticatedEventArgs? replacedSession;

        await _credentialRefreshSemaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            lock (_credentialLock)
            {
                ObjectDisposedException.ThrowIf(_atProtoAgentDisposed, this);

                // Read and replaced in one step so that the session being displaced is described exactly as it was
                // when it was displaced, and cannot be reported twice by two logins racing each other.
                replacedSession = _credentials is AccessCredentials replaced && replaced.Did is Did replacedDid
                    ? new UnauthenticatedEventArgs(replacedDid, replaced.Service)
                    : null;

                _credentials = accessCredentials;
                _credentialGeneration++;
                Service = accessCredentials.Service;
            }

            // Any remembered token records an exchange made by a session which is not the one being started, whether
            // this login displaced that session or it had already ended, so none of them describe the new session.
            // Retaining them would let a refresh token matching one be treated as already spent on its first use.
            ForgetExchangedRefreshTokens();

            StartTokenRefreshTimer();
        }
        finally
        {
            _credentialRefreshSemaphore.Release();
        }

        // Raised outside the refresh semaphore so that a handler which calls back into the agent cannot deadlock it,
        // and ordered behind any credential update notification still running so that a handler suspended part way
        // through persisting the previous session cannot finish afterwards and write it over this one.
        if (replacedSession is not null)
        {
            await RaiseUnauthenticatedAsync(replacedSession).ConfigureAwait(false);
        }

        await RaiseAuthenticatedAsync(new AuthenticatedEventArgs(
            accessCredentials.Did,
            accessCredentials.Service,
            accessCredentials)).ConfigureAwait(false);
    }

    internal async Task<bool> RefreshOAuthIssuedCredentials(DPoPRefreshCredential refreshCredential, Did? expectedDid = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refreshCredential);
        ArgumentNullException.ThrowIfNull(refreshCredential.Service);

        ArgumentException.ThrowIfNullOrWhiteSpace(refreshCredential.RefreshToken);

        if (Options is null || Options.OAuthOptions is null)
        {
            throw new OAuthException("OAuthOptions have not been configured.");
        }

        Options.OAuthOptions.Validate();

        if (refreshCredential.AuthenticationType != AuthenticationType.OAuth)
        {
            throw new CredentialException(refreshCredential);
        }

        // Declined before anything is sent to the token endpoint, so the supplied credential is left unspent and still
        // usable. Rejecting only after the exchange would destroy it.
        if (IsRefreshForADifferentActor(expectedDid, out Did? agentDid))
        {
            Logger.RefreshedCredentialsDiscardedAsTheyAreForADifferentActor(_logger, expectedDid!, agentDid);

            return false;
        }

        string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshCredential.RefreshToken)));

        AccessCredentials? credentialsToNotify = null;
        bool sessionStartedByRefresh = false;
        TokenRefreshFailedEventArgs? tokenRefreshFailedEventArgs = null;
        UnauthenticatedEventArgs? unauthenticatedEventArgs = null;
        bool timerStopped = false;
        bool succeeded = false;

        await _credentialRefreshSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        long credentialGeneration = CredentialGeneration;

        try
        {
            using (_logger.BeginScope($"RefreshOAuthIssuedCredentials() with refresh token #{tokenHash}"))
            {
                Logger.RefreshOAuthIssuedCredentialsCalled(_logger, refreshCredential.Service, tokenHash);

                if (HasRefreshTokenAlreadyBeenExchanged(refreshCredential.RefreshToken, tokenHash))
                {
                    succeeded = HasExchangeOfRefreshTokenProducedNewCredentials(refreshCredential.RefreshToken, tokenHash);

                    if (!succeeded)
                    {
                        tokenRefreshFailedEventArgs = CreateTokenRefreshFailedEventArgs(refreshCredential, null, null);

                        if (TryClearCredentialsForSpentRefreshToken(refreshCredential.RefreshToken, out unauthenticatedEventArgs))
                        {
                            StopTokenRefreshTimer();
                        }
                    }

                    return succeeded;
                }

                StopTokenRefreshTimer();
                timerStopped = true;

                // Get authorization server
                Uri? authorizationServer = await ResolveAuthorizationServer(refreshCredential.Service, cancellationToken).ConfigureAwait(false) ??
                    throw new ArgumentException($"Authorization server cannot be found for {refreshCredential.Service}", nameof(refreshCredential));

                OAuthClient oAuthClient = CreateOAuthClient();

                DPoPAccessCredentials? refreshedCredentials = await oAuthClient.RefreshCredentials(
                    refreshCredential: refreshCredential,
                    authority: authorizationServer,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (refreshedCredentials is null)
                {
                    tokenRefreshFailedEventArgs = CreateTokenRefreshFailedEventArgs(refreshCredential, null, null);

                    return false;
                }

                // The server has spent the refresh token by this point, so record it before anything which can fail, otherwise
                // a retry re-presents a token which has already been exchanged.
                RememberExchangedRefreshToken(refreshCredential.RefreshToken);

                // The refresh token was issued to one actor, so the tokens it is exchanged for must belong to that same actor.
                // Without this an authorization server which answered with a token for a different subject would silently
                // re-point the agent, and everything built on it, at another account.
                if (expectedDid is not null && refreshedCredentials.Did != expectedDid)
                {
                    Logger.RefreshOAuthIssuedCredentialsReturnedUnexpectedDid(_logger, expectedDid, refreshedCredentials.Did, refreshCredential.Service);

                    tokenRefreshFailedEventArgs = CreateTokenRefreshFailedEventArgs(refreshCredential, null, null);

                    throw new SecurityTokenValidationException("The issued access token was not issued for the account being refreshed.");
                }

                if (!await ValidateJwtToken(refreshedCredentials.AccessJwt, refreshedCredentials.Did, refreshCredential.Service).ConfigureAwait(false))
                {
                    Logger.RefreshOAuthIssuedCredentialsTokenValidationFailed(_logger, refreshedCredentials.Did, refreshCredential.Service);

                    tokenRefreshFailedEventArgs = CreateTokenRefreshFailedEventArgs(refreshCredential, null, null);

                    throw new SecurityTokenValidationException("The issued access token could not be validated.");
                }

                Logger.RefreshOAuthIssuedCredentialsSucceeded(_logger, refreshedCredentials.Did, refreshedCredentials.Service);

                if (!TryPublishRefreshedCredentials(refreshedCredentials, credentialGeneration, out bool sessionStarted))
                {
                    Logger.RefreshedCredentialsDiscardedAsAgentCredentialsChanged(_logger, refreshedCredentials.Did, refreshedCredentials.Service);

                    return false;
                }

                StartTokenRefreshTimer();
                succeeded = true;

                credentialsToNotify = refreshedCredentials;
                sessionStartedByRefresh = sessionStarted;
            }
        }
        finally
        {
            _credentialRefreshSemaphore.Release();

            // A refresh which stopped the timer and then failed must not leave it stopped, otherwise a single failed or
            // throwing call to RefreshCredentials() silently ends background refresh for the lifetime of the agent.
            if (timerStopped && !succeeded)
            {
                RestartTokenRefreshTimer(_backgroundRefreshRetryInterval, null);
            }

            // Raised outside the refresh semaphore so that a handler which calls back into the agent cannot deadlock it.
            // The session ending is raised first: the credentials have already been cleared, so a failure subscriber
            // which throws must not be able to suppress it, and one which reauthenticates must not be able to make it
            // arrive after the new session's Authenticated.
            if (unauthenticatedEventArgs is not null)
            {
                await RaiseUnauthenticatedAsync(unauthenticatedEventArgs).ConfigureAwait(false);
            }

            if (tokenRefreshFailedEventArgs is not null)
            {
                OnTokenRefreshFailed(tokenRefreshFailedEventArgs);
            }
        }

        // Raised outside the refresh semaphore so that a handler which calls back into the agent cannot deadlock it.
        if (credentialsToNotify is not null)
        {
            // A refresh of stored credentials on an agent which had no session starts one, so it is announced as a
            // session starting before the credentials for it are notified. The notification is made even if that
            // announcement fails, because the credentials are already live and the token they replaced is spent.
            await RaiseSessionStartAndCredentialsUpdatedAsync(credentialsToNotify, sessionStartedByRefresh, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    internal async Task<bool> RefreshSessionIssuedCredentials(RefreshCredential refreshCredential, Did? expectedDid = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refreshCredential);
        ArgumentNullException.ThrowIfNull(refreshCredential.Service);

        ArgumentException.ThrowIfNullOrWhiteSpace(refreshCredential.RefreshToken);

        if (refreshCredential.AuthenticationType != AuthenticationType.UsernamePassword &&
            refreshCredential.AuthenticationType != AuthenticationType.UsernamePasswordAuthFactorToken)
        {
            throw new CredentialException(refreshCredential);
        }

        if (refreshCredential is IAccessCredential)
        {
            refreshCredential = new RefreshCredential(refreshCredential.Service, refreshCredential.AuthenticationType, refreshCredential.RefreshToken);
        }

        // Declined before anything is sent to the server, so the supplied credential is left unspent and still usable.
        // Rejecting only after the exchange would destroy it.
        if (IsRefreshForADifferentActor(expectedDid, out Did? agentDid))
        {
            Logger.RefreshedCredentialsDiscardedAsTheyAreForADifferentActor(_logger, expectedDid!, agentDid);

            return false;
        }

        string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshCredential.RefreshToken)));

        AccessCredentials? credentialsToNotify = null;
        bool sessionStartedByRefresh = false;
        TokenRefreshFailedEventArgs? tokenRefreshFailedEventArgs = null;
        bool timerStopped = false;
        bool succeeded = false;

        await _credentialRefreshSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        long credentialGeneration = CredentialGeneration;

        try
        {
            using (_logger.BeginScope($"RefreshSessionIssuedCredentials() with refresh token #{tokenHash}"))
            {
                Logger.RefreshSessionIssuedCredentialsCalled(_logger, refreshCredential.Service, tokenHash);

                if (HasRefreshTokenAlreadyBeenExchanged(refreshCredential.RefreshToken, tokenHash))
                {
                    succeeded = HasExchangeOfRefreshTokenProducedNewCredentials(refreshCredential.RefreshToken, tokenHash);

                    if (!succeeded)
                    {
                        tokenRefreshFailedEventArgs = CreateTokenRefreshFailedEventArgs(refreshCredential, null, null);
                    }

                    return succeeded;
                }

                StopTokenRefreshTimer();
                timerStopped = true;

                AtProtoHttpResult<Session> refreshSessionResult;
                try
                {
                    refreshSessionResult = await AtProtoServer.RefreshSession(
                        refreshCredential,
                        HttpClient,
                        credentialsUpdated: null,
                        loggerFactory: LoggerFactory,
                        maximumResponseSize: MaximumResponseSize,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.TokenRefreshApiThrew(_logger, e);
                    throw;
                }

                if (!refreshSessionResult.Succeeded || refreshSessionResult.Result.AccessJwt is null || refreshSessionResult.Result.RefreshJwt is null)
                {
                    Logger.RefreshSessionApiCallFailed(_logger, refreshCredential.Service, tokenHash, refreshSessionResult.StatusCode);

                    tokenRefreshFailedEventArgs = CreateTokenRefreshFailedEventArgs(
                        refreshCredential,
                        refreshSessionResult.StatusCode,
                        refreshSessionResult.AtErrorDetail);

                    return false;
                }

                // The server has spent the refresh token by this point, so record it before anything which can fail, otherwise
                // a retry re-presents a token which has already been exchanged.
                RememberExchangedRefreshToken(refreshCredential.RefreshToken);

                if (!await ValidateJwtToken(refreshSessionResult.Result.AccessJwt, refreshSessionResult.Result.Did, refreshCredential.Service).ConfigureAwait(false))
                {
                    Logger.RefreshSessionIssuedCredentialsTokenValidationFailed(_logger, refreshSessionResult.Result.Did, refreshCredential.Service);

                    tokenRefreshFailedEventArgs = CreateTokenRefreshFailedEventArgs(refreshCredential, null, null);

                    throw new SecurityTokenValidationException("The issued access token could not be validated.");
                }

                AccessCredentials refreshedCredentials = new(
                        refreshCredential.Service,
                        refreshCredential.AuthenticationType,
                        refreshSessionResult.Result.AccessJwt,
                        refreshSessionResult.Result.RefreshJwt);

                Logger.RefreshSessionIssuedCredentialsSucceeded(_logger, refreshedCredentials.Did, refreshedCredentials.Service);

                if (!TryPublishRefreshedCredentials(refreshedCredentials, credentialGeneration, out bool sessionStarted))
                {
                    Logger.RefreshedCredentialsDiscardedAsAgentCredentialsChanged(_logger, refreshedCredentials.Did, refreshedCredentials.Service);

                    return false;
                }

                StartTokenRefreshTimer();
                succeeded = true;

                credentialsToNotify = refreshedCredentials;
                sessionStartedByRefresh = sessionStarted;
            }
        }
        finally
        {
            _credentialRefreshSemaphore.Release();

            // A refresh which stopped the timer and then failed must not leave it stopped, otherwise a single failed or
            // throwing call to RefreshCredentials() silently ends background refresh for the lifetime of the agent.
            if (timerStopped && !succeeded)
            {
                RestartTokenRefreshTimer(_backgroundRefreshRetryInterval, null);
            }

            // Raised outside the refresh semaphore so that a handler which calls back into the agent cannot deadlock it.
            if (tokenRefreshFailedEventArgs is not null)
            {
                OnTokenRefreshFailed(tokenRefreshFailedEventArgs);
            }
        }

        // Raised outside the refresh semaphore so that a handler which calls back into the agent cannot deadlock it.
        if (credentialsToNotify is not null)
        {
            // A refresh of stored credentials on an agent which had no session starts one, so it is announced as a
            // session starting before the credentials for it are notified. The notification is made even if that
            // announcement fails, because the credentials are already live and the token they replaced is spent.
            await RaiseSessionStartAndCredentialsUpdatedAsync(credentialsToNotify, sessionStartedByRefresh, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Creates the arguments describing a failed refresh of <paramref name="refreshCredential"/>, if the agent knows which
    /// account the credential belongs to.
    /// </summary>
    /// <param name="refreshCredential">The refresh credential which could not be exchanged.</param>
    /// <param name="statusCode">The <see cref="HttpStatusCode"/> returned by the API, if the failure came from an API call.</param>
    /// <param name="error">The <see cref="AtErrorDetail"/> returned by the API, if any.</param>
    /// <returns>
    ///   The event arguments, or <see langword="null"/> if the agent no longer holds credentials and so cannot say which
    ///   account failed.
    /// </returns>
    /// <remarks>
    /// <para>
    ///   A refresh credential does not carry a <see cref="Did"/>, so it has to come from the agent. It can be absent when a
    ///   logout races the refresh, and publishing a null through a property declared as non nullable would push the problem
    ///   into every handler.
    /// </para>
    /// </remarks>
    private TokenRefreshFailedEventArgs? CreateTokenRefreshFailedEventArgs(RefreshCredential refreshCredential, HttpStatusCode? statusCode, AtErrorDetail? error)
    {
        Did? did = Did;

        if (did is null)
        {
            return null;
        }

        return new TokenRefreshFailedEventArgs(
            did: did,
            service: refreshCredential.Service,
            refreshToken: refreshCredential.RefreshToken,
            statusCode: statusCode,
            error: error);
    }

    /// <summary>
    /// Returns a flag indicating whether the already recorded exchange of <paramref name="refreshToken"/> left the agent
    /// holding refreshed credentials.
    /// </summary>
    /// <param name="refreshToken">The refresh token which has already been exchanged.</param>
    /// <param name="tokenHash">The hash of <paramref name="refreshToken"/>, used for logging.</param>
    /// <returns>
    ///   <see langword="true"/> if the agent credentials have moved on from <paramref name="refreshToken"/>, otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    ///   A token is recorded as exchanged before the exchange has completed, so that a failure part way through cannot let a
    ///   retry re-present a token the server has already spent. A recorded token therefore does not on its own mean the agent
    ///   has usable credentials: if the agent is still holding the spent token then the exchange did not finish, and reporting
    ///   success would tell the caller it had been refreshed when it had not, and stop the background refresh retrying.
    /// </para>
    /// </remarks>
    private bool HasExchangeOfRefreshTokenProducedNewCredentials(string refreshToken, string tokenHash)
    {
        AccessCredentials? currentCredentials = Credentials;

        if (currentCredentials is not null && !string.Equals(currentCredentials.RefreshToken, refreshToken, StringComparison.Ordinal))
        {
            return true;
        }

        Logger.RefreshTokenExchangedButCredentialsUnchanged(_logger, tokenHash);

        return false;
    }

    /// <summary>
    /// Returns a flag indicating whether <paramref name="refreshToken"/> has already been exchanged for new credentials.
    /// </summary>
    /// <param name="refreshToken">The refresh token about to be presented to the server.</param>
    /// <param name="tokenHash">The hash of <paramref name="refreshToken"/>, used for logging.</param>
    /// <returns><see langword="true"/> if <paramref name="refreshToken"/> has already been exchanged, otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    ///   Refresh tokens are single use. When callers race to refresh the same credentials the loser of the race would otherwise
    ///   present a token the winner has already spent, which fails and, on servers which revoke on reuse, can end the session.
    /// </para>
    /// </remarks>
    private bool HasRefreshTokenAlreadyBeenExchanged(string refreshToken, string tokenHash)
    {
        lock (_exchangedRefreshTokenLock)
        {
            if (!_exchangedRefreshTokens.Contains(refreshToken, StringComparer.Ordinal))
            {
                return false;
            }
        }

        Logger.RefreshTokenAlreadyExchanged(_logger, tokenHash);

        return true;
    }

    /// <summary>
    /// Records that <paramref name="refreshToken"/> has been exchanged, so it is not presented again.
    /// </summary>
    /// <param name="refreshToken">The refresh token which has just been exchanged.</param>
    private void RememberExchangedRefreshToken(string refreshToken)
    {
        lock (_exchangedRefreshTokenLock)
        {
            _exchangedRefreshTokens.Enqueue(refreshToken);

            while (_exchangedRefreshTokens.Count > MaximumRememberedRefreshTokens)
            {
                _exchangedRefreshTokens.Dequeue();
            }
        }
    }

    /// <summary>
    /// Forgets every remembered refresh token, so spent tokens are not retained once the session they belong to has ended.
    /// </summary>
    private void ForgetExchangedRefreshTokens()
    {
        lock (_exchangedRefreshTokenLock)
        {
            _exchangedRefreshTokens.Clear();
        }
    }

    /// <summary>
    /// Clears the agent credentials without going through the <see cref="Credentials"/> property.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Used during disposal, where the <see cref="Credentials"/> setter would throw because the agent has already been
    ///   marked as disposed. Leaving the credentials in place would keep a live access token and refresh token reachable
    ///   for as long as anything holds a reference to the disposed agent.
    /// </para>
    /// </remarks>
    private void ClearCredentials()
    {
        lock (_credentialLock)
        {
            _credentials = null;
            _credentialGeneration++;
        }
    }

    private void RefreshTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        Logger.BackgroundTokenRefreshFired(_logger);

        BackgroundRefreshCredentials().FireAndForget();
    }

    /// <summary>
    /// Refreshes the agent credentials on behalf of the refresh timer, restarting the timer if the refresh does not succeed.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    ///   A failed refresh must not leave the timer stopped, otherwise a single transient failure silently ends background
    ///   refresh for the lifetime of the agent and the session expires.
    /// </para>
    /// </remarks>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A background refresh must not let any failure escape unobserved, and every failure is retried and logged.")]
    private async Task BackgroundRefreshCredentials()
    {
        bool refreshed = false;
        Exception? exception = null;

        try
        {
            refreshed = await RefreshCredentials().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        if (!refreshed)
        {
            RestartTokenRefreshTimer(_backgroundRefreshRetryInterval, exception);
        }
    }

    /// <summary>
    /// Restarts the token refresh timer after a failed background refresh, so the refresh is retried.
    /// </summary>
    /// <param name="retryIn">The interval to wait before retrying.</param>
    /// <param name="exception">The exception, if any, which caused the refresh to fail.</param>
    /// <remarks>
    /// <para>
    ///   The timer is created if it does not already exist. A refresh which runs before the timer has been created,
    ///   which is what happens when the access token issued at login is already close to expiry, would otherwise have
    ///   nothing to restart and background refresh would never run again.
    /// </para>
    /// </remarks>
    private void RestartTokenRefreshTimer(TimeSpan retryIn, Exception? exception)
    {
        lock (_timerLock)
        {
            if (!_enableTokenRefresh || _atProtoAgentDisposed || Credentials is not AccessCredentials)
            {
                return;
            }

            EnsureTokenRefreshTimer();

            _credentialRefreshTimer.Interval = retryIn.TotalMilliseconds;
            _credentialRefreshTimer.Enabled = true;
            _credentialRefreshTimer.Start();

            Logger.BackgroundTokenRefreshFailed(_logger, _credentialRefreshTimer.Interval, exception);
        }
    }

    /// <summary>
    /// Creates the token refresh timer if it does not already exist.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The timer is created here, and only here, so that the Elapsed handler is subscribed exactly once. Subscribing on
    ///   every start would leave the handler attached multiple times, and every tick would then start as many concurrent
    ///   refreshes as there are subscriptions, each racing to spend the same refresh token.
    /// </para>
    /// <para>Callers must hold <see cref="_timerLock"/>.</para>
    /// </remarks>
    [MemberNotNull(nameof(_credentialRefreshTimer))]
    private void EnsureTokenRefreshTimer()
    {
        if (_credentialRefreshTimer is null)
        {
            _credentialRefreshTimer = new System.Timers.Timer();
            _credentialRefreshTimer.Elapsed += RefreshTimerElapsed;
        }
    }

    private void StartTokenRefreshTimer()
    {
        if (_enableTokenRefresh && Credentials is AccessCredentials accessCredentials && !string.IsNullOrEmpty(accessCredentials.AccessJwt))
        {
            TimeSpan accessTokenExpiresIn = GetTimeToJwtTokenExpiry(accessCredentials.AccessJwt);

            if (accessTokenExpiresIn.TotalSeconds <= 60)
            {
                // As we're about to expire, go refresh the token. Sixty seconds is included because the refresh
                // interval below subtracts a minute, which at exactly sixty seconds would leave nothing to wait for.
                ScheduleImmediateTokenRefresh();
                return;
            }

            TimeSpan refreshIn = _refreshAccessTokenInterval;
            if (accessTokenExpiresIn < _refreshAccessTokenInterval)
            {
                refreshIn = accessTokenExpiresIn - new TimeSpan(0, 1, 0);
            }

            lock (_timerLock)
            {
                if (_atProtoAgentDisposed)
                {
                    return;
                }

                EnsureTokenRefreshTimer();

                _credentialRefreshTimer.Interval = refreshIn.TotalMilliseconds >= int.MaxValue ? int.MaxValue : refreshIn.TotalMilliseconds;
                _credentialRefreshTimer.Enabled = true;
                _credentialRefreshTimer.Start();

                Logger.TokenRefreshTimerStarted(_logger, _credentialRefreshTimer.Interval);
            }
        }
    }

    /// <summary>
    /// Restarts the token refresh timer when an operation which stopped it did not complete successfully.
    /// </summary>
    /// <param name="succeeded">A flag indicating whether the operation which stopped the timer succeeded.</param>
    /// <remarks>
    /// <para>
    ///   An operation which stops the timer and then fails must not leave it stopped, otherwise a single failed login or
    ///   logout silently ends background refresh for the lifetime of the agent, and the session it left in place expires.
    /// </para>
    /// <para>
    ///   Nothing is restarted when the failure also cleared the agent credentials, because <see cref="StartTokenRefreshTimer"/>
    ///   only starts the timer when there are credentials to refresh.
    /// </para>
    /// </remarks>
    private void RestoreTokenRefreshTimerAfterFailure(bool succeeded)
    {
        if (!succeeded)
        {
            StartTokenRefreshTimer();
        }
    }

    /// <summary>
    /// Schedules a token refresh to run as soon as practical, rather than running it inline.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   <see cref="StartTokenRefreshTimer"/> is called from inside a refresh, whilst the refresh semaphore is still held.
    ///   Refreshing inline from there would recurse through the refresh, and a server issuing access tokens which are already
    ///   close to expiry would drive an unbounded chain of immediate refreshes. Going through the timer breaks the recursion
    ///   and puts a floor under how often the refresh can run.
    /// </para>
    /// </remarks>
    private void ScheduleImmediateTokenRefresh()
    {
        lock (_timerLock)
        {
            if (_atProtoAgentDisposed)
            {
                return;
            }

            EnsureTokenRefreshTimer();

            _credentialRefreshTimer.Interval = _immediateRefreshInterval.TotalMilliseconds;
            _credentialRefreshTimer.Enabled = true;
            _credentialRefreshTimer.Start();

            Logger.TokenRefreshTimerStarted(_logger, _credentialRefreshTimer.Interval);
        }
    }

    /// <summary>
    /// Stops the token refresh timer, and optionally disposes of it.
    /// </summary>
    /// <param name="dispose">A flag indicating whether the timer should be disposed of as well as stopped.</param>
    /// <remarks>
    /// <para>
    ///   Stopping the timer does not recall an Elapsed callback which has already been handed to the thread pool, so a refresh
    ///   which stops the timer can still be joined by one the timer started moments earlier. The second refresh blocks on the
    ///   refresh semaphore and then finds the token it holds has already been exchanged, which is what stops it presenting a
    ///   spent token to the server.
    /// </para>
    /// </remarks>
    private void StopTokenRefreshTimer(bool dispose = false)
    {
        lock (_timerLock)
        {
            if (_credentialRefreshTimer is not null)
            {
                _credentialRefreshTimer.Stop();
                Logger.TokenRefreshTimerStopped(_logger);

                if (dispose)
                {
                    _credentialRefreshTimer.Elapsed -= RefreshTimerElapsed;
                    _credentialRefreshTimer.Dispose();
                    _credentialRefreshTimer = null;
                }
            }
        }
    }

    [SuppressMessage("Security", "CA5404:Do not disable token validation checks", Justification = "PDSs do not issue JWTs with issuers whose signing key can be retrieved.")]
    private static async Task<bool> ValidateJwtToken(string jwt, Did did, Uri service)
    {
        bool isValid = false;

        // Disable issuer and signature validation because the Bluesky PDS implementation does not expose a
        // .well-known/openid-configuration endpoint to retrieve the issuer and signing key from.

        // Oauth token signature validation is done during the oauth flow.

        TokenValidationParameters validationParameters = new()
        {
            ValidateAudience = true,
            ValidAudience = $"did:web:{service.Host}",
            ValidateIssuer = false,
            ValidateIssuerSigningKey = false,
            ValidateLifetime = true,
            IssuerSigningKeyValidator = (securityKey, securityToken, validationParameters) => true,
            SignatureValidator = (token, validationParameters) => new JsonWebToken(token)
        };

        JsonWebTokenHandler tokenHandler = new();
        TokenValidationResult validationResult = await tokenHandler.ValidateTokenAsync(jwt, validationParameters).ConfigureAwait(false);

        if (validationResult.IsValid)
        {
            // Validate the subject matches the expected DID. DIDs are case sensitive, so the comparison is ordinal
            // rather than case insensitive, which would accept a token issued for a different identifier.
            isValid = string.Equals((string?)validationResult.Claims.FirstOrDefault(c => c.Key == "sub").Value, did.ToString(), StringComparison.Ordinal);
        }

        return isValid;
    }
}
