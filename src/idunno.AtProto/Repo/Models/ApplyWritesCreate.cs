// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Encapsulates a create operation for the repo.applyWrites api
    /// </summary>
    internal sealed record ApplyWritesCreate : ApplyWritesRequestValueBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="ApplyWritesCreate"/>.
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="rkey"></param>
        /// <param name="value"></param>
        public ApplyWritesCreate(Nsid collection, RecordKey? rkey, JsonNode value) : base(collection, rkey)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the value of the record for the create operation.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public JsonNode Value { get; init; }
    }
}
