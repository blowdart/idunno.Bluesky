# idunno.Bluesky.AspNet.Labeler

Host `com.atproto.label.queryLabels` and `com.atproto.label.subscribeLabels` without
Ozone. Choose a shared label storage provider and initialize its database before
starting the host. This package publishes committed labels and distributes their
persisted signatures; it does not generate keys or expose administrative APIs.
`MapBlueskyLabeler()` also maps `/health/labeler/live` and `/health/labeler/ready`.
Readiness checks publication and durable storage. Hosting metrics use meter
`idunno.Bluesky.AspNet.Labeler`. Enable OpenTelemetry collection separately with
`AddBlueskyLabelerMetrics()` on `MeterProviderBuilder` (namespace
`OpenTelemetry.Metrics`), then configure your application's readers and exporter.

See the [hosting guide](https://bluesky.idunno.dev/docs/labeller/hosting.html) for
configuration, discovery, Ozone-compatible retained content, cursors and limits.
