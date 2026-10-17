# Writing a followers-only reply bot

The [durable bot tutorial](durableBotSdk.md) queues replies when a post matches a trigger. In this guide, we'll restrict those replies to authors who follow the bot account, then reduce the cost of checking that relationship with three caching strategies.

Start with the application, `SimpleReplyBotPolicy`, packages, and configuration from that tutorial. Keep its policy's trigger checks and `BotActions.Reply()` call. The classes below belong in separate files in that application, using the namespace `SimpleReplyBot`.

[!include[Untrusted data warning](../includes/untrusted-data-warning.md)]

## Decide what "followers only" means

This guide checks whether the author follows the bot **before sending a new reply**, not whether they followed it when the source post was written. Queued actions and archive replay can make those very different times.

An authenticated profile's `Viewer.FollowedBy` is the author's follow record targeting the bot. `Viewer.Following` means the opposite: the bot follows the author.

No approach makes checking membership and publishing a reply atomic. AppView indexing can lag, and an author can unfollow between a successful check and the write. Caching introduces an additional, deliberate freshness window.

| Approach | Relationship-read cost | Appropriate when |
| --- | --- | --- |
| Fresh profile check | One profile request per new reply attempt | Reply volume is low, or you need the freshest available relationship information. |
| Per-author TTL cache | One profile request per uncached or expired author | A modest bot sees repeated posts from the same authors and can tolerate briefly stale membership. |
| Refreshed follower snapshot | All follower pages per refresh | Many candidate replies share a manageable follower population, and a refresh interval's delay is acceptable. |
| Snapshot plus fresh confirmation | Snapshot refreshes, plus one profile request per snapshot-positive attempt | Many candidates are non-followers, but new writes to likely followers need fresh confirmation. |

The last strategy does not remove the per-reply lookup for followers. It only avoids that lookup for authors absent from the snapshot, at the cost of temporarily rejecting new followers.

## Keep network calls out of the policy

`IBotPolicy.Evaluate()` is synchronous and must not make network calls. Leave it responsible for matching posts and constructing complete reply intents. Put the membership check in an `IBotActionHandler`, where the runtime provides an authenticated `remote.Agent` with protected, rate-accounted transport.

We'll use a small application-owned interface so the reply handler is independent of the selected caching strategy:

```csharp
using idunno.AtProto;
using idunno.Bluesky.BotSdk;

namespace SimpleReplyBot;

public interface IFollowerEligibility
{
    Task<bool> IsFollowerAsync(
        IBotActionRemote remote,
        Did author,
        CancellationToken cancellationToken);
}
```

First, create `FreshFollowerEligibility.cs`. All strategies use this implementation when they need a profile lookup:

```csharp
using idunno.AtProto;
using idunno.Bluesky.BotSdk;

namespace SimpleReplyBot;

public sealed class FreshFollowerEligibility : IFollowerEligibility
{
    public async Task<bool> IsFollowerAsync(
        IBotActionRemote remote,
        Did author,
        CancellationToken cancellationToken)
    {
        var response = await remote.Agent.GetProfile(
            author,
            cancellationToken: cancellationToken);

        if (!response.Succeeded)
        {
            throw new RemoteFailureException(
                response.StatusCode,
                response.AtErrorDetail?.Error,
                "checking follower eligibility");
        }

        if (response.Result.Did != author ||
            response.Result.Viewer is not { } viewer)
        {
            throw new InvalidOperationException(
                "The authenticated profile response has no matching viewer state.");
        }

        return viewer.FollowedBy is not null;
    }
}
```

A failed request is not evidence that an author does not follow the bot. Let it propagate so the runtime can classify the failure; never cache it as `false`. Missing viewer state is also an explicit error, rather than an apparently successful membership check.

## Wrap the built-in reply handler

Create `FollowersOnlyReplyHandler.cs`:

```csharp
using idunno.AtProto;
using idunno.Bluesky.BotSdk;

namespace SimpleReplyBot;

public sealed class FollowersOnlyReplyHandler(
    IFollowerEligibility eligibility) : IBotActionHandler
{
    private readonly ReplyActionHandler _inner = new();

    public string Kind => _inner.Kind;

    public int PayloadVersion => _inner.PayloadVersion;

    public void Validate(BotAction action) => _inner.Validate(action);

    public Task<BotActionResult?> ReconcileAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken) =>
        _inner.ReconcileAsync(action, remote, cancellationToken);

    public async Task<BotActionResult> ExecuteAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken)
    {
        if (action.SourceDid == remote.BotDid)
        {
            return new BotActionResult(false, null, "ignoring own post");
        }

        if (!await eligibility.IsFollowerAsync(
            remote, new Did(action.SourceDid), cancellationToken))
        {
            return new BotActionResult(
                false, null, "author does not follow the bot");
        }

        return await _inner.ExecuteAsync(action, remote, cancellationToken);
    }
}
```

Delegating validation and reconciliation preserves the built-in handler's fixed record key, saved payload verification, and thread/reply-restriction checks. Do not replace these with an ad hoc `Reply()` call.

The order matters: reconcile an uncertain write **before** applying a new membership exclusion. If a previous attempt published successfully but lost its response, the existing reply must be recognized even if the author has since unfollowed. Only an attempt that still needs a write reaches the membership check.

A negative membership result is a terminal `not-sent` outcome, not a deferred action. If that author follows later, the same saved intent is not automatically retried. This also applies to stale negative cache entries.

## Register the handler and choose a strategy

Use the durable tutorial's `Program.cs`, adding the following after `AddBlueskyBot<SimpleReplyBotPolicy>(...).WithSqliteStorage(...)`, but before `builder.Build()`:

```csharp
using Microsoft.Extensions.DependencyInjection;

// These namespaces may already be imported by Program.cs.
using idunno.Bluesky.BotSdk;
using SimpleReplyBot;

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<
    IFollowerEligibility, FreshFollowerEligibility>();

var builtInReply = builder.Services.Single(descriptor =>
    descriptor.ServiceType == typeof(IBotActionHandler) &&
    descriptor.ImplementationType == typeof(ReplyActionHandler));

builder.Services.Remove(builtInReply);
builder.Services.AddSingleton<
    IBotActionHandler, FollowersOnlyReplyHandler>();
```

Place the `using` directives with the other directives at the top of `Program.cs`. Register exactly one `IFollowerEligibility` strategy; the sections below replace that registration. Keep the clock registration for the cached strategies.

Remove only the built-in reply registration, not all `IBotActionHandler` services. Adding the wrapper alongside the built-in handler would register two handlers for `("reply", 1)`, which the runtime rejects. The wrapper intentionally retains the `"reply"` kind so the built-in transport can create and reconcile its records. See [Writing a custom bot action](customBotActions.md) for action identity and versioning.

These registrations are singletons for one bot account. Do not share an eligibility instance across accounts; its cached membership is relative to the authenticated bot.

## Approach 1: Cache profile checks by author

Cache both positive and negative answers for a short time. A shorter positive TTL notices unfollows sooner; a shorter negative TTL admits new followers sooner. Choose these as product behavior, not just performance settings.

Create `CachedFollowerEligibility.cs`:

```csharp
using idunno.AtProto;
using idunno.Bluesky.BotSdk;

namespace SimpleReplyBot;

public sealed class CachedFollowerEligibility(
    TimeProvider time) : IFollowerEligibility, IDisposable
{
    private const int MaximumEntries = 10_000;
    private readonly FreshFollowerEligibility _fresh = new();
    private readonly Dictionary<Did, Entry> _entries = [];
    private readonly LinkedList<Did> _insertionOrder = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<bool> IsFollowerAsync(
        IBotActionRemote remote,
        Did author,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_entries.TryGetValue(author, out Entry? entry) &&
                time.GetElapsedTime(entry.CheckedAt) < entry.Lifetime)
            {
                return entry.IsFollower;
            }

            if (_entries.Remove(author, out Entry? expired))
            {
                _insertionOrder.Remove(expired.Node);
            }
            long checkedAt = time.GetTimestamp();
            bool follows = await _fresh.IsFollowerAsync(
                remote, author, cancellationToken);

            TimeSpan lifetime = follows
                ? TimeSpan.FromMinutes(2)
                : TimeSpan.FromSeconds(30);
            if (time.GetElapsedTime(checkedAt) >= lifetime)
            {
                throw new TimeoutException(
                    "The profile lookup exceeded its freshness window.");
            }

            // Bound memory even when many distinct authors match the trigger.
            if (_entries.Count >= MaximumEntries)
            {
                LinkedListNode<Did> oldest = _insertionOrder.First!;
                _entries.Remove(oldest.Value);
                _insertionOrder.RemoveFirst();
            }

            LinkedListNode<Did> node = _insertionOrder.AddLast(author);
            _entries[author] = new Entry(follows, checkedAt, lifetime, node);

            return follows;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private sealed record Entry(
        bool IsFollower,
        long CheckedAt,
        TimeSpan Lifetime,
        LinkedListNode<Did> Node);
}
```

Replace the eligibility registration with:

```csharp
builder.Services.AddSingleton<
    IFollowerEligibility, CachedFollowerEligibility>();
```

This deliberately simple cache serializes access, including lookups: concurrent requests for the same author reuse a successful answer instead of creating a burst. Different authors also wait for the lookup in progress. `MaximumEntries` caps the combined positive and negative entries at 10,000, checked before insertion. At capacity, first-in, first-out (FIFO) eviction removes only the oldest inserted entry. Cache hits do not change insertion order; replacing an expired entry puts it at the back. Removing an expired entry also removes its linked-list node, so eviction bookkeeping cannot grow independently of the dictionary. Do not remove the size limit: TTL alone does not bound how many distinct authors can enter the cache before expiry.

The TTL starts before the request, using a monotonic `TimeProvider` timestamp. Errors and cancellation release the gate without installing a new entry. A restart starts with an empty cache; it does not reset the durable outbox.

This approach works best when authors repeat. If every matching post has a different author, the cache saves few requests. An author who unfollows can still receive a reply during the positive TTL, in addition to any AppView lag.

## Approach 2: Refresh a complete follower snapshot

Fetch the bot's followers in pages, store only their DIDs, and replace the published set only after all pages succeed. Membership checks then use the set without individual profile requests.

Create `SnapshotFollowerEligibility.cs`:

```csharp
using idunno.AtProto;
using idunno.Bluesky.BotSdk;

namespace SimpleReplyBot;

public sealed class SnapshotFollowerEligibility(
    TimeProvider time) : IFollowerEligibility, IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HashSet<Did>? _followers;
    private long _refreshStartedAt;

    public async Task<bool> IsFollowerAsync(
        IBotActionRemote remote,
        Did author,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_followers is null ||
                time.GetElapsedTime(_refreshStartedAt) >= RefreshInterval)
            {
                long refreshStartedAt = time.GetTimestamp();
                HashSet<Did> replacement = [];
                HashSet<string> seenCursors = new(StringComparer.Ordinal);
                string? cursor = null;

                do
                {
                    var response = await remote.Agent.GetFollowers(
                        actor: new Did(remote.BotDid),
                        limit: 100,
                        cursor: cursor,
                        cancellationToken: cancellationToken);

                    if (!response.Succeeded)
                    {
                        throw new RemoteFailureException(
                            response.StatusCode,
                            response.AtErrorDetail?.Error,
                            "refreshing the bot's follower snapshot");
                    }

                    foreach (var follower in response.Result)
                    {
                        replacement.Add(follower.Did);
                    }

                    cursor = response.Result.Cursor;
                    if (cursor is not null && !seenCursors.Add(cursor))
                    {
                        throw new InvalidOperationException(
                            "Follower pagination repeated a cursor.");
                    }
                }
                while (cursor is not null);

                if (time.GetElapsedTime(refreshStartedAt) >= RefreshInterval)
                {
                    throw new TimeoutException(
                        "The follower scan exceeded its freshness window.");
                }

                _followers = replacement;
                _refreshStartedAt = refreshStartedAt;
            }

            return _followers.Contains(author);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
```

Replace the eligibility registration with:

```csharp
builder.Services.AddSingleton<
    IFollowerEligibility, SnapshotFollowerEligibility>();
```

The sample refreshes **on demand**: the first candidate after startup or expiry waits for a complete scan. There is no timer, no idle polling, and no background task retaining the runtime-owned `remote.Agent`. Subsequent candidates reuse the set until it expires. The gate coalesces concurrent refreshes and prevents readers from seeing partial sets.

If a page fails, the previous snapshot remains stored but the check throws; it does not use expired data or treat a partial scan as complete. A later attempt starts a new scan. The freshness window starts before the first page, and a scan that takes the whole window is rejected. For a very large follower population, use a different strategy or explicitly choose a longer freshness window.

### Snapshot memory requirements

The retained snapshot is a `HashSet<Did>` containing only follower DIDs, not profiles, handles, avatars, or posts. As a rough planning estimate on a 64-bit .NET runtime, allow around 1-2 MB per 10,000 typical `did:plc` followers, or around 10-20 MB per 100,000. These are approximate managed-memory estimates, not measured guarantees or process-memory limits: DID length, SDK object representation, hash-set capacity, and runtime version affect the actual footprint. Longer `did:web` identifiers can cost more. Profile your deployment with representative data.

During refresh, the previous and replacement sets coexist, so budget roughly twice the steady-state snapshot storage, plus pagination cursors, the current page's deserialized profiles, HTTP/JSON buffers, and transient collection-resizing and garbage-collection overhead. Only each follower's DID is retained from those profiles; the rest of the page can be collected after processing. The `seenCursors` set also grows with the number and length of pagination cursors.

This snapshot intentionally has no entry cap or FIFO eviction: it must retain the complete follower set for absence to mean non-membership. Its memory use therefore grows with the follower population, and the freshness timeout is not a memory limit. Monitor memory and follower growth; use the bounded per-author strategy if a full snapshot no longer fits your budget. Never silently truncate or evict snapshot members while continuing to treat misses as non-followers.

You can instead refresh on a schedule in an application-owned service, but that requires an explicit authenticated-client lifetime, shutdown handling, and the same rate accounting. Publish only complete replacements, expose refresh failures, and block or defer membership-dependent writes when the last successful snapshot is too old. Do not start an untracked background task inside this handler.

A complete pagination pass is not a transactionally consistent snapshot of the social graph. Follows and unfollows can happen during the scan, and AppView can lag. "Complete" here means that every returned page was read successfully, not that every relationship was observed at one instant.

At up to 100 followers per request, a bot with 2,000 followers needs roughly 20 requests for a full scan, rather than a profile request for every candidate. The actual page count depends on server responses. This wins when there are many candidates per refresh; it can be much more expensive than individual checks for a quiet bot with a large following. Memory grows with the follower population, and separate bot processes maintain separate caches.

## Approach 3: Use a snapshot, then confirm before writing

Use the snapshot as a cheap negative filter, but perform a fresh profile check for authors present in it.

Create `ConfirmedSnapshotFollowerEligibility.cs`:

```csharp
using idunno.AtProto;
using idunno.Bluesky.BotSdk;

namespace SimpleReplyBot;

public sealed class ConfirmedSnapshotFollowerEligibility(
    SnapshotFollowerEligibility snapshot) : IFollowerEligibility
{
    private readonly FreshFollowerEligibility _fresh = new();

    public async Task<bool> IsFollowerAsync(
        IBotActionRemote remote,
        Did author,
        CancellationToken cancellationToken)
    {
        if (!await snapshot.IsFollowerAsync(
            remote, author, cancellationToken))
        {
            return false;
        }

        return await _fresh.IsFollowerAsync(
            remote, author, cancellationToken);
    }
}
```

Replace the eligibility registration with these two registrations:

```csharp
builder.Services.AddSingleton<SnapshotFollowerEligibility>();
builder.Services.AddSingleton<
    IFollowerEligibility, ConfirmedSnapshotFollowerEligibility>();
```

This is useful when many trigger matches come from non-followers, and you want to reduce the chance of replying to someone who has unfollowed since the snapshot. The final profile read is deliberately not TTL-cached.

This strategy reuses the same complete snapshot and has the same memory requirements; it does not maintain another cache.

It still performs one profile request for each snapshot-positive new write attempt, plus the snapshot scans. If almost every candidate is a follower, it costs more than fresh checks alone. New followers absent from the snapshot are rejected until a refresh sees them; their already-rejected actions are not revived. If that delay is unacceptable, use fresh checks rather than this negative filter.

## Rate limits, failures, and operation

All network calls above use `remote.Agent`. The bot runtime accounts for requests at the HTTP boundary, including failed requests and pagination, and honors server cooldowns. A local budget can delay delivery, and server throttling can defer an action through the durable sender. Caching reduces request demand; it does not remove service limits or the built-in reply handler's other reads and writes. Do not bypass accounting with a second agent, and do not catch a throttle or transport exception and convert it to `false`.

The runtime retries supported transient failures, including transport/timeouts and retryable `RemoteFailureException` instances. A non-retryable `RemoteFailureException` or an unexpected programming or response-validation error stops the host rather than silently discarding retained work. Investigate these errors rather than treating every exception as a transient relationship lookup failure.

All caches here are process-local, disposable, and reconstructible. The durable action remains in the outbox if the host stops; its original payload and record key are reused. A follower cache is not an audit of historical membership. These handler-only strategies also do not reduce the number of matching intents stored by the reader, and the built-in reply reconciliation can read thread targets before membership is checked.

Keep the policy's trigger narrow, and exclude the bot's own posts in the policy as well if you want to avoid enqueuing them. For example, a bot intended to answer mentions should not reply to every post made by every follower.

> [!IMPORTANT]
> In developer mode, the runtime validates actions and marks them simulated without invoking `ReconcileAsync` or `ExecuteAsync`. It does not exercise these membership checks or populate their caches. Follow the durable tutorial's separate-database instructions when moving to live mode; simulated actions are not replayed as live replies.

Before deploying, exercise positive and negative membership, unfollow/follow transitions, cache expiry, concurrent lookups, cancellation, failed profile requests, multi-page refreshes, a failed later page, and restart with an uncertain reply already present remotely. Check the per-author cache at capacity, FIFO eviction, unchanged insertion order on hits, and removal of expired entries from eviction bookkeeping. Verify that an aborted refresh never publishes a partial snapshot, and measure snapshot memory during refresh with a representative follower population. Use `FakeTimeProvider` for expiry checks and explicit synchronization for concurrency; do not use sleeps to establish ordering.
