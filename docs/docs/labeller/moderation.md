# Optional Xenon moderation

Xenon adds independent-labeler reports, review cases, fixed workflow views,
explicit label decisions, appeals and versioned policy administration to the
[standalone labeler](deployment.md). It is optional: public label distribution
does not require Xenon, reports, cases or an Ozone server.

This backend does not implement a moderation UI, browser cookie authentication,
notifications, full Ozone compatibility, or PDS/AppView administrative powers.
Issuing a label does not take down content, suspend an account, restore deleted
content, or guarantee that downstream clients remove their cached copies.

## Enabling the backend

The canonical host defaults `LabelerModeration:Enabled` to `false`. Enable it
explicitly with `LabelerModeration__Enabled=true`. Use the existing provider,
issuer, signing-key, administration, HTTPS and trusted-proxy configuration; do
not create a second moderation database or a second label-signing path.

Custom applications register `AddLabelerModeration(issuer, httpClient, options)`
after exactly one storage provider and before `AddLabelerAdministration`.
Use the same caller-owned, hardened identity-resolution HTTP client as ordinary
labeler administration. Before serving, initialize `ILabelStore` and then call
`ILabelPolicyStore.InitializePolicyAsync` with the configured freshness interval.
Map `MapLabelerModeration()` alongside the existing distribution and
administration routes. Do not separately register an issuance-policy provider:
Xenon's independently verified policy service supplies it.

Initialization permanently enables fail-closed issuance policy checks for that
issuer database. Removing the HTTP registration or changing configuration does
not erase that persisted gate or bypass its decisions. A fresh Xenon
installation remains unverified until an administrator saves a policy, begins
publication and independently verifies the authoritative declaration.

| Option | Default | Meaning |
|---|---|---|
| `MaximumConcurrentReports` | 16 | Concurrent report requests admitted by one process |
| `ReportsPerMinute` | 120 | Requests admitted in one process's admission window |
| `VerificationFreshness` | 60 seconds | Maximum persisted independent-observation age |
| `MaximumServiceTokenLifetime` | 15 minutes | Maximum reporting assertion lifetime |

All hosts sharing a database must use its persisted verification freshness.
Traffic limits are process-local, not cluster quotas. Apply additional
deployment-wide limits at the trusted proxy. Report intake and policy-health
verification have bounded deadlines; a lookup timeout does not refresh
permission.

`MapLabelerModeration()` maps `/health/labeler/moderation` for separate policy
monitoring. Do not use this endpoint for traffic routing: a closed policy gate
must not hide distribution or authenticated recovery routes.
`/health/labeler/ready` continues to check only the distribution host and store.

## Reports and scope

The public route is `POST /xrpc/com.atproto.moderation.createReport`. It requires
HTTPS and a verified, method-bound service JWT from the reporter's PDS.
The signed audience must identify this issuer's `#atproto_labeler` service;
the legacy bare issuer DID is accepted for reporting only. Neither form grants
team or administrative authority.

Reporter identity comes from authentication, never the JSON body. Ordinary
reporters do not need team membership. Reporter assertions are reserved
transactionally through their expiry and clock-skew allowance, across hosts and
restarts. Reusing an assertion is rejected. Retrying the same contents with
fresh assertions creates distinct reports: the standard method has no client
operation identifier, and replay protection is not content deduplication.

Supported subjects are an exact account DID (`repoRef`) or a full DID-based
record URI and CID (`strongRef`). An account is distinct from its profile record.
Different record CIDs identify different cases. Intake never fetches the
reported subject as a prerequisite: deleted and historical records remain
reportable.

Reason identifiers are open strings, including granular `tools.ozone.report.defs`
reasons; recognizing those identifiers does not implement Ozone APIs.
The selected declaration must explicitly support account and/or record subjects.
Missing `reasonTypes` and `subjectCollections` mean unrestricted within the
supported subject kinds; empty arrays permit none. Explicit JSON null and
unsupported subjects, including chat messages, are rejected.

The host bounds request bytes, depth, report context and optional tool metadata,
and rejects duplicate, unknown and forged fields. Report context may be empty
or whitespace. Limits apply before costly authentication and durable audit where
possible. Do not log report contents, credentials or service JWTs.

A successful response uses the standard report output, not the private
administration envelope. It excludes case identifiers, captured appeal labels,
policy/authentication evidence and tool metadata. Report, replay reservation,
case linkage, any appeal capture and audit commit atomically. Intake never
issues or withdraws a label.

## Cases, workflow views and decisions

One unresolved case exists per exact subject. Concurrent reports retain every
submission while joining that case. New reports after resolution open another
case; explicit reopening rejects a conflicting unresolved case.

Cases are `Open`, `InReview` or `Resolved`. Moderators can assign an eligible
active moderator, release/reassign work, append reasoned notes, start review,
resolve without labels, and reopen. Revocation takes effect immediately but
does not rewrite historical assignment; another moderator can reassign work.

`GetCaseQueueAsync` supplies fixed views: unresolved, open, in review,
unassigned, assigned to the caller, pending appeals, resolved, and all cases.
Filters combine exact subject/version, associated report reason and assignee.
An opaque continuation is bound to all filters, authenticated caller and issuer.
Changing those inputs rejects the continuation. Pages are live keyset reads,
not frozen queue snapshots: concurrent status or assignment changes can change
membership between requests. Every page rechecks authority.

`LabelIssuanceService.ExecuteCaseDecisionAsync` prepares at most sixteen distinct
label values using the same resolution, authorization, signing and policy rules
as manual issuance. One transaction commits every action, original signed/no-op
receipt, case revision, reason, policy version and audit. It does not commit
actions sequentially. Zero-action decisions require no signing key.

The provider rechecks live authority, case/policy revisions, current labels,
lifecycle, maintenance and key selection before mutation. Optional expected
label sequences fence explicit corrections against newer manual decisions.
Network lookups and signing occur outside database transactions. Label-bearing
decisions join the host's existing admission/drain accounting.

Manual account and record labeling remains available without a report or case
through the ordinary shared issuance service and administration methods.

## Appeals and explicit corrections

Both `com.atproto.moderation.defs#reasonAppeal` and
`tools.ozone.report.defs#reasonAppeal` require the authenticated subject owner.
Moderators can open an internal review using `LabelCaseAction.OpenAppeal`,
without impersonating a reporter or creating an external report.

Capture retains all currently active local labels matching the exact account
or record CID, including manually issued labels. Current-state selection uses
signed creation time, not append sequence. Negated, expired and superseded
outcomes are excluded. Original signed bytes and sequences are retained; later
manual changes or signature rotation do not rewrite that provenance.
An empty capture is a valid review request. Capture examines a bounded set of
at most 1,000 distinct historical label values for the subject; exceeding that
bound fails explicitly without accepting a partial report.

Identical pending captures on the same case share one appeal, while every fresh
external report remains separately retained and linked. A different current
capture creates another pending appeal.

A case decision explicitly names up to sixteen pending appeals and an outcome:
`Upheld` and `Superseded` cannot mutate labels; `Withdrawn` explicitly negates
selected challenged values; `Replaced` explicitly issues replacement outcomes,
optionally withdrawing challenged labels. Arbitrary reason text never chooses
labels to reverse. Every captured sequence must still be current and active for
an uphold or correction. Newer or expired outcomes require an explicit
superseded acknowledgment instead of withdrawing a newer outcome.

Other pending appeals keep the case in review. Ordinary resolution cannot
silently orphan them. The original decision-maker may resolve an appeal; a
second reviewer is not required.

## Policies, publication and recovery

`LabelPolicyDefinition` retains the complete declaration, creation time, custom
definitions and extension data, plus independent issuance permissions and rules.
Definitions use the SDK's publication validator. Canonical declaration encoding
is bounded to 64 KiB and rejects duplicate object names and malformed Unicode.
Numbers, including extension values, must use signed 64-bit integer syntax
as required by repository records; fractional and exponent spellings are rejected.
Negative zero is normalized to zero before persistence and publication.
Saving an immutable version does not disrupt the current active policy.

Publication is deliberately separate from a database transaction:

1. Save the complete intended immutable version.
2. Begin publication with a stable operation identity, observed gate revision
   and expected previous record CID, or absence for creation. This closes
   reports and new issuance.
3. Explicitly sign in as the labeler account and use conditional declaration
   creation/update against its discovered PDS. Do not retain PDS credentials in
   the host or send them through the administration API.
4. Ask the host to independently resolve the issuer/PDS and verify
   `app.bsky.labeler.service/self`. Client assertions of publication success
   cannot reopen the gate.

Keep the original creation time and extension data when constructing a new
declaration. A concurrent `InvalidSwap` is a conflict, not permission to
overwrite. A write timeout or cancellation after submission is uncertain,
not proof of rollback. Inspect durable intent, retained operation/history and
the authoritative record before deciding whether another write is safe.

External declaration drift produces a sticky conflict. Cache expiry or
unavailable verification fails closed; neither extends permission. Reads,
public distribution and safe negations remain possible. Matching content alone
cannot clear a detected conflict: an administrator must explicitly begin
reconciliation and independently verify it.
An oversized, excessively deep or otherwise invalid authoritative declaration
also creates a durable conflict. History retains its CID and JSON `null` as a
bounded invalid-observation marker, not the malformed payload. Authoritative
absence instead has no observed CID.

Verification windows use database time. If the database clock moves backwards
behind the lookup's start, the observation is rejected rather than extending
permission. Restart independent verification from a fresh database sample;
do not backdate the observation, weaken the guard or repeat a PDS write merely
because verification was rejected.
Previously verified permission also fails closed if database time precedes
that observation's start, both in cached checks and transactional report and
issuance commits. Safe negations and recovery reads remain available.

`AbandonPolicyPublicationAsync` records an explicit fail-closed recovery result.
It does not cancel an in-flight PDS write or assume rollback. A late write may
still become visible. Retained history includes publication intent, conditional
CID, selected version, authoritative observations and abandonment. Historical
gate freshness is not current permission.

## Administration, audit and storage

Viewer may inspect confidential reports, cases, appeals, queues, retained policy
versions/history and audit. Moderator may change workflows and make label
decisions. Administrator additionally manages policy, team and lifecycle.
All use provider-issued callers and live shared team authorization.

The shared dispatcher exposes the same services over protected local IPC and
opt-in HTTPS administration. Moderation methods use authenticated POST bodies,
including reads, so confidential filters do not enter URL query strings.
Disabling remote administration does not disable the independent reporter
route. Browser authentication and CSRF protection belong to the separate UI.

SQLite, PostgreSQL and MySQL store moderation and labels in one issuer-bound
database, using the existing cross-process transaction boundary. Schema nine
additively preserves schema-eight stream/signature, team, maintenance and key
selection state. Stop old writers and take a consistent backup before upgrading.
MySQL requires InnoDB for moderation tables as well as ordinary label tables.

Audit is application-append-only and atomic with mutations. Successful
inspection does not append another audit entry; denied operations remain
audited. Database administrators and backup custody remain trusted. There is
no hash-chain or tamper-proof-storage claim. Moderation history is not silently
purged by ordinary label retirement; moderation retention is a separate concern.

## CLI policy administration

The canonical CLI's `policy` group uses the same local/remote transport options
as other commands. `status`, `version`, `versions`, `history` and `operation`
inspect retained state. Pages default to eight entries and are limited to
sixteen, keeping complete declarations within the administration response
bound. Use `--after-version` for versions and `--after-sequence` for history.

`save --file policy.json --operation-id draft-v1 --reason "Reviewed rules."`
accepts a UTF-8 JSON file without a BOM, bounded to one MiB:

```json
{
  "version": "v1",
  "declaration": {
    "$type": "app.bsky.labeler.service",
    "createdAt": "2026-01-01T00:00:00Z",
    "policies": { "labelValues": ["spam"], "labelValueDefinitions": [] },
    "subjectTypes": ["account", "record"]
  },
  "allowAccounts": true,
  "allowRecords": true,
  "rules": "Review evidence before applying a spam label."
}
```

The creation time above is illustrative; retain the existing declaration's
original creation time and extension data when updating. Optional
`allowedRecordCollections` independently restricts issuance: omission means
unrestricted collections, and `[]` permits none. It does not replace the
declaration's reporting restrictions. The CLI validates the file before closing
the gate and preserves the declaration without injecting DTO defaults.

`begin` records publication without signing in to the labeler account. `publish`
and `recover` additionally perform the conditional PDS write and request
independent host verification. For example, after saving the example draft:

```powershell
.\idunno.Bluesky.Labeler.Cli.exe policy publish `
  --admin-url https://labeler.example `
  --audience did:web:labeler.example#labeler_admin `
  --handle administrator.example `
  --client-id https://labeler.example/native-client-metadata.json `
  --labeler-handle labeler.example `
  --labeler-client-id https://labeler.example/native-client-metadata.json `
  --version v1 --expected-revision 1 --create `
  --operation-id publish-v1 --reason "Publish reviewed rules."
```

Use the observed gate revision, not an assumed revision of one. For updates,
replace `--create` with `--previous-cid <observed-record-cid>`. Exactly one is
required. Mutations require interactive confirmation or explicit `--yes`.
With local administration, replace the administrator's remote options with
`--local-endpoint`; the separate labeler-account sign-in is still required.

The administrator and labeler account need not be the same DID or team member.
Administrative sign-in requests only the three publication/recovery RPC scopes;
labeler-account sign-in requests only create or update permission for
`app.bsky.labeler.service`, never delete, identity, account or general repository
authority. The authenticated labeler DID must match the issuer returned by the
protected policy gate. The host receives neither PDS credentials nor the OAuth
callback.

Reuse the original operation ID, version, reason, expected revision and
conditional CID for recovery. The CLI rechecks live administrative authority and
the current publication identifier before attempting a write; matching only a
historical receipt or version is insufficient. Already matching declarations
are independently verified without another write. Abandoned/superseded intent,
changed CIDs and failed conditional writes are never automatically overwritten.
There is no atomic transaction spanning the database and PDS: abandonment racing
an already admitted remote write cannot cancel it.

`verify --reason ...` forces host-side verification without PDS mutation.
`abandon --expected-revision ... --operation-id ... --reason ...` records a new,
explicit fail-closed recovery decision. Neither command is an unconditional
publication retry.
