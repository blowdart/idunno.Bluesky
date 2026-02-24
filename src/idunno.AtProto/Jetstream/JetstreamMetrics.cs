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
        public JetstreamMetrics(IMeterFactory meterFactory)
        {
            ArgumentNullException.ThrowIfNull(meterFactory);

            Meter meter = meterFactory.Create(MeterName, MeterVersion);

            Initialize(meter);
        }

        /// <summary>
        /// Increments the MessagesReceived metric by the specified amount.
        /// </summary>
        /// <param name="quantity">The quantity to increment the metric by</param>
        public void MessagesReceived(int quantity) => _messagesReceivedCounter.Add(quantity);

        /// <summary>
        /// Increments the EventsParsed metric by the specified amount.
        /// </summary>
        /// <param name="quantity">The quantity to increment the metric by</param>
        public void EventsParsed(int quantity) => _eventsParsedCounter.Add(quantity);

        internal static string MeterName  => "idunno.AtProto.Jetstream";

        internal static string MeterVersion => "1.0.0";

        [MemberNotNull(nameof(_messagesReceivedCounter), nameof(_eventsParsedCounter))]
        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Guidelines suggest all lower case.")]
        private void Initialize(Meter meter)
        {
            _messagesReceivedCounter = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.total_messages",
                description: "Number of messages received from the jetstream.",
                unit: "Messages per second");

            _eventsParsedCounter = meter.CreateCounter<long>(
                name: $"{MeterName.ToLowerInvariant()}.total.events_parsed",
                description: "Number of events parsed from the jetstream.",
                unit: "Evens per second");
        }
    }
}
