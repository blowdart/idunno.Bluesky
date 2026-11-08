# Remote labeler client for bots

`idunno.Bluesky.Labeler.Agent` provides `LabelerAgent`, a bot-facing client for issuing and querying labels through a separately hosted labeler. The bot authenticates to its own PDS; the client requests a short-lived service token bound to the configured labeler service reference and exact XRPC method. The labeler host continues to enforce team authorization and policy, and retains the signing key, storage and publication responsibilities.

## Use the command-line sample

The `Samples.LabelerAgent` console app has four commands: `label` issues a label, `negate` negates a label, `list` prints the labels supported by the labeler, and `query` reads published labels for an account or record. The mutation and query commands accept either a complete AT URI or an account DID or handle. For example, issue a label to a post:

```powershell
dotnet run --project samples\Samples.LabelerAgent -- label `
  --username bot.example `
  --api-key xxxx-xxxx-xxxx-xxxx `
  --labeler-host https://labeler.example `
  --aturi at://did:plc:example/app.bsky.feed.post/3lxyz... `
  --label-value spam
```

Use `--account did:plc:...` or `--account person.example` instead of `--aturi` to issue or negate an account label. Specify exactly one target for `label` and `negate`. The `remove` command is an alias for `negate`; it publishes a negation rather than deleting label history. To negate a label on a post, use the same options with the `negate` command:

```powershell
dotnet run --project samples\Samples.LabelerAgent -- negate `
  --username bot.example `
  --api-key xxxx-xxxx-xxxx-xxxx `
  --labeler-host https://labeler.example `
  --aturi at://did:plc:example/app.bsky.feed.post/3lxyz... `
  --label-value spam
```

Successful mutations print "Label applied" or "Label negated". Successful no-ops print "Already applied" or "Nothing to negate" instead of exposing operation IDs, sequence numbers or raw change flags. Label outcomes have a label symbol, negation outcomes have a negation symbol, and application validation and operation errors have a cross mark.

The sample resolves handles to account IDs before sending negations, including handle-based record URIs. If resolution fails, no negation is sent. DID-based targets remain usable without handle resolution.

In interactive query output, negated label names use ANSI strikethrough, alongside the explicit negation indicator. Their timestamp is captioned "Negated at" because it describes the withdrawal, not the original application. Expiry information is omitted for negated labels. Redirected output omits ANSI styling.

For a time-limited label, add one of these options to `label`:

* `--expires-in 01:00:00` expires one hour after issuance. Durations use `[d.]hh:mm:ss`, for example `1.02:30:00` for one day, two hours and thirty minutes.
* `--expires-at "2030-01-04T18:00:00Z"` expires at an absolute date and time. Use `Z` for UTC or include an offset such as `-08:00`; dates without an offset use the machine's local timezone.

The options cannot be combined and are not accepted by `negate`, `list` or `query`. Durations must be positive and within the supported date range; absolute expiry must be in the future. Expiry is checked again immediately before issuance, so an absolute date that passes during sign-in is rejected. Without either option, the label does not expire.

List the label values declared by the labeler's current issuance policy with:

```powershell
dotnet run --project samples\Samples.LabelerAgent -- list `
  --username bot.example `
  --api-key xxxx-xxxx-xxxx-xxxx `
  --labeler-host https://labeler.example
```

The `label`, `negate` and `list` commands require an authenticated issuer account with labeler access. You can omit `--username` and `--api-key` when `_BlueskyHandle` and `_BlueskyPassword` are set in the environment. App passwords supplied directly as command-line arguments may be visible in shell history or process listings; prefer the environment variables for routine use. The legacy `--post` option remains an alias for `--aturi`.

Query published labels without credentials:

```powershell
dotnet run --project samples\Samples.LabelerAgent -- query `
  --labeler-host https://labeler.example `
  --account person.example
```

Use `--aturi at://did:plc:example/app.bsky.feed.post/3lxyz...` instead of `--account` to query a record. Specify exactly one target. The command follows all result pages and displays verified handles rather than DIDs, including in record URIs, with "Issued by" and "Targeted at" identifying the issuer and subject. If a handle cannot be verified, it says "Handle unavailable". Each property appears on its own line with its own Unicode symbol using UTF-8 console output. Friendly dates show local time followed by `/` and the UTC equivalent. Results are retained published labels, not necessarily active labels: negated labels are marked, and expiry reads "Expired" or "Expires" as appropriate. A query failure returns a nonzero exit code; output from earlier pages may already have been printed.

The labeler host derives two service references from its issuer DID: `labeler_admin` for administration APIs and `labeler_issuance` for the labeler-agent issuance APIs. Configure the host's `Labeler:AdministrationAudience` with the issuer DID and `labeler_admin` fragment; the host derives the issuance audience from that same DID. For authenticated commands, the sample discovers the DID and uses `Audience.Issuance` through `LabelerAgentBuilder`, then checks the requested value against the host's available labels before attempting a mutation. The account must still have live team permission to issue labels. The public `query` command calls `com.atproto.label.queryLabels` directly on the selected host without service authentication.

Authenticated `getAvailableLabels` queries use the host's configured issuance policy, including when moderation is disabled. An optional account or record target adds issuer-local applied-label state, with optional CID scope for records. Expired and negated labels are not treated as applied; URI-wide labels and labels on other record versions are distinguished. These lookups still require live team inspection permission.

## Configure an owned account agent

Use an owned agent when the bot does not already have an authenticated `AtProtoAgent` or `BlueskyAgent`. The `AccountAgent` property exposes the agent for the existing login, logout, credential persistence and refresh lifecycle:

```csharp
using idunno.AtProto;
using idunno.Bluesky.Labeler.Agent;

Did labelerDid = new("did:web:labeler.example");

using LabelerAgent labeler = LabelerAgentBuilder.Create()
    .WithLabelerService(new Uri("https://labeler.example/"), labelerDid)
    .ConfigureAccountAgent(agent => agent.ForService(new Uri("https://bsky.social")))
    .Build();

AtProtoHttpResult<bool> login = await labeler.AccountAgent.Login(
    identifier: "bot.example",
    password: password,
    cancellationToken: cancellationToken);
if (!login.Succeeded)
{
    throw new AtProtoHttpRequestException("The bot could not log in.");
}
```

The owned account agent is disposed with `LabelerAgent`. Persist and restore its credentials using the existing agent credential events and storage guidance; do not keep an app password in source code.

## Reuse an existing authenticated agent

When the bot already has an agent, pass it to the builder. `LabelerAgent` borrows the instance and does not dispose it. Construction requires an active session, and protected calls check again in case the account has since logged out or credentials expired.

```csharp
using idunno.AtProto;
using idunno.Bluesky;
using idunno.Bluesky.Labeler.Agent;

Did labelerDid = new("did:web:labeler.example");

using LabelerAgent labeler = LabelerAgentBuilder.Create()
    .WithLabelerService(new Uri("https://labeler.example/"), labelerDid)
    .WithAccountAgent(blueskyAgent)
    .Build();
```

`AtProtoAgent` and `BlueskyAgent` are both accepted. This shares the existing login, token refresh and credentials rather than maintaining a second session.

## Issue and negate labels

Supply a stable, caller-generated `operationId` and retain it with the bot's work item. The result contains that ID, the stream sequence when a label event was appended, and whether the operation changed state.

```csharp
LabelOperationReceipt receipt = (await labeler.IssueLabel(
    operationId: operationId,
    target: LabelTarget.Account(new Did("did:plc:example")),
    value: "spam",
    reason: "Automated review matched the configured rule.",
    cancellationToken: cancellationToken)).EnsureSucceeded().Result;
```

For a record, use `LabelTarget.Record(atUri, cid)` and optionally pass an absolute expiry to `IssueLabel`. `NegateLabel` accepts an account or record target and a declared value. The account DID used for authorization is taken from the authenticated service token; the request body cannot choose the actor.
Issue and negate calls require a nonblank reason, which the host retains with the operation audit. Receipt and available-label queries default to an administrative inspection reason when one is omitted.

Service tokens are short-lived and single-use. The client obtains a fresh token for each call and binds it to the exact method. If a request's outcome is uncertain, repeat the same operation with the same `operationId` and original intent; the host returns the durable original receipt for a matching retry and rejects conflicting reuse.

## Recover an operation receipt

`GetLabelOperationReceipt` makes one authenticated lookup. It is not a processing-status endpoint and does not poll. Use it when the mutation response was lost or the bot restarted before saving the receipt:

```csharp
AtProtoHttpResult<LabelOperationReceipt> result =
    await labeler.GetLabelOperationReceipt(operationId, cancellationToken: cancellationToken);
```

The lookup requires current inspection permission on the host. A missing or expired receipt is returned as a protocol error rather than an empty success.

## Discover available and applied labels

`GetAvailableLabels` reads the current host issuance policy and requires an authenticated team member with inspection permission:

```csharp
AtProtoHttpResult<IReadOnlyList<string>> available =
    await labeler.GetAvailableLabels(cancellationToken: cancellationToken);
```

`LabelerAgentBuilder.WithLabelerService` accepts the labeler service URI and its DID, then constructs the `labeler_issuance` audience automatically. The agent's issue, negate, operation-recovery and available-label methods call the separate `dev.idunno.labeler.issuance` XRPC namespace. Administrative CLI methods remain under `dev.idunno.labeler.admin` and use the `labeler_admin` audience. The host enforces the audience and exact method independently; neither audience is accepted by the other's routes.

The issuance procedures are:

| Procedure | HTTP method | XRPC path |
|---|---|---|
| Issue a label | `POST` | `/xrpc/dev.idunno.labeler.issuance.issueLabel` |
| Negate a label | `POST` | `/xrpc/dev.idunno.labeler.issuance.negateLabel` |
| Read an operation receipt | `GET` | `/xrpc/dev.idunno.labeler.issuance.getOperationReceipt` |
| List available labels | `GET` | `/xrpc/dev.idunno.labeler.issuance.getAvailableLabels` |

The lexicons and shared DTO definitions are in `lexicons/dev/idunno/labeler/issuance/`.
Query parameters, including the audit reason and optional target, appear in request URLs and may be logged; do not put secrets in them.
The labeler administration UI retains its separate `dev.idunno.labeler.admin.getManualLabelChoices` procedure.

`GetAppliedLabels` queries the public label distribution endpoint for the configured labeler source and a DID or record AT URI. It does not require the bot to be authenticated, and it never forwards the bot's PDS access credentials to the labeler. Use the returned page cursor to continue through results. Distribution queries expose the host's retained latest published slots; negations and expired winning labels can be present, so callers should inspect each `Label`'s negation and expiry fields rather than treating every returned entry as an active label.

## Errors and permissions

Issuance success bodies contain only the result declared by their lexicon; success is determined from the HTTP status, not a response `succeeded` field. Errors use the standard XRPC `{ "error": "...", "message": "..." }` body: authentication and permission failures use `AuthenticationRequired` and `Forbidden`, and malformed requests use `InvalidRequest`. Other lexicon-defined failures are mapped to typed `LabelerAgentError` subclasses; standard AT Protocol errors retain their existing typed mappings. The host checks current team membership, permissions, policy, lifecycle, target identity and durable idempotency on every mutation and protected lookup. An unauthenticated or logged-out agent cannot make a protected call; no anonymous fallback is attempted. Applied-label queries are explicitly public.
