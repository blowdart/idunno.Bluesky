# idunno.Bluesky.Labeler

## About

Host-neutral shared issuance, signed withdrawal and persistence contracts for AT Protocol labels.

## Key Features

* Target verification and policy-backed label issuance and withdrawal
* Atomic persistence of signed labels, audit and publication progress
* Shared team roles, authorization and service-token replay protection
* Signing-key management and durable lifecycle coordination
* Optional Xenon reports, cases, appeals and policy history
* SQLite, PostgreSQL and MySQL storage providers in separate packages

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

Register exactly one SQLite, PostgreSQL or MySQL provider, an authorizer and a declaration/policy
provider. The application service verifies targets, enforces policy, reconciles decisions and commits
signed labels, audit and publication progress atomically. Provider-backed team roles, explicit OS
bindings, service-token replay protection and commit-valid authorization fences share that transaction
boundary. Transport adapters, publication workers and private-key custody remain separate concerns.

### Finding the APIs

Feature directories match their namespaces, with one type per file:

| Namespace | APIs |
|---|---|
| `idunno.Bluesky.Labeler` | Common targets, result wrappers and errors |
| `idunno.Bluesky.Labeler.Issuance` | Issuance service, policies, authorization and decision requests/receipts |
| `idunno.Bluesky.Labeler.Administration` | Team services, roles, permissions and administrative persistence |
| `idunno.Bluesky.Labeler.Maintenance` | Lifecycle phases, draining, host participation and durable coordination |
| `idunno.Bluesky.Labeler.Moderation` | Optional Xenon reports, exact-subject cases, workflow views, appeals and immutable policy/recovery history |
| `idunno.Bluesky.Labeler.Signing` | Key rings, durable selection, protected key files and re-signing |
| `idunno.Bluesky.Labeler.Storage` | Label persistence contracts, signed snapshots, pages and metrics |
| `idunno.Bluesky.Labeler.Identity` | Public labeler identity-document creation and verification |

Import the feature namespaces used by your application. Provider registration extensions such as
`AddSqliteLabelStorage` remain in the root namespace and in their existing provider packages.
These namespace changes affect the unreleased labeler APIs; existing consumers must update their
`using` directives and any fully qualified references.

## Related Packages

* [idunno.Bluesky.Labeler.SQLite](https://www.nuget.org/packages/idunno.Bluesky.Labeler.SQLite) provides SQLite storage.
* [idunno.Bluesky.Labeler.PostgreSQL](https://www.nuget.org/packages/idunno.Bluesky.Labeler.PostgreSQL) provides PostgreSQL storage.
* [idunno.Bluesky.Labeler.MySQL](https://www.nuget.org/packages/idunno.Bluesky.Labeler.MySQL) provides MySQL storage.
* [idunno.Bluesky.AspNet.Labeler](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Labeler) provides ASP.NET Core hosting and administration.
* [idunno.Bluesky.Labeler.Agent](https://www.nuget.org/packages/idunno.Bluesky.Labeler.Agent) provides a client for a separately hosted labeler.

## Documentation

Start with the [labeler overview](https://bluesky.idunno.dev/docs/labeler/overview.html)
and [issuance guide](https://bluesky.idunno.dev/docs/labeler/issuance.html), including error causes,
retry rules and [administration setup](https://bluesky.idunno.dev/docs/labeler/administration.html)
required before exposing administrative endpoints.

[Optional Xenon moderation](https://bluesky.idunno.dev/docs/labeler/moderation.html)
shares provider authorization, issuance and audit rather than requiring Ozone or
a second persistence system. Intake never automatically labels content. Explicit
case decisions atomically commit bounded label actions and appeal outcomes; manual
labeling remains independent of reports and cases.
