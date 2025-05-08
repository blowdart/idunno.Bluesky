// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream
{
    /// <summary>
    /// Encapsulates the properties of a commit operation in a Jetstream event.
    /// </summary>
    public sealed record AtJetstreamCommit
    {
        /// <summary>
        /// Gets the type of the operation the commit refers to.
        /// </summary>
        public required string Operation { get; init; }

        /// <summary>
        /// Gets the <see cref="Nsid"/> of the collection the operation happened against.
        /// </summary>
        public required Nsid Collection { get; init; }

        /// <summary>
        /// Gets the revision key for the operation.
        /// </summary>
        public required string Rev { get; init; }

        /// <summary>
        /// Gets the record key of the record the operation happened against.
        /// </summary>
        [JsonPropertyName("rkey")]
        public required RecordKey RKey { get; init; }

        /// <summary>
        /// Gets the value, if any, of the record that triggered the commit event.
        /// </summary>
        public JsonDocument? Record { get; init; }

        /// <summary>
        /// Gets the content identifier for the commit event.
        /// </summary>
        public Cid? Cid { get; init; } 
    }
}
