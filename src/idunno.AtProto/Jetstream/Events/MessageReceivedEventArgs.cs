// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Jetstream.Events
{
    /// <summary>
    /// Encapsulates information given when a the message received from a jetstream instance.
    /// </summary>
    /// <remarks>
    /// <para>Creates a new instance of <see cref="MessageReceivedEventArgs"/></para>
    /// </remarks>
    public sealed class MessageReceivedEventArgs(string message) : EventArgs
    {

        /// <summary>
        /// The message received from the jetstream.
        /// </summary>
        public string Message { get; } = message;
    }
}
