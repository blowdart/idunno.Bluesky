## Overview

`idunno.Bluesky` is a .NET 8/9/10 SDK for [Bluesky](https://bsky.social/) and the underlying
[AT Protocol](https://docs.bsky.app/docs/api/at-protocol-xrpc-api). The solution ships several NuGet packages layered on top of each other; most work happens in `src/idunno.AtProto` (the AT Protocol/XRPC client) and `src/idunno.Bluesky` (the Bluesky-specific client built on top of it).

## Build, test, and lint

* Requires the .NET SDK pinned in `global.json` (SDK 10). The solution file is `idunno.Bluesky.slnx`.
* Build (also runs all code + documentation analyzers): `dotnet build` from the repository root.
* Run the full test suite: `dotnet test` from the repository root.
* Run a single test project: `dotnet test test/idunno.Bluesky.Test/idunno.Bluesky.Test.csproj`.
* Run a single test or class: `dotnet test --filter "FullyQualifiedName~PostBuilderTests"` (or `--filter "DisplayName~..."`).
* Tests multi-target `net8.0;net9.0;net10.0`. Restrict to one framework with `-f net10.0` to iterate faster.
* Linting is not a separate step: analyzers (SonarAnalyzer, PublicApiAnalyzers, documentation analyzers) run during `dotnet build`, and `<TreatWarningsAsErrors>` is on, so a clean build is the lint gate.

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
