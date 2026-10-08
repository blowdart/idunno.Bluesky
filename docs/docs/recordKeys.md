# Reliable repository-record creation with fixed keys

A record key (`rKey`) is the final component of a record's AT URI. Supplying one lets an application know the record URI before sending a create request. If the response is lost or the request times out, the application can retrieve that URI and verify the record before deciding what to do.

The SDK does not persist keys or reconcile uncertain writes. Generate or choose a key before the first request, persist it with the intended operation, and reuse that same key after an uncertain outcome. Before retrying, read the record at the known URI and verify that its contents match the intended record. If it does, treat the operation as complete; if it does not exist, retry with the same key. A fixed key alone does not provide exactly-once delivery, and create remains create: an existing key is not overwritten or assigned a replacement.

## Posting with a fixed key

The keyed overloads of `Post`, `ReplyTo`, and `Quote` accept a `RecordKey? rKey`. Post and quote overloads support their text, language, image, video, and external-card variants. For example:

```csharp
RecordKey postKey = new("3lcf6ry7xy22x");

AtProtoHttpResult<CreateRecordResult> result = await agent.Post(
    rKey: postKey,
    text: "A post whose URI is known before it is sent.",
    cancellationToken: cancellationToken);
```

Persist `postKey` before sending. If the outcome is uncertain, construct the same URI from the authenticated repository DID, the `app.bsky.feed.post` collection, and `postKey`, then retrieve and verify that record before retrying. Do not call `TimestampIdentifier.Next()` again for a retry.

`PostBuilder` does not own the key; pass it to the keyed `Post(rKey, postBuilder, ...)` overload. For an already-constructed `Post`, setting `extractFacets: false` preserves the supplied post (including its timestamps, facets, and reply references). When gates are created with a post, the post's key is also used for the corresponding threadgate or postgate record, as required by those records' lexicons.

## Other supported records

The following single-record convenience APIs expose a keyed overload:

| Records | APIs |
| --- | --- |
| Likes | `Like` overloads accepting a strong reference, URI and CID, `PostView`, or `FeedViewPost` |
| Reposts | `Repost` overloads accepting a strong reference, URI and CID, `PostView`, or `FeedViewPost` |
| Follows and blocks | `Follow` and `Block` |
| Lists | `CreateList`, `AddToList`, and `BlockModList` |
| Reference-list opt-outs | `CreateReferenceListOptOut` |
| Posts | `Post`, `ReplyTo`, and `Quote` |

These collections use caller-selectable `tid` keys. Supply a key that satisfies the collection's lexicon; `TimestampIdentifier.Next()` is a convenient way to create the initial key before persisting it.

For example, a list can be created at a URI known in advance:

```csharp
RecordKey listKey = new("3lcf6ry7xy22y");

AtProtoHttpResult<CreateRecordResult> result = await agent.CreateList(
    rKey: listKey,
    list: list,
    cancellationToken: cancellationToken);
```

The supplied value is forwarded to the existing `createRecord` or `applyWrites` path. The SDK keeps its normal validation, authentication, and protected HTTP transport. It does not look up or silently deduplicate likes, follows, list items, or other records; the server's response, including an existing-record error, is returned normally. Handle-based `Follow` and `AddToList` first resolve the handle and can return `NotFound` without issuing a create request if it cannot be resolved.

## Keys that are fixed, derived, or unavailable

Some repository records do not allow caller-selected keys:

* Profile, status, notification-declaration, content-visibility-declaration, and conversation-declaration helpers use their protocol-defined singleton key, `self`. `CreateProfile`, `CreateStatus`, `CreateLiveStatus`, and `SetNotificationDeclaration` therefore do not accept `rKey`; update-style APIs remain updates.
* `AddThreadGate` and `AddPostGate` create gates for an existing post. Their record keys are derived from that post's key and cannot be independently selected. The keyed post helpers preserve this relationship when they create a post and its gates together.
* Draft publishing and multi-post/thread publishing may create several posts. Those paths do not currently accept a distinct key for each post and are unchanged; one key must never be reused for unrelated records. A future multi-record API would need a per-record identity mechanism.
* Blob uploads, chat operations, reads, updates, deletes, and other operations that do not create a caller-keyable repository record are unchanged.

The generic `CreateBlueskyRecord<TRecord>` helper already accepts `rKey`. This page describes the additional convenience APIs that forward it; it does not change protocol-fixed key rules.

Finally, a fixed key identifies the primary post record, not an entire higher-level workflow. Gate writes may be grouped with a post, but other helper or application work is not automatically atomic. Applications remain responsible for checking every operation involved in recovery.
