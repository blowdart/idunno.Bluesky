// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Jetstream.Events
{
    /// <summary>
    /// Encapsulates information given when a the message received from a jetstream instance.
    /// </summary>
    public sealed class MessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of <see cref="MessageReceivedEventArgs"/>
        /// </summary>
        public MessageReceivedEventArgs(string message) => Message = message;

        /// <summary>
        /// The message received from the jetstream.
        /// </summary>
        public string Message { get; }
    }
}
