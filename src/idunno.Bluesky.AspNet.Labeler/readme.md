# idunno.Bluesky.AspNet.Labeler

## About

Host `com.atproto.label.queryLabels` and `com.atproto.label.subscribeLabels` without
Ozone.

## Key Features

* Public label queries and WebSocket subscriptions with persisted signatures
* Liveness and readiness health checks
* Opt-in remote service-token administration and protected local administration
* Optional Xenon reports, cases, appeals and versioned-policy administration
* Opt-in Ozone compatibility with a browser-origin allowlist
* OpenTelemetry metrics for distribution and optional moderation

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

Choose a shared label storage provider and initialize its database before
starting the host. This package publishes committed labels and distributes their
persisted signatures; it does not generate keys. Explicit `AddLabelerAdministration` /
`MapLabelerAdministration` opt into exact-audience service JWT authentication and shared team
authorization. `AddLabelerLocalAdministration` provides protected Windows pipe/Linux socket access
with explicit OS-to-team bindings, including root-only VPS setup.
`MapBlueskyLabeler()` also maps `/health/labeler/live` and `/health/labeler/ready`.
Readiness checks publication and durable storage. Hosting metrics use meter
`idunno.Bluesky.AspNet.Labeler`. Enable OpenTelemetry collection separately with
`AddBlueskyLabelerMetrics()` on `MeterProviderBuilder` (namespace
`OpenTelemetry.Metrics`), then configure your application's readers and exporter.

Explicit `AddLabelerModeration` / `MapLabelerModeration` opt into independent-labeler
report intake and shared Xenon case, appeal and versioned-policy administration.
Register moderation before administration and explicitly initialize its persisted
policy gate. The separate `/health/labeler/moderation` endpoint checks independent
declaration freshness. Use it for monitoring, not traffic routing: a closed policy
gate must leave public distribution and authenticated recovery reachable.
`/health/labeler/ready` continues to check only distribution readiness.
This does not enable remote CLI administration, add a moderation UI, or change the
distribution-only default.
`AddBlueskyLabelerMetrics()` also selects the optional
`idunno.Bluesky.AspNet.Labeler.Moderation` meter; selecting it does not enable Xenon.

## Related Packages

* [idunno.Bluesky.Labeler](https://www.nuget.org/packages/idunno.Bluesky.Labeler) provides shared issuance, signing and persistence contracts.
* [idunno.Bluesky.Labeler.SQLite](https://www.nuget.org/packages/idunno.Bluesky.Labeler.SQLite) provides SQLite storage.
* [idunno.Bluesky.Labeler.PostgreSQL](https://www.nuget.org/packages/idunno.Bluesky.Labeler.PostgreSQL) provides PostgreSQL storage.
* [idunno.Bluesky.Labeler.MySQL](https://www.nuget.org/packages/idunno.Bluesky.Labeler.MySQL) provides MySQL storage.
* [idunno.Bluesky.AspNet.Labeler.UI](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Labeler.UI) provides the optional Xenon web UI.
* [idunno.Bluesky.Labeler.Agent](https://www.nuget.org/packages/idunno.Bluesky.Labeler.Agent) provides a client for remote label issuance.

## Documentation

See the [hosting guide](https://bluesky.idunno.dev/docs/labeler/hosting.html) for
configuration, discovery, Ozone-compatible retained content, cursors and limits.
See [administration](https://bluesky.idunno.dev/docs/labeler/administration.html) for bootstrap,
native desktop OAuth, local channels, roles, revocation and audit, and
[Xenon moderation](https://bluesky.idunno.dev/docs/labeler/moderation.html) for optional moderation setup.
