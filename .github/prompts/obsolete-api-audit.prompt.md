---
description: 'Compare obsolete APIs on the current branch against main to inform major-version removal decisions and check guidance accuracy.'
---

# Major-version obsolete API audit

Compare the current branch against `main` and prepare a report that helps the user
evaluate which obsolete functions, types and other members could be removed in a
new major version. Scan both revisions for `[Obsolete]` attributes, including
qualified names and `ObsoleteAttribute`.
Do not modify files, remove APIs, or commit changes.

## Branch comparison

* Identify the current branch and commit. Refresh `origin/main` when possible and
  record the exact main commit used. If fetching is unavailable, state which local
  main ref was used and that its freshness is unverified.
* Inspect main through Git objects without switching branches or accessing another
  checkout. Preserve the current worktree and all uncommitted changes.
* Treat the current branch's committed HEAD as the comparison revision. Inspect
  relevant uncommitted changes too, but report their effects separately so a local
  fix is not mistaken for a committed branch change.
* Inventory obsolete declarations on both revisions and match by fully qualified
  symbol and overload signature, not just file path or attribute text. Account for
  renamed or moved declarations and members of obsolete types.
* Prioritize APIs already obsolete on main that still exist on the current branch.
  Separately identify APIs removed since main, APIs whose obsolete attribute was
  removed, and APIs newly marked obsolete or newly introduced as obsolete on the
  current branch. Presence and obsolete status are separate facts.
* Do not treat a deprecation newly introduced on the current branch as though
  consumers of main already received its migration warning. Explain that distinction
  when presenting removal options, without deciding removal policy for the user.

## Removal scope

* Identify obsolete SDK conveniences, compatibility wrappers and unsupported options
  that can be removed in a major version.
* Exclude APIs that align with Bluesky or AT Protocol endpoints, wire models or
  protocol streams from removal candidates. These must remain for compatibility,
  including obsolete Firehose fields, Jetstream classes and Bluesky chat models.
* Exclude lexicon-sourced deprecations from removal candidates. An obsolete
  attribute reflecting an upstream lexicon's deprecation is protocol guidance,
  not an SDK removal decision, even when it was already present on main.
  Verify the source before classifying it; retain the API for compatibility.
* Ignore obsolete constructors accepting `ISystemClock` on authentication handlers.
  These are a common authentication-handler compatibility pattern; do not report
  them as removal candidates or accuracy findings, including their retention
  suppressions. Include them only in raw obsolete-attribute counts.
* Distinguish public library APIs from internal implementation details and samples.
  An obsolete sample entry point is not a reason to remove the sample.
* Do not assume an obsolete API is safe to delete. Inspect its implementation,
  callers, inheritance, serialization registrations and relevant tests.
* Identify directly coupled cleanup, such as legacy conversions, argument guards,
  source-generated metadata, public API tracking and migration documentation.

## Accuracy checks for every obsolete API

* Check obsolete messages and related documentation regardless of whether the API
  is eligible for removal, except for the explicitly ignored authentication-handler
  `ISystemClock` constructors. Protocol compatibility exclusions do not exclude an
  API from accuracy checks or prevent reporting incorrect guidance.
* Verify that each named replacement exists, using its exact namespace, type or
  method name and applicable overload. Search for renamed types and endpoint
  methods before concluding that no replacement exists.
* Verify the replacement's behavior and intended use, not just its name. Compare
  parameters, result shapes, wire values, service routing and serialization where
  relevant. Do not describe an API for another endpoint as a drop-in replacement.
* Distinguish a replacement type from a migration to a different endpoint or
  workflow. Report semantic differences and compatibility requirements.
* Distinguish lexicon-sourced deprecation and replacement guidance from wording
  added by the SDK. Do not flag accurate upstream deprecation guidance merely
  because the SDK retains the API. If the SDK adds an unsupported removal promise
  or incorrect replacement name, report only that wording as an accuracy issue,
  not the API as a removal candidate.
* For example, the `ChatPreference` notice must not recommend a nonexistent
  `ChatNotificationPreferences` type. Inspect `Chat.Notifications.Preference`,
  `Chat.Notifications.Preferences` and the chat-notification methods to determine
  accurate migration guidance without removing the legacy wire model.
* Check claims that protocol fields are deprecated or always have particular
  values against available evidence. When upstream verification is necessary,
  use a pinned lexicon or protocol snapshot and cite it. Mark unsupported claims
  as unverified rather than repeating them as facts.
* Check removal promises against the requirement to retain protocol compatibility.
  Flag misleading notices even when their named replacement exists.
* Treat analyzer suppressions as explanations to investigate, not proof that an
  obsolete member must remain, subject to the authentication-handler constructor
  exclusion above.

## Report

* State the compared branches and commit SHAs, the number of obsolete attributes
  on each revision, and whether relevant uncommitted changes affect the findings.
* Present eligible APIs already obsolete on main and still present on the current
  branch in a comparison table. Include the exact API, main/current obsolete
  status, current repository-relative file and line, replacement, removal rationale,
  compatibility risks and required coupled cleanup.
* Separately summarize eligible APIs removed since main, obsolete attributes
  removed since main, and newly obsolete APIs on the current branch. Do not imply
  that a removed attribute means the API itself was removed.
* Provide evidence and tradeoffs to support the user's removal decisions; do not
  equate an obsolete attribute with permission or a requirement to delete an API.
* Separately list accuracy issues, including issues on retained protocol APIs,
  with evidence and the recommended correction.
* Explain genuine blockers or compatibility reasons for retaining SDK-level APIs.
  Do not list excluded protocol APIs as removal candidates.
* Distinguish confirmed findings from uncertain or unverified conclusions.
* Confirm that no files were changed. Return the report in the response rather
  than creating a report file unless explicitly requested.
