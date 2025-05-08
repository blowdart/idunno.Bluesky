// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Jetstream
{
    /// <summary>
    /// Encapsulates the properties of a Jetstream commit event.
    /// </summary>
    public sealed record AtJetstreamCommitEvent : AtJetstreamEvent
    {
        /// <summary>
        /// Gets the commit that triggered the event.
        /// </summary>
        public required AtJetstreamCommit Commit { get; set; }
    }
}
