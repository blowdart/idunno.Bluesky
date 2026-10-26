# idunno.Bluesky.AspNet.Labeler

Host `com.atproto.label.queryLabels` and `com.atproto.label.subscribeLabels` without
Ozone. Choose a shared label storage provider and initialize its database before
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

See the [hosting guide](https://bluesky.idunno.dev/docs/labeller/hosting.html) for
configuration, discovery, Ozone-compatible retained content, cursors and limits.
See [administration](https://bluesky.idunno.dev/docs/labeller/administration.html) for bootstrap,
native desktop OAuth, local channels, roles, revocation and audit.

Explicit `AddLabelerModeration` / `MapLabelerModeration` opt into independent-labeler
report intake and shared Xenon case, appeal and versioned-policy administration.
Register moderation before administration and explicitly initialize its persisted
policy gate. The separate `/health/labeler/moderation` endpoint checks independent
declaration freshness. Use it for monitoring, not traffic routing: a closed policy
gate must leave public distribution and authenticated recovery reachable.
`/health/labeler/ready` continues to check only distribution readiness.
This does not enable remote CLI administration, add a moderation UI, or change the
distribution-only default. See [Xenon moderation](https://bluesky.idunno.dev/docs/labeller/moderation.html).
`AddBlueskyLabelerMetrics()` also selects the optional
`idunno.Bluesky.AspNet.Labeler.Moderation` meter; selecting it does not enable Xenon.
