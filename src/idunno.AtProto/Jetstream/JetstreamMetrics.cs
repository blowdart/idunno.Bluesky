// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace idunno.AtProto.Jetstream
{
    /// <summary>
    /// Metrics for an <see cref="AtProtoJetstream"/>.
    /// </summary>
    public sealed class JetstreamMetrics
    {
        private Counter<long> _messagesReceivedCounter;

        private Counter<long> _eventsParsedCounter;

        private Counter<long> _accountEventsReceived;

        private Counter<long> _commitEventsReceived;

        private Counter<long> _identityEventsReceived;

        private Counter<long> _unknownEventTypesIgnored;

        private Counter<long> _connectionsOpened;

        private Counter<long> _connectionsClosed;

        private Counter<long> _connectionFailures;

        private Counter<long> _messageParsingFailures;

        private Counter<long> _faults;

        private Counter<long> _messageDecompressionFailures;

        /// <summary>
        /// Creates a new instance of <see cref="JetstreamMetrics"/>.
        /// </summary>
        /// <param name="meter">The meter to use to create counters on.</param>
        /// <remarks><para>This is a fallback constructor for non-DI aware environments.</para></remarks>
        internal JetstreamMetrics(Meter meter)
        {
            ArgumentNullException.ThrowIfNull(meter);

            Initialize(meter);
        }

        /// <summary>
        /// Creates a new instance of <see cref="JetstreamMetrics"/>.
        /// </summary>
        /// <param name="meterFactory">The <see cref="IMeterFactory"/> to use to create meters.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="meterFactory"/> is <see langword="null"/>.</exception>
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = " IMeterFactory automatically manages the lifetime of any Meter objects it creates")]
        internal JetstreamMetrics(IMeterFactory meterFactory)
        {
            ArgumentNullException.ThrowIfNull(meterFactory);

            Meter meter = meterFactory.Create(MeterName, MeterVersion);

            Initialize(meter);
        }

        /// <summary>
        /// Increments the MessagesReceived metric by the specified amount.
        /// </summary>
        /// <param name="quantity">The quantity to increment the metric by</param>
        internal void MessagesReceived(int quantity) => _messagesReceivedCounter.Add(quantity);

        /// <summary>
        /// Increments the EventsParsed metric by the specified amount.
        /// </summary>
        /// <param name="quantity">The quantity to increment the metric by</param>
        internal void EventsParsed(int quantity) => _eventsParsedCounter.Add(quantity);

        /// <summary>
        /// Increments the AccountEventsReceived metric by the specified amount.
        /// </summary>
        /// <param name="quantity">The quantity to increment the metric by</param>
        internal void AccountEventsReceived(int quantity) => _accountEventsReceived.Add(quantity);

        /// <summary>
        /// Increments the CommitEventsReceived metric by the specified amount.
        /// </summary>
        /// <param name="quantity">The quantity to increment the metric by</param>
        internal void CommitEventsReceived(int quantity) => _commitEventsReceived.Add(quantity);

        /// <summary>
        /// Increments the IdentityEventsReceived metric by the specified amount.
        /// </summary>
        /// <param name="quantity">The quantity to increment the metric by</param>
        internal void IdentityEventsReceived(int quantity) => _identityEventsReceived.Add(quantity);

        /// <summary>
        /// Increments the UnknownEventsReceived metric by the specified amount.
        /// </summary>
        /// <param name="quantity">The quantity to increment the metric by</param>
        internal void UnknownEventsReceived(int quantity) => _unknownEventTypesIgnored.Add(quantity);

        /// <summary>
        /// Increments the ConnectionsOpened metric by the specified amount.
        /// </summary>
        /// <param name="quantity">The quantity to increment the metric by</param>
        internal void ConnectionsOpened(int quantity) => _connectionsOpened.Add(quantity);

        /// <summary>
        /// Increments the ConnectionsClosed metric by the specified amount.
        /// </summary>
        /// <param name="quantity">The quantity to increment the metric by</param>
        internal void ConnectionsClosed(int quantity) => _connectionsOpened.Add(quantity);

        /// <summary>
        /// Increments the ConnectionFailures metric by the specified amount.
        /// </summary>
        /// <param name="quantity">The quantity to increment the metric by</param>
        internal void ConnectionFailures (int quantity) => _connectionsOpened.Add(quantity);

        /// <summary>
        /// Increments the MessageParsingFailures metric by the specified amount.
        /// </summary>
        /// <param name="quantity">The quantity to increment the metric by</param>
        internal void MessageParsingFailures(int quantity) => _messageParsingFailures.Add(quantity);

        /// <summary>
        /// Increments the Faults metric by the specified amount.
        /// </summary>
        /// <param name="quantity">The quantity to increment the metric by</param>
        internal void Faults(int quantity) => _faults.Add(quantity);

        internal void MessageDecompressionFailures(int quantity) => _messageDecompressionFailures.Add(quantity);

        /// <summary>
        /// Gets the meter name publishing metrics.
        /// </summary>
        public static string MeterName  => "idunno.AtProto.Jetstream";

        /// <summary>
        /// Gets the current version of the meter.
        /// </summary>
        public static string MeterVersion => "2.0.0";

        [MemberNotNull(
            nameof(_messagesReceivedCounter),
            nameof(_eventsParsedCounter),
            nameof(_accountEventsReceived),
            nameof(_commitEventsReceived),
            nameof(_identityEventsReceived),
            nameof(_unknownEventTypesIgnored),
            nameof(_connectionsOpened),
            nameof(_connectionsClosed),
            nameof(_connectionFailures),
            nameof(_messageParsingFailures),
            nameof(_faults),
            nameof(_messageDecompressionFailures)
            )]
        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Guidelines suggest all lower case.")]
        private void Initialize(Meter meter)
        {
            _messagesReceivedCounter = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.total.messages",
                description: "Number of messages received from the jetstream.",
                unit: "{messages}");

            _eventsParsedCounter = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.total.events_parsed",
                description: "Number of events parsed from the jetstream.",
                unit: "{events}");

            _accountEventsReceived = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.total.account_events",
                description: "Number of account events parsed from the jetstream.",
                unit: "{events}");

            _commitEventsReceived = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.total.commit_events",
                description: "Number of commit events parsed from the jetstream.",
                unit: "{events}");

            _identityEventsReceived = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.total.identity_events",
                description: "Number of identity events parsed from the jetstream.",
                unit: "{events}");

            _unknownEventTypesIgnored = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.total.unknown_events",
                description: "Number of unknown events skipped over.",
                unit: "{events}");

            _connectionsOpened = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.total.connections_opened",
                description: "Number of jetstream connections opened.",
                unit: "{connections}");

            _connectionsClosed = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.total.connections_closed",
                description: "Number of jetstream connections closed.",
                unit: "{connections}");

            _connectionFailures = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.total_connections_failed",
                description: "Number of connection failures.",
                unit: "{connections}");

            _messageParsingFailures = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.total.message_parsing_failures",
                description: "Number of message parsing failures.",
                unit: "{messages}");

            _faults = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.total.faults",
                description: "Number of faults.",
                unit: "{faults}");

            _messageDecompressionFailures = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.total.message_decompression_failures",
                description: "Number of message decompression failures.",
                unit: "{messages}");
        }
    }
}
