// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream
{
    /// <summary>
    /// Encapsulates the properties of a Jetstream account event.
    /// </summary>
    public sealed record AtJetstreamAccountEvent : AtJetstreamEvent
    {
        /// <summary>
        /// Gets the account state change that triggered the event.
        /// </summary>
        public required AtJetstreamAccount Account { get; set; }
    }
}
