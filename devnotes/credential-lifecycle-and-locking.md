# Credential lifecycle, locking and ordering

This note describes how `AtProtoAgent` holds credentials, how it tells subscribers about them, and the rules which keep
those two things consistent. It is aimed at anyone changing authentication code in `idunno.AtProto`, or writing an
agent subclass or a credential store on top of it.

Everything described here lives in `src/idunno.AtProto/Authentication/AtProtoAgent.cs` unless stated otherwise.

## Why any of this exists

An agent holds one set of credentials. Several things can change them, and more than one of those can be in flight at
the same time:

* a **login**, which installs a session,
* a **logout**, which ends one,
* a **token refresh**, either started by the caller or by the background refresh timer,
* a **DPoP nonce rotation**, which the server can force on any request at any time.

Each of those also has to reach the application, because the application is what persists credentials to durable
storage. If notifications arrive out of order, or a handler which suspended finishes after the thing it was told about
has been replaced, the application ends up storing credentials which no longer work. For OAuth that is not recoverable:
refresh tokens are single use, so storing a spent one loses the session permanently. That failure is
[#559](https://github.com/blowdart/idunno.Bluesky/issues/559).

The rules below exist to make one guarantee:

> **The last thing a subscriber is told about a session is the state that session actually ended in.**

## The moving parts

| Member | Type | Guards |
| --- | --- | --- |
| `_credentialLock` | `Lock` (net9.0+) / `object` (net8.0) | `_credentials`, `_credentialGeneration`, and every piece of deferral state below. Held only for short, synchronous critical sections. |
| `_credentialGeneration` | `long` | Incremented on every change to `_credentials`. Lets an in-flight refresh detect that the world moved underneath it. |
| `_credentialRefreshSemaphore` | `SemaphoreSlim(1)` | Serialises token refreshes, so two refreshes cannot both spend from the same session. |
| Notification queue (`_nextNotificationTurn`, `_notificationTurnNowServing`, `_notificationTurnInProgress`, `_notificationTurnWaiters`) | numbered turns, guarded by `_credentialLock` | Serialises delivery of `CredentialsUpdated`, `Authenticated` and `Unauthenticated`, so only one is ever being handled at a time, **in commit order** rather than arrival order. |
| `_raisingCredentialNotification` | `AsyncLocal<CredentialNotificationScope?>` | Detects a notification raised from inside a handler. |
| `_deferredCredentialNotification` | `AccessCredentials?` | The newest credentials a handler deferred, to be raised once it returns. |
| `_deferredCredentialNotificationCommitted` | `bool` | Sticky. Records that the deferred credentials are already live in the agent. |
| `_deferredSessionEvents` | `List<DeferredSessionEvent>` | `Authenticated`/`Unauthenticated` raised from inside a handler, drained in the order deferred, and compared against `_deferredCredentialNotification` by sequence rather than always taking one channel first. |
| `_exchangedRefreshTokens` / `_exchangedRefreshTokenLock` | bounded `Queue<string>` + `Lock`/`object` | Remembers the last few refresh tokens this session has exchanged, so a retry cannot re-present a spent one. Independent of `_credentialLock`. |

### Lock ordering

There is one ordering rule, and it is not negotiable:

```
_credentialRefreshSemaphore  →  notification turn  →  _credentialLock
```

Acquire in that order or not at all. Never acquire in the reverse order, and **never hold `_credentialLock` across an
`await`** — every critical section under it is synchronous by design. Raising an event while holding a notification
turn is fine; raising one while holding `_credentialLock` is not, because a handler calling back into the agent would
deadlock instantly.

The turn splits into two steps, and the difference matters:

* **Taking** a turn (`TakeNotificationTurn`) only allocates a number. It never waits, so it is safe to do while
  `_credentialRefreshSemaphore` is held — and it has to be, because the number has to reflect commit order.
* **Entering** a turn (`EnterNotificationTurnAsync`) waits. It must *never* be done while `_credentialRefreshSemaphore`
  is held: a handler running in the turn ahead is free to log in or out, which needs that semaphore, so waiting for a
  turn while holding it deadlocks the agent. Commit sites therefore take the turn, release the refresh semaphore, and
  only then raise.

```mermaid
flowchart LR
    R["_credentialRefreshSemaphore<br/>(one refresh at a time)"] --> N["notification turn<br/>(one handler at a time,<br/>served in commit order)"] --> L["_credentialLock<br/>(short, synchronous)"]
```

## How a change reaches a subscriber

Every credential change follows the same shape: take a turn, mutate state under `_credentialLock`, release it, then
notify outside every lock.

```mermaid
sequenceDiagram
    participant C as Caller / timer
    participant A as AtProtoAgent
    participant L as _credentialLock
    participant Q as Notification queue
    participant H as Subscriber

    C->>A: Login / Logout / RefreshCredentials
    A->>Q: TakeNotificationTurn (does not wait)
    A->>L: take lock
    L-->>A: check generation, swap _credentials, bump generation
    A->>L: release lock
    Note over A: Credentials are now live in the agent
    A->>Q: RaiseCredentialsUpdatedAsync (passing the turn)
    Q->>Q: wait until this turn is the one being served
    Q->>H: OnCredentialsUpdatedAsync (only if agent still holds these credentials)
    H-->>Q: persisted
    Q->>Q: drain anything the handler deferred
    Q->>H: Authenticated / Unauthenticated (last, except on a session start)
    A->>Q: FinishNotificationTurn (in a finally)
```

Three consequences worth internalising:

* **State changes before anyone is told.** By the time a handler runs, the agent has already committed. A handler
  cannot veto a change; it can only fail to persist it.
* **Delivery order is commit order.** The queue place is taken *before* the commit, not after. Committing first and
  queueing afterwards lets another caller commit and queue in between, so a session which ended before another began
  is reported as ending after it, and a subscriber which discards its stored credentials on `Unauthenticated` discards
  the session which is actually live.
* **Session events come last, except on a session start.** `Authenticated` and `Unauthenticated` are ordered *behind*
  the credential notification queue on purpose, so a subscriber which discards its stored credentials on
  `Unauthenticated` discards any stale write that a suspended handler made on its way out. The one deliberate exception
  is `RaiseSessionStartAndCredentialsUpdatedAsync`, used when a refresh of stored credentials starts a session: it
  raises `Authenticated` *before* the credential notification, so a subscriber is told a session began before it is
  handed credentials for it. The credential notification is still made when that `Authenticated` handler throws,
  because the refresh token it replaced has already been spent.

## Guards, and what each one prevents

### Generation check — `TryPublishRefreshedCredentials`

A refresh spans a network call. The check and the write both happen inside one `_credentialLock` critical section:

```csharp
if (_atProtoAgentDisposed || _credentialGeneration != expectedGeneration)
{
    return false;
}
```

Prevents a refresh which started before a logout from re-establishing the session that logout ended, and a refresh
which started before a login from overwriting the session that login installed.

> **Warning.** A `false` return here means the refresh token has been spent and its replacement discarded. The caller
> must treat that as the session being over — not as something to retry.

### Actor check — `IsRefreshForADifferentActor`, and again in `TryPublishRefreshedCredentials`

`RefreshCredentials(credential)` takes an arbitrary credential, which need not belong to the agent's own session.
Publishing the result blindly would silently re-point the agent, and everything built on it, at another account.

The check is made **twice, deliberately**:

* **Before** the token endpoint is called, so a mismatch leaves the supplied credential unspent and still usable
  elsewhere. Rejecting only afterwards destroys the very session the caller asked to refresh.
* **After** the response, as the backstop for what the early check cannot see: a credential carrying no DID, a login
  racing the refresh, and a server answering with a token for the wrong subject.

### Freshness check — `InternalOnCredentialsUpdatedCallBack`

This is the #559 path. A DPoP nonce rotation arrives on an arbitrary request, carrying the credentials snapshot that
request was built with. If a refresh completed in the meantime, that snapshot is stale, and assigning it back would
restore dead tokens *and* bump the generation so the fresh ones were discarded too.

The callback now verifies under `_credentialLock` that the credentials the agent is holding still match the snapshot the
request carried — comparing the access token, the refresh token, the DPoP proof key and the service, not object
identity — and if so mutates **only** the nonce, in place. It never reassigns `Credentials` and never touches the
generation.

> **Warning.** Nonce rotation must stay an in-place mutation. Swapping the credentials object to carry a new nonce
> makes every nonce update look like a credential change, which is what caused the original bug.

### Spent refresh token cache — `_exchangedRefreshTokens`

Refresh tokens are single use. The token is recorded as spent *before* anything which can fail, so a retry cannot
re-present it. The last `MaximumRememberedRefreshTokens` (4) are kept rather than just the most recent one, because a
caller holding an older credential can otherwise present a token which was spent several refreshes ago.

A token being remembered is not on its own fatal: `HasExchangeOfRefreshTokenProducedNewCredentials` distinguishes
"already exchanged, and the agent got new credentials from it" — which succeeds — from "already exchanged, and nothing
came back", which ends the session via `TryClearCredentialsForSpentRefreshToken`.

This cache is per session and must be cleared whenever the session ends or is replaced — `ForgetExchangedRefreshTokens`
is called from `InternalLogin`, `TryClearCredentialsForSpentRefreshToken` and
`ClearCredentialsAndRaiseUnauthenticatedAsync`. Leaving stale entries means a later session whose token happens to
match one is cleared on its first refresh.

> **Warning.** `InternalLogin` forgets **unconditionally**, not only when it displaces a live session. Any remembered
> token belongs to some earlier session, however that session ended.

### Reentrancy and deferral

A handler may call back into the agent, and a call which refreshes raises a notification of its own. Raising that
inline would let the outer handler — still holding the credentials which have just been replaced — finish *last* and
persist them.

So a reentrant notification is parked and raised once the handler it came from returns:

```mermaid
stateDiagram-v2
    [*] --> Raising: notification's turn is served
    Raising --> HandlerRunning: OnCredentialsUpdatedAsync
    HandlerRunning --> Deferred: handler refreshes or logs in/out, deferring a nested notification, tagged with its own turn
    Deferred --> HandlerRunning: parked, not raised inline
    HandlerRunning --> Draining: handler returns
    Deferred --> Draining
    Draining --> HandlerRunning: raise anything deferred before the earliest turn genuinely still waiting
    Draining --> SessionEvents: nothing left safe to raise (unsafe items, if any, are left for that earlier turn)
    SessionEvents --> [*]: Authenticated / Unauthenticated raised last (except on a session start)
```

Some subtleties in that machinery:

* **A turn is always reserved, even when reentrant.** `TakeNotificationTurn` hands out a turn whether or not a
  notification is running. A reentrant caller never queues behind itself, because its raises are deferred and return
  without waiting, and the unused turn is simply stepped over when it is given up. Skipping the reservation because a
  notification happened to be running would not be safe: work a handler started but did not await carries that
  handler's scope, so it can see the scope running when it commits and find it closed by the time it raises, taking its
  place in the queue behind a change which was committed *after* its own.

* **The deferral slot holds only the newest set.** It is written only when the credentials being deferred are still the
  agent's (`ReferenceEquals(_credentials, credentials)`). A notification can reach the slot *later* than one for the
  credentials which superseded it; without the currency check the stale set would displace the newer one and then be
  dropped as stale when drained, losing both.
* **Closing the scope waits for both queues to drain, not just one pass each.** `DrainDeferredNotificationsAsync` loops
  draining committed credential notifications and deferred session events until a single check, made under
  `_credentialLock`, finds nothing safe left in either at the same instant — only then is the scope closed. A session
  event subscriber is free to log in or refresh, deferring a committed credential notification, and a credential
  notification subscriber is equally free to log in or out, deferring a session event; either can leave the other with
  something still to raise. Closing the scope after a single pass each would make a notification deferred into it by
  the pass which already ran wait for a turn of its own instead, deadlocking against the turn this call still holds.
* **Scope, not a flag.** `_raisingCredentialNotification` holds an object rather than a `bool` because an `AsyncLocal`
  value flows into work a handler *starts but does not await*. That work can run long after its originating
  notification finished; the object lets it discover the scope is closed and queue properly.
* **A session event is a notification scope too.** `RaiseSessionEventAsync` opens its own scope, so a subscriber to
  `Authenticated` or `Unauthenticated` which refreshes the agent has that refresh's notification deferred into it. That
  path therefore drains committed credential notifications, exactly as the credential path does, before it closes the
  scope and empties the slots. Dropping them would leave a subscriber storing a refresh token which has been spent.

### Turn-ordered draining

Deferring a reentrant raise is not enough on its own: without more information, draining the deferred slots as soon as
the handler which caused them returns can let that reentrant work leapfrog a change which is already committed and
genuinely waiting for its own, earlier, place in the queue.

Concretely: turn 0 is running and its handler makes a reentrant change, taking turn 2, while a second, genuinely
concurrent, caller has already committed a change of its own, taken turn 1, and is waiting for it. Draining turn 2's
deferred notifications as soon as turn 0's handler returns — before turn 1 has run at all — would announce turn 2's
session as current, then have turn 1 announce its own session on top of it once it is finally granted, back-to-front
from what was actually committed.

```mermaid
sequenceDiagram
    participant T0 as Turn 0 (running)
    participant T1 as Turn 1 (concurrent, reserved and waiting)
    participant T2 as Turn 2 (reentrant, from T0's handler)
    participant Q as Deferred queue

    T1->>T1: commits, takes turn 1
    T1->>Q: EnterNotificationTurnAsync — genuinely waits
    T0->>T0: handler runs
    T0->>T2: reentrant login, commits, takes turn 2
    T2->>Q: raises deferred into T0's scope, tagged with turn 2
    T2-->>T0: returns (turn 2 stepped over, never entered)
    T0->>Q: drain — turn 2 is NOT before turn 1, left in place
    T0->>T0: scope closes, turn given up
    T1->>T1: granted, entered, raises its own notification
    T1->>Q: drain — nothing else waiting now, turn 2 is safe, raised
```

Every item deferred into the queue is therefore tagged with the notification turn number of whichever call deferred
it — `_deferredCredentialNotificationTurnNumber` for the single credential slot, and a turn number carried alongside
each entry in `_deferredSessionEvents` (a list of `DeferredSessionEvent` records, tagging a turn number, a sequence
number and the action to raise, rather than a plain queue). A drain only takes an item whose turn number is strictly
*before* the earliest notification turn genuinely still waiting for its own place in the queue —
`EarliestGenuinelyWaitingNotificationTurn`.

* **A reservation, not just a registered waiter, is what makes a turn "genuinely waiting".**
  `EarliestGenuinelyWaitingNotificationTurn` scans `_reservedNotificationTurns`, a set every turn number is added to
  the moment `TakeNotificationTurn` hands it out and removed from, unconditionally, the moment
  `FinishNotificationTurn` gives it up — not `_notificationTurnWaiters`, which a turn is only added to once it either
  has to wait or is given up without ever running. A turn which has reserved its place but not yet reached
  `EnterNotificationTurnAsync` — because it is still doing genuine work first, an HTTP exchange for example — is
  invisible to `_notificationTurnWaiters` but must not be invisible here: scanning the waiters dictionary alone would
  let a reentrant drain leapfrog it during exactly that window, regardless of how it eventually resolves. A
  reservation is skipped only once it is positively known to never run — a `null` entry in `_notificationTurnWaiters`
  — so a turn still deciding its own fate is always assumed to be one worth holding back for. The turn currently
  entered and running is also skipped: it is what is asking, not what it is asking about, and counting it would make
  every later turn look unsafe against itself.
* **Turn numbers only ever increase, under the same lock that computes the earliest waiting one,** so once an item is
  found safe to raise it stays safe: nothing taken afterwards can insert itself before a turn already reserved and
  waiting. This is what lets a drain reuse the same "earliest waiting" value across everything it takes in one pass,
  rather than recomputing it after every raise.
* **An item judged unsafe is left exactly where it is, not requeued or discarded.** It is picked up later by whichever
  turn eventually becomes the earliest one waiting — that turn drains the same shared slots itself once it is granted,
  so nothing is lost, only raised at the point it was always going to be safe to raise it.
* **A scope closing drops what is left only if nothing else is reserved and waiting at all.**
  `DropOrphanedDeferredNotifications` checks `EarliestGenuinelyWaitingNotificationTurn` is `long.MaxValue` — meaning no
  turn anywhere is waiting — before clearing anything. If some turn is genuinely waiting, whatever is left belongs to
  it, or to one before it, and clearing it would lose it outright; the normal case is both slots are already empty, and
  what survives past that point is what would otherwise be raised out of order or never raised at all.
* **A caller with no turn of its own — a DPoP nonce rotation, for example — is tagged with the currently active scope's
  own turn number.** It has no reserved place in the queue to be leapfrogged out of, so it is treated as belonging to
  whichever notification it is deferring into.
* **Two channels, one order.** A credential notification and a session event deferred by the same reentrant call — the
  two raises `RaiseSessionStartAndCredentialsUpdatedAsync` makes when a refresh starts a session, for example — carry
  the same turn number, so turn safety alone cannot tell a drain which to take first. Each deferral is also stamped
  with a sequence number from one shared counter (`NextDeferredNotificationSequence`), and `TakeNextDeferredNotification`
  takes whichever of the two channels holds the lower sequence. A drain which always took the credential channel first
  would silently reverse a caller's intended ordering the moment both ended up deferred into the same scope.
* **Among deferred session events, the lowest safe turn number wins, not the earliest entry in the list.** Two
  reentrant callers commit turns in ascending order, but nothing serializes the append each makes to
  `_deferredSessionEvents` afterwards — both have already released `_credentialRefreshSemaphore` by then — so the
  later commit's event can land in the list before the earlier commit's. `TakeNextDeferredNotification` scans every
  entry for the lowest safe `TurnNumber` rather than stopping at the first one it finds safe.

### Committed credentials

A notification is flagged `credentialsCommitted: true` when the credentials are already live in the agent and the token
they replaced has been spent. Those notifications are special:

* they are raised with `CancellationToken.None`, because cancelling the caller must not leave a superseded set as the
  last thing persisted;
* they are still delivered when the handler they were deferred from **throws**, and when an `Authenticated` subscriber
  throws, because nothing else can bring durable storage back into step.

A failure of the replacement notification itself is logged (message 302) rather than thrown, so it cannot displace the
exception the caller needs to see.

## Rules for maintainers

1. **Use the `Raise*` helpers.** Never call `OnCredentialsUpdatedAsync`, `OnAuthenticated` or `OnUnauthenticated`
   directly. Go through `RaiseCredentialsUpdatedAsync`, `RaiseAuthenticatedAsync` or `RaiseUnauthenticatedAsync`, which
   apply the queueing, ordering and reentrancy rules.
2. **Never clear credentials silently.** On any failure path which discards a session, call
   `ClearCredentialsAndRaiseUnauthenticatedAsync`. A silent clear leaves a subscriber holding a rejected session in
   durable storage, which it will restore on the next start.
3. **Never hold `_credentialLock` across an `await`,** and never raise an event while holding it.
4. **Respect the lock order** given above.
5. **Edit both `#if` branches.** `_credentialLock` and `_timerLock` are declared twice, as `Lock` on net9.0+ and
   `object` on net8.0. A change to one branch without the other breaks a target framework.
6. **Do not make the locks `protected`.** They cannot be — the type differs per target framework, so a single
   `PublicAPI` file cannot express it, and locking a boxed `Lock` is a compile error (CS9216). A subclass holding these
   locks across a callback would also deadlock the agent. Subclasses should override the event methods instead; the
   ordering guarantees are provided for them, not something they need to reimplement.
7. **The public `Credentials` setter raises nothing and is not ordered.** It is a raw assignment. Use `Login`, `Logout`
   or `RefreshCredentials` for anything which should be observable.
8. **The refresh semaphore is never disposed,** and is CA2213-suppressed. Disposing it while it is held makes the
   release throw.
9. **Take the notification turn before the commit, and give it up exactly once.** Anything which changes the
   credentials calls `TakeNotificationTurn` before publishing, passes that turn into every `Raise*` call the change
   produces, and calls `FinishNotificationTurn` from a `finally` — including on the paths where nothing was committed
   and nothing was raised. A turn which is never given up stops the notification queue for the lifetime of the agent.
   Methods which `return` from inside a `try` need an outer `try`/`finally` for this; `RefreshOAuthIssuedCredentials`
   and `RefreshSessionIssuedCredentials` both have one. A turn given up without ever being entered is either stepped
   over immediately, if it is at the head, or recorded as one to step over when it gets there — but only while it is
   still ahead of the queue. A turn the queue has already passed, which is what a cancelled waiter becomes when the
   turn ahead of it is given up first, is left alone: recording it would add an entry nothing ever removes.
10. **Never wait for a turn while holding `_credentialRefreshSemaphore`.** See the lock ordering section above. `Logout`
    holds that semaphore, so it splits the work: `ClearCredentialsAndRecordSessionEnd` discards the credentials and
    takes a turn — neither of which waits — under the semaphore, and `RaisePendingSessionEndAsync` raises the session
    end only once the semaphore has been released. Calling `ClearCredentialsAndRaiseUnauthenticatedAsync`, which does
    both, from under the semaphore deadlocks the agent.
11. **Give the turn up before raising a synchronous event which is not ordered.** `TokenRefreshFailed` is raised with
    `OnTokenRefreshFailed` on the calling thread and carries no ordering guarantee of its own, so the turn is finished
    first. A subscriber which reauthenticates would otherwise take the next turn and block on the very thread which is
    the only one able to give up the turn ahead of it. `FinishNotificationTurn` is idempotent, so the `finally` which
    normally gives the turn up is still correct.
12. **Anything raised after a subscriber has run may be stale.** A subscriber is free to log in or out, and by the time
    it returns everything it caused has already been raised. `InternalLogin` therefore rechecks that the agent still
    holds the credentials it is about to announce before raising `Authenticated`; announcing them unconditionally would
    put a session the agent has already moved on from last.
13. **Tag anything deferred with the turn number it belongs to, and never drain past a turn genuinely still waiting.**
    A reentrant change is always taken and deferred before its caller returns, but it must not be raised ahead of a
    change which committed, and reserved its own turn, first. Adding a new deferral slot means adding the same turn
    number to it and checking `EarliestGenuinelyWaitingNotificationTurn` before taking from it, exactly as
    `TakeNextDeferredNotification` already does — otherwise a reentrant caller can leapfrog a genuinely concurrent one
    that was already ahead of it in commit order.
14. **Taking a turn and committing are not the same step — hold the semaphore across both, or not at all.**
    `TakeNotificationTurn` only reserves a place in the queue; it does not serialize against another caller's commit.
    A caller which takes a turn and then, separately, reads or writes `_credentials` under `_credentialLock` leaves a
    gap in which a login or refresh holding `_credentialRefreshSemaphore` can commit with a later turn number. Because
    the queue always serves the earlier turn first, that later commit would then be reported as ending before it is
    reported as having started. `ClearCredentialsAndRaiseUnauthenticatedAsync` is the one caller of
    `ClearCredentialsAndRecordSessionEnd` which does not already run under the semaphore — see rule 10 — so it takes
    the semaphore itself for exactly the take-turn-and-clear step, and releases it before raising. Every other caller
    of `ClearCredentialsAndRecordSessionEnd` is inside `Logout`, already holding the semaphore for the same reason.
15. **A turn is "genuinely waiting" from the moment it is reserved, not from the moment it starts to wait.**
    `EarliestGenuinelyWaitingNotificationTurn` must check `_reservedNotificationTurns`, not `_notificationTurnWaiters`.
    A caller can do real work — an HTTP exchange, another raise — between `TakeNotificationTurn` and
    `EnterNotificationTurnAsync`, and during that window it has no entry at all in `_notificationTurnWaiters`. Checking
    only the waiters dictionary lets a reentrant drain leapfrog a turn which fully intends to run but has not yet
    reached the point of waiting for it.
16. **A credential notification and a session event deferred together keep the order they were deferred in, not the
    order their channel is drained in.** `RaiseSessionStartAndCredentialsUpdatedAsync` raises `Authenticated` before
    `CredentialsUpdated`; if both end up deferred by the same reentrant commit, they carry the same turn number, so
    turn safety cannot order them against each other. `TakeNextDeferredNotification` breaks the tie with a shared
    sequence counter (`NextDeferredNotificationSequence`), stamped on every deferral in both
    `_deferredCredentialNotification` and `_deferredSessionEvents`. A drain which always took one channel ahead of the
    other, regardless of sequence, would silently reverse whichever caller relied on the other order.
17. **Among deferred session events, "next" means the lowest turn number safe to raise, not the first entry appended.**
    Two reentrant callers can take turns in ascending order and still append to `_deferredSessionEvents` in the
    reverse of that order — nothing serializes the append against another caller's, since it happens after each has
    already released `_credentialRefreshSemaphore`. `TakeNextDeferredNotification` must scan the whole list for the
    lowest safe `TurnNumber`, not stop at the first entry whose turn is safe, or a later commit's session event can be
    raised ahead of an earlier commit's.

## Notes for writing a credential store

* Persist on `CredentialsUpdated`. `Authenticated` also carries credentials, but it is a session-start signal — treat
  `CredentialsUpdated` as the one that stores. It normally arrives *before* the session event; the exception is a
  refresh of stored credentials which starts a session, where `Authenticated` is raised first.
* Discard stored credentials on `Unauthenticated`. Because session events are raised last, doing so also discards any
  stale write which preceded it.
* Handlers are serialised, so a handler does not need its own lock against other handlers.
* A handler may call back into the agent. It will not deadlock, and its notification will be ordered after the current
  one — but expect to be re-entered with newer credentials before your original call returns.
* A handler which throws does not stop committed credentials being delivered to the next attempt.

## Tests

The race coverage lives in `test/idunno.AtProto.Integration.Test/OAuthCredentialRefreshTests.cs`. Each test there has a
verified negative control: the fix was neutered and the test confirmed to fail before being accepted. Keep that
discipline — a concurrency test which has never been seen to fail proves nothing.

`OAuthTestAgent` exposes internal hooks (`NotifyCredentialsUpdated`, `RaiseCredentialsUpdated`,
`CredentialNotificationQueueing`) so ordering can be asserted deterministically rather than with timing delays.

> **Warning.** Do not assume a notification has been delivered by the time the call which caused it returns. It may be
> queued behind one which is still running. Poll for the condition instead.
