# Retire a labeller safely

Retirement permanently closes ordinary label issuance/negation, new reports and appeals.
It is not a switch back to Active, whole-database erasure, or a request to delete downstream
copies. Announce the shutdown and arrange an operator to monitor distribution and recovery.
Keep the labeler endpoint, discovery and public verification material available while withdrawals
are distributed. Already signed history can remain available after retirement without private keys.

## Preview, then confirm the exact intent

Use the shared authenticated administration CLI or Xenon's **Lifecycle** page as a currently
authorized Administrator. Inspection uses `getRetirementState` (GET remotely). Preflight uses
`prepareRetirement` (POST) and does not execute its intended action.

Every mutating command needs a stable operation ID and nonblank audit reason. Preflight's
`target` is the **exact administration method name**, not the CLI subcommand. Use the same
operation ID, reason, explicitly selected grace days, inventory and attestation in the subsequent
command. Begin, purge, completion, key-removal request and key-removal attestation require their
own expiring, server-issued confirmation. A challenge for one action cannot approve another.

For example, on a host with protected local administration configured:

```text
labeler-admin retire status --local-endpoint /run/bean-bureau/admin.sock
labeler-admin retire preflight --action beginRetirement --operation-id retirement-start --reason "Close the service" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin retire begin --operation-id retirement-start --reason "Close the service" --confirmation "<retirement.confirmation from preflight>" --local-endpoint /run/bean-bureau/admin.sock
```

Inspect the entire preflight, including phase, boundaries, publication backlog, maintenance/host
blockers, retained-data scope and consequences. The response's `retirement.confirmationExpiresAt`
is the challenge expiry. If history or generation changes, or the challenge expires, obtain a fresh
preflight and explicitly retry. Never manufacture a challenge or substitute `--yes`.
The CLI deliberately uses two explicit invocations rather than silently fetching approval.
Remote use selects `--admin-url`, `--audience`, `--handle` and `--client-id` instead of the local
endpoint; each invocation requests its own method-bound service permission.

Xenon first displays a read-only preview. A separate antiforgery-protected submission carries the
intended immutable fields and challenge; type the exact action name to confirm. A generic checkbox
does not authorize an irreversible action. Every submission obtains fresh browser caller evidence.
The backend, not visible buttons or hidden inputs, validates live authority, immutable intent and
challenge freshness.

## Watch the durable worker

Confirmed `begin` starts a durable server-side worker. Browser closure and CLI disconnection do
not cancel it. The worker scans frozen history, withdraws applicable unexpired positive labels
with their CID scope intact, and publishes withdrawals in bounded checkpointed work.
Expired or already-withdrawn history does not need another negation.

`retire status` shows lifecycle/stage, frozen boundary, timestamp-scan and negation checkpoints,
issued/published boundaries, last activity and last actual checkpoint progress. Recent activity
is not proof that a worker is alive, and local publication is not downstream acknowledgement.
When `Distributing` has no publication backlog, an operator must explicitly begin grace.

All new publication, including retirement negations, shares the default
[Bluesky AppView label-service limits](https://github.com/bluesky-social/bsky-docs/blob/main/docs/advanced-guides/rate-limits.md#label-limits):
5 labels per rolling second, 10,000 per rolling hour and 100,000 per rolling 24 hours.
Committed excess remains pending; restarting, adding hosts or retrying does not reset the budget.
The host continues polling without treating throttled backlog as a failure. Monitor the issued/published
gap and allow enough time before beginning grace or draining hosts for a recovery rotation.
Publication-only waiting does not advance the retirement generation or last-progress timestamp,
or create repeated empty-batch audit events. Completion is checkpointed once the publication backlog clears.
Query and subscription replay of already-published history does not consume new-publication allowance;
local pacing does not prove a downstream consumer received or accepted every label.

Manual advancement and the background worker share execution and failure recovery. A failed operation
retains its administrative checkpoint while the host retries only persistence of the pause, not the
failed batch. Disconnecting the administration request does not discard that pending pause.
Inspect the saved failure and explicitly resume after restoring storage or signing.
When positive observations have identical creation timestamps, retirement retains the longest-lived
one, including a label without expiry, so an expired duplicate cannot hide a still-applicable label.

```text
labeler-admin retire pause --operation-id pause-1 --reason "Investigate the signing host" --yes --local-endpoint /run/bean-bureau/admin.sock
labeler-admin retire resume --operation-id resume-1 --reason "Restored key and reviewed saved progress" --yes --local-endpoint /run/bean-bureau/admin.sock
```

Pause stops new batches after any already-committed atomic batch; it never reopens issuance.
Errors, revoked/expired worker authority and missing keys require investigation and an explicit
authorized resume. Resume uses a **new command operation ID** to renew authority for the existing
saved workflow; it does not replace its original scope, history boundary or checkpoints.
Retain the same resume command ID if its acknowledgement is uncertain.
The original authorized receipt is recoverable even after the worker changes phase or the
signing configuration changes. A failed batch read, uncertain batch commit or failed publication
stops that worker from admitting more work until its pause is durably recorded. Recovery uses
the administrative execution checkpoint, so a delayed fault cannot override a later explicit
resume. Exhausted label timestamps are recorded as a durable pause, not retried as running work.
`retire advance` remains a manual one-step compatibility command, not the normal execution loop.
Inspect maintenance/rotation blockers rather than repeatedly advancing around them.

An explicitly requested signing-key recovery rotation must preserve the retirement phase.
Use the coordinated [rotation workflow](deployment.md#key-installation-and-rotation),
including host drain and replacement-publication checks. Resolve an interrupted rotation before
releasing maintenance. A previously fault-paused retirement still requires explicit resume.
Review every distribution interruption before completing grace.

## Select grace and attest availability

There is **no default duration and no protocol-mandated duration**. Explicitly choose 1-365 days.
The deadline uses elapsed database time; outages do not pause that clock. Review public query and
subscription availability, publication backlog, discovery, host failures and any rotation outage.
Extend grace when that review warrants more time; extensions never shorten the deadline.

```text
labeler-admin retire grace --days 14 --operation-id grace-1 --reason "Selected fourteen days for distribution" --yes --local-endpoint /run/bean-bureau/admin.sock
labeler-admin retire extend-grace --days 7 --operation-id grace-extension-1 --reason "Distribution interrupted; allow additional time" --yes --local-endpoint /run/bean-bureau/admin.sock
labeler-admin retire preflight --action completeRetirement --operation-id complete-1 --reason "Reviewed grace availability" --attestation "Reviewed endpoint, query/replay and outage records; extended grace for the recorded outage" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin retire complete --operation-id complete-1 --reason "Reviewed grace availability" --attestation "Reviewed endpoint, query/replay and outage records; extended grace for the recorded outage" --confirmation "<completion challenge>" --local-endpoint /run/bean-bureau/admin.sock
```

Completion requires the deadline and a nonblank availability attestation bound to current state.
The operator describes what was checked; the service does not claim to measure uninterrupted
availability or that every downstream consumer fetched a withdrawal. Completion does not stop
distribution automatically. Retained negations remain queryable/replayable until a separate
offline or purge decision.

## Optional label-history cleanup

After completion, `purgeRetiredData` requires a separate preflight and confirmation:

```text
labeler-admin retire preflight --action purgeRetiredData --operation-id purge-1 --reason "Approved label-history cleanup" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin retire purge --operation-id purge-1 --reason "Approved label-history cleanup" --confirmation "<purge challenge>" --local-endpoint /run/bean-bureau/admin.sock
```

The worker deletes **label history and label-operation receipts**, including their distribution
indexes. It retains reports, cases, appeals, policy history, team, administrative audit, and
minimal lifecycle/operation records. Retained moderation or audit may still reference historical
labels. This is not whole-database erasure. Purge can make old query/replay positions unavailable;
sequence identities must not be reused.
Recent publication accounting and its database-clock high-water mark are retained independently
of label-history cleanup.
The CLI still accepts the legacy `purge --limit` hint, but server-owned bounded worker batches
replace the old one-invocation-per-batch loop. Repeating the same purge operation recovers its
original command receipt; use `retire status` for current progress.

Purge does not remove private signing keys, overwrite storage, erase database/key backups,
or erase downstream copies/caches. Keep these separate operator decisions.

## Separate external key-removal workflow

After completed retirement and full publication, key removal is optional independently of purge.
`requestKeyRemoval` records an inventory and fences subsequent signing/recovery rotation before
external removal begins. It does **not** delete a key. Inventory public key IDs/fingerprints,
every signing host/process, secret stores, mounts, environment copies, old deployment configuration
and backup categories. Never put private keys, passwords or tokens in inventory, reasons or attestations.

```text
labeler-admin retire preflight --action requestKeyRemoval --operation-id key-request-1 --reason "Plan external key removal" --inventory "Public fingerprint; signing hosts A/B; secret-store versions; encrypted/offline backup categories" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin retire request-key-removal --operation-id key-request-1 --reason "Plan external key removal" --inventory "Public fingerprint; signing hosts A/B; secret-store versions; encrypted/offline backup categories" --confirmation "<key-request challenge>" --local-endpoint /run/bean-bureau/admin.sock
```

Stop/drain or replace **every signing process** before removing its key source; a process may retain
a loaded key in memory. An unreachable host is not evidence that it stopped. Record partial progress,
failures, unresolved copies and the retention policy for immutable/offline backups.

For each registered host independently verified as stopped, use the separate retirement action
`confirmRetirementHostStopped`, available only in Retired/Purged with live Maintain authority.
This remains available after key removal is Requested, when rotation is fenced. Do not try to
start rotation merely to acknowledge a stopped retirement host.

```text
labeler-admin retire preflight --action confirmRetirementHostStopped --host-id "<exact instanceId from retirement.hosts>" --operation-id stopped-host-1 --reason "Verified host shutdown independently" --attestation "Verified this process is stopped using deployment control and host inspection" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin retire confirm-host-stopped --host-id "<same exact instanceId>" --operation-id stopped-host-1 --reason "Verified host shutdown independently" --attestation "Verified this process is stopped using deployment control and host inspection" --confirmation "<stopped-host challenge>" --local-endpoint /run/bean-bureau/admin.sock
```

The host GUID and attestation are immutable intent in both requests. Xenon offers a separate
per-host preview/confirmation carrying that exact registration. The action records the operator's
attestation transactionally; it does not detect or stop a process, erase in-memory keys or verify
deletion. Never infer stopped state from stale heartbeats. Acknowledgement changes generation and
invalidates earlier previews: obtain a fresh key-removal attestation challenge afterwards.

Then obtain a **different** `attestKeyRemoval` challenge using the inventory and a nonblank
external-removal attestation, and execute `retire attest-key-removal` with exactly those same values:

```text
labeler-admin retire preflight --action attestKeyRemoval --operation-id key-attest-1 --reason "Record external removal evidence" --inventory "Public fingerprint; signing hosts A/B; secret-store versions; encrypted/offline backup categories" --attestation "Hosts A/B stopped; active secret versions removed; offline backup retained under policy and still outstanding" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin retire attest-key-removal --operation-id key-attest-1 --reason "Record external removal evidence" --inventory "Public fingerprint; signing hosts A/B; secret-store versions; encrypted/offline backup categories" --attestation "Hosts A/B stopped; active secret versions removed; offline backup retained under policy and still outstanding" --confirmation "<key-attestation challenge>" --local-endpoint /run/bean-bureau/admin.sock
```

This is **operator-attested removal, not verified deletion or guaranteed secure erasure**.
Do not delete public DID verification material, repository keys, OAuth client keys, volumes or
database files as an incidental step. Private keys are unnecessary for serving already signed
history; explicitly eligible keyless retired hosts can retain distribution and administration.
Missing keys before an eligible retired state must fail closed, not fabricate a keyless role.
Custom hosts selecting `AddKeylessLabelerAdministration` must not register an `ILabelSigningKeyRing`,
before or after that call. Administration and retirement-worker construction reject the conflicting
configuration rather than silently enabling signing.

## Recovery and backup checklist

- Preserve operation IDs, confirmations/receipts and exported operator evidence under access controls.
  Do not include bearer credentials or private keys in output or audit reasons.
- After an uncertain acknowledgement, retry the same immutable command and operation ID to recover
  its receipt. Changed options under an existing ID conflict. A fresh preview does not authorize
  changed intent under a previously committed ID.
- Keep encrypted database and key backups, immutable copies, secret-manager versions and historical
  deployment configuration under an explicit retention/removal policy. Database purge does not erase them.
- Restore **offline**. Reconcile the latest retirement/purge/key-removal record and exported evidence
  before serving or signing. An old Active backup is not permission to resurrect issuance; a tombstone
  inside a rolled-back database cannot prevent that by itself.
- Explain remaining moderation/audit, retained public history, outstanding private copies and downstream
  data separately. Never describe a completed retirement or successful purge as universal erasure.
