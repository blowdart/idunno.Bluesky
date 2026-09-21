# Built-in metrics in idunno.AtProto

This is a reference for metrics built-in for .NET, produced using the [System.Diagnostics.Metrics](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metric) API.

> [!TIP]
> For more information about how to collect and report these metrics, see the .NET documentation
> [Collecting metrics](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-collection).
>
> During development you can use the [dotnet-counters](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters) tool to
> observe these metrics in real time. e.g.
>
> ```c#
> dotnet-counters monitor --process-id <pid> --counters idunno.AtProto.AtProtoHttpClient
> ```
>
> For production environments, you can use a variety of exporters to send these metrics to monitoring systems
> like Aspire, Prometheus, Grafana, or Azure Monitor.

## idunno.AtProto.AtProtoHttpClient

The `idunno.AtProto.AtProtoHttpClient` Meter reports measures from the `idunno.AtProto.AtProtoHttpClient`.

### Metric: requests.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total` | Counter&lt;long&gt; | {requests} | Total number of requests made by an instance of `idunno.AtProto.AtProtoHttpClient`.|

### Metric: responses.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `responses.total` | Counter&lt;long&gt; | {responses} | Total number of responses received by an instance of `idunno.AtProto.AtProtoHttpClient`. |

### Metric: requests.total.successful

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total.successful` | Counter&lt;long&gt; | {requests} | Total number of successful requests made by an instance of `idunno.AtProto.AtProtoHttpClient`. |

### Metric: requests.total.failure

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total.failure` | Counter&lt;long&gt; | {requests} | Total number of failed requests made by an instance of `idunno.AtProto.AtProtoHttpClient`. |

### Metric: requests.total.dpop_retry

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total.dpop_retry` | Counter&lt;long&gt; | {requests} | Total number of requests retried due to DPoP nonce rotation or other DPoP related issues by an instance of the `idunno.AtProto.AtProtoHttpClient`. |

### Metric: responses.total.deserialization_failure

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `responses.total.deserialization_failure` | Counter&lt;long&gt; | {requests} | Total number of responses that could not be deserialized from JSON by an instance of the `idunno.AtProto.AtProtoHttpClient`. |

### Metric: request.duration

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `request.duration` | Histogram&lt;double&gt; | s | Duration of individual requests made by an instance of the `idunno.AtProto.AtProtoHttpClient`. |

### Metric: requests.total.xrpc_request

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total.xrpc_request` | Counter&lt;long&gt; | {requests} | Total number of xRPC requests made by an instance of the `idunno.AtProto.AtProtoHttpClient`, tagged with the xrpc_endpoint. |

## idunno.AtProto.Jetstream

The `idunno.AtProto.Jetstream` Meter reports measures from the `idunno.AtProto.Jetstream.AtProtoJetstream` client.

### Metric: total.messages

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.messages` | Counter&lt;long&gt; | {messages} | Total number of messages received from the JetStream by a `AtProtoJetstream` instance. |

### Metric: total.message_parsing_failures

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.message_parsing_failures` | Counter&lt;long&gt; | {messages} | Total number of messages that failed to parse after receipt by a `AtProtoJetstream` instance. |

### Metric: total.message_decompression_failures

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.message_decompression_failures` | Counter&lt;long&gt; | {messages} | Total number of messages that failed to decompress after receipt by a `AtProtoJetstream` instance. |

### Metric: total.events_parsed

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.events_parsed` | Counter&lt;long&gt; | {events} | Total number of events parsed from received messages by a `AtProtoJetstream` instance. |

### Metric: total.unknown_events

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.unknown_events` | Counter&lt;long&gt; | {events} | Total number of events with unknown type received in messages by a `AtProtoJetstream` instance. |

### Metric: total.faults

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.faults` | Counter&lt;long&gt; | {faults} | Total number of WebSocket faults that occurred by a `AtProtoJetstream` instance. |

### Metric: total.connections_opened

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.connections_opened` | Counter&lt;long&gt; | {connections} | Total number of WebSocket connections to the JetStream opened by a `AtProtoJetstream` instance. |

### Metric: total.connections_closed

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.connections_closed` | Counter&lt;long&gt; | {connections} | Total number of WebSocket connections to the JetStream closed by a `AtProtoJetstream` instance. |

### Metric: total.connections_failed

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.connections_failed` | Counter&lt;long&gt; | {connections} | Total number of WebSocket connections to the JetStream that failed by a `AtProtoJetstream` instance. |

## idunno.AtProto.Directory

The `idunno.AtProto.Directory` Meter reports measures from the `idunno.DidPlcDirectory` service.

### Metric: idunno.atproto.directory.requests.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total` | Counter&lt;long&gt; | {requests} | Total number of requests made for DID documents.|

### Metric: idunno.atproto.directory.requests.total.failed

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total.failed` | Counter&lt;long&gt; | {requests} | Total number of requests made for DID documents that failed. |

### Metric: idunno.atproto.directory.requests.total.succeeded

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total.succeeded` | Counter&lt;long&gt; | {requests} | Total number of requests made for DID documents that succeeded. |

### Metric: idunno.atproto.directory.request.duration

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `request.duration` | Histogram&lt;double&gt; | s | Duration of individual requests made for DID documents. |

## idunno.Bluesky.AspNet.Authentication

The `idunno.Bluesky.AspNet.Authentication` Meter reports measures from the `idunno.Bluesky.AspNet.Authentication` components,
the authentication handler, the sign-in manager, the profile claims transformer and the identity stores.

All instrument names are prefixed with `idunno.bluesky.aspnet.authentication.`, which is omitted from the tables below for brevity.

### Metric: idunno.bluesky.aspnet.authentication.authentications.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `authentications.total` | Counter&lt;long&gt; | {authentications} | Total authentication attempts against a request carrying an authentication cookie. Requests with no authentication cookie are not counted, as they are not authentication attempts. |

Tagged with `result`, whose value is `success` when the request authenticated, or one of the following when it did not.

| `result` | Meaning |
| --- | --- |
| `unprotect_ticket_failed` | The authentication cookie could not be unprotected. |
| `did_missing_in_cookie` | The ticket carried no DID claim. |
| `invalid_did_in_cookie` | The ticket carried a DID claim which is not a valid DID. |
| `identity_missing_in_store` | The identity store no longer holds the identity the ticket points at. |
| `ticket_expired` | The ticket expired and could not be renewed. |
| `no_principal` | The ticket carried no principal. |
| `token_refresh_failed` | The access token needed refreshing and the refresh failed. |
| `identity_missing_after_refresh` | The identity vanished from the store while its token was being refreshed. |
| `token_refresh_wait_expired` | The request gave up waiting for another request to finish refreshing the token. |
| `request_cancelled` | The request was cancelled during authentication. |
| `failure` | A catch-all for any other failure, including results substituted by an application event handler. |

### Metric: idunno.bluesky.aspnet.authentication.signins.total.successful

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `signins.total.successful` | Counter&lt;long&gt; | {signins} | Total successful sign-ins. |

### Metric: idunno.bluesky.aspnet.authentication.signins.total.failure

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `signins.total.failure` | Counter&lt;long&gt; | {signins} | Total failed sign-ins. |

Tagged with `reason`, one of `NoQueryString`, `NoCorrelationState` or `OAuth2StateFailure`.

### Metric: idunno.bluesky.aspnet.authentication.signouts.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `signouts.total` | Counter&lt;long&gt; | {signouts} | Total sign-outs. |

### Metric: idunno.bluesky.aspnet.authentication.credentialrevocations.failures.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `credentialrevocations.failures.total` | Counter&lt;long&gt; | {failures} | Total failures revoking credentials at the PDS during sign-out. A non-zero value means tokens remain live at the PDS after the local session ended. |

### Metric: idunno.bluesky.aspnet.authentication.correlationstate.rejections.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `correlationstate.rejections.total` | Counter&lt;long&gt; | {rejections} | Total OAuth correlation states rejected when processing a login callback. |

Tagged with `reason`.

| `reason` | Meaning |
| --- | --- |
| `missing_cookie` | The callback carried no correlation cookie, so it could not be tied to a login this application started. |
| `expired_cookie` | The correlation cookie was readable but had passed the expiry it was written with. |
| `unprotect_failed` | The correlation cookie could not be unprotected. This also counts against `dataprotection.failures.total`. |
| `malformed_cookie` | The correlation cookie was unprotected successfully but its contents could not be parsed. |
| `state_not_found` | The cookie was readable but the login state it referred to was not in the state cache, either because it expired or because it had already been consumed. |

### Metric: idunno.bluesky.aspnet.authentication.tokenrefreshes.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `tokenrefreshes.total` | Counter&lt;long&gt; | {refreshes} | Total access tokens refreshed. |

Tagged with `outcome`, either `self` when the request performed the refresh itself, or `concurrent` when it picked up a token refreshed by another request.

### Metric: idunno.bluesky.aspnet.authentication.tokenrefreshfailures.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `tokenrefreshfailures.total` | Counter&lt;long&gt; | {refreshes} | Total access tokens refresh failures. |

### Metric: idunno.bluesky.aspnet.authentication.tokenrefreshwaits.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `tokenrefreshwaits.total` | Counter&lt;long&gt; | {waits} | Total waits during token refresh as another refresh is in progress. |

Tagged with `reason`, either `refresh_in_progress` for the ordinary case where the request queued behind a refresh it could see was running,
or `lock_denied` where the request tried to take the refresh lock and lost the race. A rising proportion of `lock_denied` indicates lock contention
rather than ordinary queueing.

### Metric: idunno.bluesky.aspnet.authentication.tokenrefreshwaits.duration

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `tokenrefreshwaits.duration` | Histogram&lt;double&gt; | s | Duration of waits during token refresh as another refresh is in progress. |

### Metric: idunno.bluesky.aspnet.authentication.identitystore.operations.duration

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `identitystore.operations.duration` | Histogram&lt;double&gt; | s | Duration of identity store operations. Every authenticated request reads from the identity store, so this is on the hot path for every request. |

Tagged with `operation`, one of `add`, `get`, `remove` or `update`.

### Metric: idunno.bluesky.aspnet.authentication.identitystore.misses.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `identitystore.misses.total` | Counter&lt;long&gt; | {misses} | Total identities which were not in the identity store when a request needed them. Each miss signs a user out. |

Tagged with `phase`, either `authentication` when the identity was missing as the authentication cookie was read,
or `token_refresh` when it disappeared while its access token was being refreshed.

### Metric: idunno.bluesky.aspnet.authentication.identitystore.writefailures.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `identitystore.writefailures.total` | Counter&lt;long&gt; | {failures} | Total identities which could not be written to the identity store. A write failure leaves the store without an identity the session depends on, so the user is signed out on their next request. |

### Metric: idunno.bluesky.aspnet.authentication.identitystore.evictions.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `identitystore.evictions.total` | Counter&lt;long&gt; | {evictions} | Total identities evicted from the identity store because it reached its size limit. Only the `EphemeralIdentityStore` has a size limit. Evicting an identity silently signs that user out, so a non-zero value means the store is too small for the number of concurrent users. |

> [!NOTE]
> The matching capacity warning is logged only once for the lifetime of the process, so this counter is the only way to see how often
> the `EphemeralIdentityStore` is evicting identities.

### Metric: idunno.bluesky.aspnet.authentication.profilecache.hits.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `profilecache.hits.total` | Counter&lt;long&gt; | {hits} | Total profile cache hits. |

### Metric: idunno.bluesky.aspnet.authentication.profilecache.misses.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `profilecache.misses.total` | Counter&lt;long&gt; | {misses} | Total profile cache misses. A miss makes a call out to the PDS, so the hit rate against these two counters is a measure of how much request latency the profile cache is saving. |

### Metric: idunno.bluesky.aspnet.authentication.handleverification.failures.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `handleverification.failures.total` | Counter&lt;long&gt; | {failures} | Total handles returned by a profile which did not resolve back to the DID they were returned for. A handle is only as trustworthy as the resolution which confirms it, so a failure means the handle was discarded rather than surfaced as a claim. |

### Metric: idunno.bluesky.aspnet.authentication.dataprotection.failures.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `dataprotection.failures.total` | Counter&lt;long&gt; | {failures} | Total data protection failures. A sustained rise usually means key ring rotation or a key ring which is not shared across every instance of a multi-instance deployment. |

Tagged with `source`, either `correlation_cookie` or `identity_store`, identifying which payload could not be unprotected.
