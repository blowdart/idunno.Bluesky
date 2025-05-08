// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Jetstream
{
    /// <summary>
    /// The kind of message sent.
    /// </summary>
    public enum JetStreamEventKind
    {
        /// <summary>
        /// The message kind is unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The message is for an account event.
        /// </summary>
        Account,

        /// <summary>
        /// The message is about a commit event.
        /// </summary>
        Commit,

        /// <summary>
        /// The message is about an identity event.
        /// </summary>
        Identity
    }
}
