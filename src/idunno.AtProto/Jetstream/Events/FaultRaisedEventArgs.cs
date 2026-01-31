// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Jetstream.Events
{
    /// <summary>
    /// Encapsulates information given when a fault occurs in jetstream processing.
    /// </summary>
    /// <param name="fault">The fault message.</param>
    public class FaultRaisedEventArgs(string fault) : EventArgs
    {
        /// <summary>
        /// The message received from the jetstream.
        /// </summary>
        public string Fault { get; } = fault;
    }
}
