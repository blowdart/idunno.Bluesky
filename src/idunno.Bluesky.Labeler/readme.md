# idunno.Bluesky.Labeler

Host-neutral shared issuance, signed withdrawal and persistence contracts for AT Protocol labels.
Register exactly one SQLite, PostgreSQL or MySQL provider, an authorizer and a declaration/policy
provider. The application service verifies targets, enforces policy, reconciles decisions and commits
signed labels, audit and publication progress atomically. Provider-backed team roles, explicit OS
bindings, service-token replay protection and commit-valid authorization fences share that transaction
boundary. Transport adapters, publication workers and private-key custody remain separate concerns.

Start with the [labeller overview](https://bluesky.idunno.dev/docs/labeller/overview.html)
and [issuance guide](https://bluesky.idunno.dev/docs/labeller/issuance.html), including error causes,
retry rules and [administration setup](https://bluesky.idunno.dev/docs/labeller/administration.html)
required before exposing administrative endpoints.
