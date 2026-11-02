# Issuing and withdrawing labels

The Bean Bureau has made a decision: this account talks about beans. Before publishing anything, it
needs to check who made that decision, apply its policy, resolve the target, sign a label and save it
without losing the audit trail. `LabelIssuanceService` provides that shared path.

Start with the [labeller overview](overview.md) and [persistence setup](persistence.md).
This is a host-neutral application service, not a background worker or a finished server. It doesn't
require Ozone. A future CLI, moderation UI or protocol adapter should call the same service rather than
write database rows or implement its own duplicate and negation rules.

## Set up the boundary

Register exactly one storage provider, one `ILabelIssuanceAuthorizer` and one
`ILabelIssuancePolicyProvider`. Initialize storage explicitly before accepting decisions.
`AddLabelIssuance(issuer, signingKey, httpClient)` registers a scoped service using those dependencies
and an optional `TimeProvider` registered in DI. It rejects missing or multiple implementations rather than choosing the last registration.
You can also construct `LabelIssuanceService` directly.

Import `idunno.Bluesky.Labeler.Issuance` for the service, policies, authorizers and operation
models, and `idunno.Bluesky.Labeler` for common targets and results. Team-backed authorization
is in `idunno.Bluesky.Labeler.Administration`; storage contracts are in
`idunno.Bluesky.Labeler.Storage`.

The host supplies an established signing key, separate from its repository key, and a configured
`HttpClient`. The service borrows both; the host owns disposal. Registration doesn't generate a key,
connect to a database, publish a declaration, migrate storage or start a worker.

An authorizer must return a grant or an explicit failure. There is no default allow-all authorizer.
The service first checks admission, before looking up receipts or resolving targets, then checks the
resolved scope. Receipt recovery is authorized against the stored target, even if a handle now points
elsewhere. When an unpinned withdrawal copies a stored CID, the service authorizes that final scope
again before signing or committing, including for audited no-ops. A denial commits neither a label
nor a receipt; a successful operation records the final grant's version.
`GetOperationAsync` and `GetPublicationStateAsync` are authorized reads too.

`LabelOperationContext` contains the actor DID, reason and optional correlation identity. These are
audit inputs, **not proof of authentication**: a trusted host must establish the actor instead of copying
an untrusted request field. Don't expose the storage interfaces as administrative endpoints.

> [!IMPORTANT]
> An authorization version string is audit evidence, not a database-enforced revocation fence.
> [Administrative authentication and teams](administration.md) supplies provider-issued caller
> evidence through `LabelTeamAuthorizer`, with current role/credential checks under the label commit
> lock. Bootstrapped stores reject unfenced decisions. Custom non-administrative embedding remains
> supported, but a custom version string never becomes equivalent to that typed fence.

The policy lists declared values and independently permits account and record labels. A null
`AllowedRecordCollections` permits every collection; an empty collection permits none. These are
issuance permissions, not the declaration's report-reason or reporting-subject scopes.
The policy provider must supply an immutable, versioned snapshot of the active declaration and rules.
Keep that snapshot in step with the published declaration. The shared package doesn't depend on
Bluesky declaration DTOs or publish them for you.

## Choose the subject precisely

An account target becomes a DID label URI. A record target becomes a complete AT URI with a DID
repository. Handles are resolved and verified in both directions for new issuance. The DID document
must match; record issuance also resolves the PDS and verifies the returned record identity.

| What you mean | Target |
|---------------|--------|
| An account | `LabelTarget.Account(didOrHandle)` |
| A post | `LabelTarget.Record(new AtUri("at://did:plc:example/app.bsky.feed.post/record-key"))` |
| The profile record, not the account | `LabelTarget.Record(new AtUri("at://did:plc:example/app.bsky.actor.profile/self"))` |
| A list | A record URI in `app.bsky.graph.list` |
| A feed generator | A record URI in `app.bsky.feed.generator` |
| Any other AT Protocol record | A complete record URI in its collection |

There is no Bluesky-only target-kind enum or lexicon allowlist. The service checks record existence,
not the contents against an application's schema. Collection and record-key components are required;
wildcards, queries and fragments aren't record targets.

Pass an optional CID to `LabelTarget.Record(uri, cid)` for a version-specific label. The service fetches
that version and checks the returned CID. Omitting it verifies the current record but signs a URI-wide
label; it **does not silently pin the label** to the fetched CID. Version existence is checked at
resolution time; this isn't an atomic transaction with the remote repository, which can change later.

Account and profile-record labels are never interchangeable. Account labels cannot carry a CID.

## Make a decision

Here `service` is the configured `LabelIssuanceService`, and the actor comes from your trusted host:

```csharp
LabelOperationContext context = new(actorDid, "Confirmed bean discussion", correlationId);
LabelOperationRequest request = LabelOperationRequest.Issue(
    operationId,
    LabelTarget.Account(subjectDid),
    "beans",
    context);

var result = await service.ExecuteAsync(request, cancellationToken);
if (!result.Succeeded)
{
    // Surface the code to the operator. Don't treat a failed decision as an empty success.
    throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
}

LabelOperationReceipt receipt = result.Result;
```

Keep the operation identity with your business decision. A receipt includes the original request,
resolved target, whether a new signed event was created, its `StoredLabel` when one exists, the state
at commit and audit context. The audit holds evaluation time, authorization version, policy version
for issuance and the public signing key when a new label was signed. Private keys aren't stored.

The provider commits label history, its permanent sequence, pending publication, the immutable receipt
and audit together. A failure before commit cannot leave just an audit or just a label.
No-op receipts are durable too: they advance the mutation revision but allocate no event sequence.
Their `Label` describes the existing decision and can be null when there was nothing to withdraw.

### Duplicates, retries and concurrency

The decision identity is **issuer + URI + label value**, not CID. A later label for another CID replaces
the previous decision for that URI/value. Storage event sequence orders distribution; the greatest
signed creation instant determines the current decision, including unpublished and legacy appended
labels. Conflicting payloads at the greatest identical timestamp fail as `AmbiguousHistory` rather than
silently choosing whichever row came last.

With a new operation ID, issuing the same active scope, CID and expiry is an audited no-op. A changed
CID or expiry creates a new label. An expired positive label can be issued again. Negating an already
negated or absent decision is a no-op; an expired positive label can still be explicitly withdrawn.

Reusing an operation ID with the same intent recovers its **original** receipt, even after policy,
publication or current labels have changed. Recovery doesn't resolve again or sign again.
Changed intent under that ID fails as `IdempotencyConflict`. Intent includes the operation kind, original
target, CID, value, UTC expiry, actor and reason; it excludes correlation identity, generated timestamp
and signature, and policy/authorization versions. CID comparisons use parsed CID identity, not equivalent
base encodings. The receipt still preserves the exact signed representation.
Changing a handle to its DID is a changed original target, even if both resolved to the same account.

Service operation IDs are ordinal UTF-8 strings, nonblank, without U+0000 and at most 256 bytes. They
have a separate namespace from low-level `ILabelStore.AppendAsync` operation IDs. Retain them until your
deliberate purge; don't recycle them after losing a response.

Concurrent writers compare the store revision and recheck the decision under the provider's stream
lock. One can receive `StaleDecision`, including when publication or another target changed the global
revision. Retry the original request after re-evaluation; the same ID recovers a concurrent winner if
it committed identical intent. The service doesn't repeatedly reauthorize and resign in a hidden loop.
Direct provider writes are trusted infrastructure operations, not an alternate authorization path.

### Time and expiry

The service chooses creation time; callers don't supply it. Each new decision is strictly later than
the current one for that URI/value, even if the clock stalls or moves backwards. It uses the service
clock or one tick after the predecessor, whichever is later. Historic signed timestamps retain
fractional precision beyond .NET ticks when comparing ordering and expiry. Advancing past the maximum
representable instant fails as `TimestampExhausted`.

An optional issuance expiry must be later than the final assigned creation time. A clock adjustment
can therefore make an otherwise future-looking expiry invalid. Negations never expire. Expiry doesn't
delete history, generate a negation or run a scheduled task.

## Withdraw a decision

```csharp
LabelOperationRequest withdrawal = LabelOperationRequest.Negate(
    withdrawalOperationId,
    LabelTarget.Account(subjectDid),
    "beans",
    new LabelOperationContext(actorDid, "Appeal upheld", correlationId));

var result = await service.ExecuteAsync(withdrawal, cancellationToken);
```

Use a DID-based target for withdrawal. It reads stored decisions without contacting the subject's PDS,
so deleted records and unavailable accounts can still be corrected. A supplied CID must match the
current decision or the operation fails as `StaleDecision`. If omitted, the service copies the current
label's CID; it doesn't accidentally produce a URI-wide withdrawal for a CID-specific label.

Negation is a newly signed protocol label with the same issuer, URI and value, the stored scope,
`neg: true`, a later creation time and no expiry. It never deletes or edits the positive label.
Withdrawal still requires authorization but doesn't require the old value or collection to remain in
issuance policy. Removing a declaration mustn't make a correction impossible.

## Recover and publish

On `OutcomeUnknown`, don't substitute a new operation ID. Call
`GetOperationAsync(operationId, currentContext)` or retry the same `ExecuteAsync` request.
A recovered receipt proves that original decision committed. `OperationNotFound` means no receipt
is retained; it isn't proof that an earlier ambiguous transaction rolled back or that history was
never purged. Restore storage availability and reconcile the same identity before making a replacement.

Cancellation before commit submission propagates `OperationCanceledException`. Once submitted, a
commit-time database failure or cancellation is an explicit `OutcomeUnknown`. The service attempts
one receipt lookup when the caller token permits it; failed or cancelled recovery never becomes a
success-shaped rollback. It performs no detached recovery and never blindly retries a submitted commit.
An inspection cancelled independently remains an ordinary cancelled read.

Durable success means **queued**, not visible to readers. Compare `IssuedThrough` and `PublishedThrough`
using authorized `GetPublicationStateAsync`; a receipt's `StateAtCommit` is historical, not live status.
A trusted hosting worker calls the existing `ILabelStore.PublishAsync` to advance the contiguous
publication boundary. There is no separate public service publish command, and this package starts
no worker. A restart resumes from the stored boundary without skipping earlier labels.
Publication means durable local availability, not delivery acknowledgement from every subscriber.

Retirement closes ordinary issuance and negation. Identical committed retries remain recoverable;
new operations, including no-ops, fail as `IssuanceClosed`. Dedicated retirement orchestration uses the
existing lifecycle/provider contracts and is outside this service.

## Error codes and their causes

Always inspect `Succeeded` before reading `Result`. Expected operational failures return
`LabelServiceError`; HTTP status, AT error detail and original exceptions are retained where available.
The service logs failure categories without recording subjects, reasons or credentials. Don't expose
exception diagnostics to unauthenticated clients.

| Code | Cause | Operator or caller action |
|------|-------|---------------------------|
| `InvalidTarget` | A withdrawal uses a handle instead of the stable DID target. | Use the stored DID account or record URI. Syntactically invalid factory inputs throw instead. |
| `TargetResolutionFailed` | Identity verification, DID/PDS discovery or record retrieval failed, timed out, or returned a different identity. | Inspect retained diagnostics and identity configuration; retry the same request after correction. |
| `TargetNotFound` | The PDS authoritatively reports the record/version missing (`404` or `RecordNotFound`). | Check the URI and requested CID; don't mistake connectivity failure for absence. |
| `CidMismatch` | A successful record fetch returned a different CID from the explicitly requested version. | Investigate the PDS response and request; don't silently relabel another version. |
| `Unauthorized` | The required authorizer denied admission, a resolved decision, recovery or inspection. | Authenticate appropriately or obtain the necessary grant; no mutation is allowed by default. |
| `PolicyUnavailable` | The required policy provider cannot supply a valid active snapshot. | Restore declaration/policy availability; never fall back to allowing issuance. |
| `ValueNotDeclared` | The issuance value isn't in the active policy's declared values. | Choose a declared value or deliberately update the declaration and policy. |
| `TargetNotAllowed` | Policy forbids the account, records or the record's collection. | Review issuance permissions, not reporting scopes. |
| `InvalidExpiry` | Issuance expiry is at or before the final service-owned creation time. | Choose a later expiry, accounting for predecessor timestamps. |
| `TimestampExhausted` | No representable creation time can supersede the current decision. | Inspect the historic timestamp; don't wrap or reuse it. |
| `StaleDecision` | The revision changed concurrently, or an explicit withdrawal CID isn't current. | Re-read and re-evaluate; retry the original ID with unchanged intent or deliberately create a new decision. |
| `IdempotencyConflict` | The service operation ID already identifies different semantic intent. | Recover the original operation; use a new ID only for a genuinely new decision. |
| `OperationNotFound` | No service receipt is retained for that ID. | Check the ID, store and purge history; reconcile uncertain commits without assuming rollback. |
| `AmbiguousHistory` | Different canonical payloads share the latest creation instant for a decision. | Inspect signed history and resolve the conflict operationally; no arbitrary winner is chosen. |
| `IssuanceClosed` | The store is retiring, distributing, in grace, retired or purged. | Use the lifecycle workflow; don't reopen issuance through database edits. |
| `SigningFailed` | The established key's cryptographic/platform signing operation failed. | Restore key/platform availability; never generate a replacement key implicitly. |
| `StorageUnavailable` | A classified database operation failed before commit submission, or a read failed. | Restore database availability and retry the original request. |
| `OutcomeUnknown` | A submitted commit lost acknowledgement or was cancelled; or a concurrent committed receipt couldn't be authorized because of cancellation. | Recover or retry the original ID before any replacement operation. |

Authorizer and policy implementations are responsible for returning their documented failure codes,
including `Unauthorized` and `PolicyUnavailable`; the service preserves their explicit errors.
Invalid arguments, disposed dependencies, uninitialized/wrong-issuer stores and corrupt or unsupported
stored representations remain exceptions. These are programming, configuration or integrity failures,
not ordinary denials or missing targets.

## Provider contract and upgrade

`ILabelStore` provides both signed-label persistence and atomic decision operations.
`ReadDecisionAsync` includes unpublished history. `CommitDecisionAsync` atomically
checks revision/lifecycle/current intent and saves the signed event, receipt, audit and queue progress.
It distinguishes a known precommit database failure from an uncertain submitted commit without parsing
exception messages. Custom providers must preserve these semantics for every entry point.

Schema version two adds `label_decisions` for versioned immutable receipt envelopes and their audit
context. It doesn't replace signed history, rename old tables or change legacy append identity.
Stop services, take a complete backup and run `InitializeAsync` with setup privileges to upgrade
version one in place. Existing streams, exact label bytes, receipts, sequence numbers, publication and
retirement progress survive. MySQL setup resumes additive DDL after interruption; see the
[provider upgrade and backup guidance](persistence.md#upgrade-and-back-up-your-database).

Current-decision reconciliation scans authoritative history under the existing stream lock rather
than maintaining a projection that legacy writes could bypass. Large histories can make issuance
expensive and serialize unrelated operations; monitor this before choosing a deployment scale.
Audit reasons and actors persist inside receipts until deliberate retirement/purge, including no-ops.
Protect them through database access controls, backups and your retention policy.
