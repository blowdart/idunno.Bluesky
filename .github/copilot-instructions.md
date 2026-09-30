## Overview

`idunno.Bluesky` is a .NET 8/9/10 SDK for [Bluesky](https://bsky.social/) and the underlying
[AT Protocol](https://endpoints.bsky.app/#bluesky-app/description/introduction). The solution ships several NuGet packages layered on top of each other;
most work happens in `src/idunno.AtProto` (the AT Protocol/XRPC client) and `src/idunno.Bluesky` (the Bluesky-specific client built on top of it).
DTOs are built from the AtProto and Bluesky [lexicon]https://github.com/bluesky-social/atproto/tree/main/lexicons.

## Build, test, and lint

* Requires the .NET SDK pinned in `global.json` (SDK 10). The solution file is `idunno.Bluesky.slnx`.
* Build (also runs all code + documentation analyzers): `dotnet build` from the repository root.
* Run the full test suite: `dotnet test` from the repository root.
* Run a single test project: `dotnet test test/idunno.Bluesky.Test/idunno.Bluesky.Test.csproj`.
* Run a single test or class: `dotnet test --filter "FullyQualifiedName~PostBuilderTests"` (or `--filter "DisplayName~..."`).
* Tests multi-target `net8.0;net9.0;net10.0`. Restrict to one framework with `-f net10.0` to iterate faster.
* Linting is not a separate step: analyzers (SonarAnalyzer, PublicApiAnalyzers, documentation analyzers) run during `dotnet build`, and `<TreatWarningsAsErrors>` is on, so a clean build is the lint gate.
* **CI builds `Debug`, not `Release`.** Some analyzers, notably the documentation analyzers (`CSENSE*`), only run in `Debug`, so a clean `Release` build proves nothing about CI.
* **Never trust an incremental build as a pre-commit gate.** If you have just built an individual project, a subsequent solution build reuses that result and silently skips its analyzers. Pass `--no-incremental`.

### GitHub Actions failed-test protocol

When a GitHub Actions workflow fails, especially its test stage:

1. Do not rerun, rerun failed jobs, or dispatch the workflow until the original failure has been fully diagnosed or the user explicitly authorizes a rerun.
2. Treat the original workflow attempt as perishable evidence. Before any rerun, record the workflow run ID, job IDs, attempt number, commit SHA, and failing check names. Retrieve the failed job logs with `gh run view <run-id> --log-failed`, inspect check-run annotations and summaries with `gh api`, and list and download all workflow artifacts, especially test-result, JUnit, TRX, coverage, and diagnostic artifacts. Save relevant output locally if GitHub only exposes it transiently.
3. Identify and record each failing test, target framework, test project, exception, and stack trace before taking recovery action.
4. If logs are unavailable because the run is still in progress, wait for the run to finalize. Do not rerun merely because one job is failed while another job remains in progress.
5. Only after preserving the evidence may a failed job be rerun. Report clearly that a rerun tests for flakiness but does not diagnose or fix the original failure.
6. If the original test name or failure details cannot be recovered, state that explicitly. Do not infer the failing test from aggregate counts or claim that a green rerun fixes the issue.
7. A PR may be merged only after the original failure has been diagnosed or explicitly accepted as an unresolvable infrastructure/flaky failure, the required checks pass on the final commit, and any required CodeQL/Copilot review conditions are satisfied.

Never use `gh run rerun --failed` as the first response to a failed CI test job. Preserve the original logs, annotations, artifacts, and failing-test identity first. A rerun can overwrite or obscure the diagnostic context.

## Requesting Copilot code review

To request Copilot code review on a pull request, run `gh pr edit <number> --repo <owner/repo> --add-reviewer '@copilot'`, then verify with `gh pr view <number> --repo <owner/repo> --json reviewRequests`. Do not claim success unless Copilot is present in `reviewRequests`. If it is not present, report that the request could not be verified and use the PR page's reviewer UI or explain that Copilot code review may be disabled or unavailable for the repository. Do not use `gh pr review` to request Copilot: it submits a review as the authenticated user instead.

## Pre-commit gate

Before *every* commit, run both of these from the repository root and require both to be clean:

1. `dotnet build idunno.Bluesky.slnx -c Debug --no-incremental` — must report 0 warnings and 0 errors.
2. `dotnet test` — every test in every test project must pass.

Do not commit on the strength of a single project build, a `Release` build, an incremental build, or a filtered test run. Those are for iterating; this gate is what CI actually checks. If either step fails, fix it before committing rather than pushing and waiting for CI to tell you.

## Architecture

The SDK is layered; understanding the layers requires reading across `Agent`, `*Server`, and result types:

* **Two-layer API per protocol.** Each protocol has a low-level static `*Server` class (`AtProtoServer`, `BlueskyServer`) that wraps raw XRPC endpoints, and a stateful `*Agent` class (`AtProtoAgent`, `BlueskyAgent`) that layers session management, authentication, and background token refresh on top. `BlueskyAgent : AtProtoAgent : Agent`. Application code normally uses an agent.
* **Result wrapper, not exceptions, for HTTP outcomes.** API methods return `AtProtoHttpResult<T>`. Check `.Succeeded` and read `.Result`; HTTP/AT errors surface via the result (`.StatusCode`, `.AtErrorDetail`), not thrown exceptions. Exceptions are reserved for programming errors and unrecoverable states.
* **Feature-partial classes.** Both agents and servers are `partial` classes split into files/folders by feature area (`Repo`, `Server`, `Actions`, `Feed`, `Graph`, `Actor`, `Notifications`, `Chat`, `Bookmarks`, `Moderation`, etc.). Add a new endpoint by extending the relevant partial in its feature folder, not by growing one file.
* **Source-generated JSON.** Serialization is AOT/trimming-safe: `JsonSerializerIsReflectionEnabledByDefault` is `false` and types are registered in a `SourceGenerationContext` (`JsonSerializerContext`). New serializable types must be added to the appropriate context; do not rely on reflection-based `System.Text.Json`.
* **Packages.** `idunno.AtProto.Types` (base types) → `idunno.AtProto` (protocol client, Jetstream, DID/handle resolution) → `idunno.Bluesky` (Bluesky client). `idunno.AtProto.OAuthCallback` provides a local OAuth callback server; `idunno.Bluesky.AspNet.Authentication` provides ASP.NET auth handlers. Samples live in `samples/` (mostly console apps sharing `--handle`/`--password`/`--authcode` args and `Samples.Common`).

## Key conventions

* **Public API tracking.** Public surface is tracked by the Roslyn `PublicApiAnalyzers` in `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` per project. When you add, change, or remove a public member the build will fail until you update `PublicAPI.Unshipped.txt` accordingly.
* **Update the changelog.** Record user-visible additions/changes/removals under the unreleased section of `CHANGELOG.md`, grouped by package (`### idunno.AtProto`, `### idunno.Bluesky`, ...).
* **Targeting & trimming.** Trimming/AOT (`IsTrimmable`, `IsAotCompatible`) is enabled for net9.0+ only (net8.0 is excluded). Keep new code trimming-safe.
* **Strong naming / InternalsVisibleTo.** Assemblies are strong-named (`key.snk`). Internal members are exposed to matching test projects via `InternalsVisibleTo` in the csproj, so internal types are testable.
* **Spelling.** A spell-check analyzer runs during build; add legitimate domain words to the shared `exclusion.dic` at the repo root rather than suppressing warnings inline.
* **Do not edit build/config infrastructure** (`.editorconfig`, `.gitignore`, `global.json`, `Directory.Build.props`/`.targets`, `Directory.Packages.props`, `nuget.config`) unless explicitly asked. Dependency changes are maintainers-only.

## C# style

* Target the latest C# language version (currently C# 13); do not use preview language features.
* Apply the formatting in `.editorconfig`. Use file-scoped namespaces and single-line `using` directives.
* Put the opening brace of a block on its own new line; keep a method's final `return` on its own line.
* Prefer pattern matching and switch expressions. Use `nameof` instead of string literals for member names.
* Use `?.` where applicable (e.g. `scope?.Dispose()`) and `ObjectDisposedException.ThrowIf` for disposal guards.
* Every public API needs XML doc comments following the guidance in [`docs.prompt.md`](/.github/prompts/docs.prompt.md).

### Nullable reference types

* Nullable is enabled repo-wide. Declare variables non-nullable and validate `null` at entry points.
* Use `is null` / `is not null`, never `== null` / `!= null`.
* Trust the null annotations — don't add null checks the type system says are unnecessary.

## Testing conventions

* Tests use the xUnit v3 SDK (`xunit.v3`).
* Don't emit `// Arrange` / `// Act` / `// Assert` comments. Match the method naming and casing of nearby tests.
* Prefer a single `[Theory]` with `[InlineData]`/`[MemberData]` over many near-duplicate `[Fact]` methods.
* Test projects are split by purpose: `*.Test` (unit), `*.Serialization.Test` (JSON (de)serialization, often against captured responses), and `*.Integration.Test` (uses `TestServerBuilder` to stand up a mock server).
* Any code you commit must build cleanly and keep related tests passing. Actually run the build and the affected tests to confirm — don't assume a fix works.

## Checkin conventions

* Run the pre-commit gate above (full `Debug` `--no-incremental` solution build plus the whole test suite) and require it to be clean before committing. Never commit on the strength of a partial or incremental build.
* **Branch naming.** When the branch-creation mechanism supports slash-separated names, name new work branches `wip/{github-username}/{short-kebab-case-subject}` (for example, `wip/blowdart/add-feed-filter`). Determine the username from the authenticated GitHub account; do not guess it. Keep the subject concise and descriptive. If the mechanism requires a different format, follow its constraints; do not use raw Git commands or other workarounds to force the requested format.
* Do not make verbose commit messages. Use the imperative mood and keep it short (e.g., "Add X", "Fix Y", "Update Z").
* Never commit directly to `main`. Use a feature branch and open a pull request. The PR description should summarize the change, link to any relevant issues, and note any breaking changes.
* Never create unsigned commits. All commits must be signed with a GPG key or SSH key.
* If commit signing fails because the configured signer (including 1Password) requires interaction or returns an error, stop and report the error. Do not retry, change signing methods or configuration, or delegate a retry. Wait for explicit user instructions in the active session; if the user instructs you to try again, resume the signing attempt. Do not work around signing failures or commit unsigned.
