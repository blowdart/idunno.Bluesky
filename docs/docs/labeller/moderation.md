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
| `MaximumPendingReports` | 200,000 | Authenticated reports retained while policy verification is unavailable |
| `VerificationFreshness` | 30 minutes | Maximum persisted independent-observation age |
| `MaximumServiceTokenLifetime` | 15 minutes | Maximum reporting assertion lifetime |

All hosts sharing a database must use its persisted verification freshness.
They must also use the same pending-report capacity.
Configure freshness in whole milliseconds; registration rejects fractional
milliseconds rather than silently truncating them to database precision.
Traffic limits are process-local, not cluster quotas. Apply additional
deployment-wide limits at the trusted proxy. Report intake and scheduled
verification have bounded deadlines; a lookup timeout does not refresh
permission. Report intake reads only the stored verification gate and never
waits on a remote declaration lookup. A background task checks the active public
policy at startup and every five minutes, independently of traffic. Pending
publications and recovery conflicts are not automatically activated by this
task. A confirmed mismatch closes moderation immediately; an unavailable check
leaves the previous verification deadline unchanged.

`MapLabelerModeration()` maps `/health/labeler/moderation` for separate policy
monitoring. This check reads stored policy state and freshness without making
remote requests. Do not use this endpoint for traffic routing: a closed policy gate
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

An authenticated report for an allowed subject whose reason is outside
`reasonTypes` receives the normal report receipt, but is immediately retained as
handled with resolution reason `Not Applicable`. It does not enter the open-work
counts, create a pending appeal or change labels. Existing case status, assignment
and revision are unchanged; if no unresolved case exists, its case is created
already resolved. The original reporter, reason and evidence remain available.
History and audit attribute the automatic action to the issuer DID with
`system:report-reason` provenance. Authentication, token replay protection,
verified-policy fencing, subject restrictions and appeal ownership still apply.
The report, resolution, history, audit and token reservation commit atomically.

The host bounds request bytes, depth, report context and optional tool metadata,
and rejects duplicate, unknown and forged fields. Report context may be empty
or whitespace. Limits apply before costly authentication and durable audit where
possible. Do not log report contents, credentials or service JWTs.

A successful response uses the standard report output, not the private
administration envelope. It excludes case identifiers, captured appeal labels,
policy/authentication evidence and tool metadata. Report, replay reservation,
case linkage, any appeal capture and audit commit atomically. Intake never
issues or withdraws a label.

If policy verification is unavailable, authenticated reports are durably queued
and receive the ordinary successful protocol response. They remain visible in
Xenon's Reports page but are not cases and cannot be acted on or cause label
issuance. This applies when the policy is stale, conflicting or not yet
published; report requests do not block on remote verification. Pending reports
do not expire. After an administrator verifies an active policy, the background
verifier processes the queue in bounded restart-resumable batches:
subjects allowed by the policy enter ordinary case review, while subjects
outside its scope remain retained as `Not Applicable` without creating a case.
Appeal ownership is checked at intake, and eligible appeal reports preserve
their label snapshot from that time.

The configured queue limit counts only reports still awaiting verification.
When it is reached, new authenticated submissions receive `503 Service
Unavailable` with `Retry-After`; already queued reports are retained. The limit
is a storage-protection boundary, not a time-based retention policy. Database
failures and request deadlines remain explicit service errors and do not produce
a success-shaped receipt.

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
selected challenged values; `Replaced` explicitly adds or removes labels,
including partial withdrawal that retains other labels. Arbitrary reason text never chooses
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
   reports and new issuance. Explicit confirmation also authorizes permanent
   withdrawal of existing applications of any label values removed by the new
   definition.
3. If values were removed and label history exists, wait for the **Withdrawing**
   phase to finish. The host signs and publishes negations in durable, bounded
   batches before allowing declaration verification or the CLI's PDS write.
4. Explicitly sign in as the labeler account and use conditional declaration
   creation/update against its discovered PDS. Do not retain PDS credentials in
   the host or send them through the administration API.
5. Ask the host to independently resolve the issuer/PDS and verify
   `app.bsky.labeler.service/self`. Client assertions of publication success
   cannot reopen the gate.

Withdrawal scans a fixed history boundary in pages of at most 250 entries and
signs at most 250 negations per batch. Only the latest, unexpired, non-negated
applications of removed values are withdrawn, preserving their account or exact
record URI/CID scope. Values still declared by the replacement are unchanged.
Saving a draft or deleting an unpublished draft does not withdraw labels.
Cleanup does not reapply labels if a value is later reintroduced.

The host resumes from durable checkpoints after restart; it does not depend on
an open browser or enabled remote CLI administration. Each batch rechecks saved
administrator authority, lifecycle/maintenance admission and the current policy
revision. Expired or revoked authority pauses cleanup. An active administrator
can explicitly resume the same selected operation from the UI or with
`policy resume`; this renews authority without discarding completed work.
Errors leave the gate closed and are surfaced in host logs; authorization pauses
also retain a visible reason in policy state. Publication remains blocked until
the required negations are durably published, not merely signed or queued.

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
still become visible. It stops pending withdrawal batches but cannot undo
already committed negations, even if the new declaration is never published.
Retained history includes publication intent, conditional
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

Schema ten adds local search projections and restart-resumed deleted-post checks.
`AddLabelerModeration` runs one bounded existence-check batch each minute, with
database-timed five-minute spacing between checks of a case. Only
`app.bsky.feed.post` record cases with unresolved reports or pending appeals are
eligible. The checker fetches the current record without pinning its old CID:
an edited record still exists. An explicit authoritative PDS `RecordNotFound`
with HTTP 400 or 404 resolves the case, handles remaining reports and marks
pending appeals Superseded, all with reason `Post deleted`. It never changes
labels, challenges, signed history or original report text. Inconclusive failures
are logged and leave review work open. A case revision change or stale lookup
rejects closure; the durable schedule allows a later fresh check after restart.
History and audit use explicit `system:deleted-post` provenance.
This shared worker operates with or without the UI and independently of remote
CLI administration.

### Policy verification email alerts

Email alerts are optional. When enabled, independent background policy-verification
failures create one durable notification per continuous failure incident. Delivery
is retried after SMTP errors and resumes after restart; SMTP cannot participate in
the database transaction, so delivery is at-least-once. A successful verification
closes the incident without discarding an undelivered alert. Reports remain queued
and non-actionable while verification is unavailable; email configuration does not
change report intake or return behavior.

Active administrators can register or clear their own alert address on the Xenon
Team page. Only active administrators with a registered address receive alerts.
The message contains the labeler DID, failure category and first failure time, not
report contents. Addresses are persisted with team state, are visible only to
administrators, and are excluded from audit reasons and delivery logs.

Configure `LabelerModerationOptions.PolicyEmail` from application settings before
calling `AddLabelerModeration`. `SmtpClient` uses mandatory STARTTLS; a server that
does not advertise STARTTLS or cannot establish TLS is rejected, with no plaintext
fallback. SMTP username and password must be configured together. Keep the password
in a secret store or environment-backed configuration rather than a checked-in file.

```json
{
  "LabelerModeration": {
    "PolicyEmail": {
      "Enabled": true,
      "Server": "smtp.example.net",
      "Port": 587,
      "FromAddress": "xenon@example.net",
      "UserName": "xenon-smtp",
      "Password": "<secret from deployment configuration>"
    }
  }
}
```

Bind the section to `LabelerModerationOptions` and pass it to
`AddLabelerModeration(issuer, httpClient, options)`. Port defaults to 587; the
server and sender address are required when email is enabled.
When enabled, the shared label store must also implement
`ILabelPolicyAlertStore`; custom stores do not need that capability while email
alerts remain disabled.

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
independent host verification. All publication commands explicitly confirm the
irreversible cleanup of removed label values, including when later publication
fails or is abandoned. `publish` and `recover` wait for cleanup before signing
in to the labeler account or writing its declaration; cancellation does not roll
back committed negations. For example, after saving the example draft:

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
Administrative sign-in requests only the four publication/recovery RPC scopes,
including the ability to renew paused cleanup authorization;
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

If cleanup pauses because its original administrator authority expired or was
revoked, an active administrator can explicitly renew it without a labeler-account
sign-in:

```powershell
.\idunno.Bluesky.Labeler.Cli.exe policy resume `
  --local-endpoint idunno-labeler `
  --operation-id publish-v1 --reason "Continue reviewed label withdrawal." --yes
```

Use the currently selected publication operation ID, not a new one. `resume`
returns the durable state; the background worker continues the batches.
An actively waiting `publish` or `recover` command renews paused authorization
through its live administrative connection before continuing.
