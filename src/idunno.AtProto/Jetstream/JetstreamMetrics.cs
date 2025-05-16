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
        private readonly Counter<long> _messagesReceivedCounter;

        private readonly Counter<long> _eventsParsedCounter;

        /// <summary>
        /// Creates a new instance of <see cref="JetstreamMetrics"/>.
        /// </summary>
        /// <param name="meter">The meter to use to create counters on.</param>
        /// <remarks><para>This is a fallback constructor for non-DI aware environments.</para></remarks>
        internal JetstreamMetrics(Meter meter)
        {
            ArgumentNullException.ThrowIfNull(meter);

            _messagesReceivedCounter = meter.CreateCounter<long>(
                name: "idunno.bluesky.jetstream.message",
                description: "Number of messages received from the jetstream.");

            _eventsParsedCounter = meter.CreateCounter<long>(
                name: "idunno.bluesky.jetstream.event",
                description: "Number of events parsed from the jetstream.");
        }

        /// <summary>
        /// Creates a new instance of <see cref="JetstreamMetrics"/>.
        /// </summary>
        /// <param name="meterFactory"></param>
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = " IMeterFactory automatically manages the lifetime of any Meter objects it create")]
        public JetstreamMetrics(IMeterFactory meterFactory)
        {
            ArgumentNullException.ThrowIfNull(meterFactory);

            Meter meter = meterFactory.Create(MeterName);

            _messagesReceivedCounter = meter.CreateCounter<long>(
                name: "idunno.bluesky.jetstream.message",
                description: "Number of messages received from the jetstream.");

            _eventsParsedCounter = meter.CreateCounter<long>(
            name: "idunno.bluesky.jetstream.event",
            description: "Number of events parsed from the jetstream.");
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

        internal static string MeterName  => "idunno.bluesky.jetstream";

    }
}
