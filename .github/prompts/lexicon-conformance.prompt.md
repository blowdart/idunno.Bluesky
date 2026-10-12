---
description: 'Audit SDK endpoint and model conformance against published AT Protocol and Bluesky lexicons.'
---

# Lexicon conformance audit

Audit this repository against the published lexicons at
https://github.com/bluesky-social/atproto/tree/main/lexicons.
Do not modify SDK code, tests, documentation or configuration unless explicitly asked.

## Scope and evidence

* Cover AT Protocol, Bluesky and chat endpoints and all reachable request, response,
  definition and repository-record types. Respect any narrower scope supplied by the user.
* Pin the upstream lexicon commit SHA and record the SDK commit and relevant uncommitted
  changes. Use one upstream snapshot throughout the audit and link findings to that snapshot.
* Inventory lexicons and SDK implementations in both directions. Distinguish missing
  endpoints, implemented endpoints, deprecated endpoints, intentional exclusions and
  SDK methods with no current published lexicon. Endpoint presence is not conformance.
* Trace each endpoint through its public agent method, server method, request construction,
  DTOs, converters, result mapping and source-generated JSON registrations.

## Static conformance

* Compare HTTP method, XRPC path, input and output encoding, authentication and service
  routing, query keys, repeated array parameters, escaping, Boolean and enum wire values,
  pagination, optional parameters, defaults, bounds and named errors.
* Compare every reachable model's JSON field names, required versus optional fields,
  nullability, types, formats, constraints, nested references, arrays, unions and discriminators.
  Distinguish wire mismatches from SDK conveniences and from omitted optional capabilities.
* Treat lexicon `knownValues` as open sets. Check unknown-value handling, custom converters
  and preservation of unknown union members rather than assuming closed enums.
* Verify source-generated metadata using the library's configured serializer options;
  reflection is disabled. Repository records need both concrete type and
  `AtProtoRepositoryRecord<TRecord>` registrations.
* AOT and trimming compliance are required, not optional. Check all affected serialization,
  converters and request paths for reflection, dynamic code and missing metadata. Respect
  the repository's target-framework rules: AOT and trimming are enabled for .NET 9 and later.
  A passing ordinary runtime test alone does not establish AOT compliance.
* For `BlueskyRecord` derivatives, check base-type discriminator dispatch and direct
  concrete serialization of the lexicon `$type`. These are separate requirements.
* Check that streaming binary responses are not buffered through the JSON response helper,
  that streams own their responses, and that server-reported metadata is treated as untrusted.

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
* Separate confirmed variances, missing optional capabilities, intentional differences,
  deprecated API support and unverified areas. Do not claim exhaustive coverage unless
  every scoped endpoint and reachable model has been accounted for.
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

Follow repository instructions, preserve existing public signatures with overloads,
keep cancellation tokens last, update public API tracking and source-generation metadata,
and add focused request and serialization regression tests. Update related endpoint status
documentation. Update `CHANGELOG.md` for every user-visible addition, change or removal
under the unreleased section and the affected package heading. The completion summary must
explicitly state whether the changelog was updated and where, or why no update was needed.
Apply the repository's full pre-commit gate before committing.

Preserve AOT and trimming compliance in every fix, using source-generated serialization
and existing AOT-safe helpers. Do not disable AOT or trimming checks to make a build pass.

Only the following analyzer suppressions may be added without asking the user:

* `RS0027`, when retaining an existing shorter overload's optional parameters for compatibility.
* `BSKYUnspecced`, when intentionally exercising or implementing an unspecced endpoint.

Keep these suppressions narrowly scoped and justified. For every other diagnostic, obtain
explicit user approval before adding or broadening a suppression, whether by attribute,
pragma, project setting or analyzer configuration. This includes `CA1068` and AOT or trimming
diagnostics. Prefer compliant code or overloads instead. Always report every suppression
added, changed or removed, including the two permitted diagnostic IDs.
