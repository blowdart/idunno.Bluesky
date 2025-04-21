// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Encapsulates an update operation for the repo.applyWrites api
    /// </summary>
    internal sealed record ApplyWritesUpdateRequest : ApplyWritesRequestValueBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="ApplyWritesUpdateRequest"/>.
        /// </summary>
        /// <param name="collection">The collection the record will be updated in.</param>
        /// <param name="rkey">The <see cref="RecordKey"/> of the record to be updated.</param>
        /// <param name="value">The new value for the record.</param>
        public ApplyWritesUpdateRequest(Nsid collection, RecordKey rkey, JsonNode value) : base(collection, rkey)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the new value for the record.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public JsonNode Value { get; init; }
    }
}
