---
description: 'Audit missing SDK endpoints, records and types as well as conformance against published AT Protocol and Bluesky lexicons.'
---

# Lexicon conformance audit

Audit this repository against the published lexicons at:

* https://github.com/bluesky-social/atproto/tree/main/lexicons
  for canonical AT Protocol (`com.atproto`) schemas.
* https://github.com/bluesky-social/atproto/tree/main/packages/api/lexicons
  for the installed Bluesky (`app.bsky`) and chat (`chat.bsky`) schemas.
* https://github.com/bluesky-social/atproto/tree/main/packages/bsky/lexicons
  for the AppView's installed schemas, including external-content dependencies.
* https://github.com/bluesky-social/atproto/tree/main/lexicons-overrides
  for local schema overrides.

Resolve these locations at the pinned upstream revision rather than mixing moving
`main` content with pinned schemas. Verify schema authority and resolver precedence
using `lexicons/README.md`, `packages/api/lexicons.json` and
`packages/bsky/lexicons.json`; installed package copies may differ from other
consumers' snapshots. Reconcile both packages by NSID, report conflicting copies,
and inventory imported third-party schemas separately without automatically
expanding SDK scope.

### Selecting the current schema

Before using a lexicon to audit or generate SDK code, establish which copy is the
most up-to-date authoritative definition for that NSID. Do not assume either
`packages/api/lexicons` or `packages/bsky/lexicons` is always newer.

* Compare all copies at the pinned revision semantically, ignoring formatting and
  object-property order. Consult each consumer's resolver manifest and any available
  source, revision or integrity metadata, then inspect the relevant schema history
  to establish provenance and whether one copy supersedes another.
* A newer enclosing commit or file modification date alone does not establish a newer
  schema: files may have been copied, reformatted, reverted or intentionally overridden.
  Distinguish a stale installed snapshot from an intentional consumer-specific override.
* Follow the documented canonical source when installed copies disagree. If resolving
  freshness requires another upstream repository or network-published lexicon, pin and
  record that source revision or immutable content identifier as well. Do not silently
  substitute moving upstream content into the original pinned snapshot.
* Record the selected schema path and revision, competing copies, semantic differences,
  and the evidence for the selection. Use that selection consistently for implementation,
  associated types, source-generation metadata and tests; do not combine incompatible
  definitions from different versions into an invented schema.
* If authority or freshness cannot be established, report the conflict as unverified
  and obtain a user decision before generating or changing code for the affected schema.

Do not modify SDK code, tests, documentation or configuration unless explicitly asked.

## Normative grounding

* Use the [AT Protocol Lexicon standard](https://atproto.com/specs/lexicon) as the normative
  grounding for validation and conformance. Apply its schema and validation semantics when
  interpreting lexicon constraints; do not infer stricter requirements from SDK conventions
  or examples.
* Use the published lexicon definitions as the endpoint- and type-specific source of truth,
  interpreted according to the standard. Resolve apparent ambiguity by checking both sources
  and record any remaining uncertainty rather than inventing a restriction.
* A required string field must be present, but `required` does not imply non-empty.
  Accept empty strings unless an explicit lexicon constraint, such as a positive
  `minLength`, `minGraphemes`, `format`, `enum` or `const`, excludes them. Distinguish
  an empty string from a missing field or `null`, including for string array elements.
  Flag SDK guards that reject empty strings without a corresponding lexicon constraint.

## Scope and evidence

* Cover AT Protocol, Bluesky and chat endpoints and all reachable request, response,
  definition and repository-record types, plus standalone records and definitions not
  reachable from implemented endpoints. Respect any narrower scope supplied by the user.
  Exclude Ozone APIs, records and definitions unless explicitly requested; retain excluded
  entries in the coverage inventory rather than silently omitting them.
* Pin the upstream lexicon commit SHA and record the SDK commit and relevant uncommitted
  changes. Use one upstream snapshot throughout the audit and link findings to that snapshot.
* Inventory lexicons and SDK implementations in both directions. Distinguish missing
  endpoints, implemented endpoints, deprecated endpoints, intentional exclusions and
  SDK methods with no current published lexicon. Endpoint presence is not conformance.
* Trace each endpoint through its public agent method, server method, request construction,
  DTOs, converters, result mapping and source-generated JSON registrations.

## Mandatory coverage discovery

Complete upstream-first coverage discovery before checking implementation conformance.
Do not use existing SDK methods, endpoint-status documentation or SDK-referenced lexicons
as the starting universe: these cannot reveal capabilities with no SDK counterpart.

* Discover all authoritative lexicon roots, package copies and overrides at the pinned
  upstream revision. Record precedence and reconcile duplicate NSIDs. Do not assume all
  schemas live under the root `lexicons` directory. Resolve conflicting copies from
  upstream build or generation configuration; report unresolved authority as unverified.
* Enumerate every scoped lexicon NSID and every named definition, including queries,
  procedures, subscriptions, repository records and reusable definitions. Inventory
  standalone records and definitions independently, even when no implemented endpoint
  references them.
* For each item, record its NSID and definition name, kind, authoritative pinned schema
  link, corresponding SDK files and members, and implementation status. Use exactly one
  status per item: implemented, partial, missing, deprecated, explicitly excluded or
  unverified. Record the reason and authority for exclusions and deprecations; distinguish
  upstream deprecation from an SDK-only obsolete API.
* Map each endpoint to its agent and server surfaces, or document why a surface does not
  apply. For existing endpoints, enumerate missing input parameters, output fields, named
  errors and union members rather than marking an endpoint implemented solely because
  a method exists.
* Resolve local-fragment and cross-lexicon references transitively, tracking visited
  definitions so cycles do not prevent completion. Account for associated request and
  response models, nested definitions, repository records, record envelopes, converters,
  discriminator registrations and source-generated metadata.
* Distinguish missing wire support from missing typed convenience APIs. A generic
  raw-JSON escape hatch is not typed support; report the supported raw path and any
  missing typed capability separately. Do not require a dedicated CLR class for every
  definition when an existing shared type faithfully represents its shape.
* Reconcile inventory totals by kind and status, counting each authoritative
  NSID/definition pair once. Every scoped item must be accounted for, including unmatched
  upstream items and SDK APIs with no current published schema. Endpoint-status
  documentation, passing tests and representative samples are supporting evidence,
  not proof that this inventory is complete.

## Historical additions and changes

Audit both the current pinned snapshot and upstream history; neither replaces the other.
The snapshot discovers older omissions, while history identifies additions to existing
capabilities that a presence-only inventory could miss.

* Review the user-requested date range. If none is supplied, review the preceding two
  calendar months ending at the pinned upstream commit timestamp. State exact start and
  end timestamps, boundary semantics, and the upstream revisions used for comparison.
* Enumerate all lexicon-changing commits in that range across the discovered schema roots,
  including overrides and package copies. Follow renames, moves and root migrations;
  retrieve complete paginated commit/file listings and do not treat truncated API output
  as a complete change set.
* Compare schema content semantically, not just filenames or textual diff size. Classify
  new endpoints, records and definitions; added parameters, fields and union members;
  changed constraints, defaults and errors; deprecations; removals; and layout-only moves.
  Account for imported third-party schemas without automatically expanding SDK scope.
* Reconcile every semantic change with the current-snapshot inventory and SDK evidence.
  Separate already supported changes, missing capabilities, conformance defects,
  explicit exclusions and changes superseded or reverted before the pinned revision.
  Do not propose implementing a historical addition that is absent from the final
  snapshot without explaining its current status.
* Record unavailable history, unresolved references and uncertain schema authority as
  coverage gaps. Do not claim a complete lookback based only on selected example commits
  or the most recent upstream changes.

## Static conformance

* Compare HTTP method, XRPC path, input and output encoding, authentication and service
  routing, query keys, repeated array parameters, escaping, Boolean and enum wire values,
  pagination, optional parameters, defaults, bounds and named errors.
* Compare every reachable model's JSON field names, required versus optional fields,
  nullability, types, formats, constraints, nested references, arrays, unions and discriminators.
  Distinguish wire mismatches from SDK conveniences and from omitted optional capabilities.
* Treat lexicon `knownValues` as open sets. Check unknown-value handling, custom converters
  and preservation of unknown union members rather than assuming closed enums.
* Verify unknown-union round trips retain the original `$type` and all extra fields,
  including nested values. `JsonExtensionData` alone is not proof: polymorphic metadata
  handling can consume an unknown discriminator before extension data sees it.
* Check that result mappings uphold non-nullable collection element contracts. Nullable
  annotation enforcement does not reject null array elements. Follow existing
  `WithoutNullEntries` mappings where appropriate, and distinguish malformed required
  fields from optional or unknown data rather than silently defaulting them.
  Recursively inventory every response collection, including nested collections and DTOs
  returned directly without a mapping layer. Check verified and unverified collections
  separately; one sanitized array does not prove its sibling arrays are safe.
* Verify source-generated metadata using the library's configured serializer options;
  reflection is disabled. Repository records need both concrete type and
  `AtProtoRepositoryRecord<TRecord>` registrations.
* AOT and trimming compliance are required, not optional. Check all affected serialization,
  converters and request paths for reflection, dynamic code and missing metadata. Respect
  the repository's target-framework rules: AOT and trimming are enabled for .NET 9 and later.
  A passing ordinary runtime test alone does not establish AOT compliance.
* For `BlueskyRecord` derivatives, check base-type discriminator dispatch and direct
  concrete serialization of the lexicon `$type`. These are separate requirements.
* Classify binary responses, account exports and JSON Lines responses for potentially
  unbounded size. Such responses must stream rather than buffer through the JSON response
  helper; increasing the buffer limit is not a streaming fix. Verify response ownership,
  disposal on success, failure and cancellation, bounded error-body reads, and untrusted
  server-reported metadata.
* Verify service routing separately for authenticated and unauthenticated agents, including
  explicitly configured public AppView URLs. Use distinct PDS and AppView addresses in
  routing tests so an incorrect route cannot pass because both services share one address.
  Confirm that public reads do not inadvertently send private credentials.
* Verify configured logging and credential-update callbacks flow through agent, server
  and HTTP client layers. Streaming paths must retain authentication, proxy headers,
  credential updates, nonce retry behavior and error mapping, not just return a stream.
* Compare XML exception documentation with actual argument guards and nested calls.
  Check null, empty and out-of-range inputs and document the specific exception thrown.
  Check both public agent and server overloads independently, including exceptions from
  delegated validation and null, empty or whitespace entries in collection parameters.
* When streaming changes response-size or buffering guarantees, reconcile documentation
  on the endpoint, shared client and agent properties, options and response callbacks.
  Describe which successful responses bypass size limits, which error bodies remain
  bounded, and when a callback causes buffering or rejection. Endpoint documentation
  alone does not establish an accurate shared contract.
* Check every changed `<inheritdoc cref="...">` against the exact intended overload,
  including new parameter types and order. Reject self-references and circular chains;
  a clean compile alone does not prove inherited documentation is useful.

## Read endpoints: actual calls

* Validate read endpoints against both the lexicon and actual service calls where safe
  and authorized. Use public services and public data where possible.
* Never request or expose passwords, tokens, private messages or private account data.
  Use existing authorized credentials only with explicit permission for private reads.
* Record the service, endpoint, sanitized parameters, time, HTTP status and relevant
  response shape. Exercise SDK deserialization, not just a raw HTTP request.
* Check optional parameters and pagination where practical. Compare representative
  responses to their reachable DTOs; one successful response is not proof of full conformance.
* Do not use writes to set up read probes. Do not bypass rate limits or retry indefinitely.
* Label checks separately as static inspection, local mock integration, captured response
  or live call. Never describe a `TestServer` check as a live Bluesky call.
* Mark unavailable authentication, rate limits, service failures and untested paths as
  blocked or unverified, not conformant.

## Write endpoints

* Validate procedures and repository writes statically against the pinned published
  lexicons, including request bodies, repository records, outputs and named errors.
* Use existing local tests or captured fixtures where available. Do not execute live writes,
  change settings, create posts, initiate age assurance, delete accounts or perform moderation.

## Report

* Present a coverage summary and a findings table with endpoint or type, SDK file and
  member, pinned lexicon link, expected versus actual behavior, impact and evidence.
* Report inventory totals by kind and status, the exact historical window and revisions,
  discovered schema roots and precedence, and any incomplete pagination or reference
  resolution. Include missing endpoints, standalone records and definitions with no SDK
  counterpart; use "no implementation" rather than inventing an SDK member location.
* Separate missing capabilities, confirmed nonconforming implementations, intentional
  differences, deprecated API support and unverified coverage. Within missing capabilities,
  distinguish unsupported endpoints and records, partially implemented shapes, and missing
  optional typed conveniences.
* For each missing or partial capability, list the associated records, request/response
  types, nested definitions and serialization wiring needed to support it. Identify shared
  dependencies without counting them as separate endpoint omissions.
* Do not claim exhaustive coverage unless every scoped endpoint, record and named
  definition has been accounted for and inventory totals reconcile. Distinguish complete
  discovery from completed static, local-test and live validation.
* Include read-call outcomes and exact test commands and results when tests are run.
  Keep private response content and credentials out of reports and artifacts.
* Report relevant existing analyzer suppressions and their justification. If a subsequent
  fix is authorized, explicitly list every suppression added, changed or removed.
* End every audit or authorized fix with a concise completion summary stating the outcome,
  changes made (or that no changes were made), validation commands and results, and any
  remaining variances, blockers or unverified coverage. For each suppression added, changed
  or removed, give the diagnostic ID, exact repository-relative file path and line range,
  affected member or test, and justification. State explicitly when there are no suppression
  changes; a count without precise locations is insufficient.

## If fixes are subsequently requested

Before implementing newly discovered capabilities, present the proposed endpoints,
repository records and associated types, including additions to existing APIs and models.
Identify experimental status, exclusions and shared dependencies. Obtain explicit approval
for that scope; approval to fix conformance defects does not implicitly authorize new APIs
or records. If the user has already approved an enumerated scope, implement that scope
without asking again, but obtain approval before adding newly discovered capabilities.

Follow repository instructions, preserve existing public signatures with overloads,
keep cancellation tokens last, update public API tracking and source-generation metadata,
and add focused request and serialization regression tests. Update related endpoint status
documentation. Update `CHANGELOG.md` for every user-visible addition, change or removal
under the unreleased section and the affected package heading. The completion summary must
explicitly state whether the changelog was updated and where, or why no update was needed.
Apply the repository's full pre-commit gate before committing.

Add focused regression coverage for the relevant failure modes: null collection elements,
unknown-union discriminator and field round trips, distinct-service routing in both
authentication states, actual argument exceptions, and logger/callback forwarding.
For streaming changes, verify lazy consumption, exports exceeding the normal response
limit, response disposal, HTTP errors and cancellation. Use existing test helpers and
avoid live writes or private-data fixtures.
Test null elements at every response collection level, including nested collections,
directly returned DTOs and verified/unverified sibling arrays. For each public overload
with new validation, test invalid bounds and collection entries where applicable and
compare the actual exception with both agent and server documentation.

Preserve AOT and trimming compliance in every fix, using source-generated serialization
and existing AOT-safe helpers. Do not disable AOT or trimming checks to make a build pass.

Only the following analyzer suppressions may be added without asking the user:

* `RS0027`, when retaining an existing shorter overload's optional parameters for compatibility.
* `BSKYUnspecced`, when intentionally exercising or implementing an unspecced endpoint.
* `CA2227`, only on a JSON extension-data collection property marked with
  `[JsonExtensionData]`, where the setter supports JSON deserialization.
* `IL2026` (trimming) and `IL3050` (AOT), matching existing narrowly scoped endpoint
  suppressions, only when the serializer options actually passed to the suppressed call
  preserve every required type through source-generated metadata. Verify the response,
  request and all reachable nested, union and closed generic types; copying an existing
  justification or registering only the top-level DTO is insufficient.

For each permitted `IL2026`/`IL3050` suppression, require regression tests that use the
same configured serializer options as the endpoint, with reflection-based serialization
disabled. Assert required type metadata resolves, and exercise representative serialization
or deserialization through the actual request/response path, including known union members
and repository envelopes where applicable. Link each suppression to the tests proving its
justification in the completion report. Raw-string and empty-body paths must be tested as
such rather than claiming DTO deserialization coverage. Runtime tests prove metadata
coverage, not full Native AOT compatibility: retain the AOT/trimming analyzer gate, run
existing relevant publish checks when available, and report any unverified publish coverage.

Keep these suppressions narrowly scoped and justified. For every other diagnostic, obtain
explicit user approval before adding or broadening a suppression, whether by attribute,
pragma, project setting or analyzer configuration. This includes `CA1068` and AOT or trimming
diagnostics outside the narrowly defined metadata-backed exception above. Prefer compliant
code or overloads instead. Always report every suppression
added, changed or removed, including the permitted diagnostic IDs.

Before declaring completion or committing, inventory suppression changes directly from
the complete task diff against its starting revision or PR base, including new/untracked
files and attributes moved onto new overloads. Cover `SuppressMessage`,
`UnconditionalSuppressMessage`, warning pragmas and analyzer configuration changes.
Do not infer this inventory from build output, recollection or a list of known diagnostics.
Reconcile every added or broadened suppression with the permitted exceptions or explicit
user approval, and reconcile the full inventory with the completion report and PR body.
Stop for approval if any non-permitted suppression remains unapproved. Do not claim that
no AOT/trimming suppressions were added merely because their pattern matches existing code.

### Evidence-backed completion gate

Before declaring an authorized fix complete or committing, review the entire task diff
against its starting revision, or the entire PR diff against its base when working on a PR.
Do not limit this check to the latest correction or commit. Account for new/untracked files
and changes introduced earlier in the task.

Reconcile each of the following with concrete evidence:

* Every response collection and nested collection has a recorded element-nullability
  decision, the corresponding sanitization or validation path, and null-element regression
  coverage where its elements are non-nullable. Include directly returned DTOs and every
  verified/unverified sibling collection; do not substitute one representative test.
* Every new or changed public agent and server overload documents the exceptions from
  its own guards and delegated calls. Compare overloads side by side and link applicable
  invalid-input tests, including boundary values and invalid collection entries.
* Every user-visible addition, behavior change and removal in the full diff is represented
  in the unreleased changelog under the affected package. Include corrected query
  serialization, enum/filter additions, optional parameters and expanded response APIs,
  not just new endpoints. Record an explicit rationale for any omission.
* Every changed streaming guarantee is reflected consistently in endpoint and shared
  property, option and callback documentation, with tests for the stated size-limit and
  buffering behavior.
* Every suppression change is reconciled with the complete inventory, approval policy,
  proving tests where required, completion report and PR description.

Keep this checklist and its evidence in session artifacts or the completion report;
do not create repository tracking files unless requested. A passing build, a green test
suite or a resolved review thread is not a substitute for these comparisons. Report
remaining gaps explicitly rather than marking the whole change complete.
