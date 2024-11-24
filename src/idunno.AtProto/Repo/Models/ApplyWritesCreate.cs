// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Encapsulates a create operation for the repo.applyWrites api
    /// </summary>
    public sealed record ApplyWritesCreate : ApplyWritesRequestValueBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="ApplyWritesCreate"/>.
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="rkey"></param>
        /// <param name="value"></param>
        public ApplyWritesCreate(Nsid collection, RecordKey rkey, object value) : base(collection, rkey)
        {
            Value = value;
        }

        [JsonInclude]
        [JsonRequired]
        public object Value { get; init; }
    }
}
