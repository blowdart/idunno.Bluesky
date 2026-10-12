// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace idunno.AtProto.Firehose;

/// <summary>
/// Metrics for an <see cref="AtProtoFirehose"/>.
/// </summary>
public sealed class FirehoseMetrics
{
    // For non-DI scenarios, see https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#best-practices
    private static readonly Meter s_meter = new(MeterName, MeterVersion);
    private readonly ConcurrentDictionary<object, long> _connectionStartTimestamps = new();
    private readonly ConcurrentDictionary<object, long> _lastMessageTimestamps = new();

    /// <summary>
    /// Creates a new instance of <see cref="FirehoseMetrics"/>.
    /// </summary>
    /// <param name="meterFactory">The <see cref="IMeterFactory"/>, if any, to use to create meters.</param>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = " IMeterFactory automatically manages the lifetime of any Meter objects it creates")]
    internal FirehoseMetrics(IMeterFactory? meterFactory)
    {
        Initialize(meterFactory is null ? s_meter : meterFactory.Create(MeterName, MeterVersion));
    }

    /// <summary>
    /// Gets the meter name publishing metrics.
    /// </summary>
    public static string MeterName => "idunno.AtProto.Firehose";

    /// <summary>
    /// Gets the current version of the meter.
    /// </summary>
    public static string MeterVersion => "1.0.0";

    internal Counter<long> MessagesReceived { get; private set; }

    internal Counter<long> EventsParsed { get; private set; }

    internal Counter<long> UnknownEventsReceived { get; private set; }

    internal Counter<long> InvalidEventsReceived { get; private set; }

    internal Counter<long> ConnectionsOpened { get; private set; }

    internal Counter<long> ConnectionsClosed { get; private set; }

    internal Counter<long> ConnectionFailures { get; private set; }

    internal Counter<long> Reconnections { get; private set; }

    internal Counter<long> ProtocolErrors { get; private set; }

    internal Counter<long> SigningKeyCacheHits { get; private set; }

    internal Counter<long> SigningKeyCacheMisses { get; private set; }

    internal Counter<long> SigningKeyRefreshes { get; private set; }

    internal UpDownCounter<long> ActiveConnections { get; private set; }

    internal Histogram<double> ConnectionDuration { get; private set; }

    internal Histogram<double> MessageInterarrivalDuration { get; private set; }

    internal void RecordConnectionOpened(object connection, KeyValuePair<string, object?> serverTag)
    {
        ConnectionsOpened.Add(1, serverTag);
        ActiveConnections.Add(1, serverTag);
        _connectionStartTimestamps[connection] = Stopwatch.GetTimestamp();
        _lastMessageTimestamps.TryRemove(connection, out _);
    }

    internal void RecordConnectionClosed(object connection, KeyValuePair<string, object?> serverTag)
    {
        if (_connectionStartTimestamps.TryRemove(connection, out long startTimestamp))
        {
            ConnectionsClosed.Add(1, serverTag);
            ActiveConnections.Add(-1, serverTag);
            ConnectionDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, serverTag);
        }

        _lastMessageTimestamps.TryRemove(connection, out _);
    }

    internal void RecordMessageReceived(object connection, KeyValuePair<string, object?> serverTag)
    {
        MessagesReceived.Add(1, serverTag);

        long now = Stopwatch.GetTimestamp();
        if (_lastMessageTimestamps.TryGetValue(connection, out long previousTimestamp))
        {
            MessageInterarrivalDuration.Record(Stopwatch.GetElapsedTime(previousTimestamp, now).TotalSeconds, serverTag);
        }

        _lastMessageTimestamps[connection] = now;
    }

    [MemberNotNull(
        nameof(MessagesReceived),
        nameof(EventsParsed),
        nameof(UnknownEventsReceived),
        nameof(InvalidEventsReceived),
        nameof(ConnectionsOpened),
        nameof(ConnectionsClosed),
        nameof(ConnectionFailures),
        nameof(Reconnections),
        nameof(ProtocolErrors),
        nameof(SigningKeyCacheHits),
        nameof(SigningKeyCacheMisses),
        nameof(SigningKeyRefreshes),
        nameof(ActiveConnections),
        nameof(ConnectionDuration),
        nameof(MessageInterarrivalDuration))]
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Guidelines suggest all lower case.")]
    private void Initialize(Meter meter)
    {
        string prefix = MeterName.ToLowerInvariant();

        MessagesReceived = meter.CreateCounter<long>(
            name: $"{prefix}.total.messages",
            description: "Number of frames received from the firehose.",
            unit: "{messages}");

        EventsParsed = meter.CreateCounter<long>(
            name: $"{prefix}.total.events_parsed",
            description: "Number of events parsed from the firehose.",
            unit: "{events}");

        UnknownEventsReceived = meter.CreateCounter<long>(
            name: $"{prefix}.total.unknown_events",
            description: "Number of frames with an unknown operation or message type.",
            unit: "{events}");

        InvalidEventsReceived = meter.CreateCounter<long>(
            name: $"{prefix}.total.invalid_events",
            description: "Number of events which failed validation.",
            unit: "{events}");

        ConnectionsOpened = meter.CreateCounter<long>(
            name: $"{prefix}.total.connections_opened",
            description: "Number of firehose connections opened.",
            unit: "{connections}");

        ConnectionsClosed = meter.CreateCounter<long>(
            name: $"{prefix}.total.connections_closed",
            description: "Number of firehose connections closed.",
            unit: "{connections}");

        ConnectionFailures = meter.CreateCounter<long>(
            name: $"{prefix}.total.connections_failed",
            description: "Number of connection failures.",
            unit: "{connections}");

        Reconnections = meter.CreateCounter<long>(
            name: $"{prefix}.total.reconnects",
            description: "Number of reconnection attempts.",
            unit: "{connections}");

        ProtocolErrors = meter.CreateCounter<long>(
            name: $"{prefix}.total.protocol_errors",
            description: "Number of connections dropped because of malformed frames or sequence violations.",
            unit: "{errors}");

        SigningKeyCacheHits = meter.CreateCounter<long>(
            name: $"{prefix}.total.signing_key_cache_hits",
            description: "Number of signing keys, or failures to resolve one, found in the signing key cache.",
            unit: "{lookups}");

        SigningKeyCacheMisses = meter.CreateCounter<long>(
            name: $"{prefix}.total.signing_key_cache_misses",
            description: "Number of signing keys not found in the signing key cache, each of which resolved a DID document.",
            unit: "{lookups}");

        SigningKeyRefreshes = meter.CreateCounter<long>(
            name: $"{prefix}.total.signing_key_refreshes",
            description: "Number of cached signing keys resolved again because a signature failed to verify against them.",
            unit: "{lookups}");

        ActiveConnections = meter.CreateUpDownCounter<long>(
            name: $"{prefix}.connections.active",
            description: "Current number of open firehose connections.",
            unit: "{connections}");

        ConnectionDuration = meter.CreateHistogram<double>(
            name: $"{prefix}.connection.duration",
            description: "Duration of firehose connections.",
            unit: "s");

        MessageInterarrivalDuration = meter.CreateHistogram<double>(
            name: $"{prefix}.message.interarrival.duration",
            description: "Time between frames received from the firehose.",
            unit: "s");
    }
}
