# Built-in metrics in idunno.AtProto

This is a reference for metrics built-in for .NET, produced using the [System.Diagnostics.Metrics](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metric) API.

> [!TIP]
> For more information about how to collect and report these metrics, see the .NET documentation
> [Collecting metrics](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-collection).
>
> During development you can use the [dotnet-counters](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters) tool to
> observe these metrics in real time. e.g.
> ```c#
> dotnet-counters monitor --process-id <pid> --counters idunno.AtProto.AtProtoHttpClient
> ```
> For production environments, you can use a variety of exporters to send these metrics to monitoring systems
> like Aspire, Prometheus, Grafana, or Azure Monitor.


## idunno.AtProto.AtProtoHttpClient

The `idunno.AtProto.AtProtoHttpClient` Meter reports measures from the `idunno.AtProto.AtProtoHttpClient`.

### Metric : requests.total

| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total` | Counter&lt;long&gt; | Requests | Total number of requests made by an instance of `idunno.AtProto.AtProtoHttpClient`.|

### Metric: responses.total
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `responses.total` | Counter&lt;long&gt; | Responses | Total number of responses received by an instance of `idunno.AtProto.AtProtoHttpClient`. |

### Metric: requests.total.successful
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total.successful` | Counter&lt;long&gt; | Requests | Total number of successful requests made by an instance of `idunno.AtProto.AtProtoHttpClient`. |

### Metric: requests.total.failure
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total.failure` | Counter&lt;long&gt; | Requests | Total number of failed requests made by an instance of `idunno.AtProto.AtProtoHttpClient`. |

### Metric: requests.total.dpop_retry
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total.dpop_retry` | Counter&lt;long&gt; | Requests | Total number of requests retried due to DPoP nonce rotation or other DPoP related issues by an instance of the `idunno.AtProto.AtProtoHttpClient`. |

### Metric: responses.total.deserialization_failure
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `responses.total.deserialization_failure` | Counter&lt;long&gt; | Requests | Total number of responses that could not be deserialized from JSON by an instance of the `idunno.AtProto.AtProtoHttpClient`. |

### Metric: requests.total.blob_create_request
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total.blob_create_request` | Counter&lt;long&gt; | Requests | Total number of blob creation requests made by an instance of `idunno.AtProto.AtProtoHttpClient`. |

### Metric: requests.total.get_request
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total.get_request` | Counter&lt;long&gt; | Requests | Total number of HTTP GET requests made by an instance of the `idunno.AtProto.AtProtoHttpClient`. |

### Metric: requests.total.post_request
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `requests.total.post_request` | Counter&lt;long&gt; | Requests | Total number of HTTP POST requests made by an instance of the `idunno.AtProto.AtProtoHttpClient`. |

### Metric: request.duration
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `request.duration` | Histogram&lt;double&gt; | s | Duration of individual requests made by an instance of the `idunno.AtProto.AtProtoHttpClient`. |

## idunno.AtProto.JetStream

The `idunno.AtProto.Jetstream ` Meter reports measures from the `idunno.AtProto.JetStream.AtProtoJetstream` client.

### Metric: total.messages
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.messages` | Counter&lt;long&gt; | Messages | Total number of messages received from the JetStream by a `AtProtoJetstream` instance. |

### Metric: total.message_parsing_failures
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.message_parsing_failures` | Counter&lt;long&gt; | Messages | Total number of messages that failed to parse after receipt by a `AtProtoJetstream` instance. |

### Metric: total.events_parsed
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.events_parsed` | Counter&lt;long&gt; | Events | Total number of events parsed from received messages by a `AtProtoJetstream` instance. |

### Metric: total.account_events
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.account_events` | Counter&lt;long&gt; | Events | Total number of account events received in messages by a `AtProtoJetstream` instance. |
### Metric: total.commit_events
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.commit_events` | Counter&lt;long&gt; | Events | Total number of commit events received in messages by a `AtProtoJetstream` instance. |
### Metric: total.identity_events
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.identity_events` | Counter&lt;long&gt; | Events | Total number of identity events received in messages by a `AtProtoJetstream` instance. |
### Metric: total.unknown_events
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.unknown_events` | Counter&lt;long&gt; | Events | Total number of events with unknown type received in messages by a `AtProtoJetstream` instance. |
### Metric: total.faults
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.faults` | Counter&lt;long&gt; | Faults | Total number of WebSocket faults that occurred by a `AtProtoJetstream` instance. |

### Metric: total.connections_opened
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.connections_opened` | Counter&lt;long&gt; | Connections | Total number of WebSocket connections to the JetStream opened by a `AtProtoJetstream` instance. |

### Metric: total.connections_closed
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.connections_closed` | Counter&lt;long&gt; | Connections | Total number of WebSocket connections to the JetStream closed by a `AtProtoJetstream` instance. |

### Metric: total.connections_failed
| Name | Instrument Type | Unit | Description |
| --- | --- | --- | --- |
| `total.connections_failed` | Counter&lt;long&gt; | Connections | Total number of WebSocket connections to the JetStream that failed by a `AtProtoJetstream` instance. |
