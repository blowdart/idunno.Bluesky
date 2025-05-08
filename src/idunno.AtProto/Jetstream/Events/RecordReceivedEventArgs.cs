// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Jetstream.Events
{
    /// <summary>
    /// Contains the results of parsing a message from the Jetstream.
    /// </summary>
    public sealed class RecordReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the message that trigged the event, parsed as its json object.
        /// </summary>
        public required AtJetstreamEvent ParsedEvent { get; init; }
    }
}
