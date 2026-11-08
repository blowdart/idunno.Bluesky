# Labeler administration CLI

`labeler-admin` inspects and administers a deployed labeler through protected local
IPC or authenticated remote HTTPS. It also provides signing-key publication and
deployment-update helpers. Installing the executable does **not** enable remote
administration, create a team membership, or grant an account authority.

## Install and run

Use the trimmed, self-contained single-file CLI ZIP for your operating system and architecture from a
release that includes the labeler. Verify its GitHub release attestation before
extracting it to a trusted directory on your `PATH`. Keep configuration, keys,
database files and backups outside that directory. See
[release installation and updates](deployment.md#safe-updates-and-rollback).
The executable bundles its runtime and does not require a separate .NET installation.
The canonical host image includes the matching Linux executable in
`/opt/labeler-cli`, with `labeler-admin` available on `PATH`:

```text
docker exec <container> labeler-admin --help
docker exec <container> labeler-admin counts --local-endpoint /run/labeler/admin.sock
```

These commands run as the container's configured user and still require the
existing local administrator identity binding for administration operations.

When building from source, use the repository's pinned .NET SDK:

```powershell
dotnet run --project src\idunno.Bluesky.Labeler.Cli --framework net10.0 -- --help
```

To publish trimmed, self-contained, single-file executables into
`.assets\labeler-cli\<rid>` for all supported platforms, run this from any directory:

```powershell
.\build-labeler-cli.ps1
```

Pass one or more `-RuntimeIdentifier` values (`win-x64`, `win-arm64`, `linux-x64`,
or `linux-arm64`) to publish only selected platforms.

Examples below use the installed `labeler-admin` executable. On Windows this is
`labeler-admin.exe`; invoke it as `.\labeler-admin.exe` if it is only in the current
directory. Get help without signing in or changing anything:

```powershell
labeler-admin --help
labeler-admin --version
labeler-admin issue --help
labeler-admin policy publish --help
```

## Enable or forcibly disable remote access

The standalone host binds the `LabelerAdministration` configuration section.
Select **one** of these modes:

| `RemoteMode` | Result |
|---|---|
| `Disabled` | Remote CLI routes are always unavailable, regardless of the Xenon switch. This is the default for an unconfigured host. |
| `Enabled` | Remote CLI routes are enabled. Authentication, method-bound service tokens and live team authorization remain mandatory. |
| `WebUiControlled` | The current labeler owner can enable or disable remote CLI access from Xenon's **Team and membership** page. The durable switch starts disabled. |

For example, to force remote access off, set this in the host's `appsettings.json`:

```json
{
  "LabelerAdministration": {
    "RemoteMode": "Disabled"
  }
}
```

To force it on, use `"RemoteMode": "Enabled"`. To delegate the switch to Xenon,
use `"RemoteMode": "WebUiControlled"` and enable the optional
[Xenon web UI](ui.md) separately. Other Administrators cannot toggle it: the owner
is initially the DID that completed the one-time local bootstrap, or the recipient of an
owner-authorized ownership transfer, and must still be an active
Administrator. Neither a labeler issuer DID nor an arbitrary Administrator is
assumed to be the owner.

The equivalent environment variable is `LabelerAdministration__RemoteMode`, for
example in a Compose service:

```yaml
environment:
  LabelerAdministration__RemoteMode: Disabled
```

Environment variables and command-line configuration can override JSON. To force
access off, ensure the **effective** setting is `Disabled`, not an overridden file.
An explicit mode takes precedence over the legacy `RemoteEnabled` Boolean. If no
mode is supplied, legacy `RemoteEnabled=true` still enables remote access; do not
rely on omission to disable an existing deployment. Invalid modes fail closed.

These settings control **remote administration**, not whether someone can run a
local CLI executable. A separately configured protected pipe/socket stays available
in every mode. To prohibit local administration too, the host operator must remove
that endpoint's configuration and review local OS access; the remote switch is not
a machine-access control.

Custom SDK hosts may bind a differently named section. See
[administration registration and control modes](administration.md#remote-administration-control-modes).
Public label distribution and Xenon browser access are separate boundaries.

## Choose local or remote transport

Local use requires the configured OS pipe/socket and an authorized local identity
binding. It does not open a browser or infer authority from machine access.
See [local bootstrap and permissions](administration.md#deliberate-browserless-setup-and-local-use).

The following PowerShell arrays make the examples shorter:

```powershell
# Windows: use the pipe name actually configured by your host.
$ipc = @("--local-endpoint", "idunno-labeler-admin")
# Linux alternative: use the configured absolute Unix socket filename.
# $ipc = @("--local-endpoint", "/run/labeler/admin.sock")

$remote = @(
    "--labeler-url", "https://labeler.example",
    "--admin-handle", "moderator.example"
)

labeler-admin status @ipc
labeler-admin counts @ipc
labeler-admin team @remote
```

Use either `@ipc` or `@remote`, not both. `@ipc` and `@remote` are PowerShell array
splatting, not CLI options. In another shell, replace them with their literal
options and values.

| Option | Meaning and example |
|---|---|
| `--local-endpoint` | Protected pipe name or Unix socket path: `status --local-endpoint idunno-labeler-admin`. |
| `--labeler-url` | Public HTTPS **origin**, not `/Xenon` or an XRPC path: `status --labeler-url https://labeler.example --admin-handle moderator.example`. |
| `--admin-service` | Optional exact `DID#service` override. By default, the CLI discovers the audience from the native OAuth metadata's advertised labeler administration RPC scopes; discovery requires exactly one audience. |
| `--admin-handle` | Account exercising team authority, not automatically the labeler account; see the preceding example. |
| `--oauth-client-id` | Native OAuth metadata URL; defaults to `<labeler-url>/native-client-metadata.json`. Override explicitly with `team @remote --oauth-client-id https://labeler.example/native-client-metadata.json`. Remote metadata must share the labeler origin. |
| `--remember-me` | Opt into retaining that exact account/client/scope/PDS session: `team @remote --remember-me`. Not needed for IPC. |

Remote use requires an existing team membership, a published administration service,
correct trusted-proxy HTTPS forwarding, and anonymously reachable native OAuth
metadata. Discovery uses the configured metadata URL (defaulting to
`<labeler-url>/native-client-metadata.json`); use `--admin-service` explicitly if metadata
does not advertise a single unambiguous administration audience. See
[native OAuth and TLS forwarding](deployment.md#native-oauth-and-tls-forwarding).
The CLI names the account at sign-in and adds a purpose warning only when an
operation requires labeler-account authority. PDS
account sign-in alone never grants a labeler role. PLC identity changes sign in as
the labeler account; policy publication separates the administrator's authorization
from the labeler account's repository write.

### Remembered sessions

Without `--remember-me`, credentials are not persisted by the CLI. With it, sessions
are stored under `~/.xenon` and reused only for the matching account, resolved DID,
OAuth metadata, requested permissions and service. Different method-bound
permissions can require separate browser sign-ins.

Windows protects saved credentials with current-user DPAPI. Other systems require
separate explicit consent to **unencrypted** storage and enforce private directory
and file permissions. `--yes` does not approve unencrypted storage and redirected
input cannot provide that consent. Protect the cache and backups; never copy them
into a repository or executable directory.

Run `labeler-admin forget-me` to delete every saved OAuth credential in `~/.xenon`.
This removes local credential files for all accounts and permission sets but does
not revoke sessions at their PDS; revoke a session there separately when needed.

A corrupt or mismatched cache, failed session renewal or failed credential save
stops the command explicitly. It does not silently start a new browser session.
Revoke the affected session at its PDS and remove its saved cache entry before
signing in afresh. Removing a local cache file alone does not revoke PDS authority.
Suspending/removing the member blocks that DID's labeler access independently.

## Output, confirmation and retries

Results are readable text by default. Success, no-op and failure confirmations go
to standard error. Console output uses UTF-8 without a byte-order mark, including
redirected standard output and standard error. Unicode icon prefixes occupy four
terminal columns: narrow symbols use three spaces and two-column emoji use two.
They distinguish progress, sign-in, warnings, failures
and success; result fields also include icons, such as cases, reports and appeals.
Use a terminal font that supports these symbols. Add `--verbose` before or after a subcommand for raw result JSON
on standard output, including failed service responses:

```powershell
labeler-admin --verbose status @ipc
labeler-admin status @remote --verbose
labeler-admin team @ipc --verbose > team.json
```

Normal status output shows Issuer first, followed by Phase, and uses “Labels issued”
and “Labels published” for the lifetime totals, including withdrawals. Label history
says “Full label history available” or identifies the first available label number.
Internal revisions are omitted, and retirement checkpoints are hidden while Active.
These display changes do not alter verbose JSON.

`--verbose` does not enable HTTP tracing or OAuth credential logging. Treat exported
team, audit, policy and recovery material as confidential. A successful local
receipt does not prove downstream clients have consumed a label or withdrawal.

| Option | Meaning and example |
|---|---|
| `--operation-id` | Stable, nonblank identity for a mutation or receipt lookup: `operation @ipc --operation-id label-1`. Retain the same ID and exact intent after an uncertain outcome. |
| `--reason` | Nonblank audit explanation for mutations: `issue did:plc:example beansonista @ipc --operation-id label-1 --reason "Reviewed account"`. Do not include secrets. |
| `--yes` | Skip the ordinary mutation prompt: append `--yes` to the preceding example. Does not bypass authorization, a retirement challenge, attestation, or plaintext-cache consent. |
| `--verbose` | Raw JSON result output: `status @ipc --verbose`. |
| `--help`, `-h`, `-?` | Command-specific usage without executing: `retire begin --help`. |
| `--version` | Executable version at the root: `labeler-admin --version`. Policy subcommands also use `--version <id>` for a policy identifier. |

Exit codes are 0 for success (including no-op), 2 for invalid input or declined
confirmation, 3 for transport/setup failure, 4 for authentication/authorization
failure, 5 for service failure, and 130 for cancellation. Browser launch failure,
denial, incomplete login and callback timeout stop the workflow; receiving a browser
callback is not itself command success. Cancellation stops further workflow steps,
but cannot undo a mutation the server already committed.

## Inspection, labels and team commands

The example DIDs, CIDs, public keys, version IDs, GUIDs and operation receipts on
this page are placeholders. Replace them with verified values from your deployment.
Do not run the mutation examples as a batch.

`counts` reports the current number of unresolved cases, unhandled reports
(including reports awaiting policy verification), and pending appeals. It uses
the same durable totals shown by Xenon's moderation navigation.

| Command | Example |
|---|---|
| `status` | `labeler-admin status @ipc` |
| `counts` | `labeler-admin counts @ipc` |
| `team` | `labeler-admin team @remote` |
| `operation` | `labeler-admin operation @ipc --operation-id label-1 --reason "Recover uncertain reply"` |
| `audit` | `labeler-admin audit @ipc --after-sequence 0 --limit 16` |
| `issue` | `labeler-admin issue did:plc:example beansonista @ipc --operation-id label-1 --reason "Reviewed account"` |
| `negate` | `labeler-admin negate did:plc:example beansonista @ipc --operation-id withdrawal-1 --reason "Appeal upheld"` |
| `enroll` | `labeler-admin enroll moderator.example @ipc --role Moderator --operation-id enroll-1 --reason "Approved team member"` |
| `change-team` | `labeler-admin change-team did:plc:member @ipc --action SetRole --role Viewer --operation-id role-1 --reason "Changed duties"` |

| Additional option | Scope and example |
|---|---|
| `--after-sequence` | Exclusive audit cursor, nonnegative, default 0. `audit @ipc --after-sequence 16 --limit 8`. |
| `--limit` | Audit page size 1-16, default 16; other command families have their own limits below. |
| `--cid` | Pin a **record** label/withdrawal to an explicitly chosen CID: `negate at://did:plc:example/app.bsky.feed.post/record beansonista @ipc --cid <actual-cid> --operation-id withdrawal-2 --reason "Withdraw pinned version"`. Omit for URI-wide scope; never infer or manufacture a CID. |
| `--expires-at` | Absolute issue-only expiry: `issue did:plc:example beansonista @ipc --expires-at 2030-01-01T00:00:00Z --operation-id label-2 --reason "Time-limited label"`. |
| `--role` | `Viewer`, `Moderator` or `Administrator`; required for `enroll` and `change-team --action SetRole`. |
| `--action` | Team action: `SetRole`, `Suspend`, `Reinstate`, `Remove`, `BindLocal`, `RevokeLocal`, or owner-only `TransferOwnership`. |
| `--credential` | OS identity, not a password/token; required for binding actions: `change-team did:plc:member @ipc --action BindLocal --credential uid:1001 --operation-id bind-1 --reason "Approved local operator"`. Windows uses `sid:<actual-SID>`. |

For example, suspend, reinstate or remove a member by replacing `SetRole --role Viewer`
with `Suspend`, `Reinstate` or `Remove` after `--action`, and choose a new operation ID.
To revoke a local binding, use `--action RevokeLocal --credential uid:1001`.
Roles, owner protection and last-administrator protections are enforced by the service; reinstatement
does not recreate revoked local bindings.

The owner cannot be removed, suspended or demoted. Only the current owner can transfer
ownership to another active Administrator:

```shell
labeler-admin change-team did:plc:new-owner @ipc --action TransferOwnership --operation-id transfer-owner-1 --reason "Approved ownership handover"
```

Confirm the prompt only after checking the recipient. The new owner receives protection and
control of the remote CLI switch; the former owner remains an ordinary Administrator.
No role or local binding is changed, and the remote-access switch retains its current state.
Do not pass `--role` or `--credential` for this action.

## Policy commands

These require optional Xenon policy services. Saving a candidate does not publish
it. Publication can irreversibly withdraw removed label values even if the later
repository write fails. Review the
[policy publication workflow](moderation.md#policies-publication-and-recovery) first.

| Command | Example |
|---|---|
| `policy status` | `labeler-admin policy status @ipc` |
| `policy version` | `labeler-admin policy version @ipc --version policy-v2` |
| `policy versions` | `labeler-admin policy versions @ipc --after-version policy-v1 --limit 8` |
| `policy history` | `labeler-admin policy history @ipc --after-sequence 0 --limit 8` |
| `policy operation` | `labeler-admin policy operation @ipc --operation-id publish-1` |
| `policy save` | `labeler-admin policy save @ipc --file policy.json --operation-id save-1 --reason "Reviewed candidate"` |
| `policy begin` | `labeler-admin policy begin @ipc --version policy-v2 --expected-revision 3 --previous-cid <actual-declaration-cid> --operation-id publish-1 --reason "Approved publication"` |
| `policy publish` | `labeler-admin policy publish @remote --version policy-v2 --expected-revision 3 --previous-cid <actual-declaration-cid> --labeler-handle labeler.example --operation-id publish-1 --reason "Approved publication"` |
| `policy recover` | `labeler-admin policy recover @remote --version policy-v2 --expected-revision 3 --previous-cid <actual-declaration-cid> --labeler-handle labeler.example --operation-id publish-1 --reason "Approved publication"` |
| `policy resume` | `labeler-admin policy resume @ipc --operation-id publish-1 --reason "Reviewed interrupted publication"` |
| `policy verify` | `labeler-admin policy verify @ipc --reason "Independently verify declaration"` |
| `policy abandon` | `labeler-admin policy abandon @ipc --expected-revision 3 --operation-id abandon-1 --reason "Candidate withdrawn"` |

| Option | Meaning |
|---|---|
| `--file` | Operator-authored policy JSON, at most 1 MiB. See the [policy file example](moderation.md#cli-policy-administration) for its schema. |
| `--version` | Exact retained policy version; do not substitute the CLI version. |
| `--expected-revision` | Positive current publication revision from `policy status`, used for optimistic concurrency. |
| `--previous-cid` | Expected current declaration CID for a conditional replacement. |
| `--create` | First declaration only; replaces `--previous-cid`, never combines with it. Example: `policy begin @ipc --version policy-v1 --expected-revision 1 --create --operation-id first-publication --reason "Initial declaration"`. |
| `--labeler-handle` | Labeler repository account for `publish`/`recover`, distinct from `--admin-handle`. |
| `--labeler-oauth-client-id` | Optional labeler-account metadata override. Defaults to administrator metadata or the remote origin's native metadata. Required explicitly when publishing through IPC without an origin: append `--labeler-handle labeler.example --labeler-oauth-client-id https://labeler.example/native-client-metadata.json` to an otherwise complete local `policy publish` invocation. |
| `--after-version` | Exclusive retained-version cursor for `versions`. |
| `--after-sequence` | Nonnegative exclusive history cursor, default 0. |
| `--limit` | Policy page size 1-16, default 8, for `versions`/`history`. |

Use exactly one of `--create` and `--previous-cid` for `begin`, `publish` and
`recover`. Recover the saved intent using its original operation ID and fields;
do not blindly copy revision 3 or create a new operation after an uncertain write.

## Signing-key rotation and identity publication

Follow the [coordinated rotation runbook](deployment.md#key-installation-and-rotation).
Preinstall the replacement key outside the CLI, drain every participating host,
and verify public identity publication before completing rotation. These commands
do not generate or take custody of private keys.

| Command | Example |
|---|---|
| `rotation status` | `labeler-admin rotation status @ipc` |
| `rotation begin` | `labeler-admin rotation begin @ipc --replacement-key-id key-2 --operation-id rotation-1 --reason "Scheduled rotation"` |
| `rotation confirm-host-stopped` | `labeler-admin rotation confirm-host-stopped @ipc --host-id <actual-host-guid> --operation-id rotation-1 --reason "Independently verified host stopped"` |
| `rotation capture-boundary` | `labeler-admin rotation capture-boundary @ipc --operation-id rotation-1 --reason "All hosts drained"` |
| `rotation verify-publication` | `labeler-admin rotation verify-publication @ipc --operation-id rotation-1 --reason "Replacement identity published"` |
| `rotation resign-batch` | `labeler-admin rotation resign-batch @ipc --limit 250 --operation-id rotation-1 --reason "Continue retained-history re-signing"` |
| `rotation complete` | `labeler-admin rotation complete @ipc --operation-id rotation-1 --reason "Verified every live host activated key-2"` |
| `rotation did-web-snippet` | `labeler-admin rotation did-web-snippet did:web:labeler.example --public-key <actual-public-multibase> --endpoint https://labeler.example --verbose > identity-additions.json` |
| `rotation publish-plc` | `labeler-admin rotation publish-plc --account-handle labeler.example --oauth-client-id https://labeler.example/native-client-metadata.json --public-key <actual-public-multibase> --endpoint https://labeler.example` |
| `rotation recover-plc` | `labeler-admin rotation recover-plc did:plc:example --account-handle labeler.example --oauth-client-id https://labeler.example/native-client-metadata.json --public-key <actual-public-multibase> --endpoint https://labeler.example --operation-file signed-operation.json` |

| Option | Meaning |
|---|---|
| `--replacement-key-id` | Preinstalled key identity for `begin`. |
| `--host-id` | Actual participant registration GUID; stopped-host confirmation records operator evidence, not process detection. |
| `--limit` | Re-signing batch size 1-1,000, default 250. Repeat bounded batches while inspecting progress. |
| `--account-handle` | Labeler account whose PLC identity is changed or recovered, not an administrator acting on another account. |
| `--oauth-client-id` | Required HTTPS native metadata advertising identity authority for PLC commands; no remote transport default here. |
| `--public-key` | Public multibase label verification key from rotation status; never a private key. |
| `--endpoint` | Public HTTPS labeler origin for identity publication. |
| `--operation-file` | Saved signed PLC operation JSON, 1 byte-64 KiB, for interrupted publication recovery. |
| `--authorization-token` | Optional separately requested PDS email code for `publish-plc`. Prefer omitting it and using the interactive flow; putting codes in shell arguments can expose them in history/process listings. Never put one in an example, audit reason or log. |

The `did-web-snippet` command only prints additions; it does not merge or publish a
DID document. Use `--verbose` for merge-ready JSON, preserve unrelated DID fields,
then verify the published document. PLC commands mutate account identity through
the PDS after sign-in and confirmation, independently of labeler administration
transport. Use `--verbose` when retaining structured PLC outcome/recovery material.
Do not mistake signed public operation data for private signing-key material.

## Retirement commands

Retirement is irreversible: issuance never reopens. Use the complete
[retirement runbook](retirement.md), not this reference as an execution script.
The normal path uses a durable server worker, not repeated CLI `advance` calls.

| Command | Example |
|---|---|
| `retire status` | `labeler-admin retire status @ipc` |
| `retire preflight` | `labeler-admin retire preflight @ipc --action beginRetirement --operation-id retire-1 --reason "Approved closure"` |
| `retire begin` | `labeler-admin retire begin @ipc --operation-id retire-1 --reason "Approved closure" --confirmation <actual-preflight-challenge>` |
| `retire advance` | `labeler-admin retire advance @ipc --operation-id advance-1 --reason "Approved manual compatibility step"` |
| `retire pause` | `labeler-admin retire pause @ipc --operation-id pause-1 --reason "Investigate storage"` |
| `retire resume` | `labeler-admin retire resume @ipc --operation-id resume-1 --reason "Storage restored; reviewed checkpoint"` |
| `retire grace` | `labeler-admin retire grace @ipc --days 14 --operation-id grace-1 --reason "Selected distribution interval"` |
| `retire extend-grace` | `labeler-admin retire extend-grace @ipc --days 7 --operation-id extend-1 --reason "Allow for outage"` |
| `retire complete` | `labeler-admin retire complete @ipc --operation-id complete-1 --reason "Reviewed distribution" --attestation "Reviewed endpoint and outage records" --confirmation <actual-completion-challenge>` |
| `retire purge` | `labeler-admin retire purge @ipc --operation-id purge-1 --reason "Approved history cleanup" --confirmation <actual-purge-challenge> --limit 100` |
| `retire request-key-removal` | `labeler-admin retire request-key-removal @ipc --operation-id keys-1 --reason "Approved external removal plan" --inventory "Public key IDs, hosts, copies and backup categories" --confirmation <actual-key-request-challenge>` |
| `retire attest-key-removal` | `labeler-admin retire attest-key-removal @ipc --operation-id keys-2 --reason "Record operator removal" --inventory "Public key IDs, hosts, copies and backup categories" --attestation "Recorded removal and retained-backup evidence" --confirmation <actual-removal-challenge>` |
| `retire confirm-host-stopped` | `labeler-admin retire confirm-host-stopped @ipc --host-id <actual-host-guid> --operation-id stopped-1 --reason "Verified stopped process" --attestation "Independent process-stop evidence" --confirmation <actual-stopped-host-challenge>` |

| Option | Meaning |
|---|---|
| `--action` | Exact method for preflight: `beginRetirement`, `advanceRetirement`, `pauseRetirement`, `resumeRetirement`, `beginGracePeriod`, `extendGracePeriod`, `completeRetirement`, `purgeRetiredData`, `requestKeyRemoval`, `attestKeyRemoval`, or `confirmRetirementHostStopped`. |
| `--confirmation` | Expiring action-specific server challenge. Required for begin, complete, purge, both key-removal actions and stopped-host attestation. Obtain a matching preflight first; `--yes` is insufficient. |
| `--days` | Explicit grace duration/extension, 1-365 days; no default. Include identical days in a corresponding preflight. |
| `--attestation` | Required availability evidence for completion, external-removal evidence for key attestation, or process-stop evidence for stopped-host confirmation. |
| `--inventory` | Required key/host/copy/backup inventory for key-removal request/attestation; public identifiers only. |
| `--host-id` | Exact registration GUID for stopped-host confirmation. |
| `--limit` | Legacy purge hint, 1-1,000, default 100. The durable worker controls bounded purge batches. |

For each challenged action, preflight must use the **same action, operation ID,
reason, days, inventory, attestation and host ID** as execution. For example, before
the completion example, obtain its challenge with:

```powershell
labeler-admin retire preflight @ipc --action completeRetirement --operation-id complete-1 --reason "Reviewed distribution" --attestation "Reviewed endpoint and outage records"
```

Inspect the returned consequences and expiry, then copy the actual challenge.
Resume renews authority with a new command operation ID while retaining the saved
workflow; uncertain retries retain that resume ID. Purge removes label history and
receipts, not moderation/audit, backups or downstream copies. Key-removal commands
record/fence external operator work; they do not delete private keys or stop hosts.

## CLI and service updates

These commands operate locally rather than through `@ipc`/`@remote`.

```powershell
labeler-admin update --tag v9.0.1
labeler-admin service-update --compose-file compose.yml --service labeler --image ghcr.io/blowdart/idunno-bluesky-labeler-postgresql@sha256:<new-digest> --previous-image ghcr.io/blowdart/idunno-bluesky-labeler-postgresql@sha256:<previous-digest> --ready-url https://labeler.example/health/labeler/ready --backup-confirmed --schema-compatible --timeout-seconds 180 --drain-seconds 90
```

Replace the example release, registry image and digests with actual supported,
verified artifacts. See [deployment updates](deployment.md#safe-updates-and-rollback)
for the published image names, supported platforms and rollback constraints. Use the MySQL variant
for MySQL. Provider-neutral base images cannot be deployed by this command; legacy monolithic
digests remain accepted for compatible rollback. Changing variants does not migrate databases.

| Option | Meaning |
|---|---|
| `--tag` | Exact stable CLI release tag; updater verifies release attestation and stages a self-contained replacement. |
| `--compose-file` | Existing Compose deployment file. |
| `--service` | Labeler service name in that file. |
| `--image` | New public GHCR image pinned by SHA-256 digest. |
| `--previous-image` | Current pinned image for rollback if readiness fails. |
| `--ready-url` | Readiness endpoint to check after replacement. |
| `--backup-confirmed` | Explicitly attest database and external configuration backup. |
| `--schema-compatible` | Explicitly attest supported schema/upgrade compatibility. |
| `--timeout-seconds` | Readiness timeout, 30-600 seconds, default 180. |
| `--drain-seconds` | Graceful host-drain deadline, 30-600 seconds, default 90. |
| `--yes` | Skip the ordinary update confirmation only. |

`update` changes the supported direct CLI installation, not the labeler host,
configuration or database. `service-update` supports Docker Compose, not arbitrary
cloud orchestration. Review schema compatibility before rollback: never run an old
binary against an unsupported migrated database. No update command replaces
backups or independently checking an interrupted deployment.
