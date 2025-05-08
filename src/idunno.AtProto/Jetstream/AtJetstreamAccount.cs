// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream
{
    /// <summary>
    /// Encapsulates the properties of an account operation in a Jetstream event.
    /// </summary>
    public sealed record AtJetstreamAccount
    {
        /// <summary>
        /// Flag indicating the active status of the account.
        /// </summary>
        public required bool Active { get; set; }

        /// <summary>
        /// Gets the <see cref="AtProto.Did"/> of the account that triggered the event.
        /// </summary>
        public required Did Did { get; init; }

        /// <summary>
        /// Gets the sequence number for the change.
        /// </summary>
        [JsonPropertyName("seq")]
        public ulong Sequence { get; init; }

        /// <summary>
        /// Gets the status for the account, if any.
        /// </summary>
        public AccountStatus? Status { get; init; }

        /// <summary>
        /// Gets the timestamp for the change.
        /// </summary>
        [JsonPropertyName("time")]
        public DateTimeOffset TimeStamp { get; init; }
    }
}
