// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Metrics for an <see cref="AtProtoJetstream"/>.
/// </summary>
public sealed class JetstreamMetrics
{
    // For non-DI scenarios, see https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#best-practices
    private static readonly Meter s_meter = new(MeterName, MeterVersion);

    /// <summary>
    /// Creates a new instance of <see cref="JetstreamMetrics"/>.
    /// </summary>
    /// <param name="meterFactory">The <see cref="IMeterFactory"/> to use to create meters.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="meterFactory"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = " IMeterFactory automatically manages the lifetime of any Meter objects it creates")]
    internal JetstreamMetrics(IMeterFactory? meterFactory)
    {
        if (meterFactory == null)
        {
            Initialize(s_meter);
        }
        else
        {
            Initialize(meterFactory.Create(MeterName, MeterVersion));
        }
    }

    internal Counter<long> MessagesReceived { get; private set; }

    internal Counter<long> EventsParsed { get; private set; }

    internal Counter<long> UnknownEventsReceived { get; private set; }

    internal Counter<long> ConnectionsOpened { get; private set; }

    internal Counter<long> ConnectionsClosed { get; private set; }

    internal Counter<long> ConnectionFailures { get; private set; }

    internal Counter<long> MessageParsingFailures { get; private set; }

    internal Counter<long> Faults { get; private set; }

    internal Counter<long> MessageDecompressionFailures { get; private set; }

    /// <summary>
    /// Gets the meter name publishing metrics.
    /// </summary>
    public static string MeterName  => "idunno.AtProto.Jetstream";

    /// <summary>
    /// Gets the current version of the meter.
    /// </summary>
    public static string MeterVersion => "2.0.0";

    [MemberNotNull(
        nameof(MessagesReceived),
        nameof(EventsParsed),
        nameof(UnknownEventsReceived),
        nameof(ConnectionsOpened),
        nameof(ConnectionsClosed),
        nameof(ConnectionFailures),
        nameof(MessageParsingFailures),
        nameof(Faults),
        nameof(MessageDecompressionFailures)
        )]
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Guidelines suggest all lower case.")]
    private void Initialize(Meter meter)
    {
        MessagesReceived = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.total.messages",
            description: "Number of messages received from the jetstream.",
            unit: "{messages}");

        EventsParsed = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.total.events_parsed",
            description: "Number of events parsed from the jetstream.",
            unit: "{events}");

        UnknownEventsReceived = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.total.unknown_events",
            description: "Number of unknown events skipped over.",
            unit: "{events}");

        ConnectionsOpened = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.total.connections_opened",
            description: "Number of jetstream connections opened.",
            unit: "{connections}");

        ConnectionsClosed = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.total.connections_closed",
            description: "Number of jetstream connections closed.",
            unit: "{connections}");

        ConnectionFailures = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.total_connections_failed",
            description: "Number of connection failures.",
            unit: "{connections}");

        MessageParsingFailures = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.total.message_parsing_failures",
            description: "Number of message parsing failures.",
            unit: "{messages}");

        Faults = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.total.faults",
            description: "Number of faults.",
            unit: "{faults}");

        MessageDecompressionFailures = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.total.message_decompression_failures",
            description: "Number of message decompression failures.",
            unit: "{messages}");
    }
}
