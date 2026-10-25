# idunno.Bluesky.Labeler

Host-neutral shared issuance, signed withdrawal and persistence contracts for AT Protocol labels.
Register exactly one SQLite, PostgreSQL or MySQL provider, an authorizer and a declaration/policy
provider. The application service verifies targets, enforces policy, reconciles decisions and commits
signed labels, audit and publication progress atomically. Provider-backed team roles, explicit OS
bindings, service-token replay protection and commit-valid authorization fences share that transaction
boundary. Transport adapters, publication workers and private-key custody remain separate concerns.

## Finding the APIs

Feature directories match their namespaces, with one type per file:

| Namespace | APIs |
|---|---|
| `idunno.Bluesky.Labeler` | Common targets, result wrappers and errors |
| `idunno.Bluesky.Labeler.Issuance` | Issuance service, policies, authorization and decision requests/receipts |
| `idunno.Bluesky.Labeler.Administration` | Team services, roles, permissions and administrative persistence |
| `idunno.Bluesky.Labeler.Maintenance` | Lifecycle phases, draining, host participation and durable coordination |
| `idunno.Bluesky.Labeler.Signing` | Key rings, durable selection, protected key files and re-signing |
| `idunno.Bluesky.Labeler.Storage` | Label persistence contracts, signed snapshots, pages and metrics |
| `idunno.Bluesky.Labeler.Identity` | Public labeler identity-document creation and verification |

Import the feature namespaces used by your application. Provider registration extensions such as
`AddSqliteLabelStorage` remain in the root namespace and in their existing provider packages.
These namespace changes affect the unreleased labeler APIs; existing consumers must update their
`using` directives and any fully qualified references.

Start with the [labeller overview](https://bluesky.idunno.dev/docs/labeller/overview.html)
and [issuance guide](https://bluesky.idunno.dev/docs/labeller/issuance.html), including error causes,
retry rules and [administration setup](https://bluesky.idunno.dev/docs/labeller/administration.html)
required before exposing administrative endpoints.
