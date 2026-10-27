# Xenon moderation web UI

The optional `idunno.Bluesky.AspNet.Labeler.UI` package provides a server-rendered,
accessible Xenon administration interface for reports, moderation cases and appeals,
manual account and record labels, audit, policy, team administration, and labeler
lifecycle operations. It uses the shared label administration dispatcher: it does not
create a separate authorization, issuance, or audit path.

## Enabling the UI in the standalone host

The canonical labeler host keeps the UI disabled by default. Enable optional
moderation and the UI with:

```text
LabelerModeration__Enabled=true
LabelerUI__Enabled=true
```

The UI requires `LabelerModeration__Enabled=true`; the host rejects a configuration
that enables the UI without the moderation backend. Existing local administration
and remote CLI administration remain independently configurable.

The host registers Bluesky authentication and its sign-in pages only when the UI is
enabled. Browse to `/Xenon` and sign in through the Bluesky authentication pages. The
host must be served over HTTPS in production. If it is behind a proxy, configure only
the trusted forwarded-header proxies described in the
[standalone deployment guide](deployment.md).

When enabled, the host also serves the website's OAuth metadata at
`/oauth-client-metadata.json`. Its client ID and `/Bluesky/Callback` redirect default
to the configured `Labeler:PublicEndpoint` origin. Sign-in requests `atproto` and
`rpc:app.bsky.actor.getProfile?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview`
to establish identity and populate the signed-in user's profile claims. The same OAuth options drive sign-in and
metadata generation. Host configuration under `BlueskyAgent:OAuthOptions` can
override these defaults, including client signing keys; metadata publishes only
their public components. Invalid metadata configuration fails at startup.

The separate `/native-client-metadata.json` document remains available for the
desktop CLI, with its native application type, loopback callback and administrative
scope superset. Both documents are public and are served before authentication.
For a development HTTPS tunnel, set `Labeler:PublicEndpoint` to its stable HTTPS
origin and make the metadata publicly fetchable without a tunnel access-login
page. Configure trusted forwarding as for any other reverse proxy.

## Adding the UI to another host

Register label administration, the Bluesky authentication handler, and its sign-in
UI before adding the Xenon pages. Then put authentication and authorization middleware
before the mapped Razor Pages:

```csharp
builder.Services
    .AddAuthentication(BlueskyAuthenticationDefaults.AuthenticationScheme)
    .AddBluesky()
    .AddBlueskyAuthenticationUI();
builder.Services.AddAuthorization();
builder.Services.AddBlueskyClaimsTransformer();
builder.Services.AddBlueskyOAuthClientMetadata();
builder.Services.AddLabelerUI();

// After building the application:
app.UseBlueskyOAuthClientMetadata();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
```

Configure `BlueskyAgent:OAuthOptions` with the custom host's HTTPS metadata client ID,
callback URI and required scopes before publishing its metadata.
Include the read-only profile permission above when using the claims transformer.
The header displays the transformed handle and a rounded avatar; clicking either
opens an accessible account dropdown containing Sign out. When profile claims are
unavailable, it shows Account and a neutral avatar placeholder instead of inventing
a handle. The avatar uses the authentication library's safe URI accessor and loads
without a referrer. Existing sessions need a fresh sign-in to grant the added profile
permission. Profile enrichment does not grant moderation authority.
The UI package does not enable or configure authentication for custom hosts. A host
must configure its authentication cookie/data-protection lifecycle, HTTPS, and
forwarded headers according to its deployment needs.

## Authorization and operations

Every administrative page requires an authenticated Bluesky DID. The UI derives the actor only from
the authenticated principal; forms cannot choose the acting identity. A trusted
server-side adapter asks the administration store to create short-lived browser
caller evidence for an active team member. The evidence expires after ten minutes,
and the shared dispatcher rechecks membership, role, revocation, operation-specific
permissions, and concurrency at the point of each operation. Removing or suspending
a member therefore prevents further administrative actions even while their browser
cookie remains valid.

Navigation is role-aware, but it is not the security boundary: page handlers and the
shared dispatcher enforce authorization server-side. Mutating forms use Razor Pages
antiforgery validation, explicit confirmation for sensitive operations, durable
operation IDs, and backend revision/conflict results. A failed or disconnected
request can be retried using its operation ID; lifecycle work can be inspected and
advanced again after a host restart because progress is stored by the backend.

Identity displays use `DidHandleCache` to show bidirectionally verified handles.
Lists expose the canonical DID (or exact record URI) on hover and keyboard focus;
Escape dismisses the tooltip. Details show the handle followed by the canonical
identifier. When resolution fails, the DID remains visible with "Handle unavailable."
Handles are cached for one hour and failed resolutions for one minute; they are
display aids, never replacements for persisted DIDs, authorization or record targets.
A custom host can register its own `IDidHandleResolver` before `AddLabelerUI`.

List tables show 16 rows per page, with right-aligned Previous/Next navigation and
the current/total page count. Column headers sort the complete filtered dataset,
not just the visible rows; received dates default to newest first. Subject sorting
uses the canonical DID or AT URI, not a mutable display handle. Case queues expose
workflow tabs and expandable filters. Each author summary counts all retained
reports to this labeler, including handled reports, and distinct record URIs
regardless of CID. Account reports contribute to the report total, not the record
count. Hover or keyboard focus explains both counts; report volume is not a finding
of wrongdoing.
The Cases list shows these counts in a compact author-card icon's hover/focus
details. Subject lists show a target icon and a readable description such as
"Post by @handle" or "List by @handle"; profiles/accounts show just the handle
beside a person icon. Feeds, starter packs and other records have distinct
descriptions, while the full canonical identifier remains available in the
identity tooltip. Icons are decorative; the text and canonical identifiers
carry the meaning. Subject icons are unmodified Font Awesome Free 7.3.1 SVGs
bundled locally under CC BY 4.0, with their upstream license notice and footer
credit. No Font Awesome CDN, external font or icon script is requested.
The same local icons provide author cards, search, workflow statuses, the
UTC/Local globe, account controls and verified-account cautions. In-review work
uses a clipboard-check icon, distinct from unresolved work's exclamation circle.
The status column is globally sortable. Checks distinguish resolved/handled work,
an exclamation circle identifies unresolved work, and scales identify pending
appeals; visible text carries the meaning without relying on color. Only Cases,
Reports and Appeals display live open-work totals in the navigation.
The Appeals list defaults to Pending; selecting All includes completed appeals and
is preserved when sorting or paging. Completed outcomes display as "Labels withdrawn",
"Labels changed" and "Labels kept" rather than Withdrawn, Replaced and Upheld.
Reports default to Unresolved, with Handled and All status filters. Filtering happens
before counting and paging, and sorting or paging preserves the selected status.
Reports and Appeals use a filter-icon button with an accessible action label.

The top-bar search box has a magnifying-glass submit button and searches reports
and appeals together. It matches case-insensitive partial subject handles,
canonical DIDs/record URIs, original report text, externally submitted appeal
messages and retained resolution reasons. Punctuation such as `%` and `_` is
literal, not a wildcard. Results are globally ordered newest first, counted and
paged in groups of 16.
Verified subject handles are indexed in the database by a bounded background
worker, not resolved synchronously for every search. Observations expire after
one hour and failed verification removes the previous handle match. Search
explicitly reports pending or unavailable handle coverage; original text remains
searchable. Handle indexing resolves public identities only and never sends report
or appeal text to an identity service. Search itself always rechecks live team
authority. Search terms appear in the browser's URL/history: deployers should
exclude query strings from access logs and treat browser history as confidential.

Report numbers open details with direct Bluesky links and locally rendered current
content. Record subjects show their AT URI beneath the subject summary and above
the reported CID, with both identifier values aligned in a shared column.
Previews include
post/profile previews and list/starter pack names and descriptions. The server resolves the author's DID document and reads
the current post, profile, list or starter pack record anonymously from its HTTPS PDS, including
record-referenced raster blobs. Moderator block relationships and AppView filtering
do not gate this content. The fixed anonymous public AppView supplies supplementary
handle/verification metadata and profile counts; an AppView failure does not hide repository content.
Profile previews use a full-width banner and overlapping circular avatar above the
name, handle and bio. Anonymous AppView metadata also supplies follower, following
and post counts when available; these counts are omitted if AppView is unavailable.
The read-only card does not show viewer relationships or follow/message controls.
No third-party embeds or remote browser media requests are used for reported content.
PDS resolution and media reads use the SSRF-protected HTTP handler with redirects
and cookies disabled. The signed-in user's
own header avatar is separate and loads from its transformed profile URI.
Missing/deleted/restricted content produces
an explicit unavailable preview, not a failed moderation action. A differing record
CID is highlighted: current content is not preserved evidence of the reported version.
The general current-content disclaimer and duplicate current CID are omitted when the fetched record CID
matches the reported CID; it remains when no comparison is possible or the CIDs differ.
Images are limited to ten MiB each, with up to four post images or twenty gallery
images. Profile avatars and banners come from their repository blob references.
Images start blurred when the current record self-labels adult, sexual, nude or
graphic content, or Bluesky moderation applies those warnings or `!hide`/`!warn`.
Xenon also respects Bluesky's custom label definitions marked adult-only or
media-blurring (including sensitive content and self-harm); ordinary spam and
impersonation labels alone do not blur images. Anonymous label reads go to the
fixed Bluesky moderation service, not a subject-selected endpoint, and match the
account or exact record URI and current CID. Negated, expired, unrelated and
different-CID labels are not applied.

The smaller warning below the preview has an exclamation-circle icon and identifies
self-labels separately from Bluesky moderation, without repeating control instructions.
Only video previews show "Videos require opening the player."
Their accessible play-circle button opens a viewport-bounded player sized to the
record's declared aspect ratio when valid, or a temporary widescreen layout otherwise.
Loaded video metadata corrects the ratio, and resizing the window refits the player.
An eye button over each image reveals the images in that preview only, then changes to
a crossed-out eye to restore blurring. These buttons support keyboard activation and
accessible Show images / Blur images labels; clicking the image itself does nothing.
Image descriptions, when supplied, appear beneath the images. **Blur images** restores
protection. The choice is not saved across page loads. Images stay blurred without
JavaScript, and their descriptions are excluded from the accessibility tree until
revealed. Videos remain behind their explicit open-player control. If a bounded
moderation lookup fails or is incomplete, a visible notice explains that labels
could not be fully checked: known warnings still blur images, while images without
a known warning remain visible. This is a viewing aid, not a moderation decision,
and does not change any labels, reports or audit records.
Gallery images stay in one horizontally scrollable row, which can also receive
keyboard focus. Bluesky links preserve DID colons, include the locally hosted
Font Awesome Bluesky logo, and open in a new tab.
Case/review subjects and assignees show handles with canonical DIDs or record
URIs available on hover or keyboard focus rather than repeated beneath them.
Author-summary cards have a visible border and list separate open-report counts for
posts, profiles, lists, feeds, starter packs and other records. Profile counts
include account-level reports. The separate account total includes all of its
record reports and previously handled reports; repeated reports count separately.
Case, report, appeal and search lists show relative ages such as "5 minutes ago",
refreshed every minute. Hover or keyboard-focus the date to see its exact timestamp,
using the selected UTC/local time zone. Without JavaScript, the exact UTC date
remains visible.
In appeal review, changing the label selection selects **Remove labels** when
all applicable labels are cleared, or **Replace labels** for any other changed
selection, including partial removal. Returning to the original selection restores
the previously chosen outcome; outcomes remain manually selectable.
Clicking Submit confirms the case/appeal decision; there is no separate confirmation
checkbox. Opening an appeal from the Appeals list or search results preselects
that appeal for review. Case/appeal decision and assignment reasons are optional;
leaving one blank records "No reason provided." in the audit history.
If a submission is rejected, its label selections, appeal outcome,
reason and report-handling choice remain visible while the observed case, policy
and labels are unchanged. Incompatible outcomes produce a validation message, not
an exception page. Changed observations require fresh review instead of silently
rebasing the rejected decision.
**Acknowledge a superseded decision** records that newer outcomes have overtaken
the appeal's captured labels, resolving that appeal without changing current labels.
When an appeal decision commits, other pending appeals for the same subject and
CID are automatically marked superseded only if every captured outcome is no
longer current. Partly current appeals and appeals with no captured labels remain
pending. Original evidence and submissions are retained; each automatic closure
records the triggering operation and a reason in moderation history, in the same
transaction as label changes. Retrying the decision does not duplicate closures.
Videos appear as a placeholder: clicking opens a modal player, without inline
playback, preloading or autoplay. The player reads the referenced MP4 blob through
an authenticated same-origin endpoint; each request rechecks live team membership.
Media references expire after ten minutes and transfers are capped at 100 MiB and
two minutes, with no shared caching. Close or Escape stops playback and releases
the media. Reload the details page if a reference expires. Quoted posts and
external cards are not expanded.

Report and case review pages place a newest-first moderation timeline on the right,
stacking it below the review on narrow screens. It combines original submissions,
shared case events and case-decision label changes, showing actor, date, reason and
the selected report where available. History is counted and paged in batches of
16; it is scoped to the current case, not an assertion that every event acts on the
selected report. Independent manual-label operations remain in Audit.

Review shows compact, separately labeled account and exact-record label badges,
including retained active values no longer declared by the current policy.
Record decisions never change the author's account labels. Actual valid AppView
verification state produces a prominent amber caution above the review controls:
"The user has been verified by Bluesky. They may be high profile, take care in
applying or removing labels."

Moderators select configured labels to add them and clear currently applied labels
to remove them. Chips distinguish current state from pending additions/removals;
policy versions and issuance/negation plumbing are not editable fields.
Observed state is integrity-protected and commits fence case revision and label
sequence, including the absence of earlier label history. Labels active on a
different CID are explicitly identified and cannot be changed through this form.

Opening review from a report retains that report's ID. A checked-by-default option
also marks other open reports for the exact subject and CID handled, without
dismissing them as unfounded. Clearing it handles only the selected report,
irrespective of its age. A case-level decision without a selected report handles no
reports when the option is cleared. Pending reports or appeals keep the case in
review. Label changes and report-handling metadata commit atomically and retain
the original report evidence. Same-operation recovery does not handle reports that
arrive later. If a commit outcome is unknown, the UI retains the original protected
form and offers an unchanged retry rather than issuing a new decision.

While moderation is enabled, a shared background worker also checks open post
reports and pending appeals at the author's authoritative PDS. Only an explicit
`RecordNotFound` response closes work with the exact resolution reason
`Post deleted`; missing AppView previews, generic 404s, unavailable servers and
timeouts do not. Existing labels remain unchanged. Pending appeals become
Superseded, unresolved reports are handled, and the case resolves atomically.
The history identifies the action as automatic deleted-post review rather than
attributing it to a human moderator. Checks are revision-fenced, persisted and
resumed after restart; a changed post CID alone is not deletion. Account and
non-post record reviews are unaffected.

A keyboard-accessible clock button at the right of the top navigation, before
the account dropdown, switches all displayed
dates between UTC and the browser's local time zone without changing stored
timestamps. It includes list dates, report details, previews, history and deadlines,
and remains available on pages without dates. The choice is saved in browser-local storage
and restored on subsequent pages and visits; UTC is the default until a choice is
saved. Clearing browser storage resets it, and storage failures are reported
visibly without preventing the toggle from working on the current page. Assignment choices are active
moderators/administrators displayed by verified handle, and mutation buttons align
to the right. Assignment routes work, but does not restrict decision authority:
another authorized moderator can decide the case, with the actual actor retained
in history and stale revisions rejected.

With one active moderator/administrator, new cases are automatically assigned to
that member. Existing unresolved cases are reconciled during team changes or the
next authorized inspection, including after an upgrade/restart. Changes persist
with revision increments and explicit automatic-assignment history/audit; resolved
cases and original receipts remain unchanged. Assignment controls are hidden while
there is only one eligible member. Adding another eligible member restores manual
routing without unassigning existing cases.

Every Xenon page includes a footer with copyright, a GitHub source link, the UI
assembly's three-part version and its informational build identifier (including
prerelease and commit information when present). These identify the installed UI
package rather than the application's unrelated host assembly.

The footer's Theme selector offers System, Light and Dark. System is the default
and follows operating-system appearance changes. An explicit light/dark choice is
stored locally in that browser, applied before rendering, and synchronized across
open tabs on the same origin; choosing System removes the override. This preference
does not synchronize across devices or require a server-side user profile. If browser
storage is unavailable, the page reports that the choice could not be remembered.
Without JavaScript, appearance follows the operating system.

Registering Xenon also applies this shared shell and theme selector to the Bluesky
sign-in, sign-out and authentication error pages. Hosts without Xenon retain the
authentication UI's standalone layout.

The logo-only Xenon home link in the header and the favicon share the Xe element-tile SVG mark. Its violet tile,
light X and mint e remain the same in both themes, and its vector paths do not
depend on installed fonts or external image services.

The footer also links public `/Xenon/Terms` and `/Xenon/Privacy` pages. These are
operator-neutral baseline notices, not a complete deployment-specific agreement
or privacy policy. Before exposing a service, the operator must provide its
identity/contact details, actual data-processing practices, applicable legal
bases, retention periods, providers and other legally required disclosures, and
obtain appropriate legal review. Custom hosts can override the Razor Pages to
publish their notices. Reading these pages requires neither sign-in nor team
membership; it does not grant access to any administrative page.

Manual account labels target a DID. Manual record labels target an AT URI and may
include a CID to scope the label to a specific record version. Both use the same
label issuance, authorization, policy, and audit services as moderation decisions.
The manual page presents policy values as selectable label buttons rather than
free-text values. Entering a target or changing its optional CID refreshes this
labeler's active labels in the background; **Load labels** provides an explicit
alternative and works without JavaScript.
The target type is inferred on the server: DID/handle means account, and a complete
AT URI means record. With JavaScript, the optional CID field is shown only for record
targets. Repository-only AT URIs such as `at://did:plc:example` or `at://handle.example`
are accepted as account targets and displayed as resolved bare DIDs; incomplete
record paths remain validation errors. The CID field is cleared when switching
to an account. **Load labels** is normally hidden
when automatic lookup is available, appearing when lookup fails so it can be retried.
Pasted HTTPS `bsky.app` post, list, feed and starter-pack links are converted to
canonical DID-based AT URIs after lookup. Profile links become account DIDs, not
profile records. The input displays the converted target; share queries and fragments
are ignored. Unsupported routes and non-Bluesky URLs are rejected without fetching
the pasted URL. Conversion also works through **Load labels** without JavaScript.
No public moderation labeler is queried for this selection. Lookup failures keep submission disabled and are displayed
explicitly. Account handles are resolved to verified DIDs before staging changes.
With no CID, active labels on any version of that record are selected; with an
explicit CID, active labels scoped differently are identified and cannot be changed
from that selection. Account labels remain separate from record labels.

Selecting a label stages an addition; clearing a selected label stages a negation.
**Submit label changes** and **Reason for labelling** replace the old action/value
fields and redundant confirmation checkbox. Each changed label uses shared live
authorization, policy, maintenance and audit checks, and its observed sequence is
fenced against concurrent edits. Multiple changes are individual durable operations,
not an atomic batch: partial or uncertain failures identify confirmed progress and
provide an unchanged retry using the original operation identities. Loading labels
again discards staged intent and requires reviewing the current state.
The UI performs no privileged PDS or AppView actions and does not provide Ozone
compatibility.

Administrative audit entries capture the database UTC time when written, retaining
the original time when an operation receipt is recovered or retried. The Audit
table uses the shared UTC/local-time controls. Older events without persisted
timestamps show **Time unavailable**; no historical times are fabricated.
Filter the Audit table by actor DID or handle (with an optional leading `@`), outcome, and
inclusive from/to date-times. Choose UTC or local time using the radio buttons;
local time uses the browser time zone shown on hover or keyboard focus over **Local**.
The switch sits beside the date/time limits. This choice is
independent of the display time zone. Invalid or ambiguous daylight-saving times
are rejected; use UTC to select a boundary during a repeated hour.
Either date boundary may be omitted. Date
filters exclude historical entries without timestamps. Filters apply to the
provider count and selected page and remain in effect when sorting or paging.
The visible audit table uses sortable timestamps rather than sequence numbers;
sequence numbers remain internal ordering tie-breakers.
Handles are verified and resolved to DIDs before filtering or exporting; local OS
identities and system identifiers are not accepted as actor filters. Resolution
failures are shown explicitly rather than returning an unfiltered log.

The download glyph exports every event matching the applied filters, not just
the current page, as a JSON array in sequence order. JSON retains sequence numbers
and timestamps for machine processing. Downloads negotiate Brotli or gzip using
`Accept-Encoding`, or send uncompressed JSON. Responses are not cached.

Downloads require antiforgery protection and live inspection authority. Each
download first records an `audit-export-started` event with the actor, filters
and sequence cutoff. This records the start, not confirmation that the browser
saved the file. The export excludes that new event and any later events.
It reads bounded pages and rechecks authorization between them; storage errors,
cancellation or lost authority abort the response rather than completing a
misleading partial JSON file. Successful audit-feed reads do not append events,
but the UI's separate team/navigation authorization checks retain their existing audit behavior.

The administrator Policy page provides grouped report-reason checkboxes with
readable names, an all-reasons option and a custom-identifier field. Choosing only
selected reasons with none checked accepts no reasons. Ozone and legacy AT Protocol
identifiers remain distinct. The controls replace only `reasonTypes` in the draft
declaration, preserving other fields; restrictions become active only after
publication and independent verification. Authenticated reports outside the
selected reasons are retained as handled with reason `Not Applicable`, without
changing labels or existing case workflow. Automatic audit entries show the
labeler's identity and an Automatic caption; the original reporter is retained
separately.

## Running the isolated manual-test host

`tools/Xenon.TestHost` is a runnable development application, not an automated test
runner or a production labeler. It uses real Bluesky browser OAuth, real team
authorization and the same administration services as the CLI, backed by its own
SQLite database. It supplies fictional reports, cases, an appeal, account and
record targets, and a fixed fixture policy; no labeler account, DID publication,
public label queries/subscriptions or public report intake is required.

From the repository root, configure the stable HTTPS origin of a tunnel forwarding
to `http://127.0.0.1:5187`, and the actual DID of the Bluesky account you will use:

```powershell
$env:TestHost__PublicOrigin = "https://your-tunnel-host.example"
$env:TestHost__AdministratorDid = "did:plc:your-actual-did"
$env:Labeler__TrustedProxies__0 = "127.0.0.1"
$env:Labeler__TrustedProxies__1 = "::ffff:127.0.0.1"
dotnet run --project tools\Xenon.TestHost -- --seed
```

Replace the example origin and DID before running. Allow anonymous access through
the tunnel: the PDS must be able to fetch both `/oauth-client-metadata.json` and
`/native-client-metadata.json` without a tunnel authentication interstitial.
Browse to `https://your-tunnel-host.example/Xenon` and sign in with that account.
Do not expose an unrelated production host or grant the tunnel access to other
local services.

Forwarded scheme and client IP are trusted only from the exact addresses in
`Labeler:TrustedProxies`; configure the actual tunnel proxy addresses, not arbitrary
networks. Forwarding runs before the explicit host-filtering middleware, metadata
and authentication. Allowed hostnames are the configured public hostname,
`localhost` and `127.0.0.1`; other hosts receive HTTP 400. `X-Forwarded-Host` is not
used to select the OAuth origin, and metadata always uses the configured origin.
Ensure the proxy preserves the public Host header or forwards a permitted loopback
host. `TestHost:Port` changes the loopback listening port if needed.

The explicit `--seed` step verifies the administrator DID, grants the current OS
user protected local CLI administration, and creates the fictional fixture state.
Subsequent starts omit `--seed`; repeating it with the same administrator safely
reuses the setup operations instead of duplicating reports. It does not reset
changes made during manual testing. A different administrator requires the normal
team administration workflow or a fresh test directory.

State defaults to the current user's local application-data directory under
`idunno.Bluesky/Xenon.TestHost`. To start a fresh test environment, set
`TestHost__DataDirectory` to a new, dedicated directory and run with `--seed`.
Never point it at production state or an existing general-purpose directory:
the host restricts directory permissions and stores a development signing key,
authentication data-protection keys and its database there. Retain the directory
to resume testing across restarts. A missing signing key with an existing database
is an error, not permission to silently replace the signing identity.

Browser authentication also persists in `xenon.db`: an internal SQLite
`IXmlRepository` retains the Data Protection key ring, and the existing SQLite
authentication providers retain encrypted OAuth identities, refresh locks and
single-use login state. Restarting with the same data directory and OS user no
longer discards the credentials referenced by a valid browser cookie. Normal
cookie expiry, sign-out, token revocation and live team authorization still apply.
Sessions already lost by the previous in-memory store require one new sign-in.

On the first start after this change, the repository imports existing XML
Data Protection keys and revocation records from the test data directory in one
transaction, without deleting the original files. Subsequent starts use the
database key ring and do not reimport those files. Back up the entire private data
directory. New keys are protected with current-user DPAPI on Windows, so restoring
the database alone under a different Windows account is not sufficient. On other
platforms, this development harness relies on the private directory's permissions
to protect the key ring; database access also grants access to the authentication
data it protects. No additional database server or NuGet dependency is required.

### CLI access

While the host is running, use the protected local endpoint from the same OS user:

```powershell
dotnet run --project src\idunno.Bluesky.Labeler.Cli -- team --local-endpoint xenon-test-admin
dotnet run --project src\idunno.Bluesky.Labeler.Cli -- status --local-endpoint xenon-test-admin
```

On Linux, use the `admin.sock` path printed at startup instead of the Windows pipe
name. `TestHost:LocalEndpoint` overrides the endpoint on either platform.

To exercise remote CLI OAuth through the tunnel:

```powershell
dotnet run --project src\idunno.Bluesky.Labeler.Cli -- team `
  --admin-url https://your-tunnel-host.example `
  --audience "did:web:labeler.xenon.invalid#labeler_admin" `
  --handle your-actual-handle.bsky.social `
  --client-id https://your-tunnel-host.example/native-client-metadata.json
```

The CLI's native metadata is separate from the website's web metadata, but both
are served by this host. Browser/remote CLI sign-in still requires a reachable
tunnel and a real Bluesky account; the host does not replace those authentication
paths with a fake login.

### Fixtures and boundaries

The header's UTC/Local text and clock form one clickable control with the same
background styling in both modes; the text does not show a time zone identifier.

Account targets are `did:web:subject-01.xenon.invalid` through
`did:web:subject-40.xenon.invalid`. Fictional seeding creates 46 reports across 40 cases.
Cases for subjects 01, 02 and 04 have two, three and four reports respectively,
demonstrating multiple submissions grouped under the same exact-subject case.
The cases include one resolved case, so the fictional unresolved queue spans three pages
(16, 16 and 7 cases). Rerun with `--seed` to add missing fixtures to an existing
test environment without resetting prior work. Each subject has a fictional record at
`at://<subject-did>/app.bsky.feed.post/example`, with optional CID
`bafyreihdwdcefgh4dqkjv67uzcmw7ojee6xedzdetojuzjevtenxquvyku`.
The fixed policy allows `spam` and `scam` labels on accounts and records.
Report intake accepts Spam, Other and Appeal reasons, including their legacy
AT Protocol identifiers. Explicit `--seed` upgrades the original unrestricted
fixture policy to this version, without overwriting custom policies.
It also submits one fictional out-of-scope report, handled as `Not Applicable`,
with an automatic system audit entry. This example is idempotent and changes no
labels or pending review work.
Fixture identity/record reads are resolved locally; other identity reads use the
canonical host's hardened HTTPS client. Use these fictional targets for manual
labeling and decisions. Fixture identities also resolve to matching handles, such
as `subject-01.xenon.invalid`, so the test host exercises verified handle displays
without sending those identity reads to real services.

The `--seed` option is available only in Debug builds. Other builds reject it
before host initialization or any test-data changes.
The explicit `--seed` command also adds six local-only test reports against
approved public records by `bot.idunno.blue` (verified against its expected DID):
`3myawvej4uk2z` (text), `3mtmey2zsf32p` and `3mpjmw6imdy2k` (videos), and
`3mnzf3lvwvq2t` (image gallery), the Test moderation list
(`app.bsky.graph.list/3lnxvrbetf32n`) and the profile (`app.bsky.actor.profile/self`).
Their current record CIDs are captured from the
author's PDS; the reports explicitly say they are test data, not allegations.
This step requires network access and resumes missing additions on repeated
`--seed` runs. It neither labels those posts nor reports them to another service.
Normal starts do not seed or reset reports; previewing these real subjects
requires their public PDS to remain available.

The harness deliberately does not publish to a PDS or PLC, register a public
labeler, distribute labels, or accept public reports. The initial fixed policy
declaration is supplied locally, so publishing a different policy remotely is not
a supported end-to-end scenario. Team roles, suspension/removal, moderation and
manual label issuance use the real provider. Lifecycle pages expose shared
backend state, but this single-process harness without distribution hosts does
not demonstrate coordinated multi-host key activation, drains or public delivery.
