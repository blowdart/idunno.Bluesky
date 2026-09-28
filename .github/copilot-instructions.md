## Overview

`idunno.Bluesky` is a .NET 8/9/10 SDK for [Bluesky](https://bsky.social/) and the underlying
[AT Protocol](https://endpoints.bsky.app/#bluesky-app/description/introduction). The solution ships several NuGet packages layered on top of each other;
most work happens in `src/idunno.AtProto` (the AT Protocol/XRPC client) and `src/idunno.Bluesky` (the Bluesky-specific client built on top of it).
DTOs are built from the AtProto and Bluesky [lexicon]https://github.com/bluesky-social/atproto/tree/main/lexicons.

## Build, test, and lint

* Requires the .NET SDK pinned in `global.json` (SDK 10). The solution file is `idunno.Bluesky.slnx`.
* Build (also runs all code + documentation analyzers): `dotnet build` from the repository root.
* Tests run on **Microsoft.Testing.Platform (MTP)**, selected by `global.json`, with xUnit v3. Use the [MTP `dotnet test` options](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp) and the test application's `dotnet test --help` for framework-specific filters; do not assume VSTest option formats or interpret MTP exit code 5 as zero tests.
* Run the full test suite: `dotnet test` from the repository root.
* Run a single test project: `dotnet test --project test/idunno.Bluesky.Test/idunno.Bluesky.Test.csproj`.
* Run a single test class: `dotnet test --project test/idunno.Bluesky.Test/idunno.Bluesky.Test.csproj --filter-class idunno.Bluesky.Test.PostBuilderTests`. Use `--filter-method` with a fully qualified method name for a single test.
* Tests multi-target `net8.0;net9.0;net10.0`. Restrict to one framework with `-f net10.0` to iterate faster.
* Linting is not a separate step: analyzers (SonarAnalyzer, PublicApiAnalyzers, documentation analyzers) run during `dotnet build`, and `<TreatWarningsAsErrors>` is on, so a clean build is the lint gate.
* **CI builds `Debug`, not `Release`.** Some analyzers, notably the documentation analyzers (`CSENSE*`), only run in `Debug`, so a clean `Release` build proves nothing about CI.
* **Never trust an incremental build as a pre-commit gate.** If you have just built an individual project, a subsequent solution build reuses that result and silently skips its analyzers. Pass `--no-incremental`.

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

### Adding XRPC endpoints

* **Directory layout.** Group endpoints by lexicon namespace in a feature directory with three subfolders:
  `<Feature>/Server/<Method>.cs` (partial `AtProtoServer`), `<Feature>/Agent/<Method>.cs` (partial `AtProtoAgent`), and
  `<Feature>/Defs/` for lexicon types. For example, `com.atproto.sync.getHostStatus` lives in `Sync/Server/GetHostStatus.cs`,
  `Sync/Agent/GetHostStatus.cs` and `Sync/Defs/HostDescription.cs`. Server and agent partials stay in the `idunno.AtProto`
  namespace; Defs types go in `idunno.AtProto.<Feature>`, with `#pragma warning disable IDE0130` around the namespace declaration.
* **Inline endpoint paths.** Write the XRPC path directly in the `endpoint` argument or request URI, for example
  `$"/xrpc/com.atproto.sync.getLatestCommit?did={Uri.EscapeDataString(did.Value)}"`. Do not add `const` endpoint fields.
* **Follow the lexicon.** Build parameters, outputs and errors from the lexicon JSON. List the lexicon's named errors in the
  method's `<remarks>`. Reuse an existing type when its shape matches the lexicon output (e.g. `Repo.Commit` for `{ cid, rev }`)
  rather than creating a duplicate.
* **Open string enums.** A lexicon string with `knownValues` becomes a C# enum with a trailing `Unknown` member and a hand-written
  `JsonConverter` that maps unrecognised values to `Unknown` (see `AccountStatus` / `HostStatus`). Do not rely on
  `JsonStringEnumConverter`.
* **Serialization registration.** Register every new response or Defs type in `SourceGenerationContext`.
* **Binary and large responses.** `AtProtoHttpClient<T>` buffers responses and caps them at `MaximumResponseSize`, so do not use
  it for CAR files or blobs. Use the shared streaming helper in `Sync/Server/SyncStreamRequest.cs`, which returns a stream that owns,
  and disposes, the `HttpResponseMessage`. When a caller needs response metadata, return a disposable wrapper type (e.g. `BlobContent`).
* **Untrusted server data.** Values a server reports about content, such as `Content-Type` and `Content-Length`, are untrusted.
  Say so in the XML docs, and do not use them for security decisions.
* **Repo-scoped agent methods.** Agent methods that take a repository accept an `AtIdentifier`, then resolve the DID and PDS
  (see `ResolveSyncRepo`) before calling the server method.
* **Endpoint status docs.** When you implement, rename or remove an endpoint, update `docs/docs/endpointStatus.md`. Add or
  change the row in the matching protocol table and group (e.g. **Sync** under AT Protocol Endpoints), keeping rows in alphabetical
  order by endpoint. Link the endpoint to its lexicon JSON, and name the public agent method in the Class / Method column.

## Key conventions

* **Text file encoding.** Create new text files as UTF-8 without a byte-order mark (BOM), using CRLF line endings.
* **Public API tracking.** Public surface is tracked by the Roslyn `PublicApiAnalyzers` in `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` per project. When you add, change, or remove a public member the build will fail until you update `PublicAPI.Unshipped.txt` accordingly.
  Public `record` types also generate `Equals`, `GetHashCode`, `ToString`, `<Clone>$`, `==` and `!=`. All of these must be listed
  in `PublicAPI.Unshipped.txt`, alongside the properties and their `init` accessors.
* **Add new doc files to the solution.** When you create a documentation file under `docs/`, add a `<File Path="..." />` entry for it to the matching `<Folder>` in `idunno.Bluesky.slnx`, keeping entries in alphabetical order, and add it to the relevant `toc.yml`.
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
* Prefer primary constructors where possible (e.g. `PagedCidCollection(IList<Cid> list, string? cursor = null) : ReadOnlyCollection<Cid>(list)`),
  and positional records for internal response models (e.g. `internal sealed record ListReposResponse([property: JsonRequired] List<HostedRepository> Repos, string? Cursor);`).
  Keep an explicit constructor when it needs different accessibility from the type (such as an `internal` constructor on a public type) or validates its arguments.
* Every public API needs XML doc comments following the guidance in [`docs.instructions.md`](instructions/docs.instructions.md), which is applied automatically to `*.cs` files.

### Nullable reference types

* Nullable is enabled repo-wide. Declare variables non-nullable and validate `null` at entry points.
* Use `is null` / `is not null`, never `== null` / `!= null`.
* Trust the null annotations — don't add null checks the type system says are unnecessary.

## Testing conventions

* Tests use the xUnit v3 SDK (`xunit.v3`).
* Don't emit `// Arrange` / `// Act` / `// Assert` comments. Match the method naming and casing of nearby tests.
* Prefer a single `[Theory]` with `[InlineData]`/`[MemberData]` over many near-duplicate `[Fact]` methods.
* Test projects are split by purpose: `*.Test` (unit), `*.Serialization.Test` (JSON (de)serialization, often against captured responses), and `*.Integration.Test` (uses `TestServerBuilder` to stand up a mock server).
* Add endpoint tests to the matching `*.Integration.Test` project, using `TestServerBuilder`. Assert the request path and query
  parameters, a successful response, and each lexicon error surfacing through `AtErrorDetail`. A test that asserts on
  `Content-Length` must set it explicitly, because `TestServer` does not send it by default.
* Any code you commit must build cleanly and keep related tests passing. Actually run the build and the affected tests to confirm — don't assume a fix works.

## Checkin conventions

* Run the pre-commit gate above (full `Debug` `--no-incremental` solution build plus the whole test suite) and require it to be clean before committing. Never commit on the strength of a partial or incremental build.
* Do not make verbose commit messages. Use the imperative mood and keep it short (e.g., "Add X", "Fix Y", "Update Z").
* Never commit directly to `main`. Use a feature branch and open a pull request. The PR description should summarize the change, link to any relevant issues, and note any breaking changes.
* Never create unsigned commits. All commits must be signed with a GPG key or SSH key.
* Do not attempt to work around signing failures, report them, and ask if you should retry the commit. If you cannot sign commits, do not commit until the issue is resolved.
